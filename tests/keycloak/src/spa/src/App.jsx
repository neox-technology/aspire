import { useEffect, useRef, useState } from 'react'
import ConnectionStub from './ConnectionStub.jsx'
import {
  createKeycloakInitOptions,
  isKeycloakConfigured,
  keycloakConfig,
} from './authConfig.js'
import { createKeycloakClient } from './keycloakClient.js'

export default function App() {
  const [ready, setReady] = useState(!isKeycloakConfigured(keycloakConfig))
  const [authenticated, setAuthenticated] = useState(false)
  const [keycloak, setKeycloak] = useState(null)
  const [initError, setInitError] = useState(null)
  const initStarted = useRef(false)

  useEffect(() => {
    if (initStarted.current || !isKeycloakConfigured(keycloakConfig)) {
      return
    }

    initStarted.current = true
    const client = createKeycloakClient(keycloakConfig)

    client.onAuthSuccess = () => setAuthenticated(true)
    client.onAuthLogout = () => setAuthenticated(false)
    client.onAuthRefreshSuccess = () => setAuthenticated(true)
    client.onTokenExpired = () => {
      client.updateToken(30).catch(() => client.logout())
    }

    client
      .init(createKeycloakInitOptions())
      .then((auth) => {
        setAuthenticated(auth)
        setKeycloak(client)
      })
      .catch((error) => {
        setInitError(error instanceof Error ? error.message : String(error))
      })
      .finally(() => {
        setReady(true)
      })
  }, [])

  if (!ready) {
    return (
      <main>
        <h1>Keycloak connection</h1>
        <p>Initializing…</p>
      </main>
    )
  }

  return (
    <ConnectionStub
      config={keycloakConfig}
      keycloak={keycloak}
      authenticated={authenticated}
      initError={initError}
    />
  )
}
