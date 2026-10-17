<script setup lang="ts">
/**
 * پوستهٔ سراسری رابط کاربری — یک بار در layout مانت می‌شود.
 * صف Toast، دیالوگ تأیید، میزبان پنل‌ها (panels/*.panel.vue)، کشوی راهنما، پالت فرمان و نشانگر صف آفلاین.
 *
 * Toastها در ناحیهٔ aria-live قرار دارند تا صفحه‌خوان هم آن‌ها را اعلام کند.
 * دیالوگ تأیید و پنل هر دو فوکوس را حبس می‌کنند، با Esc بسته می‌شوند و فوکوس را به دکمهٔ بازکننده برمی‌گردانند.
 */
import { defineAsyncComponent, nextTick, onMounted, ref, watch } from 'vue'
import AppIcon from '../components/AppIcon.vue'
import CommandPalette from '../components/CommandPalette.vue'
import OfflineIndicator from '../components/OfflineIndicator.vue'
import { faNumber } from '../lib/format'
import { message } from '../lib/messages'
import { watchConnectivity } from '../lib/offlineQueue'
import {
  closeHelp,
  closePanel,
  confirmState,
  dismissToast,
  drainPendingToast,
  focusableElements,
  helpDrawerLoader,
  helpState,
  panelState,
  requestReload,
  settleConfirm,
  toast,
  toasts,
  trapFocus,
} from '../lib/ui'

onMounted(() => {
  // پیام موفقیتی که قبل از reload صفحه ثبت شده بود را حالا نشان بده
  drainPendingToast()

  // با برگشت اینترنت، کارهای معلق خودکار ارسال می‌شوند. تازه‌سازی پس از ارسال فقط وقتی کاری در جریان
  // نیست (blockReload)، وگرنه نوشتهٔ ذخیره‌نشدهٔ یک جدول یا پنل از دست می‌رفت.
  watchConnectivity((result) => {
    if (result.sent > 0) {
      toast.success(`${faNumber(result.sent)} کار معلق ارسال شد.`)
      requestReload({ delayMs: 1200 })
    }
    for (const rejected of result.rejected) {
      toast.error(`${rejected.label}: ${rejected.message}`)
    }
    // نشست تمام شده یا کس دیگری وارد شده: صف دست‌نخورده مانده، فقط باید دوباره وارد شد
    if (result.signInRequired) toast.warning(message('E_OFFLINE_SIGN_IN', { n: result.remaining }))
  })
})

// ── دیالوگ تأیید ──────────────────────────────────────────────────────

const dialog = ref<HTMLElement | null>(null)
const confirmButton = ref<HTMLButtonElement | null>(null)
/** عنصری که قبل از باز شدن دیالوگ فوکوس داشت — بعد از بستن به آن برمی‌گردیم */
let previouslyFocused: HTMLElement | null = null

const toastStyles: Record<string, { css: string; icon: string }> = {
  success: { css: 'border-success/30 bg-success/10 text-success', icon: 'success' },
  error: { css: 'border-error/30 bg-error-container text-on-error-container', icon: 'error' },
  warning: { css: 'border-warning/30 bg-warning/10 text-warning', icon: 'warning' },
  info: { css: 'border-outline-variant bg-surface-container-lowest text-on-surface', icon: 'info' },
}

watch(
  () => confirmState.open,
  async (open) => {
    if (open) {
      previouslyFocused = document.activeElement as HTMLElement | null
      await nextTick()
      confirmButton.value?.focus()
    } else {
      previouslyFocused?.focus()
      previouslyFocused = null
    }
  },
)

/** ESC می‌بندد؛ Tab داخل دیالوگ حبس می‌شود (focus trap). */
function onConfirmKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    settleConfirm(false)
    return
  }
  if (dialog.value) trapFocus(event, dialog.value)
}

// ── میزبان پنل ────────────────────────────────────────────────────────

const panelFrame = ref<HTMLElement | null>(null)
const panelBody = ref<HTMLElement | null>(null)
/** id عنوان پنل (اولین h1/h2/h3 داخلش) برای aria-labelledby */
const panelTitleId = ref<string | null>(null)
let panelTitleSeq = 0

