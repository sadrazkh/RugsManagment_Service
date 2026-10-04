<script setup lang="ts">
/**
 * دیالوگ میانهٔ صفحه — جای `prompt()` و مودال‌های بی‌نقشِ جزیره‌ها (H.7).
 *
 * `role="dialog"` و `aria-modal`، عنوان به‌عنوان نام دسترس‌پذیر، حبس فوکوس، Esc و کلیک بیرون برای
 * بستن، و برگشت فوکوس به دکمه‌ای که آن را باز کرد. به body منتقل می‌شود (Teleport) تا زیر نوار
 * چسبان یا کارت والد نماند. لایهٔ z (۶۸): بالای میزبان پنل UiShell (۶۶) و کشو (۶۷)، تا دیالوگی که از
 * درون یک پنل باز می‌شود پشتش نماند؛ زیر دیالوگ تأیید (۷۰)، پالت فرمان (۷۵) و Toast (۸۰)، تا تأییدی که از
 * درون همین دیالوگ باز می‌شود رویش بیاید.
 *
 *   <Modal v-model:open="open" title="ورودی تازه">
 *     …
 *     <template #footer="{ close }"><Button @click="close">انصراف</Button></template>
 *   </Modal>
 */
import { computed, ref, toRef, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { focusableIn, useFocusTrap } from './useFocusTrap'

const props = withDefaults(
  defineProps<{
    open: boolean
    title: string
    /** توضیح زیر عنوان (به‌عنوان aria-describedby) */
    description?: string
    /** پهنای بیشینه */
    size?: 'sm' | 'md' | 'lg'
    /** `alertdialog` برای پرسش‌های خطرناک */
    role?: 'dialog' | 'alertdialog'
    /** کلیک روی پس‌زمینه می‌بندد؟ (برای فرم‌های پرکردنی false بهتر است) */
    closeOnBackdrop?: boolean
    /** دکمهٔ «بستن» گوشهٔ بالا */
    showClose?: boolean
    /** گزینشگر CSS عنصری که هنگام باز شدن فوکوس بگیرد (پیش‌فرض اولین کنترل) */
    initialFocus?: string
  }>(),
  { description: undefined, size: 'md', role: 'dialog', closeOnBackdrop: true, showClose: true, initialFocus: undefined },
)

const emit = defineEmits<{ 'update:open': [value: boolean]; close: [] }>()

const panel = ref<HTMLElement | null>(null)
const uid = useId()
const titleId = `${uid}-title`
const descriptionId = `${uid}-desc`

function close() {
  emit('update:open', false)
  emit('close')
}

const body = ref<HTMLElement | null>(null)

/**
 * فوکوس اول: گزینشگر `initialFocus`، وگرنه `[autofocus]`/`[data-autofocus]` درون دیالوگ، وگرنه اولین کنترل
 * بدنه (تا در دیالوگ فرم، کاربر بی‌درنگ تایپ کند و نه اینکه فوکوس روی «بستن» بنشیند)، وگرنه دکمهٔ بستن.
 */
function firstFocus(): HTMLElement | null {
  const root = panel.value
  if (!root) return null
  if (props.initialFocus) return root.querySelector<HTMLElement>(props.initialFocus)
  return root.querySelector<HTMLElement>('[autofocus], [data-autofocus]') ?? (body.value ? focusableIn(body.value)[0] ?? null : null)
}

const { onKeydown } = useFocusTrap(panel, toRef(props, 'open'), { onEscape: close, initialFocus: firstFocus })

const widthClass = computed(() => ({ sm: 'max-w-sm', md: 'max-w-md', lg: 'max-w-2xl' })[props.size])

function onBackdrop() {
  if (props.closeOnBackdrop) close()
}

defineExpose({ close })
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0"
      leave-active-class="transition duration-100 ease-in"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-[68] grid place-items-center overflow-y-auto bg-black/50 p-4"
        data-no-print
        @mousedown.self="onBackdrop"
      >
        <div
          ref="panel"
          :role="role"
          aria-modal="true"
          :aria-labelledby="titleId"
          :aria-describedby="description ? descriptionId : undefined"
          tabindex="-1"
          class="w-full rounded-xl border border-outline-variant bg-surface-container-lowest p-5 text-on-surface shadow-xl outline-none sm:p-6"
          :class="widthClass"
          @keydown="onKeydown"
        >
          <div class="mb-4 flex items-start gap-3">
            <div class="min-w-0 flex-1">
              <h2 :id="titleId" class="text-lg font-bold">{{ title }}</h2>
              <p v-if="description" :id="descriptionId" class="mt-1 text-sm text-on-surface-variant">{{ description }}</p>
            </div>
            <button
              v-if="showClose"
              type="button"
              class="-m-1 grid h-11 w-11 shrink-0 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
              @click="close"
            >
              <AppIcon name="close" class="h-5 w-5" label="بستن" />
            </button>
          </div>

          <div ref="body">
            <slot :close="close" />
          </div>

          <div v-if="$slots.footer" class="mt-6 flex flex-wrap justify-end gap-3">
            <slot name="footer" :close="close" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
