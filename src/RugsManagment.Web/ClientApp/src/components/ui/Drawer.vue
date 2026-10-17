<script setup lang="ts">
/**
 * کشوی کناری (یا برگهٔ پایین روی گوشی) — برای راهنما و پنل‌هایی که صفحهٔ زیرین باید دیده بماند.
 *
 * همان قرارداد Modal: `role="dialog"`، حبس فوکوس، Esc، برگشت فوکوس. در راست‌به‌چپ «end» یعنی سمت چپ
 * صفحه (کلاس‌های منطقی `start/end`)، پس چیدمان با جهت صفحه درست می‌ماند.
 *
 * لایهٔ z (۶۷): بالای میزبان پنل UiShell (۶۶)، تا «؟ راهنما» روی پنل باز هم دیده شود؛ زیر Modal (۶۸) و
 * دیالوگ تأیید (۷۰).
 */
import { computed, ref, toRef, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { useFocusTrap } from './useFocusTrap'

const props = withDefaults(
  defineProps<{
    open: boolean
    title: string
    /** سمت باز شدن: end (چپ در فارسی)، start، یا bottom (برگهٔ پایین) */
    side?: 'start' | 'end' | 'bottom'
    /** پهنا برای کشوی کناری */
    size?: 'sm' | 'md' | 'lg'
  }>(),
  { side: 'end', size: 'md' },
)

const emit = defineEmits<{ 'update:open': [value: boolean]; close: [] }>()

const panel = ref<HTMLElement | null>(null)
const titleId = `${useId()}-title`

function close() {
  emit('update:open', false)
  emit('close')
}

const { onKeydown } = useFocusTrap(panel, toRef(props, 'open'), { onEscape: close })

const placement = computed(() => {
  const width = { sm: 'sm:max-w-sm', md: 'sm:max-w-md', lg: 'sm:max-w-xl' }[props.size]
  if (props.side === 'bottom') return 'inset-x-0 bottom-0 max-h-[85vh] rounded-t-2xl border-t'
  return props.side === 'start'
    ? `inset-y-0 start-0 w-full ${width} border-e`
    : `inset-y-0 end-0 w-full ${width} border-s`
})

const enterFrom = computed(() =>
  props.side === 'bottom' ? 'translate-y-full' : props.side === 'start' ? 'translate-x-full' : '-translate-x-full',
)

defineExpose({ close })
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200"
      enter-from-class="opacity-0"
      leave-active-class="transition-opacity duration-150"
      leave-to-class="opacity-0"
    >
      <div v-if="open" class="fixed inset-0 z-[67] bg-black/40" data-no-print @mousedown.self="close"></div>
    </Transition>
    <Transition
      enter-active-class="transition-transform duration-200 ease-out"
      :enter-from-class="enterFrom"
      leave-active-class="transition-transform duration-150 ease-in"
      :leave-to-class="enterFrom"
    >
      <div
        v-if="open"
        ref="panel"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        tabindex="-1"
        class="fixed z-[67] flex flex-col overflow-hidden border-outline-variant bg-surface-container-lowest text-on-surface shadow-xl outline-none"
        :class="placement"
        data-no-print
        @keydown="onKeydown"
      >
        <header class="flex items-center gap-3 border-b border-outline-variant px-5 py-3">
          <h2 :id="titleId" class="min-w-0 flex-1 truncate text-lg font-bold">{{ title }}</h2>
          <button
            type="button"
            class="-me-2 grid h-11 w-11 shrink-0 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
            @click="close"
          >
            <AppIcon name="close" class="h-5 w-5" label="بستن" />
          </button>
        </header>
        <div class="min-h-0 flex-1 overflow-y-auto px-5 py-4 pb-safe">
          <slot :close="close" />
        </div>
        <footer v-if="$slots.footer" class="flex flex-wrap justify-end gap-3 border-t border-outline-variant px-5 py-3 pb-safe">
          <slot name="footer" :close="close" />
        </footer>
      </div>
    </Transition>
  </Teleport>
</template>
