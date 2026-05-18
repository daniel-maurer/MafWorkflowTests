# Instructions for the MAF Workflow Development Agent

Build the MAF workflow application so it connects to the Workflow BFF and executes workflows requested by the React frontend.

## Context

There are three applications:

1. **React frontend**: UI only. It connects to the BFF.
2. **Workflow BFF**: ASP.NET Core bridge. It reads workflow JSON config files, creates sessions, and relays realtime events.
3. **MAF application**: workflow engine. This is the application you must implement.

The MAF application must not expose frontend-facing endpoints directly. It should connect to the BFF through SignalR and receive commands from the BFF.

## Target architecture

```text
Frontend -> BFF /hubs/workflow -> BFF /hubs/maf -> MAF application
Frontend <- BFF /hubs/workflow <- BFF /hubs/maf <- MAF application
```

## BFF endpoints used by MAF

The MAF application connects to:

```text
{BFF_BASE_URL}/hubs/maf
```

Example local URL:

```text
http://localhost:5088/hubs/maf
```

## MAF worker startup behavior

On startup, the MAF application must:

1. Create a SignalR client connection to `/hubs/maf`.
2. Authenticate with the BFF. For local development, use a mock bearer token. For production, use the service-to-service token strategy selected by the team.
3. Register itself by calling:

```csharp
RegisterWorker(workerId, supportedWorkflowIds)
```

Example:

```csharp
await connection.InvokeAsync(
    "RegisterWorker",
    "maf-worker-local-01",
    new[] { "support", "incident-triage" });
```

## Commands the MAF app must handle

Subscribe to these SignalR events from the BFF.

### `startWorkflow`

Called when the frontend creates a new session.

Payload:

```json
{
  "sessionId": "ses_...",
  "workflowId": "support",
  "ticketId": "#TKT-4821",
  "initialMessage": "optional user text",
  "mafWorkflowName": "SupportWorkflow",
  "mafWorkflowVersion": "1.0",
  "inputSchema": "support-ticket-v1"
}
```

Required behavior:

- Create or resume the internal MAF workflow instance.
- Correlate all internal state with `sessionId`.
- If `initialMessage` is present, process it as the first user message.
- Publish a `trace` event confirming that the workflow started.

### `userMessage`

Called when the frontend user sends a chat message.

Payload:

```json
{
  "sessionId": "ses_...",
  "text": "I can't log in"
}
```

Required behavior:

- Add the message to the MAF workflow context.
- Run the next workflow step.
- Publish events such as `message`, `trace`, `agent`, `context`, `kb`, and `typing`.

### `humanMessage`

Called when a human agent sends a message in handoff mode.

Payload:

```json
{
  "sessionId": "ses_...",
  "text": "I am reviewing this issue."
}
```

Required behavior:

- Only process when the workflow is in human handoff mode.
- Publish the message back as a `message` event with `senderType = "human"` and `splitMirror = true`.

### `runScenario`

Called when the frontend user clicks a configured scenario button.

Payload:

```json
{
  "sessionId": "ses_...",
  "scenarioId": "known"
}
```

Required behavior:

- Look up the scenario in your own config or receive it from the BFF in a future command extension.
- Process the scenario message through the normal workflow path.

### `markSolved`

Called when a human handoff is manually resolved.

Payload:

```json
{
  "sessionId": "ses_..."
}
```

Required behavior:

- Close the human handoff.
- Run any final MAF pattern-recording agent.
- Publish `splitMode=false`.
- Publish final `context` with `status = "resolved"`.

### `resetWorkflow`

Called when the frontend asks to reset the session.

Payload:

```json
{
  "sessionId": "ses_..."
}
```

Required behavior:

- Clear internal state for the session.
- Publish context/agent reset events if needed.

## Events the MAF app must publish to the BFF

Publish all events by calling:

```csharp
PublishEvent(envelope)
```

Envelope:

```json
{
  "sessionId": "ses_...",
  "eventType": "message",
  "payload": {},
  "occurredAt": "2026-05-16T18:00:00Z",
  "sequenceId": "optional-idempotency-key"
}
```

Use these `eventType` values.

### `message`

Payload:

```json
{
  "id": "msg_001",
  "type": "message",
  "side": "left",
  "senderType": "agent",
  "senderName": "Triage Agent",
  "icon": "git-branch",
  "bubbleStyle": "triage",
  "systemStyle": null,
  "text": "Identified as authentication issue.",
  "tools": [],
  "createdAt": "2026-05-16T18:00:00Z",
  "splitMirror": false
}
```

Rules:

- User messages usually use `side = "right"` and `senderType = "user"`.
- Agent messages usually use `side = "left"` and `senderType = "agent"`.
- System messages use `side = "center"` and `senderType = "system"`.
- In human handoff mode, set `splitMirror = true` for messages that must appear in the split view.
- If `text` contains HTML, sanitize it before publishing.

