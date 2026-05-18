# Agent Workflow Console

A React + Vite frontend for **multi-agent workflow consoles** — designed against the original `support-workflow-demo-1.html` mock-up and the endpoint contract in `For-this-html-i-want-develop-the-endpoint-contrac.md`. The app is **configurable**, **deployment-safe** (hash routing), and ships with a complete mock SignalR runtime so it can be demoed end-to-end **without a backend**.

## TL;DR

```bash
cd agent-workflow-app
npm install
npm run dev        # http://localhost:5173
```

Open `http://localhost:5173/#/login`, type any non-empty username, hit **Sign in**. Pick a workflow → **Launch** → use the chat or the demo-scenario buttons.

---

## What's in the box

### Three pages

| Route                            | Page                | Purpose                                                                                                         |
| -------------------------------- | ------------------- | --------------------------------------------------------------------------------------------------------------- |
| `/#/login`                       | `LoginPage`         | Mock username login (default) or Microsoft Entra ID sign-in (configurable).                                     |
| `/#/workflows`                   | `WorkflowsPage`     | Multi-select catalog. Pick one or more workflows side-by-side, see capabilities, hit **Launch**.                |
| `/#/workflows/:workflowId/run`   | `WorkflowRunPage`   | 3-column workflow console: agent pipeline · live chat (single or split human-handoff mode) · context/trace/KB. |

### Three workflows (`src/config/workflows.ts`)

1. **support** — Multi-Agent Support (4 agents: triage → freq → resolution → pattern). Mirrors the original demo.
2. **incident-triage** — Live incident classification (3 agents: parser → classifier → on-call dispatcher).
3. **kb-curator** — KB maintenance pipeline (2 agents: pattern miner → article drafter).

Add a new workflow by appending another `WorkflowDefinition` to the array. Everything else (UI, pipeline, scenarios, KB tabs) is data-driven.

### Two auth modes (`VITE_AUTH_MODE`)

- **`mock`** *(default)* — any non-empty username signs in. Token is `mock-token:<username>`.
- **`entra`** — Microsoft Entra ID (Azure AD) via `@azure/msal-browser` + `@azure/msal-react`. MSAL is loaded **lazily** only in entra mode so the mock bundle stays small.

### Realtime SignalR client (`src/services/signalr.ts`)

- When `VITE_API_BASE_URL` is set, the app uses a real `@microsoft/signalr` `HubConnection` against `${VITE_API_BASE_URL}/hubs/workflow`.
- When `VITE_API_BASE_URL` is **empty**, it transparently swaps in a **mock hub** (`src/services/mockSignalr.ts`) that replays the three demo flows (known issue, tool call, human handoff) with original timings — no backend needed.

---

## File map

```
agent-workflow-app/
├─ docs/
│  └─ endpoint-contracts.md       # REST + SignalR contract (read this for backend)
├─ src/
│  ├─ App.tsx                     # AuthProvider + HashRouter
│  ├─ auth/
│  │  ├─ AuthContext.tsx          # mock + entra modes, useAuth()
│  │  └─ msal.ts                  # lazy MSAL wrapper
│  ├─ components/
│  │  ├─ Icon.tsx                 # inline-SVG icon set (no lucide dep)
│  │  ├─ RequireAuth.tsx
│  │  ├─ TopBar.tsx
│  │  └─ workflow/
│  │     ├─ AgentPipeline.tsx     # left column
│  │     ├─ ChatPanel.tsx         # middle column, single + split mode
│  │     └─ ContextPanel.tsx      # right column: Context / Trace / KB tabs
│  ├─ config/
│  │  ├─ env.ts                   # all VITE_* vars in one place
│  │  └─ workflows.ts             # workflow catalog (add yours here)
│  ├─ hooks/
│  │  ├─ useTheme.ts
│  │  └─ useWorkflowSession.ts    # reducer + hub plumbing for one session
│  ├─ pages/
│  │  ├─ LoginPage.tsx
│  │  ├─ WorkflowsPage.tsx
│  │  └─ WorkflowRunPage.tsx
│  ├─ services/
│  │  ├─ apiClient.ts             # REST client (falls back to static catalog)
│  │  ├─ signalr.ts               # real-vs-mock hub dispatch
│  │  └─ mockSignalr.ts           # scripted flows for backend-less dev
│  ├─ styles/
│  │  ├─ tokens.css               # design tokens (dark/light) ported from demo
│  │  ├─ global.css
│  │  └─ workflow.css             # 3-column workflow layout
│  └─ types/workflow.ts           # single source of truth for shared types
├─ .env.example                   # documents every VITE_* variable
├─ index.html                     # loads Inter + JetBrains Mono
├─ vite.config.ts                 # base: './' so the bundle is path-portable
├─ package.json
└─ qa/                            # Playwright screenshots from acceptance run
```

