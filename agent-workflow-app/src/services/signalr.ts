/**
 * SignalR realtime client.
 *
 * In production this connects to the workflow hub at `VITE_SIGNALR_HUB_URL`
 * (or `${VITE_API_BASE_URL}/hubs/workflow`). When `VITE_API_BASE_URL` is empty
 * the implementation switches to a deterministic mock client that replays the
 * scripted flows documented in `docs/endpoint-contracts.md`. That keeps the UI
 * fully functional during local development.
 *
 * Hub server contract:
 *   • Invokable: JoinSession(sessionId), LeaveSession(sessionId),
 *                SendUserMessage(sessionId, text),
 *                SendHumanMessage(sessionId, text),
 *                RunScenario(sessionId, scenarioId),
 *                MarkSolved(sessionId),
 *                ResetSession(sessionId)
 *   • Events emitted by the server: see WorkflowEvent below.
 */
import * as signalR from '@microsoft/signalr';
import { env } from '@/config/env';
import type {
  AgentRuntimeState,
  KbItem,
  Message,
  ResolutionStep,
  SessionStatus,
  TraceEvent,
} from '@/types/workflow';
import { createMockHubConnection, type MockHubConnection } from './mockSignalr';

/** Discriminated union of all events the workflow hub emits. */
export type WorkflowEvent =
  | { type: 'message'; sessionId: string; message: Message }
  | { type: 'trace'; sessionId: string; event: TraceEvent }
  | { type: 'agent'; sessionId: string; agent: AgentRuntimeState }
  | { type: 'kb'; sessionId: string; items: KbItem[] }
  | {
      type: 'context';
      sessionId: string;
      patch: Partial<{
        status: SessionStatus;
        chatTitle: string;
        chatSubtitle: string;
        activeAgentId: string | null;
        category: string | null;
        confidence: number | null;
        intent: string | null;
        humanMode: boolean;
        resolutionSteps: ResolutionStep[];
      }>;
    }
  | { type: 'split-mode'; sessionId: string; on: boolean }
  | { type: 'typing'; sessionId: string; container: 'msgs' | 'user-msgs' | 'human-msgs'; label: string; on: boolean };

export type WorkflowEventHandler = (ev: WorkflowEvent) => void;
export type ConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'failed';
export type ConnectionStatusHandler = (status: ConnectionStatus, error?: Error) => void;

export interface WorkflowHub {
  status: ConnectionStatus;
  start(): Promise<void>;
  stop(): Promise<void>;
  onEvent(handler: WorkflowEventHandler): () => void;
  onStatus(handler: ConnectionStatusHandler): () => void;
  joinSession(sessionId: string): Promise<void>;
  leaveSession(sessionId: string): Promise<void>;
  sendUserMessage(sessionId: string, text: string): Promise<void>;
  sendHumanMessage(sessionId: string, text: string): Promise<void>;
  runScenario(sessionId: string, scenarioId: string): Promise<void>;
  markSolved(sessionId: string): Promise<void>;
  resetSession(sessionId: string): Promise<void>;
}

interface CreateHubOptions {
  hubUrl: string;
  accessTokenFactory?: () => Promise<string | null>;
  workflowId?: string;
}

export function createWorkflowHub(opts: CreateHubOptions): WorkflowHub {
  // When no backend is configured we run the mock client.
  if (!env.apiBaseUrl) {
    return wrapMock(createMockHubConnection({ workflowId: opts.workflowId ?? 'support' }));
  }
  return wrapReal(opts);
}

