import { useEffect } from 'react';
import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from '@/auth/AuthContext';
import { RequireAuth } from '@/components/RequireAuth';
import { LoginPage } from '@/pages/LoginPage';
import { WorkflowsPage } from '@/pages/WorkflowsPage';
import { WorkflowRunPage } from '@/pages/WorkflowRunPage';
import { setTokenProvider } from '@/services/apiClient';

function TokenWire() {
  const auth = useAuth();
  useEffect(() => {
    setTokenProvider(auth.getAccessToken);
  }, [auth.getAccessToken]);
  return null;
}

export default function App() {
  return (
    <AuthProvider>
      <TokenWire />
      {/* HashRouter ensures the app works at any base path (Azure Static Web
          Apps, App Service virtual directories, S3 previews) without
          server-side redirect rules. */}
      <HashRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/workflows"
            element={
              <RequireAuth>
                <WorkflowsPage />
              </RequireAuth>
            }
          />
          <Route
            path="/workflows/:workflowId/run"
            element={
              <RequireAuth>
                <WorkflowRunPage />
              </RequireAuth>
            }
          />
          <Route path="*" element={<Navigate to="/workflows" replace />} />
        </Routes>
      </HashRouter>
    </AuthProvider>
  );
}
