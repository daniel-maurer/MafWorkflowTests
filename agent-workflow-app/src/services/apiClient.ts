/**
 * REST client used by the frontend.
 *
 * Every request goes through `request()` which automatically attaches the
 * bearer token returned by `AuthContext.getAccessToken()`. The exact endpoint
 * paths are documented in `docs/endpoint-contracts.md`.
 *
 * In mock mode the catalog endpoint falls back to the in-repo
 * `WORKFLOW_CATALOG` so the UI is fully functional with no backend.
 */
import { env } from '@/config/env';
import { WORKFLOW_CATALOG } from '@/config/workflows';
import type { KbItem, WorkflowDefinition } from '@/types/workflow';

export type TokenProvider = () => Promise<string | null>;

let tokenProvider: TokenProvider = async () => null;
export function setTokenProvider(provider: TokenProvider) {
  tokenProvider = provider;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = await tokenProvider();
  const headers = new Headers(init.headers ?? {});
  headers.set('Accept', 'application/json');
  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const res = await fetch(`${env.apiBaseUrl}${path}`, { ...init, headers });
  if (!res.ok) {
    let detail = '';
    try {
      detail = await res.text();
    } catch {
      /* noop */
    }
    throw new ApiError(res.status, res.statusText || 'Request failed', detail);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export class ApiError extends Error {
  status: number;
  detail: string;
  constructor(status: number, message: string, detail = '') {
    super(message);
    this.status = status;
    this.detail = detail;
  }
}

export const apiClient = {
  async listWorkflows(): Promise<WorkflowDefinition[]> {
    if (!env.apiBaseUrl) return WORKFLOW_CATALOG;
    try {
      return await request<WorkflowDefinition[]>('/api/workflows');
    } catch (err) {
      // Fall back to the static catalog so the dev UI never breaks.
      console.warn('listWorkflows fell back to static catalog:', err);
      return WORKFLOW_CATALOG;
    }
  },

  async getWorkflow(workflowId: string): Promise<WorkflowDefinition | undefined> {
    const list = await this.listWorkflows();
    return list.find((w) => w.id === workflowId);
  },

  async createSession(input: {
    workflowId: string;
    initialMessage?: string;
  }): Promise<{ sessionId: string; ticketId: string }> {
    if (!env.apiBaseUrl) {
      const ticketNum = Math.floor(Math.random() * 8000) + 1000;
      return {
        sessionId: `mock-ses-${ticketNum}`,
        ticketId: `#TKT-${ticketNum}`,
      };
    }
    return request('/api/workflow-sessions', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async searchKnowledgeBase(workflowId: string, query: string): Promise<KbItem[]> {
    if (!env.apiBaseUrl) return [];
    const qs = new URLSearchParams({ workflowId, query }).toString();
    const r = await request<{ items: KbItem[] }>(`/api/knowledge-base?${qs}`);
    return r.items ?? [];
  },
};
