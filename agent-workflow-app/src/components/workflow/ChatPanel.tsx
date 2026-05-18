import { useEffect, useRef, useState } from 'react';
import { Icon } from '@/components/Icon';
import type { Message, MessageAudience } from '@/types/workflow';
import type { WorkflowSessionApi } from '@/hooks/useWorkflowSession';

interface Props {
  session: WorkflowSessionApi;
}

const AV_BY_SENDER: Record<string, string> = {
  user: 'user',
  'Triage Agent': 'triage',
  'Freq. Problem Agent': 'freq',
  'Resolution Agent': 'res',
  'Pattern Record Agent': 'pattern',
  human: 'human',
};

/**
 * Returns true if a message is visible in a given pane based on its `audience`
 * field. Defaults to "both" when the field is missing so messages from older
 * backends keep their previous behaviour.
 */
function isVisibleTo(m: Message, pane: 'client' | 'attendant'): boolean {
  const aud: MessageAudience = m.audience ?? 'both';
  if (aud === 'internal') return false;
  if (aud === 'both') return true;
  return aud === pane;
}

function avClass(message: Message): string {
  if (message.senderType === 'user') return 'user';
  if (message.senderType === 'human') return 'human';
  return AV_BY_SENDER[message.senderName] ?? 'triage';
}

