# Agent Workflow Console — Endpoint Contracts

This document is the single source of truth for the **backend contract** the
React frontend expects. It pairs with `src/types/workflow.ts`, which mirrors
these shapes in TypeScript.

The contract is REST + SignalR:

- **REST** is used for static reference data (workflow catalog, KB lookup) and
  session bootstrap (creating a session, fetching the initial snapshot when
  reopening one).
- **SignalR** is used for everything that happens *during* a session —
  messages, trace events, agent state transitions, KB results, context
  updates, and human handoff. The hub is the single channel for realtime
  state; the REST endpoints exist so the UI can render before the hub
  finishes connecting.

> Naming convention. The `support-workflow-demo-1.html` mockup talked about a
> `support-demo` API. We have generalized it to **workflows** because the
> frontend is configurable for any number of workflows (`support`,
> `incident-triage`, `kb-curator`, …).

---

## 1. Conventions

- **Base URL.** Configured via `VITE_API_BASE_URL`. Empty means same origin.
- **Auth.** Every protected endpoint and the SignalR hub require an
  `Authorization: Bearer <token>` header. The frontend obtains the token from
  `AuthContext.getAccessToken()` — see [Authentication](#authentication).
- **Content type.** All JSON bodies are `application/json; charset=utf-8`.
- **IDs.** Stable strings — `kb_001`, `trc_001`, `msg_001`, `ses_…`. Use
  whatever scheme you like server-side; the frontend treats them as opaque.
- **Timestamps.** ISO-8601 with timezone, e.g. `2026-05-16T13:45:10-03:00`.
- **Color theme.** One of `primary | success | warning | error | human |
  neutral`. The frontend maps each to a CSS variable.
- **Icon.** Any string in the inline icon set (`src/components/Icon.tsx`). New
  names can be added there.
- **Pagination.** Endpoints that may return long lists wrap them in
  `{ items: T[], total: number, nextCursor?: string }`. Endpoints that return
  small, bounded lists (≤ 50 items) return the raw array.

### Error envelope

All non-2xx responses must use:

```json
{
  "error": {
    "code": "WORKFLOW_NOT_FOUND",
    "message": "No workflow with id 'foo'.",
    "details": { /* optional, free-form */ }
  }
}
```

Status code semantics:

| HTTP | Meaning                                                       |
| ---- | ------------------------------------------------------------- |
| 400  | Validation error — `error.code` typically `INVALID_ARGUMENT`. |
| 401  | Missing or invalid token. The client redirects to /login.     |
| 403  | Authenticated but not allowed for this workflow / session.    |
| 404  | Resource not found.                                           |
| 409  | Conflict — session already resolved, already in human mode…   |
| 429  | Rate limit. Include `Retry-After` header in seconds.          |
| 500  | Server error — `error.code` `INTERNAL`.                       |

---

## 2. REST endpoints

### 2.1 `GET /api/workflows`

Static catalog of every workflow the user is allowed to launch. The frontend
calls this on the Workflows page.

Response: `WorkflowDefinition[]`

```json
[
  {
    "id": "support",
    "title": "Support Workflow",
    "subtitle": "Multi-Agent Support — Microsoft Agent Framework",
    "description": "Triage, KB search, automated resolution, human handoff.",
    "icon": "layers",
    "colorTheme": "primary",
    "agents": [
      {
        "id": "triage",
        "icon": "git-branch",
        "title": "Triage Agent",
        "description": "Classifies the user's problem and routes to the right agent.",
        "colorTheme": "primary",
        "order": 1,
        "tools": []
      }
      /* … */
    ],
    "scenarios": [
      {
        "id": "known",
        "title": "Known issue",
        "label": "Can't login",
        "description": "Authentication problem with KB match and automatic resolution.",
        "message": "I can't log in to the system. My password is not working.",
        "flowType": "known"
      }
      /* … */
    ],
    "capabilities": {
      "humanHandoff": true,
      "knowledgeBase": true,
      "tracing": true
    }
  }
]
```

### 2.2 `GET /api/workflows/{workflowId}`

Single workflow — same shape as one element of `GET /api/workflows`. Used by
the run page when the user deep-links to `#/workflows/:id/run` without going
through the catalog. *(Optional — the frontend falls back to filtering the
catalog response.)*

### 2.3 `POST /api/workflow-sessions`

Creates a session for one workflow. Returns the minimum info the client needs
to subscribe via SignalR.

Request:

```json
{
  "workflowId": "support",
  "initialMessage": "…optional pre-seeded user message…"
}
```

Response:

```json
{
  "sessionId": "ses_01H9X…",
  "ticketId": "#TKT-4821"
}
```

### 2.4 `GET /api/workflow-sessions/{sessionId}`

Full snapshot for reopening an existing session (browser refresh, mobile
hand-off). Response is `SessionSnapshot` (see `src/types/workflow.ts`).

```json
{
  "sessionId": "ses_01H9X…",
  "workflowId": "support",
  "ticketId": "#TKT-4821",
  "status": "human-chat",
  "chatTitle": "Human Handoff",
  "chatSubtitle": "Human agent Daniel M. is active",
  "activeAgentId": "human",
  "category": "Integration / Unknown",
  "confidence": 0.52,
  "intent": "Unrecognized integration error",
  "humanMode": true,
  "assignedHumanAgent": { "id": "hum_01", "name": "Daniel M.", "icon": "headphones" },
  "resolutionSteps": [
    { "step": 1, "label": "Human agent resolved", "ok": true }
  ],
  "agents": [
    { "id": "triage", "state": "done", "tag": "Done", "activeTools": [] },
    { "id": "freq", "state": "done", "tag": "Done", "activeTools": [] },
    { "id": "res", "state": "idle", "tag": "Idle", "activeTools": [] },
    { "id": "pattern", "state": "wait", "tag": "Waiting", "activeTools": [] }
  ],
  "messages": [
    /* Message[] — see § 2.6 schema */
  ],
  "trace": [
    /* TraceEvent[] */
  ],
  "kb": [
    /* KbItem[] */
  ]
}
```

### 2.5 `POST /api/workflow-sessions/{sessionId}/reset`

Resets a session in place. Returns the fresh `SessionSnapshot`. The hub also
broadcasts the equivalent events so other connected clients update.

### 2.6 `GET /api/workflow-sessions/{sessionId}/messages?since={iso}`

Paginated history for reconnect catch-up. Each message:

```json
{
  "id": "msg_001",
  "type": "message",                       // "message" | "system" | "typing"
  "side": "left",                          // "left" | "right" | "center"
  "senderType": "agent",                   // "user" | "agent" | "human" | "system"
  "senderName": "Triage Agent",
  "icon": "git-branch",
  "bubbleStyle": "triage",                 // optional CSS hint
  "systemStyle": null,                     // "handoff" | "resolved" | "escalate" | null
  "text": "Identified as authentication issue.",
  "tools": [                               // optional; populated for tool-call messages
    { "name": "reset_password", "args": "user_id=U-4821", "ok": true }
  ],
  "createdAt": "2026-05-16T13:45:10-03:00",
  "splitMirror": false                     // also surface this message in the human-handoff split view
}
```

### 2.7 `GET /api/workflow-sessions/{sessionId}/trace?since={iso}`

Paginated trace log. Each entry:

```json
{
  "id": "trc_001",
  "time": "2026-05-16T13:45:10-03:00",
  "icon": "git-branch",
  "color": "primary",
  "title": "TriageAgent received message",
  "description": "Started classification.",
  "level": "info"             // "info" | "success" | "warning" | "error"
}
```

### 2.8 `GET /api/knowledge-base?workflowId=&query=`

Free-text search across the workflow's KB.

Response:

```json
{
  "items": [
    {
      "id": "kb_001",
      "title": "Login failure — password reset",
      "category": "Auth",
      "score": 0.97,
      "summary": "User unable to access account. Resolution: trigger password reset flow via admin panel.",
      "resolutionType": "password-reset",
      "tags": ["login", "password", "auth"]
    }
  ],
  "total": 1
}
```

### 2.9 `GET /api/scenarios?workflowId=` *(optional)*

Same shape as the `scenarios` array embedded in `GET /api/workflows`.
Useful if scenarios live in a separate authoring store.

---

## 3. SignalR hub

- **Hub URL:** `${VITE_API_BASE_URL}/hubs/workflow` (override with
  `VITE_SIGNALR_HUB_URL`).
- **Transport:** WebSockets preferred; SSE fallback automatic via the
  `@microsoft/signalr` client.
- **Auth:** the client sets `accessTokenFactory` to the same function used for
  REST. The server validates the bearer token (Azure-issued JWT in
  `entra` mode) before allowing the connection.
- **Reconnect:** the client uses `withAutomaticReconnect()`. The server should
  push the latest snapshot on (re)connect by listening for `JoinSession`.

### 3.1 Client → server (invocations)

| Method                                  | Args                            | Notes                                                                                |
| --------------------------------------- | ------------------------------- | ------------------------------------------------------------------------------------ |
| `JoinSession(sessionId)`                | string                          | Subscribes the connection to the session's group. The server should reply with a snapshot via `context`, `agent`, `message`, etc. |
| `LeaveSession(sessionId)`               | string                          | Unsubscribes the connection.                                                         |
| `SendUserMessage(sessionId, text)`      | string, string                  | The user types into the main chat or the split user pane.                            |
| `SendHumanMessage(sessionId, text)`     | string, string                  | The human agent types into the split human pane. Only valid when `humanMode = true`. |
| `RunScenario(sessionId, scenarioId)`    | string, string                  | Pre-seeded demo scenario. The server posts the scenario's `message` as a user message and starts the matching flow. |
| `MarkSolved(sessionId)`                 | string                          | Human handoff closure → triggers PatternRecord agent.                                |
| `ResetSession(sessionId)`               | string                          | Reset to a fresh state.                                                              |

All invocations return `Task` (void). Errors should be surfaced as
`HubException` with the same `error.code` strings as the REST envelope.

### 3.2 Server → client (events)

The server can broadcast the following events into a session's group. The
client merges them into a `SessionSnapshot`.

| Event       | Args                                              | Frontend handling                                                            |
| ----------- | ------------------------------------------------- | ---------------------------------------------------------------------------- |
| `message`   | `(sessionId, message: Message)`                   | Appends to `snapshot.messages`. If `splitMirror=true` and the session is in `humanMode`, it also renders in the user / human panes. |
| `trace`     | `(sessionId, event: TraceEvent)`                  | Appends to the Trace tab.                                                    |
| `agent`     | `(sessionId, agent: AgentRuntimeState)`           | Upserts agent state — drives pipeline highlighting and tool chips.           |
| `kb`        | `(sessionId, items: KbItem[])`                    | Replaces the KB tab contents.                                                |
| `context`   | `(sessionId, patch: Partial<SessionSnapshot>)`    | Patches the session header / context panel (status, intent, category, etc.).|
| `splitMode` | `(sessionId, on: boolean)`                        | Toggles the human-handoff split view.                                        |
| `typing`    | `(sessionId, container, label, on)`               | Shows or hides the typing indicator in `"msgs"`, `"user-msgs"`, or `"human-msgs"`. |

#### Type definitions for events

```ts
// AgentRuntimeState
{ id: string; state: "idle" | "active" | "done" | "human" | "wait"; tag: string; activeTools: string[] }

// Context patch
Partial<{
  status: "idle" | "triaging" | "searching-kb" | "resolving" | "recording" | "human-chat" | "resolved" | "error";
  chatTitle: string;
  chatSubtitle: string;
  activeAgentId: string | null;
  category: string | null;
  confidence: number | null;     // 0..1
  intent: string | null;
  humanMode: boolean;
  resolutionSteps: { step: number; label: string; ok: boolean }[];
}>
```

### 3.3 Recommended emission ordering

For the canonical "Known issue" flow:

1. `agent { id: "triage", state: "active", tag: "Running" }`
2. `trace { …TriageAgent received message }`
3. `typing { container: "msgs", label: "Triage Agent analyzing", on: true }`
4. *(server-side LLM call)*
5. `typing … on:false`
6. `context { intent, category, confidence }`
7. `trace { …classified Access & Auth (93%) }`
8. `message { senderType: "agent", senderName: "Triage Agent", … }`
9. `agent { id: "triage", state: "done", tag: "Done" }`
10. *(continue with `freq` → `res` → `pattern`)*
11. Final `context { status: "resolved" }`

For the human handoff flow, after `freq` returns no match, emit
`splitMode(true)` *before* the first human message so the UI is ready.

---

## 4. Authentication

The frontend supports two modes, switchable at build/run time via
`VITE_AUTH_MODE`:

### 4.1 `mock` mode (default)

- No external dependency.
- Any non-empty username/password is accepted by the login form.
- The token sent on REST and SignalR is the literal string
  `mock-token:<username>`. The backend, if reachable, can detect this and
  treat the user as anonymous-dev.

### 4.2 `entra` mode (Microsoft Entra ID / Azure AD)

Implemented with `@azure/msal-browser`. Required env vars:

| Variable                  | Description                                                                                |
| ------------------------- | ------------------------------------------------------------------------------------------ |
| `VITE_AUTH_MODE=entra`    | Switches the build to Entra mode.                                                          |
| `VITE_ENTRA_CLIENT_ID`    | Application (client) ID of the Entra app registration.                                     |
| `VITE_ENTRA_TENANT_ID`    | Tenant GUID, domain name, or `common` / `organizations`.                                   |
| `VITE_ENTRA_REDIRECT_URI` | Must exactly match a redirect URI registered as **SPA** on the app registration.           |
| `VITE_ENTRA_API_SCOPES`   | Space-separated additional scopes (e.g. `api://1234.../access_as_user`). `openid profile email` are always added. |

#### Required Entra app registration setup

1. Azure portal → **Microsoft Entra ID → App registrations → New registration**.
2. Supported account types: pick what suits your tenant.
3. Platform: **Single-page application (SPA)**. Redirect URI = the value of
   `VITE_ENTRA_REDIRECT_URI` — typically `https://<yoursite>.azurestaticapps.net/`
   in production and `http://localhost:5173/` for local dev.
4. Under **Expose an API**: add a scope like `access_as_user` if your backend
   needs an API audience.
5. Under **API permissions**: grant the scopes you want to request, then
   *Grant admin consent*.
6. Copy the **Application (client) ID** into `VITE_ENTRA_CLIENT_ID` and the
   **Directory (tenant) ID** into `VITE_ENTRA_TENANT_ID`.

#### Backend validation

The backend should validate the bearer JWT issued by Entra (`iss =
https://login.microsoftonline.com/{tenant}/v2.0`). For an ASP.NET Core 8 host:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
```

`appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant>",
    "ClientId": "<api-app-registration-id>",
    "Audience": "api://<api-app-registration-id>"
  }
}
```

For SignalR, accept the access token from the query string when the upgrade
request can't carry an `Authorization` header:

```csharp
options.Events = new JwtBearerEvents {
  OnMessageReceived = ctx => {
    var accessToken = ctx.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(accessToken) &&
        ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
      ctx.Token = accessToken;
    return Task.CompletedTask;
  }
};
```

---

## 5. Deployment notes

### 5.1 Azure Static Web Apps (frontend only)

1. `npm run build` produces a static `dist/` directory.
2. Deploy via the Static Web Apps GitHub Action or the Azure CLI:
   ```bash
   az staticwebapp create \
     --name agent-workflow-app \
     --resource-group rg-workflows \
     --source <repo> \
     --location <region> \
     --app-location agent-workflow-app \
     --output-location dist
   ```
3. Add a `staticwebapp.config.json` if you need API routes proxied to a
   separate backend (e.g. `linkedBackend: { backendResourceId: <appservice-id> }`).
4. Configure the environment variables (`VITE_*`) as build-time vars in the
   workflow file. Vite inlines them at build time.

### 5.2 Azure App Service (frontend + .NET backend)

1. Deploy the backend (`SignalR` + REST) as an App Service.
2. Build the frontend with `VITE_API_BASE_URL=https://<your-appservice>.azurewebsites.net`.
3. Either:
   - Host the static `dist/` alongside the backend (`wwwroot/`), **or**
   - Host the static frontend on Static Web Apps and link it to the App
     Service backend.
