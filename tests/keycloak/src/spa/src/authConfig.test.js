import { describe, expect, it } from 'vitest'
import { createKeycloakConfig } from './authConfig.js'

describe('KEYCLOAK_ env mapping', () => {
  it('maps KEYCLOAK_* variables', () => {
    const config = createKeycloakConfig({
      KEYCLOAK_URL: 'http://localhost:8080',
      KEYCLOAK_REALM: 'neox',
      KEYCLOAK_CLIENT_ID: 'neox-spa',
    })

    expect(config.url).toBe('http://localhost:8080')
    expect(config.realm).toBe('neox')
    expect(config.clientId).toBe('neox-spa')
  })
})
