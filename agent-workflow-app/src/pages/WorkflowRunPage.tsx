import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { TopBar } from '@/components/TopBar';
import { Icon } from '@/components/Icon';
import { apiClient } from '@/services/apiClient';
import { useAuth } from '@/auth/AuthContext';
import { useWorkflowSession } from '@/hooks/useWorkflowSession';
import type { WorkflowDefinition } from '@/types/workflow';
import { ChatPanel } from '@/components/workflow/ChatPanel';
import { AgentPipeline } from '@/components/workflow/AgentPipeline';
import { ContextPanel } from '@/components/workflow/ContextPanel';
import '@/styles/workflow.css';

export function WorkflowRunPage() {
  const params = useParams<{ workflowId: string }>();
  const workflowId = params.workflowId ?? '';
  const navigate = useNavigate();
  const [workflow, setWorkflow] = useState<WorkflowDefinition | null | undefined>(undefined);

  useEffect(() => {
    let cancelled = false;
    apiClient.getWorkflow(workflowId).then((wf) => {
      if (!cancelled) setWorkflow(wf ?? null);
    });
    return () => {
      cancelled = true;
    };
  }, [workflowId]);

  if (workflow === undefined) {
    return (
      <div className="app-shell">
        <TopBar subtitle="Loading workflow..." />
        <div className="empty-state" data-testid="text-workflow-loading">
          <div className="empty-icon"><Icon name="cpu" size={30} /></div>
          <div className="empty-t">Loading workflow</div>
        </div>
      </div>
    );
  }

  if (workflow === null) {
    return (
      <div className="app-shell">
        <TopBar subtitle="Workflow not found" />
        <div className="empty-state" data-testid="text-workflow-missing">
          <div className="empty-icon"><Icon name="alert-circle" size={30} /></div>
          <div className="empty-t">Workflow not found</div>
          <button className="btn btn-primary" onClick={() => navigate('/workflows')} data-testid="button-back-to-list">
            Back to catalog
          </button>
        </div>
      </div>
    );
  }

  return <RunBody workflow={workflow} />;
}

function RunBody({ workflow }: { workflow: WorkflowDefinition }) {
  const { getAccessToken } = useAuth();
  const session = useWorkflowSession(workflow, getAccessToken);
  const [tab, setTab] = useState<'context' | 'trace' | 'kb'>('context');
  const navigate = useNavigate();
  const { snapshot, hubStatus, error } = session;

  // Auto-flip to KB tab whenever the KB updates with results.
  useEffect(() => {
    if (snapshot.kb.length > 0) setTab('kb');
  }, [snapshot.kb.length]);

  const pipelineState = useMemo(() => {
    const byId = new Map(snapshot.agents.map((a) => [a.id, a.state]));
    return workflow.agents.map((a) => ({
      def: a,
      state: byId.get(a.id) ?? 'idle',
    }));
  }, [snapshot.agents, workflow.agents]);

  return (
    <div className="app-shell">
      <TopBar
        subtitle={workflow.subtitle ?? workflow.title}
        center={
          <div className="wf-pipeline-steps" data-testid="pipeline-steps">
            {workflow.agents.map((a, i) => {
              const st = pipelineState[i].state;
              const cls = st === 'active' ? 'active' : st === 'done' ? 'done' : st === 'human' || st === 'wait' ? 'human' : '';
              return (
                <span key={a.id} className="wf-ps-arrow" style={{ display: 'contents' }}>
                  <span
                    className={`wf-ps ${cls}`}
                    data-testid={`pipeline-step-${a.id}`}
                    data-state={st}
                  >
                    <Icon name={a.icon} size={10} />
                    {a.title.replace(/ Agent$/, '')}
                  </span>
                  {i < workflow.agents.length - 1 && <span className="wf-ps-arrow">›</span>}
                </span>
              );
            })}
          </div>
        }
        right={
          <>
            <span
              className="status-badge"
              data-testid="text-hub-status"
              data-status={hubStatus}
              title={`SignalR hub: ${hubStatus}`}
            >
              <span
                className={`status-dot ${hubStatus === 'connected' ? 'pulse' : ''} ${
                  hubStatus === 'failed' || hubStatus === 'disconnected' ? 'err' : hubStatus === 'reconnecting' ? 'warn' : ''
                }`}
              />
              {labelForStatus(hubStatus)}
            </span>
            <button
              className="btn btn-danger"
              onClick={() => void session.reset()}
              data-testid="button-reset-session"
            >
              <Icon name="rotate-ccw" size={11} />
              Reset
            </button>
            <button
              className="btn btn-ghost"
              onClick={() => navigate('/workflows')}
              data-testid="button-back-to-catalog"
            >
              Catalog
            </button>
          </>
        }
      />
      {error && (
        <div className="alert alert-error" style={{ margin: 'var(--space-3)' }} data-testid="text-session-error" role="alert">
          {error}
        </div>
      )}
      <div className="wf-main">
        <AgentPipeline workflow={workflow} state={pipelineState} activeTools={collectActiveTools(snapshot.agents)} />
        <ChatPanel session={session} />
        <ContextPanel session={session} tab={tab} onTabChange={setTab} />
      </div>
    </div>
  );
}

function labelForStatus(s: string): string {
  switch (s) {
    case 'connected':
      return 'Online';
    case 'connecting':
      return 'Connecting…';
    case 'reconnecting':
      return 'Reconnecting…';
    case 'disconnected':
      return 'Offline';
    case 'failed':
      return 'Connection failed';
    default:
      return 'Idle';
  }
}

function collectActiveTools(agents: { activeTools: string[] }[]): Set<string> {
  const set = new Set<string>();
  for (const a of agents) for (const t of a.activeTools) set.add(t);
  return set;
}
