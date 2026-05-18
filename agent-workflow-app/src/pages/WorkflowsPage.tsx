import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { TopBar } from '@/components/TopBar';
import { Icon } from '@/components/Icon';
import { apiClient } from '@/services/apiClient';
import type { WorkflowDefinition } from '@/types/workflow';

/**
 * Multi-select workflow picker. Operators tick one-or-more workflows; the
 * primary action launches the *active* one (single-select for now, but the
 * UI stores a full Set so future server features — like wiring workflows
 * together — can ship without a redesign).
 */
export function WorkflowsPage() {
  const navigate = useNavigate();
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    apiClient
      .listWorkflows()
      .then((list) => {
        if (cancelled) return;
        setWorkflows(list);
        if (list.length > 0) setSelected(new Set([list[0].id]));
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load workflows.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const primaryId = useMemo(() => Array.from(selected)[0] ?? null, [selected]);

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function launch() {
    if (!primaryId) return;
    navigate(`/workflows/${primaryId}/run`);
  }

  return (
    <div className="app-shell">
      <TopBar subtitle="Workflow Catalog" />
      <main
        style={{
          flex: 1,
          overflow: 'auto',
          padding: 'var(--space-8) var(--space-6)',
          background: 'var(--color-bg)',
        }}
      >
        <div style={{ maxWidth: 1080, margin: '0 auto' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'flex-end',
              justifyContent: 'space-between',
              marginBottom: 'var(--space-6)',
              gap: 'var(--space-4)',
              flexWrap: 'wrap',
            }}
          >
            <div>
              <h1
                style={{
                  fontSize: 'var(--text-lg)',
                  fontWeight: 600,
                  letterSpacing: '-0.02em',
                  marginBottom: 'var(--space-2)',
                }}
              >
                Choose a workflow
              </h1>
              <p style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)' }}>
                Select one to launch its execution view. Toggle additional workflows to compare configurations.
              </p>
            </div>
            <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
              <span className="status-badge" data-testid="text-selected-count">
                {selected.size} selected
              </span>
              <button
                className="btn btn-primary"
                onClick={launch}
                disabled={!primaryId}
                data-testid="button-launch-workflow"
              >
                Launch
                <Icon name="chevron-right" size={12} />
              </button>
            </div>
          </div>

          {error && (
            <div className="alert alert-error" data-testid="text-workflows-error" role="alert">
              {error}
            </div>
          )}

          {loading ? (
            <SkeletonGrid />
          ) : (
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
                gap: 'var(--space-4)',
              }}
              data-testid="grid-workflows"
            >
              {workflows.map((wf) => {
                const isSelected = selected.has(wf.id);
                const isPrimary = wf.id === primaryId;
                return (
                  <button
                    type="button"
                    key={wf.id}
                    className="workflow-card"
                    aria-pressed={isSelected}
                    onClick={() => toggle(wf.id)}
                    onDoubleClick={() => navigate(`/workflows/${wf.id}/run`)}
                    data-testid={`card-workflow-${wf.id}`}
                    style={{
                      textAlign: 'left',
                      padding: 'var(--space-5)',
                      borderRadius: 'var(--radius-lg)',
                      border: `1px solid ${isSelected ? 'var(--color-primary)' : 'var(--color-border)'}`,
                      background: isSelected ? 'var(--color-surface-offset)' : 'var(--color-surface)',
                      boxShadow: isSelected ? '0 0 0 3px var(--color-primary-glow)' : 'var(--shadow-sm)',
                      transition: 'all var(--transition)',
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 'var(--space-3)',
                      cursor: 'pointer',
                      color: 'inherit',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                      <div
                        className="logo-icon"
                        style={{
                          background: themeColorBg(wf.colorTheme),
                          color: themeColorFg(wf.colorTheme),
                        }}
                      >
                        <Icon name={wf.icon} size={16} />
                      </div>
                      <div style={{ flex: 1 }}>
                        <div style={{ fontSize: 'var(--text-sm)', fontWeight: 600 }}>{wf.title}</div>
                        <div style={{ fontSize: '10px', color: 'var(--color-text-faint)' }}>
                          {wf.subtitle ?? `${wf.agents.length} agents`}
                        </div>
                      </div>
                      <div
                        aria-hidden
                        style={{
                          width: 22,
                          height: 22,
                          borderRadius: 'var(--radius-sm)',
                          border: `2px solid ${isSelected ? 'var(--color-primary)' : 'var(--color-border)'}`,
                          background: isSelected ? 'var(--color-primary)' : 'transparent',
                          color: 'white',
                          display: 'grid',
                          placeItems: 'center',
                        }}
                        data-testid={`checkbox-workflow-${wf.id}`}
                      >
                        {isSelected && <Icon name="check-circle" size={12} stroke={3} />}
                      </div>
                    </div>
                    <p
                      style={{
                        fontSize: 'var(--text-xs)',
                        color: 'var(--color-text-muted)',
                        lineHeight: 1.55,
                      }}
                    >
                      {wf.description}
                    </p>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                      {wf.agents.slice(0, 4).map((a) => (
                        <span
                          key={a.id}
                          style={{
                            fontSize: 10,
                            padding: '2px 7px',
                            borderRadius: 'var(--radius-full)',
                            background: 'var(--color-surface-2)',
                            color: 'var(--color-text-muted)',
                            border: '1px solid var(--color-border)',
                            fontFamily: 'var(--font-mono)',
                          }}
                        >
                          {a.title}
                        </span>
                      ))}
                    </div>
                    {isPrimary && (
                      <div
                        style={{
                          fontSize: 10,
                          color: 'var(--color-primary)',
                          fontWeight: 600,
                          letterSpacing: '0.05em',
                          textTransform: 'uppercase',
                        }}
                        data-testid={`badge-primary-${wf.id}`}
                      >
                        Will launch
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

function themeColorBg(theme: string): string {
  switch (theme) {
    case 'success':
      return 'var(--color-success)';
    case 'warning':
      return 'var(--color-warning)';
    case 'error':
      return 'var(--color-error)';
    case 'human':
      return 'var(--color-human)';
    default:
      return 'var(--color-primary)';
  }
}
function themeColorFg(_theme: string): string {
  return 'white';
}

function SkeletonGrid() {
  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
        gap: 'var(--space-4)',
      }}
      data-testid="grid-workflows-loading"
    >
      {Array.from({ length: 3 }).map((_, i) => (
        <div
          key={i}
          style={{
            height: 180,
            borderRadius: 'var(--radius-lg)',
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            opacity: 0.6,
            animation: 'pulse-dot 1.5s ease-in-out infinite',
          }}
        />
      ))}
    </div>
  );
}
