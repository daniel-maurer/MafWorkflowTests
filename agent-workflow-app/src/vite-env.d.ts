/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_SIGNALR_HUB_URL?: string;
  readonly VITE_AUTH_MODE?: 'mock' | 'entra';
  readonly VITE_ENTRA_CLIENT_ID?: string;
  readonly VITE_ENTRA_TENANT_ID?: string;
  readonly VITE_ENTRA_REDIRECT_URI?: string;
  readonly VITE_ENTRA_API_SCOPES?: string;
  readonly VITE_MOCK_LATENCY_MS?: string;
}
interface ImportMeta {
  readonly env: ImportMetaEnv;
}
