/**
 * Thin wrapper around `@azure/msal-browser` so the rest of the app does not
 * need to depend on MSAL types directly. The wrapper is loaded dynamically so
 * mock-mode bundles do not initialize MSAL at all.
 */

export interface MsalConfig {
  clientId: string;
  tenantId: string;
  redirectUri: string;
}

export interface AccountInfo {
  homeAccountId: string;
  username: string;
  name?: string;
}

export interface AuthenticationResult {
  account?: AccountInfo;
  accessToken?: string;
  idToken?: string;
}

export interface MsalLike {
  getActiveAccount(): AccountInfo | null;
  setActiveAccount(account: AccountInfo | null): void;
  loginPopup(req: { scopes: string[] }): Promise<AuthenticationResult>;
  logoutPopup(): Promise<void>;
  acquireTokenSilent(req: {
    account: AccountInfo;
    scopes: string[];
  }): Promise<AuthenticationResult>;
}

export async function createMsalApp(cfg: MsalConfig): Promise<MsalLike> {
  if (!cfg.clientId) {
    throw new Error(
      'VITE_ENTRA_CLIENT_ID is not set. Either set it or switch VITE_AUTH_MODE=mock.',
    );
  }
  const { PublicClientApplication } = await import('@azure/msal-browser');
  const authority = cfg.tenantId.startsWith('http')
    ? cfg.tenantId
    : `https://login.microsoftonline.com/${cfg.tenantId}`;
  const app = new PublicClientApplication({
    auth: {
      clientId: cfg.clientId,
      authority,
      redirectUri: cfg.redirectUri,
    },
    cache: {
      // sessionStorage works in standard browser contexts. If the host iframe
      // disables it, MSAL falls back to in-memory storage automatically.
      cacheLocation: 'sessionStorage',
    },
  });
  await app.initialize();
  // Restore active account on page reload.
  const accounts = app.getAllAccounts();
  if (accounts.length > 0 && !app.getActiveAccount()) {
    app.setActiveAccount(accounts[0]);
  }
  return app as unknown as MsalLike;
}
