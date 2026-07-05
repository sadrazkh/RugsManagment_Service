/**
 * بارگذارِ جزیره‌ها (Islands loader).
 *
 * الگو: در هر view رِیزور یک عنصر با data-island می‌گذاری و در صورت نیاز props را
 * به‌صورت JSON در data-props می‌دهی. این فایل همه‌ی آن عناصر را پیدا کرده،
 * کامپوننت Vue متناظر را (به‌صورت lazy/code-split) mount می‌کند.
 *
 *   <div data-island="hello" data-props='{"name":"دنیا"}'></div>
 *
 * درست مثل استفاده از jQuery: بک‌اند HTML را می‌سازد، Vue فقط بخش‌های تعاملی را زنده می‌کند.
 */
import './styles/app.css'
import { createApp, type Component } from 'vue'
import { islands } from './islands/registry'

function parseProps(el: HTMLElement): Record<string, unknown> {
  const raw = el.dataset.props
  if (!raw) return {}
  try {
    return JSON.parse(raw) as Record<string, unknown>
  } catch (err) {
    console.error('[island] props نامعتبر روی', el, err)
    return {}
  }
}

async function mountIsland(el: HTMLElement): Promise<void> {
  const name = el.dataset.island
  if (!name) return

  const loader = islands[name]
  if (!loader) {
    console.warn(`[island] جزیرهٔ ثبت‌نشده: "${name}"`)
    return
  }

  try {
    const mod = await loader()
    const component = mod.default as Component
    createApp(component, parseProps(el)).mount(el)
    el.setAttribute('data-island-mounted', 'true')
  } catch (err) {
    console.error(`[island] خطا در بارگذاری "${name}"`, err)
  }
}

function bootstrap(): void {
  document
    .querySelectorAll<HTMLElement>('[data-island]:not([data-island-mounted])')
    .forEach((el) => void mountIsland(el))
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrap)
} else {
  bootstrap()
}
