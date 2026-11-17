import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import vueDevTools from 'vite-plugin-vue-devtools'

// Seller centre is served at "/seller" (repo-structure §8). In development the gateway proxies to this server,
// so the browser sees one origin (http://localhost:5080). PORT comes from the AppHost.
export default defineConfig({
  base: '/seller/',
  plugins: [vue(), tailwindcss(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5174,
    strictPort: true,
    allowedHosts: true,
  },
})
