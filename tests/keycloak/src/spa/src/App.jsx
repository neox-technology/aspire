import { useEffect, useRef, useState } from 'react'
import { fetchMe } from './apiClient.js'
import ConnectionStub from './ConnectionStub.jsx'
import {
  apiConfig,
  createKeycloakInitOptions,
  isApiConfigured,
  isKeycloakConfigured,
  keycloakConfig,
} from './authConfig.js'
import { createKeycloakClient } from './keycloakClient.js'

export default function App() {
  const [ready, setReady] = useState(!isKeycloakConfigured(keycloakConfig))
  const [authenticated, setAuthenticated] = useState(false)
  const [keycloak, setKeycloak] = useState(null)
  const [initError, setInitError] = useState(null)
  const [userLoading, setUserLoading] = useState(false)
  const [userName, setUserName] = useState(null)
  const [userError, setUserError] = useState(null)
  const initStarted = useRef(false)

  useEffect(() => {
    if (initStarted.current || !isKeycloakConfigured(keycloakConfig)) {
      return
    }

    initStarted.current = true
    const client = createKeycloakClient(keycloakConfig)

    client.onAuthSuccess = () => setAuthenticated(true)
    client.onAuthError = (error) => {
      const message = error?.error ?? error?.error_description ?? 'Keycloak auth error'
      setInitError(String(message))
    }
    client.onAuthLogout = () => {
      setAuthenticated(false)
      setUserName(null)
      setUserError(null)
    }
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

  useEffect(() => {
    if (!authenticated || !keycloak || !isApiConfigured(apiConfig)) {
      setUserLoading(false)
      setUserName(null)
      setUserError(null)
      return
    }

    let cancelled = false
    setUserLoading(true)
    setUserError(null)

    keycloak
      .updateToken(30)
      .then(() => fetchMe(apiConfig.baseUrl, keycloak.token))
      .then((profile) => {
        if (!cancelled) {
          setUserName(profile.name ?? null)
        }
      })
      .catch((error) => {
        if (!cancelled) {
          setUserName(null)
          setUserError(error instanceof Error ? error.message : String(error))
        }
      })
      .finally(() => {
        if (!cancelled) {
          setUserLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [authenticated, keycloak])

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
      userLoading={userLoading}
      userName={userName}
      userError={userError}
    />
  )
}
