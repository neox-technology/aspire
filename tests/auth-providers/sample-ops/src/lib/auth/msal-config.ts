import {
  type Configuration,
  type RedirectRequest,
  LogLevel,
  PublicClientApplication,
} from '@azure/msal-browser'

function requiredEnv(name: keyof ImportMetaEnv): string {
  const value = import.meta.env[name]
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new Error(`Missing ${name}.`)
  }
  return value.trim()
}

export function isEntraConfigured(): boolean {
  return Boolean(
    import.meta.env.VITE_ENTRA_CLIENT_ID?.trim() &&
      import.meta.env.VITE_ENTRA_TENANT_ID?.trim(),
  )
}

export function getApiBaseUrl(): string {
  return (
    import.meta.env.VITE_API_BASE_URL?.trim() ||
    import.meta.env.services__api__https__0?.trim() ||
    import.meta.env.services__api__http__0?.trim() ||
    ''
  )
}

export function getApiScope(): string {
  return import.meta.env.VITE_API_SCOPE?.trim() || ''
}

export function createMsalConfig(): Configuration {
  const clientId = requiredEnv('VITE_ENTRA_CLIENT_ID')
  const tenantId = requiredEnv('VITE_ENTRA_TENANT_ID')
  const redirectUri =
    import.meta.env.VITE_ENTRA_REDIRECT_URI?.trim() || window.location.origin

  return {
    auth: {
      clientId,
      authority: `https://login.microsoftonline.com/${tenantId}`,
      redirectUri,
      postLogoutRedirectUri: redirectUri,
    },
    cache: {
      cacheLocation: 'localStorage',
    },
    system: {
      loggerOptions: {
        logLevel: LogLevel.Warning,
      },
    },
  }
}

export function getLoginRequest(): RedirectRequest {
  const apiScope = getApiScope()
  return {
    scopes: apiScope
      ? ['openid', 'profile', 'email', apiScope]
      : ['openid', 'profile', 'email'],
  }
}

/** @deprecated Prefer getLoginRequest() which includes the API scope when configured. */
export const loginRequest: RedirectRequest = {
  scopes: ['openid', 'profile', 'email'],
}

let msalInstance: PublicClientApplication | null = null

export async function getMsalInstance(): Promise<PublicClientApplication> {
  if (!msalInstance) {
    msalInstance = new PublicClientApplication(createMsalConfig())
    await msalInstance.initialize()
    const result = await msalInstance.handleRedirectPromise()
    const account =
      result?.account ??
      msalInstance.getActiveAccount() ??
      msalInstance.getAllAccounts()[0]
    if (account) {
      msalInstance.setActiveAccount(account)
    }
  }
  return msalInstance
}

export type MeProfile = {
  id?: string
  displayName?: string
  mail?: string
  userPrincipalName?: string
}

export async function fetchMe(): Promise<MeProfile> {
  const msal = await getMsalInstance()
  const account = msal.getActiveAccount()
  if (!account) {
    throw new Error('Not signed in.')
  }

  const apiScope = getApiScope()
  if (!apiScope) {
    throw new Error('Missing VITE_API_SCOPE.')
  }

  const baseUrl = getApiBaseUrl()
  if (!baseUrl) {
    throw new Error('Missing API base URL (VITE_API_BASE_URL or services__api__).')
  }

  const token = await msal.acquireTokenSilent({
    account,
    scopes: [apiScope],
  })

  const response = await fetch(new URL('/me', baseUrl).toString(), {
    headers: {
      Authorization: `Bearer ${token.accessToken}`,
    },
  })

  if (!response.ok) {
    throw new Error(`API /me failed: ${response.status} ${response.statusText}`)
  }

  return (await response.json()) as MeProfile
}
