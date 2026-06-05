import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import basicSsl from '@vitejs/plugin-basic-ssl'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const backendTarget = env.VITE_BACKEND_URL || 'http://127.0.0.1:8001'
  const referenceDir = env.VITE_REFERENCE_DIR || '../BI-API/assets'

  return {
    base: './',
    plugins: [
      vue(),
      tailwindcss(),
      basicSsl(),
    ],
    build: {
      rollupOptions: {
        output: {
          entryFileNames: `js/[name].js`,
          chunkFileNames: `js/[name].js`,
          assetFileNames: (assetInfo) => {
            const name = assetInfo?.name || (assetInfo?.names && assetInfo.names[0]) || 'asset.unknown';
            let extType = name.split('.').pop();
            if (/png|jpe?g|svg|gif|tiff|bmp|ico/i.test(extType)) {
              extType = 'img';
            } else if (/woff|woff2|eot|ttf|otf/i.test(extType)) {
              extType = 'fonts';
            } else if (/css/i.test(extType)) {
              extType = 'css';
            } else {
              extType = 'assets';
            }
            return `${extType}/[name].[ext]`;
          }
        }
      }
    },
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
          referenceDir
        ]
      },
      proxy: {
        '/api': {
          target: backendTarget,
          changeOrigin: true,
        }
      }
    }
  }
})