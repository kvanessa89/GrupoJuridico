import { defineConfig, loadEnv, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'

/** Identifica cada build: la aplicación la compara con /version.json para saber si hay una versión nueva. */
const VERSION_APP = Date.now().toString(36)

/** Publica dist/version.json con la versión del build. */
function versionApp(): Plugin {
  return {
    name: 'version-app',
    apply: 'build',
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: 'version.json', source: JSON.stringify({ version: VERSION_APP }) })
    },
  }
}

// En desarrollo, /api se envía al backend ASP.NET Core (puerto 5015 por defecto).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [react(), versionApp()],
    // En desarrollo no hay version.json: el aviso de versión nueva queda apagado.
    define: { __VERSION_APP__: JSON.stringify(mode === 'development' ? '' : VERSION_APP) },
    server: {
      port: 5173,
      proxy: {
        '/api': { target: env.VITE_PROXY_API ?? 'http://localhost:5015', changeOrigin: true },
      },
    },
  }
})
