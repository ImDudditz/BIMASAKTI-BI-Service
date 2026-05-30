import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import basicSsl from '@vitejs/plugin-basic-ssl'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// Read target backend URL dynamically from backend's appsettings.json
let backendTarget = 'http://127.0.0.1:8001'
try {
  const appSettingsPath = path.resolve(__dirname, '../backend/appsettings.json')
  if (fs.existsSync(appSettingsPath)) {
    const settings = JSON.parse(fs.readFileSync(appSettingsPath, 'utf-8'))
    const host = settings.Server?.Host === '0.0.0.0' ? '127.0.0.1' : (settings.Server?.Host || '127.0.0.1')
    const port = settings.Server?.Port || 8001
    backendTarget = `http://${host}:${port}`
    console.log(`[Vite Proxy] Configured target dynamically from appsettings.json: ${backendTarget}`)
  }
} catch (e) {
  console.warn('[Vite Proxy] Failed to read appsettings.json, falling back to default:', e.message)
}

export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    basicSsl(),
  ],
  resolve: {
    alias: {
      '@': '/src'
    }
  },
  server: {
    host: '0.0.0.0',
    port: 8002,
    strictPort: false,
    https: true,
    allowedHosts: true, // Allows any hostname on your local network
    fs: {
      allow: [
        '.',
        '../backend/assets'
      ]
    },
    proxy: {
      '/api': {
        target: backendTarget,
        changeOrigin: true,
      }
    }
  }
})