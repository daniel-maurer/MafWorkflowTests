import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/auth/AuthContext';

/**
 * Route guard that redirects unauthenticated users to /login while
 * preserving the originally requested URL in location state.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const location = useLocation();
  if (!auth.ready) {
    return (
      <div className="empty-state" data-testid="text-auth-loading">
        <div className="empty-t">Initializing…</div>
      </div>
    );
  }
  if (!auth.user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }
  return <>{children}</>;
}
