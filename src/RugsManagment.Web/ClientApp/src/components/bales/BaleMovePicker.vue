<script setup lang="ts">
/**
 * «انتقال» بی کشیدن (C.3، H.1، H.7 / WCAG 2.2 SC 2.5.7): دکمه‌های ۴۴ پیکسلی شمارهٔ نگله‌ها، «نگلهٔ تازه» و «برگشت به
 * انبار»، و برای جابه‌جایی در همان نگله «بالاتر / پایین‌تر». با دکمهٔ «انتقال» کارت، کلید M روی کارت فوکوس‌دار، یا
 * «انتقال» منوی ردیف جدول باز می‌شود؛ پس هیچ کاری فقط با کشیدن نیست.
 *
 * برای محموله‌ای با ۳۶ نگله، شمارهٔ دیگر را هم می‌شود نوشت («شمارهٔ نگلهٔ دیگر» + Enter). نگلهٔ جاری اول و با نشان
 * «جاری» می‌آید؛ نگلهٔ خودِ فرش غیرفعال و با تیک است. «افزودن به نگله» فهرست فرش‌ها همین را بی «برگشت به انبار» به کار
 * می‌برد.
 */
import { computed, ref, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Modal from '@/components/ui/Modal.vue'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import { parseDecimal } from '@/lib/money'
import type { MoveTarget } from '@/lib/types/bales'
import { faDigitsOf, maxBaleNumber } from './baleModel'

const props = withDefaults(
  defineProps<{
    open: boolean
    /** عنوان («انتقال ۳۷۷۸» یا «افزودن ۵ فرش به…») */
    title: string
    /** توضیح زیر عنوان («الان در نگلهٔ ۲ است.») */
    description?: string
    bales: readonly { number: number; pieces?: number }[]
    /** نگلهٔ جاری (اول می‌آید) */
    current?: number | null
    /** نگلهٔ خودِ فرش (غیرفعال) */
    from?: number | null
    allowStock?: boolean
    /** «بالاتر» / «پایین‌تر» در همان نگله */
    canUp?: boolean
    canDown?: boolean
  }>(),
  { description: undefined, current: null, from: null, allowStock: true, canUp: false, canDown: false },
)

const emit = defineEmits<{ 'update:open': [value: boolean]; pick: [target: MoveTarget] }>()

const terms = appContext().terms
const uid = useId()
const other = ref('')
const otherError = ref('')

const fresh = computed(() => Math.max(maxBaleNumber(props.bales) + 1, 1))

/** نگلهٔ جاری اول، بعد بقیه به ترتیب شماره. */
const ordered = computed(() => {
  const list = [...props.bales].sort((a, b) => a.number - b.number)
  const current = list.find((b) => b.number === props.current)
  return current ? [current, ...list.filter((b) => b !== current)] : list
})

function pick(target: MoveTarget) {
  emit('pick', target)
  close()
}

function close() {
  other.value = ''
  otherError.value = ''
  emit('update:open', false)
}

function pickOther() {
  const parsed = parseDecimal(other.value, 'number', { scale: 0 })
  if (parsed.status !== 'ok' || Number(parsed.value) < 1 || Number(parsed.value) > 9999) {
    otherError.value = message('E_BALE_NUMBER', { max: 9999 })
    return
  }
  const number = Number(parsed.value)
  if (number === props.from) return close()
  pick({ kind: 'bale', number })
}

/** پیکان‌ها در شبکهٔ دکمه‌ها (راست‌به‌چپ: ← بعدی). */
function onGridKeydown(event: KeyboardEvent) {
  if (!['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) return
  const buttons = [...(event.currentTarget as HTMLElement).querySelectorAll<HTMLButtonElement>('button:not([disabled])')]
  const at = buttons.indexOf(document.activeElement as HTMLButtonElement)
  if (at < 0) return
  event.preventDefault()
  const step = event.key === 'ArrowLeft' || event.key === 'ArrowDown' ? 1 : -1
  const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (at + step + buttons.length) % buttons.length
  buttons[next]?.focus()
}
</script>

<template>
  <Modal :open="open" :title="title" :description="description" size="md" initial-focus="[data-move-first]" @update:open="(v) => (v ? null : close())">
    <div class="space-y-4">
      <div
        :id="`${uid}-bales`"
        role="group"
        :aria-label="terms.bales"
        class="grid grid-cols-[repeat(auto-fill,minmax(3.5rem,1fr))] gap-2"
        @keydown="onGridKeydown"
      >
        <button
          v-for="(bale, i) in ordered"
          :key="bale.number"
          type="button"
          :data-move-first="i === 0 && bale.number !== from ? '' : undefined"
          :disabled="bale.number === from"
          :aria-current="bale.number === from ? 'true' : undefined"
          :aria-label="`${terms.baleOf} ${faDigitsOf(bale.number)}${bale.number === current ? ` (${message('I_BOARD_CURRENT')})` : ''}`"
          class="relative flex min-h-12 min-w-11 flex-col items-center justify-center rounded-lg border text-lg font-bold"
          :class="
            bale.number === current
              ? 'border-primary bg-primary/10 text-primary'
              : 'border-outline-variant bg-surface-container-lowest text-on-surface hover:bg-surface-container'
          "
          data-numeric
          @click="pick({ kind: 'bale', number: bale.number })"
        >
          {{ faDigitsOf(bale.number) }}
          <AppIcon v-if="bale.number === from" name="check" class="absolute top-0.5 end-0.5 h-3.5 w-3.5" />
          <span v-if="bale.number === current" class="text-[0.65rem] font-medium leading-none" aria-hidden="true">جاری</span>
        </button>
        <button
          type="button"
          :data-move-first="ordered.length === 0 || (ordered.length === 1 && ordered[0].number === from) ? '' : undefined"
          class="col-span-2 flex min-h-12 items-center justify-center gap-1.5 rounded-lg border border-dashed border-outline px-2 text-sm font-semibold text-on-surface hover:bg-surface-container"
          @click="pick({ kind: 'bale', number: fresh })"
        >
          <AppIcon name="plus" class="h-5 w-5" />
          {{ message('I_BOARD_NEW_BALE') }} ({{ faDigitsOf(fresh) }})
        </button>
      </div>

      <form class="flex items-end gap-2" @submit.prevent="pickOther">
        <div class="min-w-0 flex-1">
          <label :for="`${uid}-other`" class="mb-1 block text-sm font-medium">{{ message('I_BOARD_MOVE_OTHER') }}</label>
          <input
            :id="`${uid}-other`"
            v-model="other"
            inputmode="numeric"
            dir="ltr"
            autocomplete="off"
            enterkeyhint="go"
            class="min-h-11 w-full rounded-lg border bg-surface-container-lowest px-3 text-end outline-none focus:border-primary"
            :class="otherError ? 'border-error' : 'border-outline-variant'"
            :aria-invalid="otherError ? 'true' : undefined"
            :aria-describedby="otherError ? `${uid}-other-error` : undefined"
            data-numeric
          />
        </div>
        <button type="submit" class="min-h-11 rounded-lg border border-outline-variant px-4 text-sm font-semibold hover:bg-surface-container">
          برو
        </button>
      </form>
      <p v-if="otherError" :id="`${uid}-other-error`" class="-mt-2 text-xs text-error">{{ otherError }}</p>

      <div v-if="canUp || canDown" class="flex flex-wrap gap-2" role="group" :aria-label="`جا در همین ${terms.bale}`">
        <button
          type="button"
          class="inline-flex min-h-11 flex-1 items-center justify-center gap-1.5 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container"
          :disabled="!canUp"
          @click="pick({ kind: 'up' })"
        >
          <AppIcon name="chevron-up" class="h-5 w-5" />
          بالاتر
        </button>
        <button
          type="button"
          class="inline-flex min-h-11 flex-1 items-center justify-center gap-1.5 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container"
          :disabled="!canDown"
          @click="pick({ kind: 'down' })"
        >
          <AppIcon name="chevron-down" class="h-5 w-5" />
          پایین‌تر
        </button>
      </div>

      <button
        v-if="allowStock"
        type="button"
        class="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-lg border border-outline-variant px-4 text-sm font-semibold text-on-surface hover:bg-surface-container"
        @click="pick({ kind: 'stock' })"
      >
        <AppIcon name="warehouse" class="h-5 w-5" />
        {{ message('I_BOARD_BACK_TO_STOCK') }}
      </button>
    </div>
  </Modal>
</template>
