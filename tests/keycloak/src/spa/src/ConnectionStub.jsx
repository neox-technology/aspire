export default function ConnectionStub({
  config,
  keycloak = null,
  authenticated = false,
  initError = null,
  userLoading = false,
  userName = null,
  userError = null,
}) {
  const authority = `${config.url}/realms/${config.realm}`
  const configured = Boolean(config.realm && config.clientId)

  return (
    <main>
      <h1>Keycloak connection</h1>
      <dl>
        <dt>Status</dt>
        <dd>{configured ? 'configured' : 'missing config'}</dd>
        <dt>Realm</dt>
        <dd>{config.realm || '—'}</dd>
        <dt>Client ID</dt>
        <dd>{config.clientId || '—'}</dd>
        <dt>Authority</dt>
        <dd>{authority}</dd>
        {configured ? (
          <>
            <dt>Auth status</dt>
            <dd>{authenticated ? 'signed in' : 'signed out'}</dd>
            {authenticated ? (
              <>
                <dt>User</dt>
                <dd>{userLoading ? 'loading…' : (userName ?? '—')}</dd>
              </>
            ) : null}
          </>
        ) : null}
      </dl>
      {initError ? <p role="alert">{initError}</p> : null}
      {userError ? <p role="alert">{userError}</p> : null}
      {configured && keycloak ? (
        authenticated ? (
          <button type="button" onClick={() => keycloak.logout()}>
            Sign out
          </button>
        ) : (
          <button type="button" onClick={() => keycloak.login()}>
            Sign in
          </button>
        )
      ) : null}
    </main>
  )
}