4. Enable **WebSockets** on the App Service (Settings → Configuration →
   General settings → Web sockets = On). SignalR needs them.
5. If you front the app with **Azure Front Door** or **App Gateway**, enable
   **session affinity (Affinity-Cookie)** so SignalR sticks to one backend
   instance, or back the hub with **Azure SignalR Service** (recommended).

### 5.3 Routing

The app uses **hash routing** (`/#/workflows/support/run`). Hash routing is
deployment-safe — it does not require server-side rewrite rules — which means
it works on Static Web Apps, App Service, S3, GitHub Pages, etc. with no
extra configuration.

---

## 6. Environment variable reference

| Variable                  | Required | Default                                 | Description                                                                |
| ------------------------- | -------- | --------------------------------------- | -------------------------------------------------------------------------- |
| `VITE_API_BASE_URL`       | No       | `""` (same origin / mock)               | REST base URL. Leave empty in dev to use the mock backend.                 |
| `VITE_SIGNALR_HUB_URL`    | No       | `${VITE_API_BASE_URL}/hubs/workflow`    | SignalR hub URL.                                                           |
| `VITE_AUTH_MODE`          | No       | `mock`                                  | `mock` or `entra`.                                                         |
| `VITE_ENTRA_CLIENT_ID`    | If entra | —                                       | App registration client ID.                                                |
| `VITE_ENTRA_TENANT_ID`    | No       | `common`                                | Tenant.                                                                    |
| `VITE_ENTRA_REDIRECT_URI` | No       | Current page origin                     | Must match the SPA redirect URI on the app registration.                   |
| `VITE_ENTRA_API_SCOPES`   | No       | `""`                                    | Space-separated scopes to request alongside `openid profile email`.        |
| `VITE_MOCK_LATENCY_MS`    | No       | `900`                                   | Tunes the mock SignalR pacing.                                             |

---

## 7. Frontend-side conventions

These aren't part of the wire contract but make backend-frontend integration
smoother:

1. **One configurable source of truth.** `src/config/workflows.ts` is the
   static fallback for `GET /api/workflows`. Backend changes should be
   mirrored here so dev still works offline.
2. **Schema = `src/types/workflow.ts`.** When the backend evolves, update
   this file and the doc above in the same PR.
3. **`data-testid`** is set on every meaningful interactive or dynamic
   element. New components must follow the `{action}-{target}` /
   `{type}-{content}` / `{type}-{content}-{id}` naming pattern.
4. **No browser storage of secrets.** The MSAL cache lives in
   `sessionStorage`. We never store mock tokens or workflow data in
   `localStorage` because the production sandbox can disable it.
5. **Markdown in bubbles.** Agent messages may contain HTML for `<strong>`
   formatting. The backend is responsible for sanitization; the frontend
   renders trusted content via `dangerouslySetInnerHTML` (see
   `ChatPanel.tsx`).
