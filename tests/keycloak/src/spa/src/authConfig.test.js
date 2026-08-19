import { describe, expect, it } from 'vitest'
import { createApiConfig, createKeycloakConfig, isApiConfigured } from './authConfig.js'

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

describe('VITE_API_BASE_URL env mapping', () => {
  it('maps VITE_API_BASE_URL', () => {
    const config = createApiConfig({
      VITE_API_BASE_URL: 'http://localhost:5000',
    })

    expect(config.baseUrl).toBe('http://localhost:5000')
    expect(isApiConfigured(config)).toBe(true)
  })

  it('returns empty baseUrl when env is missing', () => {
    const config = createApiConfig({})

    expect(config.baseUrl).toBe('')
    expect(isApiConfigured(config)).toBe(false)
  })
})
