import { fileURLToPath, URL } from 'node:url'
import { configDefaults, defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

/**
 * آزمون‌های واحد فرانت (`npm test`). منطق خالص `lib/` (عدد، تاریخ شمسی، پیام‌ها، …) در محیط node آزموده می‌شود و DOM
 * شبیه‌سازی نمی‌شود؛ آزمونی که به localStorage یا navigator نیاز دارد آن‌ها را خودش با `vi.stubGlobal` می‌سازد.
 *
 * کامپوننت‌های جعبه‌ابزار (`components/ui/*.spec.ts`) پروژهٔ جدای «ui» دارند: همان node، ولی فایل‌های `.vue` برای
 * مرورگر ترجمه می‌شوند (محیط `src/testing/clientEnvironment.ts`)، چون ترجمهٔ SSR فقط `ssrRender` می‌سازد و setup را
 * به `useSSRContext()` گره می‌زند. DOM آن آزمون‌ها `src/testing/miniDom.ts` است (بی وابستگی تازه).
 *
 * جدا از vite.config.ts است تا Tailwind و تنظیم خروجی build در آزمون بار نشوند.
 */
const uiSpecs = 'src/components/ui/*.spec.ts'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    projects: [
      {
        extends: true,
        test: {
          name: 'unit',
          environment: 'node',
          include: ['src/**/*.spec.ts'],
          exclude: [...configDefaults.exclude, uiSpecs],
        },
      },
      {
        extends: true,
        test: {
          name: 'ui',
          environment: './src/testing/clientEnvironment.ts',
          include: [uiSpecs],
        },
      },
    ],
  },
})
