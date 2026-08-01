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
