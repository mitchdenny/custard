import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  server: {
    proxy: {
      // Proxy API calls to the app service
      '/api': {
        target: process.env.WEBSITE_HTTPS || process.env.WEBSITE_HTTP,
        changeOrigin: true
      }
    }
  }
})
