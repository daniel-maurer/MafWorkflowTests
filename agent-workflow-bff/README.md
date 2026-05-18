# Agent Workflow BFF

ASP.NET Core 8 Backend-for-Frontend for the React workflow console.

This project is intentionally **not** the workflow engine. It sits between the React frontend and your MAF application.

## Architecture

```text
React frontend
  -> REST /api/*
  -> SignalR /hubs/workflow

Workflow BFF
  -> reads Workflows/*.json
  -> creates sessions
  -> bridges commands/events

MAF application
  -> SignalR /hubs/maf
  -> executes the real workflow
```

## What this BFF owns

- Workflow catalog from JSON files.
- Frontend REST contract.
- Frontend SignalR contract.
- Session IDs and ticket IDs.
- Lightweight session snapshots for refresh/reconnect.
- Forwarding frontend commands to MAF.
- Forwarding MAF events back to frontend clients.

## What the MAF app owns

- Agent orchestration.
- Tool calls.
- Classification.
- Dynamic KB search.
- Resolution logic.
- Human handoff decisions.
- Pattern recording.

## Run locally

```bash
cd agent-workflow-bff
dotnet restore
dotnet run
```

Default BFF URL:

```text
http://localhost:5088
```

Run the frontend against the BFF:

```bash
cd ../agent-workflow-app
VITE_API_BASE_URL=http://localhost:5088 npm run dev
```

## Hubs

Frontend hub:

```text
/hubs/workflow
```

MAF worker hub:

```text
/hubs/maf
```

## Workflow configuration

Add or change workflows by editing JSON files in:

```text
Workflows/
```

Each workflow has:

- UI metadata.
- Agent pipeline metadata.
- Scenario buttons.
- Capability flags.
- MAF workflow binding.
- Fixed KB entries.

## Important docs

- `Docs/bff-architecture.md`
- `Docs/instructions-for-maf-development-agent.md`

The second file is the document to send to your MAF development agent.
