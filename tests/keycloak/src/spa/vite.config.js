import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  envPrefix: ['VITE_', 'KEYCLOAK_'],
  test: {
    environment: 'node',
  },
})
