/**
 * Workflow catalog configuration.
 *
 * Each entry declares a workflow that the user can select. The schema mirrors
 * what the backend's `GET /api/workflows` endpoint is expected to return
 * (see docs/endpoint-contracts.md). At runtime the catalog is fetched via
 * `apiClient.listWorkflows()`; this file is the static fallback used when the
 * backend is unreachable AND `VITE_AUTH_MODE=mock`.
 */
import type { WorkflowDefinition } from '@/types/workflow';

export const WORKFLOW_CATALOG: WorkflowDefinition[] = [
  {
    id: 'support',
    title: 'Support Workflow',
    subtitle: 'Multi-Agent Support — Microsoft Agent Framework',
    description:
      'Triage, KB search, automated resolution, and human handoff for end-user support tickets.',
    icon: 'layers',
    colorTheme: 'primary',
    agents: [
      {
        id: 'triage',
        icon: 'git-branch',
        title: 'Triage Agent',
        description: "Classifies the user's problem and routes to the right agent.",
        colorTheme: 'primary',
        order: 1,
        tools: [],
      },
      {
        id: 'freq',
        icon: 'database',
        title: 'Frequent Problem Agent',
        description: 'Searches KB for known issues. Routes to human when no match.',
        colorTheme: 'warning',
        order: 2,
        tools: [],
      },
      {
        id: 'res',
        icon: 'wrench',
        title: 'Resolution Agent',
        description: 'Executes automated tools to resolve known issues.',
        colorTheme: 'success',
        order: 3,
        tools: ['restart_service', 'reset_password', 'run_diagnostic', 'create_ticket'],
      },
      {
        id: 'pattern',
        icon: 'bar-chart-2',
        title: 'Pattern Record Agent',
        description: 'Records patterns and promotes repeated issues to the KB.',
        colorTheme: 'error',
        order: 4,
        tools: [],
      },
    ],
    scenarios: [
  {
        id: 'known',
        title: 'Known issue',
        label: "Can't login",
        description: 'Authentication problem with KB match and automatic resolution.',
        message: "I can't log in to the system. My password is not working.",
        flowType: 'known',
      },
      {
        id: 'tool',
        title: 'Tool call',
        label: 'Service restart',
        description: 'System availability issue that triggers diagnostics and restart.',
        message: 'The ERP service is not responding. The application is down.',
        flowType: 'tool',
      },
      {
        id: 'human',
        title: 'Unknown',
        label: 'Human handoff',
        description: 'Unknown integration issue escalated to a human agent.',
        message: 'Getting error 0xTISS-4821 during integration sync, never seen this before.',
        flowType: 'human-handoff',
      },
    ],
    capabilities: {
      humanHandoff: true,
      knowledgeBase: true,
      tracing: true,
    },
  },
  {
    id: 'incident-triage',
    title: 'Incident Triage',
    subtitle: 'Live production incident classification',
    description:
      'Classifies inbound incident reports, runs diagnostic playbooks, and dispatches to on-call when needed.',
    icon: 'siren',
    colorTheme: 'error',
    agents: [
      {
        id: 'parser',
        icon: 'file-search',
        title: 'Parser',
        description: 'Extracts incident metadata from free-text reports.',
        colorTheme: 'primary',
        order: 1,
        tools: [],
      },
      {
        id: 'classifier',
        icon: 'tag',
        title: 'Classifier',
        description: 'Assigns severity and component tags.',
        colorTheme: 'warning',
        order: 2,
        tools: [],
      },
      {
        id: 'oncall',
        icon: 'user-check',
        title: 'On-Call Dispatcher',
        description: 'Pages the right rotation if severity ≥ SEV-2.',
        colorTheme: 'error',
        order: 3,
        tools: ['page_oncall', 'open_warroom'],
      },
    ],
    scenarios: [
      {
        id: 'sev1',
        title: 'SEV-1',
        label: 'Database outage',
        description: 'Primary DB cluster unreachable, page on-call immediately.',
        message: 'Primary Postgres cluster is unreachable from app servers, 100% error rate.',
        flowType: 'human-handoff',
      },
      {
        id: 'sev3',
        title: 'SEV-3',
        label: 'Slow dashboard',
        description: 'A non-critical dashboard is responding slowly.',
        message: 'Reporting dashboard takes 30s to load for some users.',
        flowType: 'known',
      },
    ],
    capabilities: {
      humanHandoff: true,
      knowledgeBase: false,
      tracing: true,
    },
  },
  {
    id: 'kb-curator',
    title: 'KB Curator',
    subtitle: 'Knowledge base maintenance pipeline',
    description:
      'Reviews ticket history, identifies recurring patterns, and drafts new KB articles for human approval.',
    icon: 'book-open',
    colorTheme: 'success',
    agents: [
      {
        id: 'miner',
        icon: 'pickaxe',
        title: 'Pattern Miner',
        description: 'Finds recurring resolution patterns in closed tickets.',
        colorTheme: 'primary',
        order: 1,
        tools: [],
      },
      {
        id: 'drafter',
        icon: 'pen-line',
        title: 'Article Drafter',
        description: 'Drafts KB articles from clustered tickets.',
        colorTheme: 'success',
        order: 2,
        tools: ['draft_article'],
      },
    ],
    scenarios: [
      {
        id: 'curate',
        title: 'Curate this week',
        label: 'Weekly digest',
        description: 'Mine the last 7 days of tickets and draft article candidates.',
        message: 'Mine the last 7 days of tickets and draft any new KB articles.',
        flowType: 'tool',
      },
    ],
    capabilities: {
      humanHandoff: false,
      knowledgeBase: true,
      tracing: true,
    },
  },
];
