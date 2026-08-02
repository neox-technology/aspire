/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_ENTRA_CLIENT_ID?: string
  readonly VITE_ENTRA_TENANT_ID?: string
  readonly VITE_ENTRA_REDIRECT_URI?: string
  readonly VITE_API_SCOPE?: string
  readonly VITE_API_BASE_URL?: string
  /** Aspire service discovery for the api project (http). */
  readonly services__api__http__0?: string
  readonly services__api__https__0?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
