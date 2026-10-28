import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import relay from 'vite-plugin-relay'
import { defineConfig } from 'vite'

const apiTarget = process.env.NRG_API_URL ?? 'https://localhost:7148'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    relay,
    babel({ presets: [reactCompilerPreset()] })
  ],
  server: {
    proxy: {
      '/auth': { target: apiTarget, changeOrigin: true, secure: false },
      '/graphql': { target: apiTarget, changeOrigin: true, secure: false },
    },
  },
})
