import ConnectionStub from './ConnectionStub.jsx'
import { keycloakConfig } from './authConfig.js'

export default function App() {
  return <ConnectionStub config={keycloakConfig} />
}
