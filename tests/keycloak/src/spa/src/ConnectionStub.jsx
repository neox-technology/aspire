export default function ConnectionStub({ config }) {
  const authority = `${config.url}/realms/${config.realm}`

  return (
    <main>
      <h1>Keycloak connection</h1>
      <dl>
        <dt>Status</dt>
        <dd>{config.realm && config.clientId ? 'configured' : 'missing config'}</dd>
        <dt>Realm</dt>
        <dd>{config.realm || '—'}</dd>
        <dt>Client ID</dt>
        <dd>{config.clientId || '—'}</dd>
        <dt>Authority</dt>
        <dd>{authority}</dd>
      </dl>
    </main>
  )
}
