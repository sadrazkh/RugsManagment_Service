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
import { exposeGlobally } from './lib/ui'

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

/**
 * تا وقتی chunk جزیره دانلود شود یک اسکلت نشان می‌دهیم تا محتوا ناگهانی نپرد
 * (جلوگیری از پرش چیدمان و «ناحیهٔ خالی» موقت).
 * ارتفاع از data-skeleton خوانده می‌شود؛ با مقدار "off" غیرفعال است.
 */
function showSkeleton(el: HTMLElement): void {
  if (el.dataset.skeleton === 'off' || el.children.length > 0) return

  const placeholder = document.createElement('div')
  placeholder.className = 'skeleton w-full'
  placeholder.style.height = el.dataset.skeleton || '6rem'
  placeholder.setAttribute('aria-hidden', 'true')
  placeholder.dataset.islandSkeleton = 'true'
  el.appendChild(placeholder)
}

function clearSkeleton(el: HTMLElement): void {
  el.querySelector('[data-island-skeleton]')?.remove()
}

async function mountIsland(el: HTMLElement): Promise<void> {
  const name = el.dataset.island
  if (!name) return

  const loader = islands[name]
  if (!loader) {
    console.warn(`[island] جزیرهٔ ثبت‌نشده: "${name}"`)
    return
  }

  showSkeleton(el)

  try {
    const mod = await loader()
    const component = mod.default as Component
    clearSkeleton(el)
    createApp(component, parseProps(el)).mount(el)
    el.setAttribute('data-island-mounted', 'true')
  } catch (err) {
    clearSkeleton(el)
    console.error(`[island] خطا در بارگذاری "${name}"`, err)
    el.innerHTML =
      '<p class="rounded-lg border border-error/30 bg-error-container px-4 py-3 text-sm text-on-error-container">' +
      'بارگذاری این بخش ناموفق بود. صفحه را تازه کنید.</p>'
  }
}

/**
 * پوستهٔ UI (Toast + دیالوگ تأیید) در همهٔ صفحه‌ها لازم است، پس نقطهٔ اتصالش را
 * خودکار به انتهای body اضافه می‌کنیم — بدون نیاز به تغییر تک‌تک viewها.
 */
function mountUiShell(): void {
  const host = document.createElement('div')
  host.dataset.islandMounted = 'true'
  document.body.appendChild(host)

  void import('./islands/UiShell.vue').then((mod) => {
    createApp(mod.default as Component).mount(host)
    exposeGlobally()
  })
}

function bootstrap(): void {
  mountUiShell()
  document
    .querySelectorAll<HTMLElement>('[data-island]:not([data-island-mounted])')
    .forEach((el) => void mountIsland(el))
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrap)
} else {
  bootstrap()
}
