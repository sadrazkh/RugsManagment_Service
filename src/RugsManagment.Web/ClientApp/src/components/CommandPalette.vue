<script setup lang="ts">
/**
 * پالت فرمان — با Ctrl+K (یا ⌘K) باز می‌شود.
 *
 * برای اپراتوری که تمام روز با این سامانه کار می‌کند، رسیدن به یک فرش از راه
 * منو → فهرست → جستجو → کلیک کند است. اینجا با تایپ کد یا نام، مستقیم می‌رود.
 *
 * دو نوع نتیجه: فرمان‌ها (صفحه‌ها و کنش‌هایی که بیننده اجازه دارد — `commands` در app-context، ساختهٔ
 * NavBuilder روی سرور) و فرش‌های واقعی از سرور.
 *
 * میان‌برهای تک‌حرفی («n» فرش تازه، «/» جست‌وجوی صفحه) هنگام تایپ، داخل هر بخشی با
 * `data-hotkeys-off` (جدول‌ها) و وقتی یک دیالوگ یا پنل باز است کار نمی‌کنند تا نوشتهٔ ذخیره‌نشده با یک
 * حرف از دست نرود.
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import AppIcon from './AppIcon.vue'
import { api } from '@/lib/api'
import { appContext, isSimpleUi } from '@/lib/context'
import { faNumber } from '@/lib/format'
import { trapFocus } from '@/lib/ui'

interface Command {
  id: string
  label: string
  hint?: string
  icon: string
  url: string
  /** کلمات کمکی برای تطبیق جستجو (مثلاً معادل انگلیسی) */
  keywords?: string
}

interface RugHit {
  id: string
  sku: string
  title?: string
  currentStepNameFa?: string
}

const open = ref(false)
const query = ref('')
const rugs = ref<RugHit[]>([])
const searching = ref(false)
const activeIndex = ref(0)
const input = ref<HTMLInputElement | null>(null)
const listRef = ref<HTMLElement | null>(null)
const dialogRef = ref<HTMLElement | null>(null)

/** حالت ساده مرحلهٔ کارگاهِ فرش را نشان نمی‌دهد (A.3)؛ فقط عنوان و کد کوچک فرش. */
const simple = computed(() => isSimpleUi())

/** عنصری که قبل از باز شدن فوکوس داشت — بعد از بستن به آن برمی‌گردیم. */
let previouslyFocused: HTMLElement | null = null
let searchTimer: number | undefined

/**
 * فرمان‌ها از app-context می‌آیند (سرور فقط صفحه‌هایی را می‌فرستد که بیننده اجازهٔ باز کردنشان را دارد)،
 * نه از خراشیدن پیوندهای نوار کناری — پس صفحه‌های «بیشتر» و تنظیمات هم در Ctrl+K پیدا می‌شوند.
 */
const allCommands = computed<Command[]>(() =>
  appContext().commands.map((c, index) => ({
    id: `${index}:${c.href}`,
    label: c.label,
    hint: 'رفتن به',
    icon: c.icon,
    url: c.href,
  })),
)

const filteredCommands = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return allCommands.value.slice(0, 6)
  return allCommands.value.filter(
    (c) => c.label.toLowerCase().includes(q) || (c.keywords ?? '').toLowerCase().includes(q),
  )
})

/** فهرست تخت برای پیمایش با کیبورد — دستورها بالا، فرش‌ها پایین. */
const flatResults = computed(() => [
  ...filteredCommands.value.map((c) => ({ type: 'command' as const, item: c })),
  ...rugs.value.map((r) => ({ type: 'rug' as const, item: r })),
])

watch(query, (q) => {
  activeIndex.value = 0
  window.clearTimeout(searchTimer)

  if (q.trim().length < 2) {
    rugs.value = []
    return
  }

  // تأخیر تا با هر حرف یک درخواست نرود
  searchTimer = window.setTimeout(async () => {
    searching.value = true
    try {
      const res = await api.get<{ items: RugHit[] }>(
        `/api/rugs?search=${encodeURIComponent(q.trim())}&pageSize=6`,
      )
      rugs.value = res.items
    } catch {
      rugs.value = []
    } finally {
      searching.value = false
    }
  }, 250)
})