### `trace`

Payload:

```json
{
  "id": "trc_001",
  "time": "2026-05-16T18:00:00Z",
  "icon": "git-branch",
  "color": "primary",
  "title": "TriageAgent received message",
  "description": "Started classification.",
  "level": "info"
}
```

Allowed levels:

- `info`
- `success`
- `warning`
- `error`

### `agent`

Payload:

```json
{
  "id": "triage",
  "state": "active",
  "tag": "Running",
  "activeTools": []
}
```

Allowed states:

- `idle`
- `active`
- `done`
- `human`
- `wait`

### `kb`

Payload:

```json
[
  {
    "id": "kb_001",
    "title": "Login failure — password reset",
    "category": "Auth",
    "score": 0.97,
    "summary": "User unable to access account.",
    "resolutionType": "password-reset",
    "tags": ["login", "password", "auth"]
  }
]
```

Use this when the MAF workflow finds dynamic KB matches. The BFF can also expose fixed KB entries from JSON config.

### `context`

Payload:

```json
{
  "status": "triaging",
  "chatTitle": "Triage in progress",
  "chatSubtitle": "Classifying the user request.",
  "activeAgentId": "triage",
  "category": "Access & Auth",
  "confidence": 0.93,
  "intent": "Password/login failure",
  "humanMode": false,
  "resolutionSteps": [
    { "step": 1, "label": "Classified issue", "ok": true }
  ]
}
```

Allowed statuses:

- `idle`
- `triaging`
- `searching-kb`
- `resolving`
- `recording`
- `human-chat`
- `resolved`
- `error`

### `splitMode`

Payload:

```json
true
```

Use `true` when the UI must enter human handoff mode. Use `false` when the handoff closes.

### `typing`

Payload:

```json
{
  "container": "msgs",
  "label": "Triage Agent analyzing",
  "on": true
}
```

Allowed containers:

- `msgs`
- `user-msgs`
- `human-msgs`

Publish the same event with `on = false` to hide typing.

## Recommended event ordering for a known issue

1. Publish `agent` for triage active.
2. Publish `trace` that triage received the message.
3. Publish `typing` on.
4. Run MAF triage logic.
5. Publish `typing` off.
6. Publish `context` with category, confidence, and intent.
7. Publish `message` from Triage Agent.
8. Publish `agent` for triage done.
9. Publish `agent` for KB/frequent-problem agent active.
10. Publish `kb` with matched items.
11. Publish `message` explaining the KB match.
12. Publish `agent` for resolution active.
13. Run tool calls.
14. Publish `message` with `tools`.
15. Publish final `context` with `status = "resolved"`.

## Recommended event ordering for human handoff

1. Publish `context` with `status = "human-chat"` and `humanMode = true`.
2. Publish `splitMode` with `true`.
3. Publish a center `message` with `systemStyle = "handoff"`.
4. Publish human agent messages with `senderType = "human"` and `splitMirror = true`.
5. When solved, publish `splitMode = false`.
6. Run the pattern-recording MAF agent.
7. Publish final `context` with `status = "resolved"`.

## C# SignalR client skeleton

Use this pattern in the MAF application:

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5088/hubs/maf", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult("mock-token:maf-worker")!;
    })
    .WithAutomaticReconnect()
    .Build();

connection.On<MafStartWorkflowCommand>("startWorkflow", async command =>
{
    // Create or resume MAF workflow by command.SessionId.
    // Start workflow command.MafWorkflowName.
});

connection.On<MafUserMessageCommand>("userMessage", async command =>
{
    // Route the user message into the MAF workflow session.
});

connection.On<MafHumanMessageCommand>("humanMessage", async command =>
{
    // Route the human message into handoff context.
});

connection.On<MafRunScenarioCommand>("runScenario", async command =>
{
    // Trigger scenario flow.
});

connection.On<MafSessionCommand>("markSolved", async command =>
{
    // Close human handoff and record pattern.
});

connection.On<MafSessionCommand>("resetWorkflow", async command =>
{
    // Reset workflow state.
});

await connection.StartAsync();

await connection.InvokeAsync(
    "RegisterWorker",
    "maf-worker-local-01",
    new[] { "support", "incident-triage" });
```

## Acceptance criteria for the MAF implementation

- The MAF app connects to `/hubs/maf` and registers supported workflow IDs.
- Creating a frontend session causes the BFF to send `startWorkflow` to the MAF app.
- Sending a frontend chat message causes the BFF to send `userMessage` to the MAF app.
- The MAF app publishes at least `context`, `agent`, `trace`, and `message` events.
- The frontend updates in realtime without polling.
- Human handoff works through `splitMode`, `humanMessage`, and `markSolved`.
- All events include the correct `sessionId`.
- Event names and payload property names are camelCase JSON.
