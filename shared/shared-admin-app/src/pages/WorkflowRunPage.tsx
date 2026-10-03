import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { TopBar } from '../components/TopBar';
import { Icon } from '../components/Icon';
import { apiClient } from '../services/apiClient';
import { useAuth } from '../auth/AuthContext';
import { useWorkflowSession } from '../hooks/useWorkflowSession';
import type { WorkflowDefinition } from '../types/workflow';
import { ChatPanel } from '../components/workflow/ChatPanel';
import { AgentPipeline } from '../components/workflow/AgentPipeline';
import { ContextPanel } from '../components/workflow/ContextPanel';
import { CustomerIdentificationModal, type CustomerData } from '../components/workflow/CustomerIdentificationModal';
import '../styles/workflow.css';

export function WorkflowRunPage() {
  const params = useParams<{ workflowId: string }>();
  const workflowId = params.workflowId ?? '';
  const navigate = useNavigate();
  const [workflow, setWorkflow] = useState<WorkflowDefinition | null | undefined>(undefined);
  const [customerModalOpen, setCustomerModalOpen] = useState(true);
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerData | null>(null);
  const [sessionStarted, setSessionStarted] = useState(false);
  const [isResetFlow, setIsResetFlow] = useState(false);
  const resetHandlerRef = useRef<((cust: CustomerData | null) => void) | null>(null);

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

  function handleConfirmCustomer(cust: CustomerData | null) {
    setSelectedCustomer(cust);
    setCustomerModalOpen(false);

    if (isResetFlow && resetHandlerRef.current) {
      resetHandlerRef.current(cust);
      setIsResetFlow(false);
    } else {
      setSessionStarted(true);
    }
  }

  function handleRequestReset(triggerReset: (cust: CustomerData | null) => void) {
    resetHandlerRef.current = triggerReset;
    setIsResetFlow(true);
    setCustomerModalOpen(true);
  }

  return (
    <>
      <CustomerIdentificationModal
        isOpen={customerModalOpen}
        workflowTitle={workflow.subtitle ?? workflow.title}
        currentCustomer={selectedCustomer}
        onConfirm={handleConfirmCustomer}
        onClose={sessionStarted ? () => { setCustomerModalOpen(false); setIsResetFlow(false); } : undefined}
      />
      {sessionStarted && (
        <RunBody
          workflow={workflow}
          initialCustomer={selectedCustomer}
          onRequestReset={handleRequestReset}
        />
      )}
    </>
  );
}

function RunBody({
  workflow,
  initialCustomer,
  onRequestReset,
}: {
  workflow: WorkflowDefinition;
  initialCustomer?: CustomerData | null;
  onRequestReset: (triggerReset: (cust: CustomerData | null) => void) => void;
}) {
  const { getAccessToken } = useAuth();
  const session = useWorkflowSession(workflow, getAccessToken, initialCustomer);
  const [tab, setTab] = useState<'context' | 'trace' | 'kb'>('context');
  const [pipelineOpen, setPipelineOpen] = useState(true);
  const [detailsOpen, setDetailsOpen] = useState(true);
  const [mobilePipelineOpen, setMobilePipelineOpen] = useState(false);
  const [mobileDetailsOpen, setMobileDetailsOpen] = useState(false);
  const navigate = useNavigate();
  const { snapshot, hubStatus, error } = session;

  // Auto-flip to KB tab whenever the KB updates with results.
  useEffect(() => {
    if (snapshot.kb.length > 0) {
      setTab('kb');
      // If details was collapsed on desktop, open it to show findings
      setDetailsOpen(true);
    }
  }, [snapshot.kb.length]);

  const pipelineState = useMemo(() => {
    const byId = new Map(snapshot.agents.map((a) => [a.id, a.state]));
    return workflow.agents.map((a) => ({
      def: a,
      state: byId.get(a.id) ?? 'idle',
    }));
  }, [snapshot.agents, workflow.agents]);

  function closePipeline() {
    setMobilePipelineOpen(false);
    setPipelineOpen(false);
  }

  function closeDetails() {
    setMobileDetailsOpen(false);
    setDetailsOpen(false);
  }

  function openPipeline() {
    setPipelineOpen(true);
    setMobileDetailsOpen(false);
    setMobilePipelineOpen(true);
  }

  function openDetails() {
    setDetailsOpen(true);
    setMobilePipelineOpen(false);
    setMobileDetailsOpen(true);
  }

  function closeMobileDrawers() {
    setMobilePipelineOpen(false);
    setMobileDetailsOpen(false);
  }

  return (
    <div className="app-shell">
      <TopBar
        subtitle={workflow.subtitle ?? workflow.title}
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
              <span className="topbar-status-text">{labelForStatus(hubStatus)}</span>
            </span>
            <button
              className="btn btn-danger"
              onClick={() => onRequestReset((newCustomer) => {
                void session.reset(
                  newCustomer?.id,
                  newCustomer ? JSON.stringify(newCustomer) : undefined
                );
              })}
              data-testid="button-reset-session"
              title="Reset session"
            >
              <Icon name="rotate-ccw" size={11} />
              <span className="topbar-btn-text">Reset</span>
            </button>
            <button
              className="btn btn-ghost"
              onClick={() => navigate('/workflows')}
              data-testid="button-back-to-catalog"
              title="Workflow catalog"
            >
              <span className="topbar-btn-text">Catalog</span>
            </button>
          </>
        }
      />
      {error && (
        <div className="alert alert-error" style={{ margin: 'var(--space-3)' }} data-testid="text-session-error" role="alert">
          {error}
        </div>
      )}
      <div
        className={`wf-main ${!pipelineOpen ? 'pipeline-closed' : ''} ${!detailsOpen ? 'details-closed' : ''} ${
          mobilePipelineOpen ? 'mobile-pipeline-open' : ''
        } ${mobileDetailsOpen ? 'mobile-details-open' : ''}`}
      >
        <AgentPipeline
          workflow={workflow}
          state={pipelineState}
          activeTools={collectActiveTools(snapshot.agents)}
          onClose={closePipeline}
        />
        <ChatPanel
          session={session}
          pipelineOpen={pipelineOpen}
          detailsOpen={detailsOpen}
          onOpenPipeline={openPipeline}
          onOpenDetails={openDetails}
        />
        <ContextPanel
          session={session}
          tab={tab}
          onTabChange={setTab}
          onClose={closeDetails}
        />
        {(mobilePipelineOpen || mobileDetailsOpen) && (
          <div
            className="wf-drawer-backdrop"
            onClick={closeMobileDrawers}
            aria-hidden="true"
          />
        )}
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