export function ChatPanel({ session }: Props) {
  const { snapshot, sendUserMessage, sendHumanMessage, runScenario, markSolved, ready, workflow } = session;
  const [input, setInput] = useState('');
  const [userInput, setUserInput] = useState('');
  const [humanInput, setHumanInput] = useState('');
  const singleRef = useRef<HTMLDivElement | null>(null);
  const userRef = useRef<HTMLDivElement | null>(null);
  const humanRef = useRef<HTMLDivElement | null>(null);
  const isSplit = snapshot.splitMode;
  const isClosed = snapshot.status === 'resolved';

  // Auto-scroll on new messages
  useEffect(() => {
    singleRef.current?.scrollTo({ top: singleRef.current.scrollHeight, behavior: 'smooth' });
    userRef.current?.scrollTo({ top: userRef.current.scrollHeight, behavior: 'smooth' });
    humanRef.current?.scrollTo({ top: humanRef.current.scrollHeight, behavior: 'smooth' });
  }, [snapshot.messages.length, snapshot.typing.msgs, snapshot.typing['user-msgs'], snapshot.typing['human-msgs']]);

  async function onSendMain(e?: React.FormEvent) {
    e?.preventDefault();
    const text = input.trim();
    if (!text) return;
    setInput('');
    await sendUserMessage(text);
  }

  async function onSendUser(e?: React.FormEvent) {
    e?.preventDefault();
    const text = userInput.trim();
    if (!text) return;
    setUserInput('');
    await sendUserMessage(text);
  }

  async function onSendHuman(e?: React.FormEvent) {
    e?.preventDefault();
    const text = humanInput.trim();
    if (!text) return;
    setHumanInput('');
    await sendHumanMessage(text);
  }

  function onMainKey(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void onSendMain();
    }
  }
  function onUserKey(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void onSendUser();
    }
  }
  function onHumanKey(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void onSendHuman();
    }
  }

  return (
    <main className="wf-chat-area" data-testid="chat-area" data-mode={isSplit ? 'split' : 'single'}>
      <div className="wf-chat-header">
        <div className="wf-av user">
          <Icon name="user" size={12} />
        </div>
        <div className="wf-chat-header-info">
          <div className="wf-chat-title" data-testid="text-chat-title">
            {snapshot.chatTitle || workflow.title}
          </div>
          <div className="wf-chat-sub" data-testid="text-chat-sub">
            {snapshot.chatSubtitle || 'Waiting for your message...'}
          </div>
        </div>
        <span className="wf-ticket-chip" data-testid="text-ticket-id">
          {snapshot.ticketId || '#TKT-····'}
        </span>
      </div>

      <div className="wf-chat-body">
        {!isSplit && (
          <div className="wf-single-chat">
            <div className="wf-msgs" ref={singleRef} data-testid="msgs-single">
              {snapshot.messages.length === 0 && !snapshot.typing.msgs && (
                <div className="empty-state" data-testid="empty-single">
                  <div className="empty-icon"><Icon name="message-square" size={38} /></div>
                  <div className="empty-t">{workflow.title}</div>
                  <div className="empty-d">Type a message or pick a scenario in the right panel.</div>
                </div>
              )}
              {snapshot.messages
                .filter((m) => isVisibleTo(m, 'client'))
                .map((m) => (
                  <MessageRow key={m.id} message={m} />
                ))}
              {snapshot.typing.msgs && <TypingRow label={snapshot.typing.msgs} av="triage" />}
            </div>
            <div className="wf-chat-input-wrap">
              <form
                onSubmit={onSendMain}
                className={`wf-input-row ${!ready || isClosed ? 'disabled' : ''}`}
                data-testid="form-send-main"
              >
                <textarea
                  className="wf-chat-inp"
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={onMainKey}
                  placeholder={isClosed ? 'Session closed. Click Reset to start again.' : 'Describe your problem...'}
                  rows={1}
                  disabled={!ready || isClosed}
                  data-testid="input-chat"
                />
                <button
                  type="submit"
                  className="wf-send-btn"
                  aria-label="Send"
                  data-testid="button-send-main"
                  disabled={!ready || isClosed || !input.trim()}
                >
                  <Icon name="send" size={13} />
                </button>
              </form>
              <div className="wf-input-hint" data-testid="text-input-hint">
                {isClosed
                  ? 'Session closed. Click Reset to start again.'
                  : 'Enter to send · Shift+Enter for new line'}
              </div>
            </div>
          </div>
        )}

        {isSplit && (
          <div className="wf-split-chat" data-testid="split-chat">
            {/* USER PANE */}
            <div className="wf-split-pane">
              <div className="wf-split-pane-header">
                <div className="wf-av user" style={{ width: 20, height: 20 }}>
                  <Icon name="user" size={10} />
                </div>
                <span className="wf-split-pane-label user">User View</span>
                <span style={{ fontSize: 10, color: 'var(--color-text-faint)', marginLeft: 'auto' }}>
                  Customer side
                </span>
              </div>
              <div className="wf-split-msgs" ref={userRef} data-testid="msgs-user">
                {snapshot.messages
                  .filter((m) => (m.splitMirror || m.type === 'system') && isVisibleTo(m, 'client'))
                  .map((m) => (
                    <MessageRow key={`u-${m.id}`} message={mirrorForUserPane(m)} />
                  ))}
                {snapshot.typing['user-msgs'] && (
                  <TypingRow label={snapshot.typing['user-msgs']!} av="human" />
                )}
              </div>
              <div className="wf-split-input-wrap">
                <form onSubmit={onSendUser} className="wf-split-input-row" data-testid="form-send-user">
                  <textarea
                    className="wf-split-inp"
                    value={userInput}
                    onChange={(e) => setUserInput(e.target.value)}
                    onKeyDown={onUserKey}
                    rows={1}
                    placeholder="User types here..."
                    disabled={isClosed}
                    data-testid="input-user"
                  />
                  <button
                    type="submit"
                    className="wf-split-send user-send"
                    aria-label="Send as user"
                    data-testid="button-send-user"
                    disabled={isClosed || !userInput.trim()}
                  >
                    <Icon name="send" size={13} />
                  </button>
                </form>
              </div>
            </div>
            <div className="wf-split-divider" />
            {/* HUMAN PANE */}
            <div className="wf-split-pane">
              <div className="wf-split-pane-header">
                <div className="wf-av human" style={{ width: 20, height: 20 }}>
                  <Icon name="headphones" size={10} />
                </div>
                <span className="wf-split-pane-label human">Daniel M. — Human Agent</span>
                <span style={{ fontSize: 10, color: 'var(--color-text-faint)', marginLeft: 'auto' }}>
                  Agent side
                </span>
              </div>
              <div className="wf-split-msgs" ref={humanRef} data-testid="msgs-human">
                {snapshot.messages
                  .filter((m) => (m.splitMirror || m.type === 'system') && isVisibleTo(m, 'attendant'))
                  .map((m) => (
                    <MessageRow key={`h-${m.id}`} message={mirrorForHumanPane(m)} />
                  ))}
                {snapshot.typing['human-msgs'] && (
                  <TypingRow label={snapshot.typing['human-msgs']!} av="human" />
                )}
              </div>
              <div className="wf-split-input-wrap">
                <form onSubmit={onSendHuman} className="wf-split-input-row" data-testid="form-send-human">
                  <textarea
                    className="wf-split-inp human"
                    value={humanInput}
                    onChange={(e) => setHumanInput(e.target.value)}
                    onKeyDown={onHumanKey}
                    rows={1}
                    placeholder="Human agent response..."
                    disabled={isClosed}
                    data-testid="input-human"
                  />
                  <button
                    type="submit"
                    className="wf-split-send human-send"
                    aria-label="Send as human agent"
                    data-testid="button-send-human"
                    disabled={isClosed || !humanInput.trim()}
                  >
                    <Icon name="send" size={13} />
                  </button>
                </form>
              </div>
              {!isClosed && (
                <div className="wf-solve-bar" data-testid="solve-bar">
                  <span className="wf-solve-hint">
                    <Icon name="check-circle" size={12} />
                    Issue resolved with the user?
                  </span>
                  <button
                    className="wf-solve-btn"
                    onClick={() => void markSolved()}
                    data-testid="button-mark-solved"
                  >
                    <Icon name="check-square" size={14} />
                    Mark as Solved → trigger Pattern Agent
                  </button>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

    </main>
  );
}

/* ── Helpers ─────────────────────────────────────────────────────────── */

function mirrorForUserPane(m: Message): Message {
  // In the user pane: user messages appear on the right; human messages on the left.
  if (m.type === 'system') return m;
  if (m.senderType === 'user') return { ...m, side: 'right' };
  if (m.senderType === 'human') return { ...m, side: 'left' };
  return m;
}
function mirrorForHumanPane(m: Message): Message {
  // In the human pane: user messages appear on the left; human messages on the right.
  if (m.type === 'system') return m;
  if (m.senderType === 'user') return { ...m, side: 'left' };
  if (m.senderType === 'human') return { ...m, side: 'right', senderName: 'Daniel M. (you)' };
  return m;
}

function MessageRow({ message }: { message: Message }) {
  if (message.type === 'system') {
    return (
      <div
        className={`wf-sys-event ${message.systemStyle ?? ''} fade-in`}
        data-testid={`sys-event-${message.id}`}
      >
        <Icon name={message.icon} size={12} />
        <span dangerouslySetInnerHTML={{ __html: message.text }} />
      </div>
    );
  }
  const time = new Date(message.createdAt).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
  return (
    <div className={`wf-msg ${message.side} fade-in`} data-testid={`msg-${message.id}`}>
      <div className={`wf-av ${avClass(message)}`}>
        <Icon name={message.icon} size={11} />
      </div>
      <div className="wf-msg-body">
        <div className="wf-msg-meta">
          <span className="wf-msg-sender">{message.senderName}</span>
          <span>{time}</span>
        </div>
        <div className={`wf-bubble ${message.bubbleStyle ?? ''}`}>
          <span dangerouslySetInnerHTML={{ __html: message.text }} />
          {message.tools?.map((t, i) => (
            <div key={i} className="wf-tool-call">
              <Icon name="terminal" size={10} />
              <span className="wf-tc-name">{t.name}</span>
              <span style={{ color: 'var(--color-text-faint)' }}>({t.args})</span>
              <span className={t.ok ? 'wf-tc-ok' : 'wf-tc-run'}>{t.ok ? '✓ OK' : '⟳'}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function TypingRow({ label, av }: { label: string; av: 'triage' | 'freq' | 'res' | 'pattern' | 'human' }) {
  return (
    <div className="wf-typing fade-in" data-testid="typing-indicator" data-label={label}>
      <div className={`wf-av ${av}`} style={{ width: 24, height: 24 }}>
        <Icon name="cpu" size={10} />
      </div>
      <div className="wf-t-dots">
        <div className="wf-t-dot" />
        <div className="wf-t-dot" />
        <div className="wf-t-dot" />
      </div>
      <span className="wf-t-label">{label}...</span>
    </div>
  );
}
