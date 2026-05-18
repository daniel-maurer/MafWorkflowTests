# Workflow BFF Architecture

This project is a Backend-for-Frontend between the React workflow console and the MAF workflow application.

## Responsibility split

### Frontend

The frontend is only responsible for UI:

- Login and Azure token acquisition.
- Workflow catalog screen.
- Workflow run screen.
- Chat UI.
- Status, trace, KB, and human handoff rendering.

The frontend connects only to the BFF:

- REST: `/api/...`
- SignalR: `/hubs/workflow`

### BFF

The BFF is responsible for frontend-facing contracts and realtime bridging:

- Reads workflow definitions from JSON files.
- Exposes workflow catalog and fixed KB data to the frontend.
- Creates workflow sessions.
- Keeps a lightweight session snapshot for refresh/reconnect.
- Receives frontend commands and forwards them to the MAF app.
- Receives MAF events and forwards them to the correct frontend SignalR group.
- Handles Azure Entra authentication for the frontend-facing API.

The BFF should not contain business workflow logic. It is a contract adapter and realtime bridge.

### MAF application

The MAF app is responsible for workflow execution:

- Runs the real agents.
- Calls tools.
- Performs classification, KB search, resolution, pattern recording, and handoff decisions.
- Publishes realtime progress events to the BFF.

The MAF app connects to the BFF through:

- SignalR: `/hubs/maf`

## Realtime topology

```text
React frontend
  |
  | REST: /api/workflows, /api/workflow-sessions
  | SignalR: /hubs/workflow
  v
BFF
  |
  | SignalR: /hubs/maf
  v
MAF application
```

## Hub separation

### Frontend hub: `/hubs/workflow`

This hub is for browser clients. It exposes the contract already used by the React frontend:

- `JoinSession(sessionId)`
- `LeaveSession(sessionId)`
- `SendUserMessage(sessionId, text)`
- `SendHumanMessage(sessionId, text)`
- `RunScenario(sessionId, scenarioId)`
- `MarkSolved(sessionId)`
- `ResetSession(sessionId)`

The BFF forwards those commands to the MAF hub.

### MAF hub: `/hubs/maf`

This hub is for MAF workers. A MAF worker registers itself with:

```csharp
RegisterWorker(workerId, supportedWorkflowIds)
```

The BFF sends commands to MAF workers:

- `startWorkflow(command)`
- `userMessage(command)`
- `humanMessage(command)`
- `runScenario(command)`
- `markSolved(command)`
- `resetWorkflow(command)`

The MAF worker publishes workflow events back to the BFF:

```csharp
PublishEvent(envelope)
```

The BFF then forwards the event to the frontend group for the session.

## Workflow JSON configuration

Workflow configs live in the `Workflows/` folder. Each file is a full workflow definition.

Important fields:

- `id`: frontend and BFF workflow id.
- `agents`: pipeline shown on the frontend.
- `scenarios`: scenario buttons shown on the frontend.
- `capabilities`: toggles for UI features.
- `maf.workflowName`: the MAF workflow to execute.
- `maf.version`: version for routing or compatibility.
- `maf.inputSchema`: schema name for the MAF start payload.
- `fixedKnowledgeBase`: fixed KB items that the BFF can expose without calling MAF.

Example:

```json
{
  "id": "support",
  "maf": {
    "workflowName": "SupportWorkflow",
    "version": "1.0",
    "inputSchema": "support-ticket-v1"
  }
}
```

## Recommended production evolution

- Replace `InMemorySessionRegistry` with SQL Server, Redis, or Cosmos DB.
- Keep the BFF event contract stable even if the MAF internal implementation changes.
- Add a service-to-service authentication policy for MAF workers.
- Add sequence IDs to MAF events and ignore duplicate events in the BFF.
- Use Azure SignalR Service if BFF or MAF workers scale horizontally.
