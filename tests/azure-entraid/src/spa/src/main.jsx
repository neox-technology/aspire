import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { EventType, PublicClientApplication } from '@azure/msal-browser'
import App from './App.jsx'
import { msalConfig } from './authConfig.js'

const msalInstance = new PublicClientApplication(msalConfig)

await msalInstance.initialize()
const redirectResult = await msalInstance.handleRedirectPromise()

const accounts = msalInstance.getAllAccounts()
if (redirectResult?.account) {
  msalInstance.setActiveAccount(redirectResult.account)
} else if (accounts.length > 0) {
  msalInstance.setActiveAccount(accounts[0])
}

msalInstance.addEventCallback((event) => {
  if (event.eventType === EventType.LOGIN_SUCCESS && event.payload?.account) {
    msalInstance.setActiveAccount(event.payload.account)
  }
})

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App instance={msalInstance} />
  </StrictMode>,
)
