/**
 * Centralized access to Vite environment variables. Components must read env
 * through this module so we can mock/override values in tests.
 */
export type AuthMode = 'mock' | 'entra';

function readString(key: string, fallback = ''): string {
  const v = import.meta.env[key];
  return typeof v === 'string' ? v : fallback;
}

function readNumber(key: string, fallback: number): number {
  const v = import.meta.env[key];
  if (typeof v === 'string' && v.length > 0) {
    const n = Number(v);
    if (!Number.isNaN(n)) return n;
  }
  return fallback;
}

const apiBase = readString('VITE_API_BASE_URL', '').replace(/\/$/, '');

export const env = {
  apiBaseUrl: apiBase,
  signalrHubUrl: readString('VITE_SIGNALR_HUB_URL', '') || `${apiBase}/hubs/workflow`,
  authMode: (readString('VITE_AUTH_MODE', 'mock') as AuthMode) || 'mock',
  entra: {
    clientId: readString('VITE_ENTRA_CLIENT_ID', ''),
    tenantId: readString('VITE_ENTRA_TENANT_ID', 'common'),
    redirectUri: readString('VITE_ENTRA_REDIRECT_URI', window.location.origin + window.location.pathname),
    apiScopes: readString('VITE_ENTRA_API_SCOPES', '')
      .split(/\s+/)
      .map((s) => s.trim())
      .filter(Boolean),
  },
  mockLatencyMs: readNumber('VITE_MOCK_LATENCY_MS', 900),
};
