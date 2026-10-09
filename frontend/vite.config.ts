import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// En desarrollo, /api se envía al backend ASP.NET Core (puerto 5015 por defecto).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/api': { target: env.VITE_PROXY_API ?? 'http://localhost:5015', changeOrigin: true },
      },
    },
  }
})