---

## Running and building

```bash
npm install            # installs React, Vite, MSAL, SignalR, react-router-dom
npm run dev            # Vite dev server on http://localhost:5173
npm run typecheck      # tsc -b, no emit
npm run build          # tsc -b && vite build → dist/
npm run preview        # serves dist/ on http://localhost:4173
```

Build output (current):

```
dist/index.html                    0.73 KB │ gzip:  0.40 KB
dist/assets/index-*.css           23.64 KB │ gzip:  4.43 KB
dist/assets/index-vendor-*.js    265.32 KB │ gzip: 65.03 KB
dist/assets/index-app-*.js       282.24 KB │ gzip: 85.69 KB
```

---

## Environment variables

All variables must be prefixed with `VITE_` (Vite client-side requirement). See `.env.example` for the full template.

| Variable                      | Required          | Description                                                                                  |
| ----------------------------- | ----------------- | -------------------------------------------------------------------------------------------- |
| `VITE_API_BASE_URL`           | no                | REST base URL (no trailing slash). When empty, mock hub + static catalog are used.           |
| `VITE_SIGNALR_HUB_URL`        | no                | SignalR hub URL. Defaults to `${VITE_API_BASE_URL}/hubs/workflow`.                           |
| `VITE_AUTH_MODE`              | no                | `mock` (default) or `entra`.                                                                 |
| `VITE_ENTRA_CLIENT_ID`        | entra only        | App registration's Application (client) ID.                                                  |
| `VITE_ENTRA_TENANT_ID`        | entra only        | Tenant GUID, your tenant domain, or `common` / `organizations`.                              |
| `VITE_ENTRA_REDIRECT_URI`     | entra only        | Redirect URI registered in the app registration.                                             |
| `VITE_ENTRA_API_SCOPES`       | entra only        | Space-separated API scopes, e.g. `api://<api-client-id>/access_as_user`.                     |
| `VITE_MOCK_LATENCY_MS`        | no                | Simulate backend latency in mock SignalR client. Defaults to `900`.                          |

Copy `.env.example` to `.env.local` for local development.

---

## Design decisions

