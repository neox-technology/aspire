export function createKeycloakConfig(env = import.meta.env) {
  return {
    url: env.KEYCLOAK_URL ?? 'http://localhost:8080',
    realm: env.KEYCLOAK_REALM ?? '',
    clientId: env.KEYCLOAK_CLIENT_ID ?? '',
  }
}

export const keycloakConfig = createKeycloakConfig()
