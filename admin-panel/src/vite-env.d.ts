/// <reference types="vite/client" />

// Typed view of the build-time environment variables exposed through import.meta.env.
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_PROMETHEUS_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
