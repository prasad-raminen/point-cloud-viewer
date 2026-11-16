/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Optional absolute API base URL; defaults to same origin (Vite proxy / nginx). */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
