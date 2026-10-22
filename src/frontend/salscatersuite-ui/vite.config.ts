import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

/**
 * Target of the dev proxy for `/api`.
 *
 * The browser always calls the API on its own origin (`/api/...`), so the request never
 * triggers a CORS pre-flight and the API's `UseHttpsRedirection` middleware is bypassed
 * (the proxy forwards the request over plain HTTP to the Kestrel HTTP listener).
 *
 * Override per machine with:  VITE_API_PROXY_TARGET=http://localhost:7087 npm run dev
 */
const apiProxyTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5149'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiProxyTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  preview: {
    port: 4173,
    proxy: {
      '/api': {
        target: apiProxyTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