/* ── Real implementation ─────────────────────────────────────────────── */
function wrapReal({ hubUrl, accessTokenFactory }: CreateHubOptions): WorkflowHub {
  let status: ConnectionStatus = 'idle';
  const eventHandlers = new Set<WorkflowEventHandler>();
  const statusHandlers = new Set<ConnectionStatusHandler>();

  const setStatus = (next: ConnectionStatus, err?: Error) => {
    status = next;
    statusHandlers.forEach((h) => h(next, err));
  };

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: async () => (accessTokenFactory ? (await accessTokenFactory()) ?? '' : ''),
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  const dispatch = (ev: WorkflowEvent) => eventHandlers.forEach((h) => h(ev));

  connection.on('message', (sessionId: string, message: Message) =>
    dispatch({ type: 'message', sessionId, message }),
  );
  connection.on('trace', (sessionId: string, event: TraceEvent) =>
    dispatch({ type: 'trace', sessionId, event }),
  );
  connection.on('agent', (sessionId: string, agent: AgentRuntimeState) =>
    dispatch({ type: 'agent', sessionId, agent }),
  );
  connection.on('kb', (sessionId: string, items: KbItem[]) =>
    dispatch({ type: 'kb', sessionId, items }),
  );
  connection.on('context', (sessionId: string, patch: WorkflowEvent extends { type: 'context'; patch: infer P } ? P : never) =>
    dispatch({ type: 'context', sessionId, patch }),
  );
  connection.on('splitMode', (sessionId: string, on: boolean) =>
    dispatch({ type: 'split-mode', sessionId, on }),
  );
  connection.on('typing', (sessionId: string, container: 'msgs' | 'user-msgs' | 'human-msgs', label: string, on: boolean) =>
    dispatch({ type: 'typing', sessionId, container, label, on }),
  );

  connection.onreconnecting((err) => setStatus('reconnecting', err ?? undefined));
  connection.onreconnected(() => setStatus('connected'));
  connection.onclose((err) => setStatus('disconnected', err ?? undefined));

  return {
    get status() {
      return status;
    },
    async start() {
      if (status === 'connected' || status === 'connecting') return;
      setStatus('connecting');
      try {
        await connection.start();
        setStatus('connected');
      } catch (err) {
        setStatus('failed', err instanceof Error ? err : new Error(String(err)));
        throw err;
      }
    },
    async stop() {
      await connection.stop();
      setStatus('disconnected');
    },
    onEvent(handler) {
      eventHandlers.add(handler);
      return () => eventHandlers.delete(handler);
    },
    onStatus(handler) {
      statusHandlers.add(handler);
      handler(status);
      return () => statusHandlers.delete(handler);
    },
    joinSession: (sessionId) => connection.invoke('JoinSession', sessionId),
    leaveSession: (sessionId) => connection.invoke('LeaveSession', sessionId),
    sendUserMessage: (sessionId, text) => connection.invoke('SendUserMessage', sessionId, text),
    sendHumanMessage: (sessionId, text) => connection.invoke('SendHumanMessage', sessionId, text),
    runScenario: (sessionId, scenarioId) => connection.invoke('RunScenario', sessionId, scenarioId),
    markSolved: (sessionId) => connection.invoke('MarkSolved', sessionId),
    resetSession: (sessionId) => connection.invoke('ResetSession', sessionId),
  };
}

/* ── Mock wrapper (delegates to mockSignalr.ts) ──────────────────────── */
function wrapMock(mock: MockHubConnection): WorkflowHub {
  let status: ConnectionStatus = 'idle';
  const statusHandlers = new Set<ConnectionStatusHandler>();

  const setStatus = (next: ConnectionStatus) => {
    status = next;
    statusHandlers.forEach((h) => h(next));
  };

  return {
    get status() {
      return status;
    },
    async start() {
      setStatus('connecting');
      await new Promise((r) => setTimeout(r, 80));
      setStatus('connected');
    },
    async stop() {
      mock.dispose();
      setStatus('disconnected');
    },
    onEvent: (h) => mock.onEvent(h),
    onStatus(handler) {
      statusHandlers.add(handler);
      handler(status);
      return () => statusHandlers.delete(handler);
    },
    joinSession: (sessionId) => mock.joinSession(sessionId),
    leaveSession: (sessionId) => mock.leaveSession(sessionId),
    sendUserMessage: (sessionId, text) => mock.sendUserMessage(sessionId, text),
    sendHumanMessage: (sessionId, text) => mock.sendHumanMessage(sessionId, text),
    runScenario: (sessionId, scenarioId) => mock.runScenario(sessionId, scenarioId),
    markSolved: (sessionId) => mock.markSolved(sessionId),
    resetSession: (sessionId) => mock.resetSession(sessionId),
  };
}
