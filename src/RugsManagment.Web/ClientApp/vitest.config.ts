import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

/**
 * آزمون‌های واحد فرانت (`npm test`). فقط منطق خالص `lib/` آزموده می‌شود (عدد، تاریخ شمسی، پیام‌ها، …)،
 * پس محیط node کافی است و DOM شبیه‌سازی نمی‌شود؛ آزمونی که به localStorage یا navigator نیاز دارد
 * آن‌ها را خودش با `vi.stubGlobal` می‌سازد.
 *
 * جدا از vite.config.ts است تا Tailwind و تنظیم خروجی build در آزمون بار نشوند.
 */
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.spec.ts'],
  },
})
