<script setup lang="ts">
/**
 * پوستهٔ سراسری رابط کاربری — یک بار در layout مانت می‌شود.
 * صف Toast، دیالوگ تأیید، پالت فرمان و نشانگر صف آفلاین.
 *
 * Toastها در ناحیهٔ aria-live قرار دارند تا صفحه‌خوان هم آن‌ها را اعلام کند.
 */
import { nextTick, onMounted, ref, watch } from 'vue'
import AppIcon from '../components/AppIcon.vue'
import CommandPalette from '../components/CommandPalette.vue'
import OfflineIndicator from '../components/OfflineIndicator.vue'
import { faNumber } from '../lib/format'
import { watchConnectivity } from '../lib/offlineQueue'
import { confirmState, dismissToast, drainPendingToast, settleConfirm, toast, toasts } from '../lib/ui'

onMounted(() => {
  // پیام موفقیتی که قبل از reload صفحه ثبت شده بود را حالا نشان بده
  drainPendingToast()

  // با برگشت اینترنت، کارهای معلق خودکار ارسال می‌شوند
  watchConnectivity((result) => {
    if (result.sent > 0) {
      toast.success(`${faNumber(result.sent)} کار معلق ارسال شد.`)
      window.setTimeout(() => window.location.reload(), 1200)
    }
    for (const rejected of result.rejected) {
      toast.error(`${rejected.label}: ${rejected.message}`)
    }
  })
})

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
function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    settleConfirm(false)
    return
  }

  if (event.key !== 'Tab' || !dialog.value) return

  const focusable = dialog.value.querySelectorAll<HTMLElement>(
    'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
  )
  if (focusable.length === 0) return

  const first = focusable[0]
  const last = focusable[focusable.length - 1]

  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}
</script>

<template>
  <!-- ── صف Toast ── -->
  <div
    class="pointer-events-none fixed inset-x-0 bottom-0 z-[60] flex flex-col items-center gap-2 p-4 pb-safe sm:inset-x-auto sm:left-4 sm:top-4 sm:bottom-auto sm:items-start"
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

  <!-- ── دیالوگ تأیید ── -->
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
      @keydown="onKeydown"
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

        <div class="mt-6 flex justify-end gap-3">
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

  <!-- پالت فرمان (Ctrl+K) و نشانگر صف آفلاین — هر دو سراسری‌اند -->
  <CommandPalette />
  <OfflineIndicator />
</template>
