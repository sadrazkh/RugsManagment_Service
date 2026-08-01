import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

/**
 * خروجی build داخل wwwroot/dist می‌رود و توسط Razor (کلاس ViteAssets) از manifest خوانده می‌شود.
 * base = /dist/ تا chunkهای code-split شده هم از همان مسیر لود شوند.
 * سرور dev (پورت 5174) فقط برای توسعه با HMR است؛ در تولید استفاده نمی‌شود.
 */
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  base: '/dist/',
  build: {
    manifest: true,
    outDir: fileURLToPath(new URL('../wwwroot/dist', import.meta.url)),
    emptyOutDir: true,
    rollupOptions: {
      input: fileURLToPath(new URL('./src/main.ts', import.meta.url)),
    },
  },
  server: {
    port: 5174,
    strictPort: true,
    origin: 'http://localhost:5174',
  },
})
