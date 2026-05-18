import { Link } from 'react-router-dom';
import { Icon } from './Icon';
import { useTheme } from '@/hooks/useTheme';
import { useAuth } from '@/auth/AuthContext';
import type { ReactNode } from 'react';

export interface TopBarProps {
  subtitle?: string;
  /** Optional middle slot (pipeline steps, breadcrumb, etc.). */
  center?: ReactNode;
  /** Optional trailing slot (status pill, reset, etc.). */
  right?: ReactNode;
}

export function TopBar({ subtitle = 'Microsoft Agent Framework', center, right }: TopBarProps) {
  const [theme, toggleTheme] = useTheme();
  const { user, logout } = useAuth();
  return (
    <header className="topbar" data-testid="app-topbar">
      <Link to="/" className="topbar-logo" data-testid="link-home" style={{ color: 'inherit', textDecoration: 'none' }}>
        <div className="logo-icon" aria-hidden>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
          </svg>
        </div>
        <div>
          <div className="logo-text">Agent Workflow</div>
          <div className="logo-sub">{subtitle}</div>
        </div>
      </Link>

      <div style={{ flex: 1, display: 'flex', justifyContent: 'center' }}>{center}</div>

      <div className="topbar-actions">
        {right}
        <button
          className="btn-icon"
          onClick={toggleTheme}
          aria-label="Toggle theme"
          title="Toggle theme"
          data-testid="button-theme-toggle"
        >
          <Icon name={theme === 'dark' ? 'moon' : 'sun'} size={13} />
        </button>
        {user && (
          <>
            <span className="status-badge" data-testid="text-user-name">
              <Icon name="user" size={11} />
              {user.name}
            </span>
            <button
              className="btn btn-ghost"
              onClick={() => void logout()}
              data-testid="button-logout"
              aria-label="Sign out"
            >
              <Icon name="log-out" size={12} />
              Sign out
            </button>
          </>
        )}
      </div>
    </header>
  );
}
