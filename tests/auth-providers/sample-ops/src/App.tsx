import { useCallback, useEffect, useState } from 'react'
import {
  fetchMe,
  getLoginRequest,
  getMsalInstance,
  isEntraConfigured,
  type MeProfile,
} from './lib/auth/msal-config'

export function App() {
  const [accountName, setAccountName] = useState<string | null>(null)
  const [me, setMe] = useState<MeProfile | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const refresh = useCallback(async () => {
    if (!isEntraConfigured()) {
      return
    }

    setBusy(true)
    setError(null)
    try {
      const msal = await getMsalInstance()
      const account = msal.getActiveAccount()
      setAccountName(account?.username ?? account?.name ?? null)
      if (account) {
        setMe(await fetchMe())
      } else {
        setMe(null)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
      setMe(null)
    } finally {
      setBusy(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  if (!isEntraConfigured()) {
    return (
      <main>
        <h1>AuthOps sample — ops SPA</h1>
        <p>
          Entra is not configured. Expect <code>VITE_ENTRA_CLIENT_ID</code>,{' '}
          <code>VITE_ENTRA_TENANT_ID</code>, and optionally{' '}
          <code>VITE_ENTRA_REDIRECT_URI</code> from AppHost <code>WithAuth</code>.
        </p>
      </main>
    )
  }

  const onSignIn = async () => {
    setError(null)
    const msal = await getMsalInstance()
    await msal.loginRedirect(getLoginRequest())
  }

  const onSignOut = async () => {
    setError(null)
    const msal = await getMsalInstance()
    await msal.logoutRedirect()
  }

  return (
    <main>
      <h1>AuthOps sample — ops SPA</h1>
      <p>
        {accountName ? (
          <>
            Signed in as <strong>{accountName}</strong>.{' '}
            <button type="button" onClick={() => void onSignOut()}>
              Sign out
            </button>
          </>
        ) : (
          <button type="button" onClick={() => void onSignIn()}>
            Sign in
          </button>
        )}
      </p>

      {busy && <p>Loading…</p>}
      {error && <p className="error">{error}</p>}

      {me && (
        <>
          <p>User profile from API <code>/me</code> (Microsoft Graph):</p>
          <ul>
            <li>
              <strong>Id</strong>: <code>{me.id}</code>
            </li>
            <li>
              <strong>Display name</strong>: <code>{me.displayName}</code>
            </li>
            <li>
              <strong>Mail</strong>: <code>{me.mail}</code>
            </li>
            <li>
              <strong>UPN</strong>: <code>{me.userPrincipalName}</code>
            </li>
          </ul>
        </>
      )}
    </main>
  )
}
