import { isEntraConfigured } from './lib/auth/msal-config'

export function App() {
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

  return (
    <main>
      <h1>AuthOps sample — ops SPA</h1>
      <p>Entra SPA client is configured via Vite env (MSAL-ready).</p>
      <ul>
        <li>
          <strong>VITE_ENTRA_TENANT_ID</strong>:{' '}
          <code>{import.meta.env.VITE_ENTRA_TENANT_ID}</code>
        </li>
        <li>
          <strong>VITE_ENTRA_CLIENT_ID</strong>:{' '}
          <code>{import.meta.env.VITE_ENTRA_CLIENT_ID}</code>
        </li>
        <li>
          <strong>VITE_ENTRA_REDIRECT_URI</strong>:{' '}
          <code>
            {import.meta.env.VITE_ENTRA_REDIRECT_URI ?? '(window origin)'}
          </code>
        </li>
      </ul>
    </main>
  )
}
