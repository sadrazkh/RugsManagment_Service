/**
 * محیط vitest برای آزمون کامپوننت‌های Vue با `miniDom`: همان node، ولی فایل‌ها برای مرورگر (client) ترجمه می‌شوند.
 *
 * چرا: محیط پیش‌فرض node فایل‌های `.vue` را برای SSR ترجمه می‌کند و setup آن‌ها `useSSRContext()` می‌خواهد که بیرون
 * از رندر سرور نیست. این فایل فقط با `// @vitest-environment ./src/testing/clientEnvironment.ts` در بالای یک آزمون
 * به کار می‌رود؛ آزمون‌های منطق خالص `lib/` همان node می‌مانند. سراسری‌های DOM را خودِ آزمون با `domGlobals()` می‌گذارد.
 */
import type { Environment } from 'vitest/runtime'

export default {
  name: 'mini-dom',
  viteEnvironment: 'client',
  setup() {
    return { teardown() {} }
  },
} satisfies Environment
