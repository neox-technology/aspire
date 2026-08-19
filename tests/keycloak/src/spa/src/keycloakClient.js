import Keycloak from 'keycloak-js'

export function createKeycloakClient(config) {
  return new Keycloak({
    url: config.url,
    realm: config.realm,
    clientId: config.clientId,
  })
}

export function getKeycloakUsername(keycloak) {
  if (!keycloak?.tokenParsed) {
    return null
  }

  return keycloak.tokenParsed.preferred_username ?? keycloak.tokenParsed.sub ?? null
}
