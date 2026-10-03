import { useEffect } from 'react';
import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import {
  AuthProvider,
  useAuth,
  RequireAuth,
  LoginPage,
  WorkflowRunPage,
  setTokenProvider,
} from '@maf/shared-admin-app';
import { setAdminTokenProvider } from '@/services/adminApiClient';

import { DashboardPage } from '@/pages/DashboardPage';
import { ProductListPage } from '@/pages/products/ProductListPage';
import { ProductFormPage } from '@/pages/products/ProductFormPage';
import { CategoryListPage } from '@/pages/categories/CategoryListPage';
import { DiscountListPage } from '@/pages/discounts/DiscountListPage';
import { DiscountFormPage } from '@/pages/discounts/DiscountFormPage';
import { CustomerListPage } from '@/pages/customers/CustomerListPage';
import { CustomerFormPage } from '@/pages/customers/CustomerFormPage';
import { PaymentConditionsPage } from '@/pages/payment-conditions/PaymentConditionsPage';
import { FollowUpListPage } from '@/pages/follow-ups/FollowUpListPage';

function TokenWire() {
  const auth = useAuth();
  useEffect(() => {
    setTokenProvider(auth.getAccessToken);
    setAdminTokenProvider(auth.getAccessToken);
  }, [auth.getAccessToken]);
  return null;
}

export default function App() {
  return (
    <AuthProvider>
      <TokenWire />
      <HashRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />

          {/* Admin Routes */}
          <Route
            path="/"
            element={
              <RequireAuth>
                <DashboardPage />
              </RequireAuth>
            }
          />
          <Route
            path="/products"
            element={
              <RequireAuth>
                <ProductListPage />
              </RequireAuth>
            }
          />
          <Route
            path="/products/new"
            element={
              <RequireAuth>
                <ProductFormPage />
              </RequireAuth>
            }
          />
          <Route
            path="/products/:id"
            element={
              <RequireAuth>
                <ProductFormPage />
              </RequireAuth>
            }
          />

          <Route
            path="/categories"
            element={
              <RequireAuth>
                <CategoryListPage />
              </RequireAuth>
            }
          />

          <Route
            path="/discounts"
            element={
              <RequireAuth>
                <DiscountListPage />
              </RequireAuth>
            }
          />
          <Route
            path="/discounts/new"
            element={
              <RequireAuth>
                <DiscountFormPage />
              </RequireAuth>
            }
          />
          <Route
            path="/discounts/:id"
            element={
              <RequireAuth>
                <DiscountFormPage />
              </RequireAuth>
            }
          />

          <Route
            path="/customers"
            element={
              <RequireAuth>
                <CustomerListPage />
              </RequireAuth>
            }
          />
          <Route
            path="/customers/new"
            element={
              <RequireAuth>
                <CustomerFormPage />
              </RequireAuth>
            }
          />
          <Route
            path="/customers/:id"
            element={
              <RequireAuth>
                <CustomerFormPage />
              </RequireAuth>
            }
          />

          <Route
            path="/follow-ups"
            element={
              <RequireAuth>
                <FollowUpListPage />
              </RequireAuth>
            }
          />

          <Route
            path="/payment-conditions"
            element={
              <RequireAuth>
                <PaymentConditionsPage />
              </RequireAuth>
            }
          />

          {/* Workflow Execution */}
          <Route
            path="/workflow/run"
            element={
              <RequireAuth>
                <WorkflowRunPage />
              </RequireAuth>
            }
          />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </HashRouter>
    </AuthProvider>
  );
}
