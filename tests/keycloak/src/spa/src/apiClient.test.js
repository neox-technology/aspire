import { describe, expect, it, vi } from 'vitest'
import { fetchMe } from './apiClient.js'

describe('fetchMe', () => {
  it('returns profile from GET /me', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ name: 'demo-user' }),
    })
    vi.stubGlobal('fetch', fetchMock)

    await expect(fetchMe('http://localhost:5000', 'token-123')).resolves.toEqual({
      name: 'demo-user',
    })
    expect(fetchMock).toHaveBeenCalledWith('http://localhost:5000/me', {
      headers: {
        Authorization: 'Bearer token-123',
      },
    })

    vi.unstubAllGlobals()
  })

  it('throws when GET /me fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
      }),
    )

    await expect(fetchMe('http://localhost:5000', 'token-123')).rejects.toThrow(
      'GET /me failed with status 401',
    )

    vi.unstubAllGlobals()
  })
})
