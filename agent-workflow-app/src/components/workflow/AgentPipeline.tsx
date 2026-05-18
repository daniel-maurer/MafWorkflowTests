import { Icon } from '@/components/Icon';
import type { AgentDefinition, AgentState, WorkflowDefinition } from '@/types/workflow';

interface Props {
  workflow: WorkflowDefinition;
  state: { def: AgentDefinition; state: AgentState }[];
  activeTools: Set<string>;
}

const STATE_CLS: Record<AgentState, string> = {
  active: 'active',
  done: 'done',
  human: 'human',
  wait: 'human',
  idle: 'idle',
};

const STATE_BADGE: Record<AgentState, { label: string; cls: string }> = {
  active: { label: 'Running', cls: 'run' },
  done: { label: 'Done', cls: 'done' },
  wait: { label: 'Waiting', cls: 'wait' },
  human: { label: 'Human', cls: 'human' },
  idle: { label: 'Idle', cls: '' },
};

export function AgentPipeline({ workflow, state, activeTools }: Props) {
  return (
    <aside className="wf-sidebar" data-testid="agent-pipeline">
      <div className="wf-sidebar-header">
        <div className="wf-sidebar-title">Agent Pipeline</div>
      </div>
      <div className="wf-agents-scroll">
        {state.map((entry, idx) => {
          const a = entry.def;
          const cls = STATE_CLS[entry.state];
          const badge = STATE_BADGE[entry.state];
          const connectorFlow = entry.state === 'done' || entry.state === 'active';
          return (
            <div key={a.id} style={{ display: 'flex', flexDirection: 'column' }}>
              <div
                className={`wf-agent-card ${cls}`}
                data-testid={`agent-card-${a.id}`}
                data-state={entry.state}
              >
                <div className="wf-agent-head">
                  <div className="wf-agent-ico">
                    <Icon name={a.icon} size={13} />
                  </div>
                  <span className="wf-agent-name">{a.title}</span>
                  <span className={`wf-badge ${badge.cls}`} data-testid={`badge-agent-${a.id}`}>
                    {badge.label}
                  </span>
                </div>
                <p className="wf-agent-desc">{a.description}</p>
                {a.tools.length > 0 && (
                  <div className="wf-tools-row">
                    {a.tools.map((t) => (
                      <span
                        key={t}
                        className={`wf-tool-chip ${activeTools.has(t) ? 'active' : ''}`}
                        data-testid={`tool-chip-${t}`}
                      >
                        {t}
                      </span>
                    ))}
                  </div>
                )}
              </div>
              {idx < state.length - 1 && (
                <div className="wf-connector" aria-hidden>
                  <div className={`wf-conn-line ${connectorFlow ? 'flow' : ''}`} />
                  <Icon name="chevron-down" size={10} style={{ zIndex: 1 }} />
                </div>
              )}
            </div>
          );
        })}
        {workflow.capabilities.humanHandoff && (
          <div
            style={{
              marginTop: 'var(--space-3)',
              padding: 'var(--space-2) var(--space-3)',
              background: 'var(--color-surface-offset)',
              borderRadius: 'var(--radius-lg)',
              border: '1px dashed var(--color-border)',
            }}
            data-testid="fallback-human"
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                fontSize: 10,
                fontWeight: 700,
                textTransform: 'uppercase',
                letterSpacing: '0.08em',
                color: 'var(--color-human)',
                marginBottom: 4,
              }}
            >
              <Icon name="user" size={11} />
              Human Fallback
            </div>
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)' }}>
              When no KB match is found, a human agent joins the chat to resolve directly.
            </p>
          </div>
        )}
      </div>
    </aside>
  );
}
