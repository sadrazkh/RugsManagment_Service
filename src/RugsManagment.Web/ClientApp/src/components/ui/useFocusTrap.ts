/**
 * حبس فوکوس برای دیالوگ و کشو — همان حبس فوکوس دیالوگ تأیید و میزبان پنل UiShell (`trapFocus` و
 * `focusableElements` در `lib/ui`)، یک‌جا برای Modal، Drawer و HelpDrawer (H.7).
 *
 * - هنگام باز شدن، عنصری که فوکوس داشت به خاطر سپرده و فوکوس به اولین کنترل (یا `initialFocus`) می‌رود.
 * - Tab و Shift+Tab درون ظرف می‌چرخند؛ Esc `onEscape` را صدا می‌زند.
 * - هنگام بسته شدن فوکوس به همان عنصر قبلی برمی‌گردد (اگر هنوز در صفحه باشد).
 * - اسکرول صفحه تا وقتی دیالوگی باز است قفل است (شمارنده‌دار، تا دیالوگ تودرتو قفل را زود باز نکند).
 */
import { nextTick, onBeforeUnmount, watch, type Ref } from 'vue'
import { focusableElements, trapFocus } from '@/lib/ui'

/** کنترل‌های فوکوس‌پذیر و دیدنی ظرف، بی آن‌هایی که از دسترس بیرون گذاشته شده‌اند. */
export function focusableIn(root: HTMLElement): HTMLElement[] {
  return focusableElements(root).filter((el) => !el.closest('[inert]') && el.getAttribute('aria-hidden') !== 'true')
}

let scrollLocks = 0
let savedOverflow = ''

function lockScroll() {
  if (typeof document === 'undefined') return
  if (scrollLocks === 0) {
    savedOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
  }
  scrollLocks++
}

function unlockScroll() {
  if (typeof document === 'undefined' || scrollLocks === 0) return
  scrollLocks--
  if (scrollLocks === 0) document.body.style.overflow = savedOverflow
}

export interface FocusTrapOptions {
  /** وقتی Esc زده شد (معمولاً بستن) */
  onEscape?: () => void
  /** عنصری که هنگام باز شدن فوکوس بگیرد؛ پیش‌فرض اولین کنترل، وگرنه خود ظرف */
  initialFocus?: () => HTMLElement | null | undefined
  /** قفل اسکرول صفحه؛ پیش‌فرض true */
  lockScroll?: boolean
}

/**
 * `container`: ظرف دیالوگ (باید `tabindex="-1"` داشته باشد تا اگر کنترلی نبود خودش فوکوس بگیرد).
 * `active`: باز بودن. `onKeydown` را روی ظرف بگذارید: `@keydown="onKeydown"`.
 */
export function useFocusTrap(container: Ref<HTMLElement | null>, active: Ref<boolean>, options: FocusTrapOptions = {}) {
  let opener: HTMLElement | null = null
  let locked = false

  async function activate() {
    opener = (document.activeElement as HTMLElement | null) ?? null
    if (options.lockScroll !== false) {
      lockScroll()
      locked = true
    }
    await nextTick()
    const root = container.value
    if (!root) return
    const target = options.initialFocus?.() ?? focusableIn(root)[0] ?? root
    target.focus()
  }

  function deactivate() {
    if (locked) {
      unlockScroll()
      locked = false
    }
    const back = opener
    opener = null
    if (back && document.contains(back)) back.focus()
  }

  watch(
    active,
    (open, wasOpen) => {
      if (open && !wasOpen) void activate()
      else if (!open && wasOpen) deactivate()
    },
    { immediate: true },
  )

  onBeforeUnmount(() => {
    if (active.value) deactivate()
  })

  function onKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      if (options.onEscape) {
        event.preventDefault()
        event.stopPropagation()
        options.onEscape()
      }
      return
    }
    const root = container.value
    if (root) trapFocus(event, root)
  }

  return { onKeydown }
}
