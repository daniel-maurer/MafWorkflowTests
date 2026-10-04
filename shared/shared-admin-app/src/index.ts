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

// Modern UI Primitives
export { Modal } from './components/ui/Modal';
export type { ModalProps } from './components/ui/Modal';
export { Input } from './components/ui/Input';
export type { InputProps } from './components/ui/Input';
export { Select } from './components/ui/Select';
export type { SelectProps, SelectOption } from './components/ui/Select';
export { Textarea } from './components/ui/Textarea';
export type { TextareaProps } from './components/ui/Textarea';
export { Switch } from './components/ui/Switch';
export type { SwitchProps } from './components/ui/Switch';
export { Pagination } from './components/ui/Pagination';
export type { PaginationProps } from './components/ui/Pagination';
export { Card, FormField } from './components/ui/Card';
export type { CardProps, FormFieldProps } from './components/ui/Card';

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
