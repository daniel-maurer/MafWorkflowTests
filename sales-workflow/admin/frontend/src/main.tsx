import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import '@maf/shared-admin-app/styles/tokens.css';
import '@maf/shared-admin-app/styles/global.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