/** اسکرول صفحهٔ پشت پنل قفل شود تا لمس روی گوشی صفحه را جابه‌جا نکند. */
watch(
  () => panelState.name,
  async (name) => {
    document.documentElement.style.overflow = name ? 'hidden' : ''
    if (!name) {
      panelTitleId.value = null
      return
    }
    await nextTick()
    panelFrame.value?.focus()
  },
)

/** پس از رندر کامپوننت پنل: عنوان دیالوگ و فوکوس اول. */
watch(
  () => (panelState.loading ? null : panelState.component),
  async (component) => {
    if (!component) return
    await nextTick()
    const body = panelBody.value
    if (!body) return

    const heading = body.querySelector<HTMLElement>('h1, h2, h3')
    if (heading) {
      if (!heading.id) heading.id = `panel-title-${++panelTitleSeq}`
      panelTitleId.value = heading.id
    }

    const target =
      body.querySelector<HTMLElement>('[autofocus], [data-autofocus]') ?? focusableElements(body)[0] ?? panelFrame.value
    target?.focus()
  },
)

function onPanelKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    closePanel(undefined)
    return
  }
  if (panelFrame.value) trapFocus(event, panelFrame.value)
}

function onPanelClose(result?: unknown) {
  closePanel(result)
}

// ── کشوی راهنما ───────────────────────────────────────────────────────

/** کامپوننت HelpDrawer اگر در build هست (lazy، فقط با اولین «؟ راهنما» دانلود می‌شود). */
const HelpDrawer = helpDrawerLoader ? defineAsyncComponent(helpDrawerLoader) : null

/** پس از اولین باز شدن مانت می‌ماند تا انیمیشن بستن و برگشت فوکوس خودِ کشو کار کند. */
const helpMounted = ref(false)
watch(
  () => helpState.open,
  (open) => {
    if (open) helpMounted.value = true
  },
)

function onHelpOpenChange(open: boolean) {
  if (!open) closeHelp()
}
</script>

