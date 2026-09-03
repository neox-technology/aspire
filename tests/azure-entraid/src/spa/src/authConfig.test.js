import { describe, expect, it } from 'vitest'
import {
  createLoginRequest,
  createMsalConfig,
  createTokenRequest,
} from './authConfig.js'

const env = {
  ENTRA_Instance: 'https://login.microsoftonline.com/',
  ENTRA_TenantId: '11111111-1111-1111-1111-111111111111',
  ENTRA_ClientId: 'spa-client-id',
  ENTRA_Audience: 'api://spa-client-id',
  ENTRA_LoginScopes: 'openid offline_access',
  ENTRA_Scope: 'api://api-client-id/access_as_user',
}

describe('ENTRA_ env mapping', () => {
  it('maps ENTRA_* to msalConfig.auth', () => {
    const config = createMsalConfig(env)
    expect(config.auth.clientId).toBe('spa-client-id')
    expect(config.auth.authority).toBe(
      'https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111',
    )
    expect(config.auth.redirectUri).toBe('/')
  })

  it('maps ENTRA_LoginScopes to loginRequest.scopes', () => {
    expect(createLoginRequest(env).scopes).toEqual(['openid', 'offline_access'])
  })

  it('omits login scopes when ENTRA_LoginScopes is absent', () => {
    expect(createLoginRequest({}).scopes).toEqual([])
  })

  it('maps ENTRA_Scope to tokenRequest.scopes', () => {
    expect(createTokenRequest(env).scopes).toEqual([
      'api://api-client-id/access_as_user',
    ])
  })
})
