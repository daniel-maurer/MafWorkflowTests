import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  base: './',
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@maf/shared-admin-app/styles': path.resolve(__dirname, '../../../shared/shared-admin-app/src/styles'),
      '@maf/shared-admin-app': path.resolve(__dirname, '../../../shared/shared-admin-app/src/index.ts'),
    },
  },
  server: {
    port: 5174,
    host: true,
  },
});
