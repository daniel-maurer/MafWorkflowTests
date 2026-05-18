/**
 * React hook that owns one workflow session.
 *
 * Responsibilities:
 *   • Creates the session against the backend (`apiClient.createSession`).
 *   • Builds the SignalR `WorkflowHub`, starts it, joins the session group.
 *   • Reduces incoming hub events into a `SessionSnapshot` that the UI renders.
 *   • Exposes imperative actions (sendUser, sendHuman, runScenario, etc.).
 */
import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from 'react';
import { apiClient } from '@/services/apiClient';
import {
  createWorkflowHub,
  type ConnectionStatus,
  type WorkflowEvent,
  type WorkflowHub,
} from '@/services/signalr';
import { env } from '@/config/env';
import type {
  AgentRuntimeState,
  Message,
  SessionSnapshot,
  TraceEvent,
  WorkflowDefinition,
} from '@/types/workflow';

interface State extends SessionSnapshot {
  splitMode: boolean;
  typing: Record<'msgs' | 'user-msgs' | 'human-msgs', string | null>;
}

type Action =
  | { type: 'init'; workflow: WorkflowDefinition; sessionId: string; ticketId: string }
  | { type: 'event'; ev: WorkflowEvent }
  | { type: 'local-user-message'; text: string }
  | { type: 'reset'; workflow: WorkflowDefinition };

function emptyState(workflow: WorkflowDefinition, sessionId = '', ticketId = ''): State {
  return {
    sessionId,
    workflowId: workflow.id,
    ticketId,
    status: 'idle',
    chatTitle: workflow.title,
    chatSubtitle: 'Waiting for your message...',
    activeAgentId: workflow.agents[0]?.id ?? null,
    category: null,
    confidence: null,
    intent: null,
    humanMode: false,
    assignedHumanAgent: null,
    resolutionSteps: [],
    agents: workflow.agents.map((a, idx) => ({
      id: a.id,
      state: idx === 0 ? 'active' : 'idle',
      tag: idx === 0 ? 'Running' : 'Idle',
      activeTools: [],
    })),
    messages: [],
    trace: [],
    kb: [],
    splitMode: false,
    typing: { msgs: null, 'user-msgs': null, 'human-msgs': null },
  };
}

function upsertAgent(list: AgentRuntimeState[], next: AgentRuntimeState): AgentRuntimeState[] {
  const idx = list.findIndex((a) => a.id === next.id);
  if (idx === -1) return [...list, next];
  const copy = list.slice();
  copy[idx] = next;
  return copy;
}

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'init': {
      const base = emptyState(action.workflow, action.sessionId, action.ticketId);
      return { ...base, chatSubtitle: 'Connected. Type a message or pick a scenario.' };
    }
    case 'reset': {
      return emptyState(action.workflow, state.sessionId, state.ticketId);
    }
    case 'local-user-message': {
      // Não adiciona mensagem localmente; espera mensagem oficial do SignalR
      return state;
    }
    case 'event': {
      const ev = action.ev;
      switch (ev.type) {
        case 'message':
          return { ...state, messages: [...state.messages, ev.message] };
        case 'trace':
          return { ...state, trace: [...state.trace, ev.event] };
        case 'kb':
          return { ...state, kb: ev.items };
        case 'agent':
          return { ...state, agents: upsertAgent(state.agents, ev.agent) };
        case 'context':
          return { ...state, ...ev.patch };
        case 'split-mode':
          return { ...state, splitMode: ev.on, humanMode: ev.on || state.humanMode };
        case 'typing':
          return {
            ...state,
            typing: { ...state.typing, [ev.container]: ev.on ? ev.label : null },
          };
        default:
          return state;
      }
    }
    default:
      return state;
  }
}

export interface WorkflowSessionApi {
  workflow: WorkflowDefinition;
  snapshot: State;
  hubStatus: ConnectionStatus;
  error: string | null;
  ready: boolean;
  sendUserMessage(text: string): Promise<void>;
  sendHumanMessage(text: string): Promise<void>;
  runScenario(scenarioId: string): Promise<void>;
  markSolved(): Promise<void>;
  reset(): Promise<void>;
}

export function useWorkflowSession(workflow: WorkflowDefinition, accessTokenFactory: () => Promise<string | null>): WorkflowSessionApi {
  const [state, dispatch] = useReducer(reducer, workflow, emptyState);
  const [hubStatus, setHubStatus] = useState<ConnectionStatus>('idle');
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const hubRef = useRef<WorkflowHub | null>(null);
  const sessionIdRef = useRef<string>('');
  const tokenFactoryRef = useRef(accessTokenFactory);
  tokenFactoryRef.current = accessTokenFactory;

  // Connect once per workflow.
  useEffect(() => {
    let cancelled = false;
    const hub = createWorkflowHub({
      hubUrl: env.signalrHubUrl,
      accessTokenFactory: () => tokenFactoryRef.current(),
      workflowId: workflow.id,
    });
    hubRef.current = hub;

    const offEvents = hub.onEvent((ev) => dispatch({ type: 'event', ev }));
    const offStatus = hub.onStatus((s, err) => {
      setHubStatus(s);
      if (err) setError(err.message);
    });

    (async () => {
      try {
        await hub.start();
        const session = await apiClient.createSession({ workflowId: workflow.id });
        if (cancelled) return;
        sessionIdRef.current = session.sessionId;
        await hub.joinSession(session.sessionId);
        dispatch({
          type: 'init',
          workflow,
          sessionId: session.sessionId,
          ticketId: session.ticketId,
        });
        setReady(true);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to start session.');
      }
    })();

    return () => {
      cancelled = true;
      offEvents();
      offStatus();
      const sid = sessionIdRef.current;
      hub
        .leaveSession(sid)
        .catch(() => undefined)
        .finally(() => hub.stop().catch(() => undefined));
    };
  }, [workflow]);

  const sendUserMessage = useCallback(
    async (text: string) => {
      const hub = hubRef.current;
      const sid = sessionIdRef.current;
      if (!hub || !sid || !text.trim()) return;
      dispatch({ type: 'local-user-message', text });
      await hub.sendUserMessage(sid, text);
    },
    [],
  );

  const sendHumanMessage = useCallback(async (text: string) => {
    const hub = hubRef.current;
    const sid = sessionIdRef.current;
    if (!hub || !sid || !text.trim()) return;
    await hub.sendHumanMessage(sid, text);
  }, []);

  const runScenario = useCallback(async (scenarioId: string) => {
    const hub = hubRef.current;
    const sid = sessionIdRef.current;
    if (!hub || !sid) return;
    await hub.runScenario(sid, scenarioId);
  }, []);

  const markSolved = useCallback(async () => {
    const hub = hubRef.current;
    const sid = sessionIdRef.current;
    if (!hub || !sid) return;
    await hub.markSolved(sid);
  }, []);

  const reset = useCallback(async () => {
    const hub = hubRef.current;
    const sid = sessionIdRef.current;
    if (!hub || !sid) return;
    await hub.resetSession(sid);
    dispatch({ type: 'reset', workflow });
  }, [workflow]);

  return useMemo(
    () => ({
      workflow,
      snapshot: state,
      hubStatus,
      error,
      ready,
      sendUserMessage,
      sendHumanMessage,
      runScenario,
      markSolved,
      reset,
    }),
    [workflow, state, hubStatus, error, ready, sendUserMessage, sendHumanMessage, runScenario, markSolved, reset],
  );
}
