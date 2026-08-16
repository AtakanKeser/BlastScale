import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Vite configuration: React plugin only. VITE_* environment variables (API and Prometheus
// URLs) are inlined into the bundle at build time, see .env.example and the Dockerfile.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
  build: {
    sourcemap: false,
  },
});
