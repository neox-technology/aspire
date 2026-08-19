import { InteractionType } from '@azure/msal-browser'
import { MsalAuthenticationTemplate, MsalProvider } from '@azure/msal-react'
import { loginRequest } from './authConfig.js'
import ConnectionStub from './ConnectionStub.jsx'

function SignInLoading() {
  return (
    <main>
      <h1>Entra connection</h1>
      <p>Redirecting to Entra…</p>
    </main>
  )
}

function SignInError({ error }) {
  return (
    <main>
      <h1>Entra connection</h1>
      <p>{error?.errorMessage ?? error?.message ?? 'Sign-in failed'}</p>
    </main>
  )
}

export default function App({ instance }) {
  return (
    <MsalProvider instance={instance}>
      <MsalAuthenticationTemplate
        interactionType={InteractionType.Redirect}
        authenticationRequest={loginRequest}
        loadingComponent={SignInLoading}
        errorComponent={SignInError}
      >
        <ConnectionStub />
      </MsalAuthenticationTemplate>
    </MsalProvider>
  )
}