async function show() {
  previouslyFocused = document.activeElement as HTMLElement | null
  open.value = true
  query.value = ''
  rugs.value = []
  activeIndex.value = 0
  await nextTick()
  input.value?.focus()
}

function hide() {
  open.value = false
  previouslyFocused?.focus()
  previouslyFocused = null
}

function go(index: number) {
  const hit = flatResults.value[index]
  if (!hit) return

  window.location.href =
    hit.type === 'command' ? hit.item.url : `/Rugs/Details/${hit.item.id}`
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    hide()
    return
  }

  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    const max = flatResults.value.length - 1
    if (max < 0) return
    activeIndex.value =
      event.key === 'ArrowDown'
        ? (activeIndex.value + 1) % (max + 1)
        : (activeIndex.value - 1 + max + 1) % (max + 1)

    nextTick(() => {
      listRef.value?.querySelector('[data-active="true"]')?.scrollIntoView({ block: 'nearest' })
    })
    return
  }

  if (event.key === 'Enter') {
    event.preventDefault()
    go(activeIndex.value)
    return
  }

  // Tab داخل پالت بماند (دیالوگ مودال است؛ فوکوس نباید پشت پوشش تیره گم شود)
  if (dialogRef.value) trapFocus(event, dialogRef.value)
}

/**
 * آیا یک دیالوگ مودالِ *دیده‌شدنی* باز است. برگه‌های انتخابِ خانه (data-sheet) از پیش با aria-modal در صفحه
 * هستند ولی hidden؛ آن‌ها نباید میان‌برها را خاموش کنند. getClientRects برای عنصر fixed هم درست جواب می‌دهد
 * (offsetParent عنصر fixed همیشه null است).
 */
function modalOpen(): boolean {
  return [...document.querySelectorAll<HTMLElement>('[aria-modal="true"]')].some(
    (el) => el.getClientRects().length > 0,
  )
}

/** نشانی «فرش تازه» (صفحهٔ ورود فرش یا فرم تک‌فرش) — layout روی body می‌گذارد. */
function newRugHref(): string {
  return document.body.dataset.newRugHref || '/Rugs/Create'
}

/**
 * میان‌برهای سراسری. در فیلدهای ورودی، داخل [data-hotkeys-off] (جدول‌ها) و زیر هر دیالوگ/پنل باز
 * غیرفعال‌اند تا تایپ و کار ذخیره‌نشده را نشکنند.
 */
function onGlobalKey(event: KeyboardEvent) {
  const target = event.target as HTMLElement | null
  const typing =
    target?.tagName === 'INPUT' ||
    target?.tagName === 'TEXTAREA' ||
    target?.tagName === 'SELECT' ||
    target?.isContentEditable === true

  // Ctrl+K / ⌘K حتی هنگام تایپ هم کار می‌کند — قرارداد جاافتادهٔ پالت فرمان
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    open.value ? hide() : void show()
    return
  }

  if (typing || open.value || event.defaultPrevented) return
  if (target?.closest('[data-hotkeys-off]')) return
  if (modalOpen()) return

  // «/» فوکوس روی جستجوی صفحه
  if (event.key === '/') {
    const search = document.querySelector<HTMLInputElement>('#rug-search')
    if (search) {
      event.preventDefault()
      search.focus()
      search.select()
    }
    return
  }

  // «n» فرش تازه — با صفحه‌کلید فارسی همان کلید «د» می‌نویسد، پس جای کلید (KeyN) هم پذیرفته است
  const isN = event.key === 'n' || (event.code === 'KeyN' && !event.shiftKey)
  if (isN && !event.ctrlKey && !event.metaKey && !event.altKey) {
    event.preventDefault()
    window.location.href = newRugHref()
  }
}

onMounted(() => {
  document.addEventListener('keydown', onGlobalKey)
  // دکمهٔ «اسکن/جستجو» در هدر هم می‌تواند پالت را باز کند
  document.querySelectorAll('[data-open-palette]').forEach((el) =>
    el.addEventListener('click', (e) => {
      e.preventDefault()
      void show()
    }),
  )
})

onUnmounted(() => document.removeEventListener('keydown', onGlobalKey))

defineExpose({ show })
</script>

