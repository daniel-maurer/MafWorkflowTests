import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// Use './' base so the build is deployable to any subpath (Azure Static Web Apps,
// App Service virtual directories, S3 previews).
export default defineConfig({
  plugins: [react()],
  base: './',
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@maf/shared-admin-app/styles': path.resolve(__dirname, '../shared/shared-admin-app/src/styles'),
      '@maf/shared-admin-app': path.resolve(__dirname, '../shared/shared-admin-app/src/index.ts'),
    },
  },
  server: {
    port: 5173,
    host: true,
  },
});
