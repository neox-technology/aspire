import Keycloak from 'keycloak-js'

export function createKeycloakClient(config) {
  return new Keycloak({
    url: config.url,
    realm: config.realm,
    clientId: config.clientId,
  })
}