<template>
  <Transition
    enter-active-class="transition duration-150 ease-out"
    enter-from-class="opacity-0"
    leave-active-class="transition duration-100 ease-in"
    leave-to-class="opacity-0"
  >
    <div
      v-if="open"
      class="fixed inset-0 z-[75] flex items-start justify-center bg-black/50 p-4 pt-[10vh]"
      data-no-print
      @click.self="hide"
      @keydown="onKeydown"
    >
      <div
        ref="dialogRef"
        class="w-full max-w-lg overflow-hidden rounded-xl border border-outline-variant bg-surface-container-lowest shadow-xl"
        role="dialog"
        aria-modal="true"
        aria-label="پالت فرمان"
      >
        <div class="flex items-center gap-2 border-b border-outline-variant px-4">
          <AppIcon name="search" class="h-5 w-5 shrink-0 text-on-surface-variant" />
          <input
            ref="input"
            v-model="query"
            type="text"
            class="min-h-12 w-full bg-transparent text-sm outline-none"
            placeholder="جستجوی فرش یا رفتن به بخش…"
            aria-label="جستجو"
          />
          <kbd class="hidden shrink-0 rounded border border-outline-variant px-1.5 py-0.5 text-xs text-on-surface-variant sm:block" dir="ltr">Esc</kbd>
        </div>

        <div ref="listRef" class="max-h-80 overflow-y-auto p-2">
          <p v-if="flatResults.length === 0 && !searching" class="px-3 py-6 text-center text-sm text-on-surface-variant">
            چیزی پیدا نشد.
          </p>

          <template v-else>
            <!-- دستورها -->
            <template v-if="filteredCommands.length">
              <p class="px-3 py-1 text-xs text-on-surface-variant">دستورها</p>
              <button
                v-for="(c, i) in filteredCommands"
                :key="c.id"
                type="button"
                :data-active="activeIndex === i"
                class="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-start text-sm"
                :class="activeIndex === i ? 'bg-primary/10 text-primary' : 'hover:bg-surface-container'"
                @click="go(i)"
                @mouseenter="activeIndex = i"
              >
                <AppIcon :name="c.icon" class="h-4 w-4 shrink-0" />
                <span class="flex-1 truncate">{{ c.label }}</span>
                <span v-if="c.hint" class="shrink-0 text-xs text-on-surface-variant">{{ c.hint }}</span>
              </button>
            </template>

            <!-- فرش‌ها -->
            <template v-if="rugs.length">
              <p class="px-3 pb-1 pt-3 text-xs text-on-surface-variant" data-numeric>
                فرش‌ها ({{ faNumber(rugs.length) }})
              </p>
              <button
                v-for="(r, i) in rugs"
                :key="r.id"
                type="button"
                :data-active="activeIndex === filteredCommands.length + i"
                class="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-start text-sm"
                :class="activeIndex === filteredCommands.length + i ? 'bg-primary/10 text-primary' : 'hover:bg-surface-container'"
                @click="go(filteredCommands.length + i)"
                @mouseenter="activeIndex = filteredCommands.length + i"
              >
                <AppIcon name="rug" class="h-4 w-4 shrink-0" />
                <span class="min-w-0 flex-1">
                  <span class="block truncate">{{ r.title || 'بدون عنوان' }}</span>
                  <span class="block truncate font-mono text-xs text-on-surface-variant" dir="ltr">{{ r.sku }}</span>
                </span>
                <span v-if="r.currentStepNameFa && !simple" class="shrink-0 text-xs text-on-surface-variant">
                  {{ r.currentStepNameFa }}
                </span>
              </button>
            </template>
          </template>
        </div>

        <div class="flex flex-wrap items-center gap-3 border-t border-outline-variant px-4 py-2 text-xs text-on-surface-variant">
          <span><kbd class="rounded border border-outline-variant px-1">↑↓</kbd> جابه‌جایی</span>
          <span><kbd class="rounded border border-outline-variant px-1">Enter</kbd> رفتن</span>
          <span><kbd class="rounded border border-outline-variant px-1">/</kbd> جستجوی صفحه</span>
          <span><kbd class="rounded border border-outline-variant px-1">n</kbd> فرش تازه</span>
        </div>
      </div>
    </div>
  </Transition>
</template>
