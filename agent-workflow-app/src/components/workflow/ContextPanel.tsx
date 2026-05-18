import { useMemo } from 'react';
import { Icon } from '@/components/Icon';
import type { WorkflowSessionApi } from '@/hooks/useWorkflowSession';

type Tab = 'context' | 'trace' | 'kb';

interface Props {
  session: WorkflowSessionApi;
  tab: Tab;
  onTabChange: (tab: Tab) => void;
}

export function ContextPanel({ session, tab, onTabChange }: Props) {
  const { snapshot } = session;

  const statusClass = useMemo(() => {
    switch (snapshot.status) {
      case 'resolved':
        return 'ok';
      case 'human-chat':
        return 'human';
      case 'error':
        return 'err';
      default:
        return 'warn';
    }
  }, [snapshot.status]);

  const tabs: { key: Tab; label: string; testId: string }[] = [
    { key: 'context', label: 'Context', testId: 'tab-context' },
    { key: 'trace', label: 'Trace', testId: 'tab-trace' },
    { key: 'kb', label: 'KB', testId: 'tab-kb' },
  ];

  return (
    <aside className="wf-right-panel" data-testid="context-panel">
      <div className="wf-right-tabs" role="tablist">
        {tabs.map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={tab === t.key}
            className={`wf-tab-btn ${tab === t.key ? 'active' : ''}`}
            onClick={() => onTabChange(t.key)}
            data-testid={`button-${t.testId}`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'context' && (
        <div className="wf-tab-panel" data-testid="panel-context">
          <div>
            <div className="wf-ctx-label">Session</div>
            <div className="wf-ctx-card">
              <KV label="Ticket" value={snapshot.ticketId || '—'} testId="ctx-ticket" />
              <KV
                label="Status"
                value={snapshot.status}
                cls={statusClass}
                testId="ctx-status"
              />
              <KV
                label="Active Agent"
                value={snapshot.activeAgentId ?? '—'}
                testId="ctx-agent"
              />
              <KV
                label="Category"
                value={snapshot.category ?? '—'}
                testId="ctx-category"
              />
              <KV
                label="Confidence"
                value={
                  snapshot.confidence == null
                    ? '—'
                    : `${Math.round(snapshot.confidence * 100)}%`
                }
                cls={
                  snapshot.confidence != null && snapshot.confidence >= 0.8 ? 'ok' : 'warn'
                }
                testId="ctx-confidence"
              />
            </div>
          </div>
          <div>
            <div className="wf-ctx-label">Detected Intent</div>
            <div className="wf-ctx-card" data-testid="ctx-intent">
              {snapshot.intent ? (
                <>
                  <KV
                    label="Intent"
                    value={snapshot.intent}
                    cls="ok"
                    testId="ctx-intent-text"
                  />
                  {snapshot.category && <KV label="Category" value={snapshot.category} />}
                </>
              ) : (
                <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-faint)' }}>
                  No input yet.
                </p>
              )}
            </div>
          </div>
          {(snapshot.resolutionSteps ?? []).length > 0 && (
            <div data-testid="ctx-resolution">
              <div className="wf-ctx-label">Resolution Steps</div>
              <div className="wf-ctx-card">
                {snapshot.resolutionSteps.map((s) => (
                  <KV
                    key={s.step}
                    label={`Step ${s.step}`}
                    value={s.label}
                    cls={s.ok ? 'ok' : 'warn'}
                    testId={`ctx-step-${s.step}`}
                  />
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'trace' && (
        <div className="wf-tab-panel" data-testid="panel-trace">
          {snapshot.trace.length === 0 ? (
            <div className="empty-state">
              <div className="empty-icon"><Icon name="terminal" size={30} /></div>
              <div className="empty-t">No trace yet</div>
              <div className="empty-d">Events from each agent will appear here in real time.</div>
            </div>
          ) : (
            <div>
              {snapshot.trace.map((t) => {
                const ts = new Date(t.time).toLocaleTimeString('en-US', {
                  hour: '2-digit',
                  minute: '2-digit',
                  second: '2-digit',
                });
                return (
                  <div className="wf-trace-row" key={t.id} data-testid={`trace-${t.id}`}>
                    <span className="wf-tr-time">{ts}</span>
                    <Icon
                      name={t.icon}
                      size={10}
                      style={{ flexShrink: 0, color: colorVar(t.color) }}
                    />
                    <span
                      className="wf-tr-txt"
                      dangerouslySetInnerHTML={{ __html: t.title }}
                    />
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {tab === 'kb' && (
        <div className="wf-tab-panel" data-testid="panel-kb">
          {snapshot.kb.length === 0 ? (
            <div className="empty-state">
              <div className="empty-icon"><Icon name="book-open" size={30} /></div>
              <div className="empty-t">Knowledge Base</div>
              <div className="empty-d">KB search results appear here when an agent queries it.</div>
            </div>
          ) : (
            snapshot.kb.map((kb) => (
              <div className="wf-kb-card fade-in" key={kb.id} data-testid={`kb-${kb.id}`}>
                <div className="wf-kb-title">{kb.title}</div>
                <div className="wf-kb-meta">
                  <span className="wf-kb-score">Score: {kb.score.toFixed(2)}</span>
                  <span>{kb.category}</span>
                </div>
                <p className="wf-kb-txt">{kb.summary}</p>
              </div>
            ))
          )}
        </div>
      )}
      <div className="wf-scenario-section">
        <div className="wf-sc-label">Demo Scenarios</div>
        {session.workflow.scenarios.map((sc) => (
          <button
            key={sc.id}
            className="wf-sc-btn"
            onClick={() => void session.runScenario(sc.id)}
            disabled={!session.ready || snapshot.status === 'resolved'}
            data-testid={`button-scenario-${sc.id}`}
            title={sc.description}
          >
            {sc.title}: {sc.label}
          </button>
        ))}
      </div>
    </aside>
  );
}

function KV({
  label,
  value,
  cls,
  testId,
}: {
  label: string;
  value: string;
  cls?: string;
  testId?: string;
}) {
  return (
    <div className="wf-kv">
      <span className="wf-kv-k">{label}</span>
      <span className={`wf-kv-v ${cls ?? ''}`} data-testid={testId}>
        {value}
      </span>
    </div>
  );
}

function colorVar(color: string): string {
  switch (color) {
    case 'primary':
      return 'var(--color-primary)';
    case 'success':
      return 'var(--color-success)';
    case 'warning':
      return 'var(--color-warning)';
    case 'error':
      return 'var(--color-error)';
    case 'human':
      return 'var(--color-human)';
    default:
      return 'var(--color-text-faint)';
  }
}
