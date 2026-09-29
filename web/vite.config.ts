/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: { port: 5173 }, // must match the API's Cors:AllowedOrigins
  test: {
    environment: 'jsdom', // a fake browser DOM for React Testing Library
    setupFiles: './src/test/setup.ts',
  },
})
