/**
 * Shared types for workflows. These match the response shapes documented in
 * `docs/endpoint-contracts.md`. Keep this file the single source of truth for
 * the frontend — backend changes should be reflected here and in the docs.
 */

export type ColorTheme = 'primary' | 'success' | 'warning' | 'error' | 'human' | 'neutral';

export type AgentState = 'idle' | 'active' | 'done' | 'human' | 'wait';

export interface AgentDefinition {
  id: string;
  icon: string;
  title: string;
  description: string;
  colorTheme: ColorTheme;
  order: number;
  tools: string[];
}

export type ScenarioFlowType = 'known' | 'tool' | 'human-handoff' | string;

export interface ScenarioDefinition {
  id: string;
  title: string;
  label: string;
  description: string;
  message: string;
  flowType: ScenarioFlowType;
}

export interface WorkflowCapabilities {
  humanHandoff: boolean;
  knowledgeBase: boolean;
  tracing: boolean;
}

export interface WorkflowDefinition {
  id: string;
  title: string;
  subtitle?: string;
  description: string;
  icon: string;
  colorTheme: ColorTheme;
  agents: AgentDefinition[];
  scenarios: ScenarioDefinition[];
  capabilities: WorkflowCapabilities;
}

/* ── Session / runtime types ─────────────────────────────────────────── */

export type SessionStatus =
  | 'idle'
  | 'triaging'
  | 'searching-kb'
  | 'resolving'
  | 'recording'
  | 'human-chat'
  | 'resolved'
  | 'error';

export type MessageSide = 'left' | 'right' | 'center';
export type SenderType = 'user' | 'agent' | 'human' | 'system';

export interface ToolCall {
  name: string;
  args: string;
  ok: boolean;
}

export interface Message {
  id: string;
  type: 'message' | 'system' | 'typing';
  side: MessageSide;
  senderType: SenderType;
  senderName: string;
  icon: string;
  bubbleStyle?: string;
  systemStyle?: 'handoff' | 'resolved' | 'escalate';
  text: string;
  tools?: ToolCall[];
  createdAt: string; // ISO-8601
  /** When true the message should also appear in the human-handoff split view. */
  splitMirror?: boolean;
}

export interface TraceEvent {
  id: string;
  time: string; // ISO-8601
  icon: string;
  color: string;
  title: string;
  description?: string;
  level: 'info' | 'success' | 'warning' | 'error';
}

export interface KbItem {
  id: string;
  title: string;
  category: string;
  score: number;
  summary: string;
  resolutionType?: string;
  tags?: string[];
}

export interface ResolutionStep {
  step: number;
  label: string;
  ok: boolean;
}

export interface AgentRuntimeState {
  id: string;
  state: AgentState;
  tag: string;
  activeTools: string[];
}

export interface SessionSnapshot {
  sessionId: string;
  workflowId: string;
  ticketId: string;
  status: SessionStatus;
  chatTitle: string;
  chatSubtitle: string;
  activeAgentId: string | null;
  category: string | null;
  confidence: number | null;
  intent: string | null;
  humanMode: boolean;
  assignedHumanAgent: { id: string; name: string; icon: string } | null;
  resolutionSteps: ResolutionStep[];
  agents: AgentRuntimeState[];
  messages: Message[];
  trace: TraceEvent[];
  kb: KbItem[];
}
