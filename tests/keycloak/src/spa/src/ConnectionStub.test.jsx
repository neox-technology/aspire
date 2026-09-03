import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import ConnectionStub from './ConnectionStub.jsx'
import { createKeycloakConfig, isKeycloakConfigured } from './authConfig.js'

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
    expect(isKeycloakConfigured(config)).toBe(true)
  })

  it('returns empty realm and clientId when env is missing', () => {
    const config = createKeycloakConfig({})

    expect(config.url).toBe('http://localhost:8080')
    expect(config.realm).toBe('')
    expect(config.clientId).toBe('')
    expect(isKeycloakConfigured(config)).toBe(false)
  })

  it('derives authority from url and realm', () => {
    const config = createKeycloakConfig({
      KEYCLOAK_URL: 'https://localhost:50938',
      KEYCLOAK_REALM: 'master',
      KEYCLOAK_CLIENT_ID: 'neox-spa',
    })

    expect(`${config.url}/realms/${config.realm}`).toBe(
      'https://localhost:50938/realms/master',
    )
  })
})

describe('ConnectionStub', () => {
  it('shows configured status when realm and clientId are present', () => {
    const config = createKeycloakConfig({
      KEYCLOAK_URL: 'https://localhost:50938',
      KEYCLOAK_REALM: 'master',
      KEYCLOAK_CLIENT_ID: 'neox-spa',
    })
    const html = renderToStaticMarkup(<ConnectionStub config={config} />)

    expect(html).toContain('configured')
    expect(html).toContain('master')
    expect(html).toContain('neox-spa')
    expect(html).toContain('https://localhost:50938/realms/master')
  })

  it('shows missing config when realm or clientId is absent', () => {
    const config = createKeycloakConfig({})
    const html = renderToStaticMarkup(<ConnectionStub config={config} />)

    expect(html).toContain('missing config')
  })

  it('shows Sign in when configured and signed out', () => {
    const config = createKeycloakConfig({
      KEYCLOAK_URL: 'https://localhost:50938',
      KEYCLOAK_REALM: 'master',
      KEYCLOAK_CLIENT_ID: 'neox-spa',
    })
    const html = renderToStaticMarkup(
      <ConnectionStub
        config={config}
        keycloak={{ login: () => {}, logout: () => {} }}
        authenticated={false}
      />,
    )

    expect(html).toContain('signed out')
    expect(html).toContain('Sign in')
  })

  it('shows user from /me and Sign out when authenticated', () => {
    const config = createKeycloakConfig({
      KEYCLOAK_URL: 'https://localhost:50938',
      KEYCLOAK_REALM: 'master',
      KEYCLOAK_CLIENT_ID: 'neox-spa',
    })
    const html = renderToStaticMarkup(
      <ConnectionStub
        config={config}
        keycloak={{
          login: () => {},
          logout: () => {},
        }}
        authenticated
        userName="demo-user"
      />,
    )

    expect(html).toContain('signed in')
    expect(html).toContain('demo-user')
    expect(html).toContain('Sign out')
  })

  it('shows loading while /me is in flight', () => {
    const config = createKeycloakConfig({
      KEYCLOAK_URL: 'https://localhost:50938',
      KEYCLOAK_REALM: 'master',
      KEYCLOAK_CLIENT_ID: 'neox-spa',
    })
    const html = renderToStaticMarkup(
      <ConnectionStub
        config={config}
        keycloak={{
          login: () => {},
          logout: () => {},
        }}
        authenticated
        userLoading
      />,
    )

    expect(html).toContain('loading…')
  })
})
