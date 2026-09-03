import { useEffect, useState } from 'react'
import { useAccount, useMsal } from '@azure/msal-react'
import { tokenRequest } from './authConfig.js'

export default function ConnectionStub() {
  const { instance, accounts } = useMsal()
  const account = useAccount(accounts[0])
  const [token, setToken] = useState({ status: 'pending', error: null, expiresOn: null, scopes: [] })

  useEffect(() => {
    if (!account) {
      return
    }

    let cancelled = false
    instance
      .acquireTokenSilent({ ...tokenRequest, account })
      .then((result) => {
        if (!cancelled) {
          setToken({
            status: 'ok',
            error: null,
            expiresOn: result.expiresOn?.toISOString() ?? null,
            scopes: result.scopes ?? [],
          })
        }
      })
      .catch((error) => {
        if (!cancelled) {
          setToken({
            status: 'error',
            error: error.message,
            expiresOn: null,
            scopes: [],
          })
        }
      })

    return () => {
      cancelled = true
    }
  }, [account, instance])

  return (
    <main>
      <h1>Entra connection</h1>
      <dl>
        <dt>Status</dt>
        <dd>{token.status === 'ok' ? 'connected' : token.status}</dd>
        <dt>Account</dt>
        <dd>{account?.name ?? '—'}</dd>
        <dt>Username</dt>
        <dd>{account?.username ?? '—'}</dd>
        <dt>Tenant</dt>
        <dd>{account?.tenantId ?? import.meta.env.ENTRA_TenantId ?? '—'}</dd>
        <dt>Client ID</dt>
        <dd>{import.meta.env.ENTRA_ClientId ?? '—'}</dd>
        <dt>Scope</dt>
        <dd>{token.scopes.join(' ') || import.meta.env.ENTRA_Scope || '—'}</dd>
        <dt>Token expires</dt>
        <dd>{token.expiresOn ?? '—'}</dd>
      </dl>
      {token.error ? <p>{token.error}</p> : null}
    </main>
  )
}
