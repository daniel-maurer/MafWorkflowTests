/**
 * In-memory mock for the workflow SignalR hub.
 *
 * Re-implements the three scripted flows from the original
 * `support-workflow-demo-1.html` (known / tool / human-handoff) so the React
 * frontend can be developed and demoed against zero backend.
 *
 * Each "invoke" returns immediately. Server-side events are scheduled with
 * setTimeout and pushed to subscribers via `onEvent`. The cadence mirrors the
 * timings used in the source demo.
 */
import { env } from '@/config/env';
import type {
  AgentRuntimeState,
  AgentState,
  KbItem,
  Message,
  ResolutionStep,
  SessionStatus,
  TraceEvent,
} from '@/types/workflow';
import type { WorkflowEvent, WorkflowEventHandler } from './signalr';

type ContextPatch = Partial<{
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

type TraceInput = Omit<TraceEvent, 'id' | 'time'>;
type MessageInput = Omit<Message, 'id' | 'createdAt'> & { id?: string };

export interface MockHubConnection {
  onEvent(handler: WorkflowEventHandler): () => void;
  joinSession(sessionId: string): Promise<void>;
  leaveSession(sessionId: string): Promise<void>;
  sendUserMessage(sessionId: string, text: string): Promise<void>;
  sendHumanMessage(sessionId: string, text: string): Promise<void>;
  runScenario(sessionId: string, scenarioId: string): Promise<void>;
  markSolved(sessionId: string): Promise<void>;
  resetSession(sessionId: string): Promise<void>;
  dispose(): void;
}

const SCALE = Math.max(0.05, env.mockLatencyMs / 900); // tweak speed via env

function genId(prefix: string) {
  return `${prefix}-${Date.now().toString(36)}-${Math.floor(Math.random() * 1e6).toString(36)}`;
}
function nowIso() {
  return new Date().toISOString();
}
function delay(ms: number) {
  return new Promise<void>((r) => setTimeout(r, ms * SCALE));
}

export function createMockHubConnection(opts: { workflowId: string }): MockHubConnection {
  const handlers = new Set<WorkflowEventHandler>();
  let cancelled = false;
  let currentRun: Promise<void> | null = null;
  let humanMode = false;

  const emit = (ev: WorkflowEvent) => {
    if (cancelled) return;
    handlers.forEach((h) => h(ev));
  };

  const sessionAware = (sessionId: string) => ({
    msg(message: MessageInput) {
      const full: Message = {
        id: message.id ?? genId('msg'),
        createdAt: nowIso(),
        ...message,
      };
      emit({ type: 'message', sessionId, message: full });
    },
    trace(ev: TraceInput) {
      const full: TraceEvent = { id: genId('trc'), time: nowIso(), ...ev };
      emit({ type: 'trace', sessionId, event: full });
    },
    agent(id: string, state: AgentState, tag: string, activeTools: string[] = []) {
      const a: AgentRuntimeState = { id, state, tag, activeTools };
      emit({ type: 'agent', sessionId, agent: a });
    },
    kb(items: KbItem[]) {
      emit({ type: 'kb', sessionId, items });
    },
    ctx(patch: ContextPatch) {
      emit({ type: 'context', sessionId, patch });
    },
    splitMode(on: boolean) {
      emit({ type: 'split-mode', sessionId, on });
    },
    typing(container: 'msgs' | 'user-msgs' | 'human-msgs', label: string, on: boolean) {
      emit({ type: 'typing', sessionId, container, label, on });
    },
  });

  // ── Flow scripts ───────────────────────────────────────────────────
  async function runKnownFlow(sessionId: string, userText: string) {
    const s = sessionAware(sessionId);
    s.ctx({ status: 'triaging', chatSubtitle: 'Processing...', activeAgentId: 'triage' });
    s.agent('triage', 'active', 'Running');
    s.trace({ icon: 'git-branch', color: 'primary', title: 'TriageAgent received message', level: 'info' });
    s.typing('msgs', 'Triage Agent analyzing', true);
    await delay(1400);
    s.typing('msgs', '', false);
    s.ctx({ intent: 'Cannot log in', category: 'Access & Authentication', confidence: 0.93 });
    s.trace({ icon: 'check-circle', color: 'success', title: 'TriageAgent classified: Access & Auth (93%)', level: 'success' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Triage Agent',
      icon: 'git-branch',
      bubbleStyle: 'triage',
      text: 'Identified as authentication issue. Checking knowledge base for known solutions.',
      splitMirror: true,
    });
    s.agent('triage', 'done', 'Done');
    void userText;

    await delay(500);
    s.agent('freq', 'active', 'Running');
    s.ctx({ status: 'searching-kb', activeAgentId: 'freq' });
    s.trace({ icon: 'database', color: 'warning', title: 'FreqProblemAgent searching KB...', level: 'warning' });
    s.typing('msgs', 'Frequent Problem Agent searching', true);
    await delay(1800);
    s.typing('msgs', '', false);
    s.kb([
      {
        id: 'kb_001',
        title: 'Login failure — password reset',
        category: 'Auth',
        score: 0.97,
        summary: 'User unable to access account. Resolution: trigger password reset flow via admin panel.',
        resolutionType: 'password-reset',
        tags: ['login', 'password', 'auth'],
      },
      {
        id: 'kb_002',
        title: 'Account locked after failed attempts',
        category: 'Auth',
        score: 0.82,
        summary: 'Unlock via admin console and send reset link.',
        resolutionType: 'unlock-account',
        tags: ['lockout', 'auth'],
      },
    ]);
    s.trace({ icon: 'check-circle', color: 'success', title: 'KB match found (0.97)', level: 'success' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Freq. Problem Agent',
      icon: 'database',
      bubbleStyle: 'freq',
      text: 'Found a known solution! KB match: "Login failure — password reset" (score 0.97). Handing off to Resolution Agent.',
      splitMirror: true,
    });
    s.agent('freq', 'done', 'Done');

    await delay(500);
    s.agent('res', 'active', 'Running', ['reset_password']);
    s.ctx({ status: 'resolving', activeAgentId: 'res' });
    s.trace({ icon: 'wrench', color: 'success', title: 'ResolutionAgent executing tools...', level: 'success' });
    s.typing('msgs', 'Resolution Agent executing', true);
    await delay(1000);
    s.trace({ icon: 'terminal', color: 'primary', title: 'reset_password(user_id=U-4821)', level: 'info' });
    await delay(900);
    s.typing('msgs', '', false);
    s.trace({ icon: 'check-circle', color: 'success', title: 'reset_password → OK. Email sent.', level: 'success' });
    s.agent('res', 'active', 'Running', ['create_ticket']);
    s.trace({ icon: 'terminal', color: 'primary', title: 'create_ticket(category=auth, action=pwd_reset)', level: 'info' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Resolution Agent',
      icon: 'wrench',
      bubbleStyle: 'res',
      text: '✅ Resolution applied.',
      tools: [
        { name: 'reset_password', args: 'user_id=U-4821', ok: true },
        { name: 'create_ticket', args: 'category=auth', ok: true },
      ],
    });
    await delay(300);
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Resolution Agent',
      icon: 'wrench',
      bubbleStyle: 'res',
      text: 'A password reset link has been sent to your email. The link expires in 30 minutes.',
    });
    s.ctx({
      resolutionSteps: [
        { step: 1, label: 'reset_password → OK', ok: true },
        { step: 2, label: 'Email sent', ok: true },
        { step: 3, label: 'Ticket logged', ok: true },
      ],
    });
    s.agent('res', 'done', 'Done');

    await delay(700);
    s.agent('pattern', 'active', 'Running');
    s.ctx({ status: 'recording', activeAgentId: 'pattern' });
    s.trace({ icon: 'bar-chart-2', color: 'error', title: 'PatternRecordAgent recording pattern...', level: 'info' });
    await delay(1000);
    s.trace({ icon: 'check-circle', color: 'success', title: 'Pattern recorded. KB entry refreshed.', level: 'success' });
    s.agent('pattern', 'done', 'Done');
    s.msg({
      type: 'system',
      side: 'center',
      senderType: 'system',
      senderName: 'System',
      icon: 'check-circle',
      systemStyle: 'resolved',
      text: 'Ticket resolved automatically. Pattern recorded.',
    });
    s.ctx({ status: 'resolved', chatSubtitle: 'Resolved ✓' });
  }

  async function runToolFlow(sessionId: string, userText: string) {
    const s = sessionAware(sessionId);
    void userText;
    s.ctx({ status: 'triaging', chatSubtitle: 'Processing...', activeAgentId: 'triage' });
    s.agent('triage', 'active', 'Running');
    s.trace({ icon: 'git-branch', color: 'primary', title: 'TriageAgent received message', level: 'info' });
    s.typing('msgs', 'Triage Agent analyzing', true);
    await delay(1200);
    s.typing('msgs', '', false);
    s.ctx({ intent: 'Service not responding', category: 'System & Services', confidence: 0.89 });
    s.trace({ icon: 'check-circle', color: 'success', title: 'Classified: System & Services (89%)', level: 'success' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Triage Agent',
      icon: 'git-branch',
      bubbleStyle: 'triage',
      text: 'This is a service availability issue. Checking knowledge base.',
    });
    s.agent('triage', 'done', 'Done');

    await delay(500);
    s.agent('freq', 'active', 'Running');
    s.ctx({ status: 'searching-kb', activeAgentId: 'freq' });
    s.typing('msgs', 'Frequent Problem Agent searching', true);
    await delay(1600);
    s.typing('msgs', '', false);
    s.kb([
      {
        id: 'kb_010',
        title: 'Service unresponsive — restart procedure',
        category: 'System',
        score: 0.91,
        summary: 'Application service unresponsive. Fix: restart_service. Monitor 60s post-restart.',
        resolutionType: 'restart-service',
        tags: ['service', 'restart'],
      },
    ]);
    s.trace({ icon: 'check-circle', color: 'success', title: 'KB match found (0.91)', level: 'success' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Freq. Problem Agent',
      icon: 'database',
      bubbleStyle: 'freq',
      text: 'Known solution found — service restart procedure (score 0.91). Dispatching Resolution Agent.',
    });
    s.agent('freq', 'done', 'Done');

    await delay(500);
    s.agent('res', 'active', 'Running', ['run_diagnostic', 'restart_service']);
    s.ctx({ status: 'resolving', activeAgentId: 'res' });
    s.typing('msgs', 'Resolution Agent executing', true);
    await delay(900);
    s.trace({ icon: 'terminal', color: 'primary', title: 'run_diagnostic(service=ERP-SAUDE-01)', level: 'info' });
    await delay(800);
    s.trace({ icon: 'terminal', color: 'primary', title: 'restart_service(service=ERP-SAUDE-01)', level: 'info' });
    await delay(900);
    s.typing('msgs', '', false);
    s.trace({ icon: 'check-circle', color: 'success', title: 'restart_service → OK (uptime 100%)', level: 'success' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Resolution Agent',
      icon: 'wrench',
      bubbleStyle: 'res',
      text: 'Service restarted! 🚀 Uptime: 100%.',
      tools: [
        { name: 'run_diagnostic', args: 'service=ERP-SAUDE-01', ok: true },
        { name: 'restart_service', args: 'force=true', ok: true },
        { name: 'create_ticket', args: 'category=system', ok: true },
      ],
    });
    await delay(300);
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Resolution Agent',
      icon: 'wrench',
      bubbleStyle: 'res',
      text: 'The service is now running normally. Please try accessing the application again.',
    });
    s.ctx({
      resolutionSteps: [
        { step: 1, label: 'run_diagnostic → Healthy', ok: true },
        { step: 2, label: 'restart_service → OK', ok: true },
        { step: 3, label: 'Ticket logged', ok: true },
      ],
    });
    s.agent('res', 'done', 'Done');

    await delay(700);
    s.agent('pattern', 'active', 'Running');
    s.ctx({ status: 'recording', activeAgentId: 'pattern' });
    await delay(900);
    s.trace({
      icon: 'check-circle',
      color: 'success',
      title: 'Pattern recorded. 4th occurrence this week — flagged for preventive review.',
      level: 'success',
    });
    s.agent('pattern', 'done', 'Done');
    s.msg({
      type: 'system',
      side: 'center',
      senderType: 'system',
      senderName: 'System',
      icon: 'check-circle',
      systemStyle: 'resolved',
      text: 'Ticket resolved. Pattern recorded (4th this week — flagged for review).',
    });
    s.ctx({ status: 'resolved', chatSubtitle: 'Resolved ✓' });
  }

  async function runHumanFlow(sessionId: string, userText: string) {
    const s = sessionAware(sessionId);
    void userText;
    s.ctx({ status: 'triaging', chatSubtitle: 'Processing...', activeAgentId: 'triage' });
    s.agent('triage', 'active', 'Running');
    s.trace({ icon: 'git-branch', color: 'primary', title: 'TriageAgent received message', level: 'info' });
    s.typing('msgs', 'Triage Agent analyzing', true);
    await delay(1300);
    s.typing('msgs', '', false);
    s.ctx({ intent: 'Unrecognized integration error', category: 'Integration / Unknown', confidence: 0.52 });
    s.trace({ icon: 'alert-circle', color: 'warning', title: 'Low-confidence classification (52%)', level: 'warning' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Triage Agent',
      icon: 'git-branch',
      bubbleStyle: 'triage',
      text: 'Potential integration issue (confidence 52%). Checking knowledge base...',
      splitMirror: true,
    });
    s.agent('triage', 'done', 'Done');

    await delay(500);
    s.agent('freq', 'active', 'Running');
    s.ctx({ status: 'searching-kb', activeAgentId: 'freq' });
    s.typing('msgs', 'Frequent Problem Agent searching', true);
    await delay(2000);
    s.typing('msgs', '', false);
    s.kb([]);
    s.trace({ icon: 'x-circle', color: 'error', title: 'No KB match (score below threshold)', level: 'error' });
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'agent',
      senderName: 'Freq. Problem Agent',
      icon: 'database',
      bubbleStyle: 'freq',
      text: 'No matching solution found in the knowledge base. Escalating to human support agent.',
      splitMirror: true,
    });
    s.agent('freq', 'done', 'Done');

    await delay(600);
    s.msg({
      type: 'system',
      side: 'center',
      senderType: 'system',
      senderName: 'System',
      icon: 'user',
      systemStyle: 'handoff',
      text: 'FreqProblemAgent → no KB match. Routing to human agent queue...',
      splitMirror: true,
    });
    s.msg({
      type: 'system',
      side: 'center',
      senderType: 'system',
      senderName: 'System',
      icon: 'user-check',
      systemStyle: 'escalate',
      text: 'Human agent Daniel M. assigned. Joining the conversation now.',
      splitMirror: true,
    });
    s.trace({ icon: 'user', color: 'human', title: 'Human handoff — ticket assigned to Daniel M.', level: 'info' });
    s.ctx({
      status: 'human-chat',
      chatTitle: 'Human Handoff',
      chatSubtitle: 'Human agent Daniel M. is active',
      activeAgentId: 'human',
      humanMode: true,
    });
    s.agent('pattern', 'wait', 'Waiting');

    await delay(800);
    humanMode = true;
    s.splitMode(true);

    await delay(500);
    s.typing('user-msgs', 'Daniel M. typing', true);
    s.typing('human-msgs', 'Daniel M. typing', true);
    await delay(1800);
    s.typing('user-msgs', '', false);
    s.typing('human-msgs', '', false);
    s.msg({
      type: 'message',
      side: 'left',
      senderType: 'human',
      senderName: 'Daniel M.',
      icon: 'headphones',
      bubbleStyle: 'human',
      text: "Hi! I'm Sarah, your human support specialist. I've reviewed the case so far. This looks like a TISS schema integration issue. Which connector version are you using, and what exact error code are you seeing?",
      splitMirror: true,
    });
    s.trace({ icon: 'user', color: 'human', title: 'Daniel M. joined and sent opening message', level: 'info' });
  }

  // ── Public API ─────────────────────────────────────────────────────
  function classifyAndRun(sessionId: string, text: string): Promise<void> {
    const lower = text.toLowerCase();
    if (/login|password|senha|acesso|entrar/.test(lower)) return runKnownFlow(sessionId, text);
    if (/servi|restart|down|respond|caindo|parou|sistema/.test(lower)) return runToolFlow(sessionId, text);
    return runHumanFlow(sessionId, text);
  }

  return {
    onEvent(handler) {
      handlers.add(handler);
      return () => handlers.delete(handler);
    },
    async joinSession() {
      // No-op for mock
    },
    async leaveSession() {
      // No-op for mock
    },
    async sendUserMessage(sessionId, text) {
      // The client renders an optimistic copy of the user's message immediately
      // (see `local-user-message` in useWorkflowSession). Real backends typically
      // ack the message without re-broadcasting it to the sender, so we only emit
      // server-side side effects here (trace + agent flow), not an echoed message.
      const s = sessionAware(sessionId);
      s.trace({ icon: 'message-circle', color: 'neutral', title: `User: "${text.slice(0, 50)}${text.length > 50 ? '...' : ''}"`, level: 'info' });
      if (!humanMode) {
        currentRun = classifyAndRun(sessionId, text);
      }
    },
    async sendHumanMessage(sessionId, text) {
      const s = sessionAware(sessionId);
      s.msg({
        type: 'message',
        side: 'left',
        senderType: 'human',
        senderName: 'Daniel M.',
        icon: 'headphones',
        bubbleStyle: 'human',
        text,
        splitMirror: true,
      });
      s.trace({ icon: 'headphones', color: 'human', title: `Daniel M.: "${text.slice(0, 50)}${text.length > 50 ? '...' : ''}"`, level: 'info' });
    },
    async runScenario(sessionId, scenarioId) {
      const text =
        scenarioId === 'known'
          ? "I can't log in to the system. My password is not working."
          : scenarioId === 'tool'
          ? 'The ERP service is not responding. The application is down.'
          : scenarioId === 'human'
          ? 'Getting error 0xTISS-4821 during integration sync, never seen this before.'
          : `Scenario ${scenarioId}`;
      const s = sessionAware(sessionId);
      s.msg({
        type: 'message',
        side: 'right',
        senderType: 'user',
        senderName: 'You',
        icon: 'user',
        text,
      });
      currentRun = classifyAndRun(sessionId, text);
    },
    async markSolved(sessionId) {
      const s = sessionAware(sessionId);
      s.msg({
        type: 'message',
        side: 'left',
        senderType: 'human',
        senderName: 'Daniel M.',
        icon: 'headphones',
        bubbleStyle: 'human',
        text: 'The issue has been resolved on our end. ✅ The integration configuration has been updated. Please test the connection again.',
        splitMirror: true,
      });
      s.trace({ icon: 'check-circle', color: 'success', title: 'Daniel M. marked issue as resolved', level: 'success' });
      await delay(800);
      s.msg({
        type: 'system',
        side: 'center',
        senderType: 'system',
        senderName: 'System',
        icon: 'check-circle',
        systemStyle: 'resolved',
        text: 'Human agent Daniel M. marked issue as resolved. Triggering Pattern Record Agent...',
        splitMirror: true,
      });
      await delay(600);
      s.agent('pattern', 'active', 'Running');
      s.ctx({ status: 'recording', activeAgentId: 'pattern' });
      s.trace({ icon: 'bar-chart-2', color: 'error', title: 'PatternRecordAgent triggered — extracting pattern from human conversation...', level: 'info' });
      s.typing('user-msgs', 'Pattern Agent processing', true);
      s.typing('human-msgs', 'Pattern Agent processing', true);
      await delay(1500);
      s.typing('user-msgs', '', false);
      s.typing('human-msgs', '', false);
      s.trace({ icon: 'terminal', color: 'primary', title: 'PatternRecordAgent analyzing conversation transcript...', level: 'info' });
      await delay(800);
      s.trace({ icon: 'trending-up', color: 'success', title: 'New pattern identified: "TISS schema mismatch — integration v3.x"', level: 'success' });
      await delay(600);
      s.trace({ icon: 'check-circle', color: 'success', title: 'New KB entry created. Promoted to Frequent Problems.', level: 'success' });
      s.msg({
        type: 'system',
        side: 'center',
        senderType: 'system',
        senderName: 'System',
        icon: 'check-circle',
        systemStyle: 'resolved',
        text: 'PatternRecordAgent created new KB entry — future occurrences will be resolved automatically.',
        splitMirror: true,
      });
      s.agent('pattern', 'done', 'Done');
      s.ctx({
        status: 'resolved',
        chatTitle: 'Session Resolved ✓',
        chatSubtitle: 'Closed — Pattern promoted to KB',
        resolutionSteps: [
          { step: 1, label: 'Human agent resolved', ok: true },
          { step: 2, label: 'Pattern extracted', ok: true },
          { step: 3, label: 'KB entry promoted', ok: true },
        ],
      });
      humanMode = false;
    },
    async resetSession() {
      humanMode = false;
    },
    dispose() {
      cancelled = true;
      handlers.clear();
      void currentRun;
    },
  };
}
