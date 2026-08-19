function splitScopes(value) {
  return String(value ?? '').split(/\s+/).filter(Boolean)
}

export function createMsalConfig(env = import.meta.env) {
  const instance = env.ENTRA_Instance ?? 'https://login.microsoftonline.com/'
  const tenantId = env.ENTRA_TenantId
  return {
    auth: {
      clientId: env.ENTRA_ClientId ?? '',
      authority: tenantId ? `${instance}${tenantId}` : instance,
      redirectUri: '/',
    },
  }
}

export function createLoginRequest(env = import.meta.env) {
  return {
    scopes: splitScopes(env.ENTRA_LoginScopes),
  }
}

export function createTokenRequest(env = import.meta.env) {
  return {
    scopes: env.ENTRA_Scope ? [env.ENTRA_Scope] : [],
  }
}

export const msalConfig = createMsalConfig()
export const loginRequest = createLoginRequest()
export const tokenRequest = createTokenRequest()
