/**
 * Authentication context.
 *
 * Two modes are supported, controlled by `VITE_AUTH_MODE`:
 *
 *   • mock   — local dev login. Any non-empty username/password is accepted.
 *              The "user" is held in memory only (no localStorage — sandbox-safe).
 *   • entra  — Microsoft Entra ID / Azure AD via MSAL (`@azure/msal-browser`).
 *              Uses `loginPopup` so it works inside hash-routed apps deployed
 *              to Azure Static Web Apps or App Service without server-side
 *              redirect handling.
 *
 * Components should call `useAuth()` to get the current user, login/logout
 * functions, and `getAccessToken()` for outbound API calls.
 */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { env } from '@/config/env';
import { createMsalApp, type MsalLike } from './msal';

export interface AuthUser {
  id: string;
  name: string;
  username: string;
  email?: string;
}

export interface AuthState {
  mode: 'mock' | 'entra';
  user: AuthUser | null;
  ready: boolean;
  loading: boolean;
  error: string | null;
  login: (opts?: { username?: string; password?: string }) => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
}

const AuthCtx = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [ready, setReady] = useState(env.authMode !== 'entra');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const msalRef = useRef<MsalLike | null>(null);

  // Initialize MSAL once if running in entra mode.
  useEffect(() => {
    if (env.authMode !== 'entra') return;
    let cancelled = false;
    (async () => {
      try {
        const app = await createMsalApp({
          clientId: env.entra.clientId,
          tenantId: env.entra.tenantId,
          redirectUri: env.entra.redirectUri,
        });
        if (cancelled) return;
        msalRef.current = app;
        const account = app.getActiveAccount();
        if (account) {
          setUser({
            id: account.homeAccountId,
            name: account.name ?? account.username,
            username: account.username,
            email: account.username,
          });
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to initialize Entra ID.');
      } finally {
        if (!cancelled) setReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const login = useCallback<AuthState['login']>(async (opts) => {
    setError(null);
    setLoading(true);
    try {
      if (env.authMode === 'mock') {
        const username = (opts?.username ?? '').trim();
        if (!username) throw new Error('Enter a username to continue.');
        // Treat any non-empty password as valid for dev mode.
        setUser({
          id: 'mock-' + username.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
          name: username,
          username,
          email: username.includes('@') ? username : `${username}@example.dev`,
        });
        return;
      }
      const app = msalRef.current;
      if (!app) throw new Error('Entra ID not initialized. Check VITE_ENTRA_CLIENT_ID.');
      const result = await app.loginPopup({
        scopes: ['openid', 'profile', 'email', ...env.entra.apiScopes],
      });
      if (result?.account) {
        app.setActiveAccount(result.account);
        setUser({
          id: result.account.homeAccountId,
          name: result.account.name ?? result.account.username,
          username: result.account.username,
          email: result.account.username,
        });
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Login failed.';
      setError(msg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback<AuthState['logout']>(async () => {
    setError(null);
    if (env.authMode === 'entra' && msalRef.current) {
      try {
        await msalRef.current.logoutPopup();
      } catch {
        /* user cancelled — ignore */
      }
    }
    setUser(null);
  }, []);

  const getAccessToken = useCallback<AuthState['getAccessToken']>(async () => {
    if (env.authMode === 'mock') {
      return user ? `mock-token:${user.username}` : null;
    }
    const app = msalRef.current;
    const account = app?.getActiveAccount();
    if (!app || !account) return null;
    try {
      const result = await app.acquireTokenSilent({
        account,
        scopes: env.entra.apiScopes.length > 0 ? env.entra.apiScopes : ['openid'],
      });
      return result.accessToken ?? result.idToken ?? null;
    } catch {
      return null;
    }
  }, [user]);

  const value = useMemo<AuthState>(
    () => ({
      mode: env.authMode,
      user,
      ready,
      loading,
      error,
      login,
      logout,
      getAccessToken,
    }),
    [user, ready, loading, error, login, logout, getAccessToken],
  );

  return <AuthCtx.Provider value={value}>{children}</AuthCtx.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthCtx);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>.');
  return ctx;
}