- **HashRouter** so the bundle works under any path (Azure Static Web Apps subpath, S3 preview, custom hosting). `vite.config.ts` also sets `base: './'`.
- **No `localStorage`** — sandbox-blocked in some hosting environments. MSAL is configured with `cacheLocation: sessionStorage`, and the mock auth state lives in React context only (re-login required on full page reload, which matches the demo's intent).
- **Design tokens ported verbatim** from the original `support-workflow-demo-1.html`. Dark theme is the default — most workflow consoles run in low-light environments.
- **`<TopBar>` shows session identity and a theme toggle** on every authenticated page. Pipeline chips collapse on small screens.
- **Inline-SVG `<Icon>`** instead of a lucide-react dependency, to keep the bundle small and avoid icon-set drift.
- **HTML in agent messages is rendered via `dangerouslySetInnerHTML`** because the original demo emits formatted snippets (e.g. `<code>`). **The backend is responsible for sanitization.** See `docs/endpoint-contracts.md` § Message schema.
- **Optimistic local user messages** (`local-user-message` in `useWorkflowSession`) so the user sees their text instantly. The backend is expected to ack the message without echoing it back to the sender.
- **`data-testid` everywhere** for interactive controls and meaningful dynamic content. Pattern:
  - Interactive: `{action}-{target}` (e.g. `button-send-main`, `input-username`).
  - Display: `{type}-{content}` (e.g. `text-ticket-id`, `text-chat-title`).
  - Dynamic lists: `{type}-{content}-{id}` (e.g. `card-workflow-support`, `pipeline-step-freq`, `trace-trc_001`).

---

## Backend integration

The full contract lives in **[`docs/endpoint-contracts.md`](./docs/endpoint-contracts.md)**. Highlights:

- **REST**: `/api/workflows`, `/api/workflow-sessions`, `/api/workflow-sessions/:id/messages`, `/api/knowledge-base`, etc.
- **SignalR hub**: `/hubs/workflow`. Seven client→server methods (`JoinSession`, `SendUserMessage`, `RunScenario`, …) and seven server→client events (`message`, `trace`, `agent`, `kb`, `context`, `splitMode`, `typing`).
- **Auth**: `Authorization: Bearer <jwt>` on REST; same token via `accessTokenFactory` on SignalR.
- **Azure deployment notes** for Static Web Apps and App Service are included.

To wire the app to a real backend, set `VITE_API_BASE_URL` and (if different from the default) `VITE_SIGNALR_HUB_URL`. No other code changes required.

---

## QA performed

All tests executed against `npm run dev` on `http://127.0.0.1:5173` with Playwright (chromium, headless). Screenshots are in `qa/`.

- ✅ `tsc -b` clean
- ✅ `vite build` clean (warnings only — Rollup `/*#__PURE__*/` comments inside `@microsoft/signalr`, harmless)
- ✅ Login (mock mode) — username/password fields, Sign-in disables until username is filled, submit navigates to `/#/workflows`
- ✅ Workflows page — three cards rendered, multi-select via checkbox UI, **Launch** navigates to first selected
- ✅ Workflow run (known-issue flow) — typed "I can't login" → triage → freq → resolution (tool calls + KB match score 0.97) → pattern → resolved. **No duplicate user message** after fix to mock echo.
- ✅ Workflow run (human-handoff scenario) — split chat appears (user view ‖ Sarah M. agent view), messages mirror across both, **Mark as Solved → trigger Pattern Agent** closes the session and updates pipeline.
- ✅ Trace tab and KB tab populate live as events arrive.
- ✅ Theme toggle (dark ⇄ light) — verified visually, both themes legible.
- ✅ Mobile 390×844 — topbar wraps; pipeline chips hidden < 880 px; three columns stack vertically; no horizontal overflow.
- ✅ Zero `console.error` and zero uncaught page errors during the full QA cycle.

---

## Conventions for future contributors

- **Add a workflow** → append to `src/config/workflows.ts`. Fields are typed in `src/types/workflow.ts`.
- **Add a SignalR event** → add a member to `HubEvent` in `src/types/workflow.ts`, handle it in `useWorkflowSession` reducer, emit it from `mockSignalr.ts` if you want a dev demo.
- **Add a REST endpoint** → add a method to `apiClient` in `src/services/apiClient.ts` and document it in `docs/endpoint-contracts.md`.
- **Add a page** → add the component to `src/pages/`, register the route in `src/App.tsx` (inside `RequireAuth` for protected routes), add a `data-testid` for the page's primary container.
- **Run typecheck + build before committing** (`npm run typecheck && npm run build`).
- **Update the contract doc whenever the wire format changes** — that file is the source of truth for backend authors.

---

## Deployment

Static bundle in `dist/`. Suitable for Azure Static Web Apps, Azure App Service (with a Node serve), S3 + CloudFront, or any static host.

For **Azure Static Web Apps** with Entra ID, see `docs/endpoint-contracts.md` § "Azure deployment notes" — the redirect URI in your app registration must match `VITE_ENTRA_REDIRECT_URI`.

For preview deployment from this workspace (Perplexity sandbox), the main agent can run:

```
deploy_website(
  project_path="/home/user/workspace/agent-workflow-app/dist",
  site_name="agent-workflow-app",
  entry_point="index.html",
)
```