<template>
  <!-- ── صف Toast (روی گوشی بالای نوار پایین، از sm به بالا گوشهٔ بالا) ── -->
  <div
    class="pointer-events-none fixed inset-x-0 bottom-16 z-[80] flex flex-col items-center gap-2 p-4 pb-safe sm:inset-x-auto sm:left-4 sm:top-4 sm:bottom-auto sm:items-start"
    role="status"
    aria-live="polite"
    aria-atomic="false"
    data-no-print
  >
    <TransitionGroup
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="translate-y-2 opacity-0 sm:translate-y-0 sm:-translate-x-2"
      leave-active-class="transition duration-150 ease-in"
      leave-to-class="opacity-0"
    >
      <div
        v-for="t in toasts"
        :key="t.id"
        class="pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl border px-4 py-3 text-sm shadow-lg"
        :class="toastStyles[t.kind].css"
      >
        <AppIcon :name="toastStyles[t.kind].icon" class="mt-0.5 h-5 w-5" />
        <span class="flex-1">{{ t.message }}</span>
        <button
          type="button"
          class="touch-target -m-1 rounded p-1 opacity-70 hover:opacity-100"
          aria-label="بستن پیام"
          @click="dismissToast(t.id)"
        >
          <AppIcon name="close" class="h-4 w-4" />
        </button>
      </div>
    </TransitionGroup>
  </div>

  <!-- ── پنل (کشوی کناری روی دسکتاپ، برگهٔ پایین روی گوشی) ── -->
  <Transition
    enter-active-class="transition duration-200 ease-out"
    enter-from-class="opacity-0"
    leave-active-class="transition duration-150 ease-in"
    leave-to-class="opacity-0"
  >
    <div v-if="panelState.name" class="fixed inset-0 z-[66]" data-no-print @keydown="onPanelKeydown">
      <div class="absolute inset-0 bg-black/50" aria-hidden="true" @click="closePanel(undefined)" />
      <div
        ref="panelFrame"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="panelTitleId ?? undefined"
        :aria-label="panelTitleId ? undefined : 'پنل'"
        :aria-busy="panelState.loading ? 'true' : undefined"
        tabindex="-1"
        :data-panel="panelState.name"
        class="pb-safe absolute inset-x-0 bottom-0 flex max-h-[92dvh] flex-col rounded-t-2xl border border-outline-variant bg-surface-container-lowest text-on-surface shadow-xl outline-none md:inset-y-0 md:left-0 md:right-auto md:max-h-none md:w-[32rem] md:max-w-[92vw] md:rounded-none md:rounded-r-2xl"
      >
        <div class="flex shrink-0 justify-end p-2">
          <button
            type="button"
            class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container"
            aria-label="بستن"
            @click="closePanel(undefined)"
          >
            <AppIcon name="close" />
          </button>
        </div>
        <div ref="panelBody" class="min-h-0 flex-1 overflow-y-auto px-5 pb-5">
          <div v-if="panelState.loading" class="space-y-3" aria-hidden="true">
            <div class="skeleton h-8 w-1/2" />
            <div class="skeleton h-40 w-full" />
          </div>
          <component
            :is="panelState.component"
            v-else-if="panelState.component"
            v-bind="panelState.props"
            @close="onPanelClose"
          />
        </div>
      </div>
    </div>
  </Transition>

  <!-- ── دیالوگ تأیید (بالاتر از پنل، چون پنل‌ها هم تأیید می‌خواهند) ── -->
  <Transition
    enter-active-class="transition duration-150 ease-out"
    enter-from-class="opacity-0"
    leave-active-class="transition duration-100 ease-in"
    leave-to-class="opacity-0"
  >
    <div
      v-if="confirmState.open"
      class="fixed inset-0 z-[70] grid place-items-center bg-black/50 p-4"
      data-no-print
      @click.self="settleConfirm(false)"
      @keydown="onConfirmKeydown"
    >
      <div
        ref="dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        :aria-describedby="confirmState.message ? 'confirm-message' : undefined"
        class="w-full max-w-md rounded-xl border border-outline-variant bg-surface-container-lowest p-6 shadow-xl"
      >
        <div class="flex items-start gap-3">
          <span
            class="grid h-10 w-10 shrink-0 place-items-center rounded-full"
            :class="confirmState.danger ? 'bg-error-container text-on-error-container' : 'bg-secondary-container text-on-secondary-container'"
          >
            <AppIcon :name="confirmState.danger ? 'warning' : 'info'" />
          </span>
          <div class="min-w-0 flex-1">
            <h2 id="confirm-title" class="font-bold text-on-surface">{{ confirmState.title }}</h2>
            <p v-if="confirmState.message" id="confirm-message" class="mt-1 text-sm text-on-surface-variant">
              {{ confirmState.message }}
            </p>
          </div>
        </div>

        <div class="mt-6 flex flex-wrap justify-end gap-3">
          <button
            type="button"
            class="min-h-11 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
            @click="settleConfirm(false)"
          >
            {{ confirmState.cancelLabel }}
          </button>
          <button
            ref="confirmButton"
            type="button"
            class="min-h-11 rounded-lg px-5 text-sm font-semibold"
            :class="confirmState.danger
              ? 'bg-error text-on-error hover:opacity-90'
              : 'bg-primary text-on-primary hover:bg-primary-hover'"
            @click="settleConfirm(true)"
          >
            {{ confirmState.confirmLabel }}
          </button>
        </div>
      </div>
    </div>
  </Transition>

  <!-- کشوی راهنما («؟ راهنما» در سرصفحه) — کامپوننتش مال کیت UI است -->
  <component
    :is="HelpDrawer"
    v-if="HelpDrawer && helpMounted"
    :open="helpState.open"
    :page="helpState.page || undefined"
    @close="closeHelp"
    @update:open="onHelpOpenChange"
  />

  <!-- پالت فرمان (Ctrl+K) و نشانگر صف آفلاین — هر دو سراسری‌اند -->
  <CommandPalette />
  <OfflineIndicator />
</template>
