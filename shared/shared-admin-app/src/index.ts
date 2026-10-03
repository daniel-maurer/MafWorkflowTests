// Auth
export { AuthProvider, useAuth } from './auth/AuthContext';
export type { AuthUser, AuthState } from './auth/AuthContext';
export { createMsalApp } from './auth/msal';

// Components
export { RequireAuth } from './components/RequireAuth';
export { TopBar } from './components/TopBar';
export { Icon } from './components/Icon';
export { AgentPipeline } from './components/workflow/AgentPipeline';
export { ChatPanel } from './components/workflow/ChatPanel';
export { ContextPanel } from './components/workflow/ContextPanel';
export { CustomerIdentificationModal } from './components/workflow/CustomerIdentificationModal';
export type { CustomerData } from './components/workflow/CustomerIdentificationModal';

// Pages
export { LoginPage } from './pages/LoginPage';
export { WorkflowRunPage } from './pages/WorkflowRunPage';

// Services
export { apiClient, setTokenProvider } from './services/apiClient';
export { createWorkflowHub } from './services/signalr';
export type { WorkflowHub, WorkflowEvent, WorkflowEventHandler, ConnectionStatus } from './services/signalr';
export { createMockHubConnection } from './services/mockSignalr';

// Hooks
export { useTheme } from './hooks/useTheme';
export { useWorkflowSession } from './hooks/useWorkflowSession';

// Config
export { env } from './config/env';

// Utils
export { formatMarkdown, formatMarkdown as renderMarkdown } from './utils/markdown';

// Types
export type * from './types/workflow';
