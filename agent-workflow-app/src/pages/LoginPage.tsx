import { useState, type FormEvent } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/auth/AuthContext';
import { Icon } from '@/components/Icon';
import { TopBar } from '@/components/TopBar';

export function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const from = (location.state as { from?: string } | null)?.from ?? '/workflows';

  async function handleMockSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setLocalError(null);
    setSubmitting(true);
    try {
      await auth.login({ username, password });
      navigate(from, { replace: true });
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : 'Login failed.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleEntraClick() {
    setLocalError(null);
    setSubmitting(true);
    try {
      await auth.login();
      navigate(from, { replace: true });
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : 'Microsoft sign-in failed.');
    } finally {
      setSubmitting(false);
    }
  }

  const error = localError ?? auth.error;

  return (
    <div className="app-shell">
      <TopBar subtitle="Sign in" />
      <main
        style={{
          flex: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 'var(--space-6)',
          overflow: 'auto',
        }}
      >
        <div className="form-card" data-testid="login-card">
          <h1
            style={{
              fontSize: 'var(--text-lg)',
              fontWeight: 600,
              marginBottom: 'var(--space-2)',
              letterSpacing: '-0.02em',
            }}
          >
            Welcome back
          </h1>
          <p
            style={{
              fontSize: 'var(--text-xs)',
              color: 'var(--color-text-muted)',
              marginBottom: 'var(--space-6)',
            }}
            data-testid="text-auth-mode"
          >
            {auth.mode === 'entra'
              ? 'Sign in with your Microsoft Entra ID account.'
              : 'Local development sign-in. Any non-empty username will work.'}
          </p>

          {error && (
            <div className="alert alert-error" data-testid="text-login-error" role="alert">
              {error}
            </div>
          )}

          {auth.mode === 'mock' ? (
            <form onSubmit={handleMockSubmit}>
              <div className="field">
                <label htmlFor="login-username" className="field-label">
                  Username or email
                </label>
                <input
                  id="login-username"
                  className="field-input"
                  type="text"
                  autoComplete="username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="agent@example.dev"
                  data-testid="input-username"
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="login-password" className="field-label">
                  Password
                </label>
                <input
                  id="login-password"
                  className="field-input"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  data-testid="input-password"
                />
              </div>
              <button
                type="submit"
                className="btn btn-primary"
                style={{ width: '100%', justifyContent: 'center', padding: 'var(--space-3)' }}
                disabled={submitting || !username.trim()}
                data-testid="button-login-mock"
              >
                {submitting ? 'Signing in…' : 'Sign in'}
              </button>
              <p
                style={{
                  marginTop: 'var(--space-4)',
                  fontSize: '10px',
                  color: 'var(--color-text-faint)',
                  textAlign: 'center',
                }}
              >
                To enable Microsoft sign-in set <code>VITE_AUTH_MODE=entra</code> in your env file.
              </p>
            </form>
          ) : (
            <button
              type="button"
              className="btn btn-primary"
              style={{ width: '100%', justifyContent: 'center', padding: 'var(--space-3)' }}
              onClick={() => void handleEntraClick()}
              disabled={submitting || !auth.ready}
              data-testid="button-login-entra"
            >
              <Icon name="user" size={14} />
              {submitting ? 'Opening Microsoft sign-in…' : 'Sign in with Microsoft'}
            </button>
          )}
        </div>
      </main>
    </div>
  );
}
