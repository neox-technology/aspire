import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

function resolveApiBaseUrl() {
  if (process.env.API_BASE_URL) {
    return process.env.API_BASE_URL
  }

  const serviceDiscoveryKey = Object.keys(process.env).find(
    (key) => key.startsWith('services__') && key.endsWith('__http__0') && key.includes('-api'),
  )

  return serviceDiscoveryKey ? process.env[serviceDiscoveryKey] : ''
}

const apiBaseUrl = resolveApiBaseUrl()

export default defineConfig({
  plugins: [react()],
  envPrefix: ['VITE_', 'KEYCLOAK_', 'API_'],
  define: {
    'import.meta.env.API_BASE_URL': JSON.stringify(apiBaseUrl),
  },
  test: {
    environment: 'node',
  },
})
