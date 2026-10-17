/**
 * فقط ردیف‌های دیده‌شده (و چند ردیف پیش و پس) رندر می‌شوند تا جدول ۳۰۰ ردیفی روی تبلت روان بماند (G.1). ارتفاع
 * ردیف ثابت است (۲٫۷۵rem، با لمس ۳rem) و یک بار از خودِ ردیف اندازه گرفته می‌شود؛ پس پهنا و اندازهٔ نوشته هر چه باشد
 * جای هر ردیف `index × rowHeight` است. ردیف فعال همیشه رندر می‌ماند تا فوکوسش با اسکرول گم نشود.
 *
 * بی `@vueuse/core`: یک گوش‌دهندهٔ scroll و یک ResizeObserver.
 */
import { computed, onBeforeUnmount, onMounted, ref, watch, type Ref } from 'vue'

export interface VisibleWindow {
  /** اولین ردیف رندرشده (شامل) */
  start: number
  /** بعد از آخرین ردیف رندرشده */
  end: number
}

/**
 * پنجرهٔ ردیف‌های دیده‌شده. `offset` فاصلهٔ اولین ردیف از بالای ظرف اسکرول است (ارتفاع سرستون چسبان).
 */
export function visibleWindow(
  scrollTop: number,
  viewportHeight: number,
  rowHeight: number,
  count: number,
  overscan = 6,
  offset = 0,
): VisibleWindow {
  if (count <= 0 || rowHeight <= 0) return { start: 0, end: 0 }
  const top = Math.max(0, scrollTop - offset)
  const first = Math.floor(top / rowHeight)
  const visible = Math.ceil(Math.max(viewportHeight, rowHeight) / rowHeight) + 1
  const start = Math.max(0, first - overscan)
  const end = Math.min(count, first + visible + overscan)
  return { start: Math.min(start, count), end: Math.max(end, Math.min(count, start)) }
}

/**
 * اندیس ردیف‌هایی که رندر می‌شوند: پنجره، به‌اضافهٔ ردیف فعال اگر بیرون از آن است — همیشه به ترتیب. ردیف فعالِ بالای
 * پنجره اول فهرست می‌آید و ردیف پایین آخرش؛ چون اگر (مثل پیش‌تر) همیشه ته فهرست می‌آمد، وقتی پنجره به آن می‌رسید Vue
 * گرهٔ همان ردیف را از ته به وسط جابه‌جا می‌کرد و جابه‌جا شدن گرهٔ فوکوس‌دار فوکوس را به body می‌انداخت (PageUp به ردیفی
 * درست بالای پنجره، پیش از اسکرول خودکار).
 */
export function renderedIndices(window_: VisibleWindow, pinned: number | null, count: number): number[] {
  const { start, end } = window_
  const list: number[] = []
  for (let i = start; i < end; i++) list.push(i)
  if (pinned === null || pinned < 0 || pinned >= count || (pinned >= start && pinned < end)) return list
  if (pinned < start) list.unshift(pinned)
  else list.push(pinned)
  return list
}

/** scrollTop تازه تا ردیف `index` کامل دیده شود (یا همان قبلی اگر دیده می‌شود). */
export function scrollToRow(
  index: number,
  scrollTop: number,
  viewportHeight: number,
  rowHeight: number,
  offset = 0,
  bottomInset = 0,
): number {
  const rowTop = offset + index * rowHeight
  const rowBottom = rowTop + rowHeight
  const visibleTop = scrollTop + offset
  const visibleBottom = scrollTop + viewportHeight - bottomInset
  if (rowTop < visibleTop) return Math.max(0, rowTop - offset)
  if (rowBottom > visibleBottom) return Math.max(0, rowBottom - viewportHeight + bottomInset)
  return scrollTop
}

export interface VirtualRowsOptions {
  /** ظرف اسکرول */
  container: Ref<HTMLElement | null>
  count: Ref<number>
  /** ردیفی که باید همیشه رندر شود (فعال) */
  pinned: Ref<number | null>
  /** ارتفاع سرستون چسبان */
  headerHeight: Ref<number>
  overscan?: number
}

export function useVirtualRows(options: VirtualRowsOptions) {
  const scrollTop = ref(0)
  const viewport = ref(600)
  /** ارتفاع ردیف به پیکسل؛ پیش‌فرض ۴۴ تا اندازه‌گیری برسد */
  const rowHeight = ref(44)

  const window_ = computed(() =>
    visibleWindow(scrollTop.value, viewport.value, rowHeight.value, options.count.value, options.overscan ?? 6, options.headerHeight.value),
  )

  /** اندیس ردیف‌هایی که رندر می‌شوند (پنجره + ردیف فعال، به ترتیب). */
  const indices = computed(() => renderedIndices(window_.value, options.pinned.value, options.count.value))

  const totalHeight = computed(() => options.count.value * rowHeight.value)

  function onScroll() {
    const el = options.container.value
    if (el) scrollTop.value = el.scrollTop
  }

  let observer: ResizeObserver | null = null
  let attached: HTMLElement | null = null

  function measure() {
    const el = options.container.value
    if (!el) return
    viewport.value = el.clientHeight
    const probe = el.querySelector<HTMLElement>('[data-grid-row]')
    if (probe && probe.offsetHeight > 0) rowHeight.value = probe.offsetHeight
  }

  function detach() {
    attached?.removeEventListener('scroll', onScroll)
    observer?.disconnect()
    observer = null
    attached = null
  }

  /**
   * ظرف اسکرول شاید بعد از mount بیاید یا عوض شود (گوشی در نمای کارت باز شد و بعد پهن شد): هر بار به ظرف تازه وصل
   * شو، وگرنه پنجرهٔ ردیف‌ها با اسکرول جلو نمی‌رود.
   */
  function attach(el: HTMLElement | null) {
    if (el === attached) return
    detach()
    if (!el) return
    attached = el
    el.addEventListener('scroll', onScroll, { passive: true })
    scrollTop.value = el.scrollTop
    measure()
    if (typeof ResizeObserver !== 'undefined') {
      observer = new ResizeObserver(() => measure())
      observer.observe(el)
    }
  }

  onMounted(() => {
    attach(options.container.value)
    watch(options.container, (el) => attach(el), { flush: 'post' })
  })

  onBeforeUnmount(detach)

  // اندازهٔ نوشته یا تعداد ردیف عوض شد: ارتفاع را دوباره بگیر
  watch(options.count, () => requestAnimationFrame(measure))

  /** ردیف را به دید بیاور. */
  function reveal(index: number, bottomInset = 0) {
    const el = options.container.value
    if (!el) return
    const next = scrollToRow(index, el.scrollTop, el.clientHeight, rowHeight.value, options.headerHeight.value, bottomInset)
    if (next !== el.scrollTop) {
      el.scrollTop = next
      scrollTop.value = next
    }
  }

  return { indices, rowHeight, totalHeight, window: window_, reveal, measure }
}
