import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  envPrefix: ['VITE_', 'ENTRA_'],
  build: {
    target: 'es2022',
  },
  test: {
    environment: 'node',
  },
})
