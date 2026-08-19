export function createKeycloakConfig(env = import.meta.env) {
  return {
    url: env.KEYCLOAK_URL ?? 'http://localhost:8080',
    realm: env.KEYCLOAK_REALM ?? '',
    clientId: env.KEYCLOAK_CLIENT_ID ?? '',
  }
}

export function isKeycloakConfigured(config) {
  return Boolean(config.realm && config.clientId)
}

export function createKeycloakInitOptions() {
  return {
    onLoad: 'check-sso',
    pkceMethod: 'S256',
    checkLoginIframe: false,
  }
}

export function createApiConfig(env = import.meta.env) {
  return {
    baseUrl: env.API_BASE_URL ?? '',
  }
}

export function isApiConfigured(config) {
  return Boolean(config.baseUrl)
}

export const keycloakConfig = createKeycloakConfig()
export const apiConfig = createApiConfig()
