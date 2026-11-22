import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import vueDevTools from 'vite-plugin-vue-devtools'

// Seller centre is served on its own host at "/" (ADR-0027). In development the gateway proxies to this server by host,
// so the browser sees http://seller.localhost:5080. PORT comes from the AppHost.
export default defineConfig({
  base: '/',
  plugins: [vue(), tailwindcss(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5181,
    strictPort: true,
    allowedHosts: true,
  },
})
