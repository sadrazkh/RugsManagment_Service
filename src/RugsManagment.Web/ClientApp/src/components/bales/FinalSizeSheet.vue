<script setup lang="ts">
/**
 * برگهٔ «اندازه» (H.6 پ): ثبت اندازهٔ نهایی یک فرش روی تبلت، بی صفحه‌کلید سخت‌افزاری. دو `NumberField` (طول، عرض)
 * با همان قاعدهٔ «سانت؟» جدول (۲۰۸ ← ۲٫۰۸ متر؟)، «208×210» هر دو را پر می‌کند، و دکمه‌های «همان اندازهٔ اول» و «ثبت».
 *
 * نوار ۵۶ پیکسلی پایین (H.6 ب) همان کارهای صفحه‌کلید را دارد، پس هیچ کاری فقط صفحه‌کلیدی نیست:
 * «همان اندازه» (=) · «قبلی» (Shift+Tab) · «بعدی» (Tab/Enter) · «برگرد» (Ctrl+Z) · «بستن» (Esc). «قبلی» و «بعدی» اگر
 * چیزی نوشته شده اول ثبت می‌کنند، بعد سراغ فرش قبلی/بعدی می‌روند؛ پس اندازه‌گیری یک نگله پشت سر هم است.
 *
 * ذخیره را والد انجام می‌دهد (`save`، با «برگردان»)؛ این برگه فقط می‌پرسد و منتظر جواب می‌ماند.
 */
import { computed, nextTick, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import Drawer from '@/components/ui/Drawer.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { message } from '@/lib/messages'
import type { FinalSizeInput } from '@/lib/types/bales'
import type { BaleRow } from '@/lib/types/shipments'
import { confirmDialog } from '@/lib/ui'
import { faDigitsOf, faFixed, serialText, sizeChangeBadge, sizeText } from './baleModel'

const props = withDefaults(
  defineProps<{
    open: boolean
    row: BaleRow | null
    /** والد ثبت می‌کند؛ true یعنی ذخیره شد */
    save: (size: FinalSizeInput) => Promise<boolean>
    hasPrev?: boolean
    hasNext?: boolean
    canUndo?: boolean
    locked?: boolean
  }>(),
  { hasPrev: false, hasNext: false, canUndo: false, locked: false },
)

const emit = defineEmits<{ 'update:open': [value: boolean]; navigate: [step: -1 | 1]; undo: [] }>()

interface FieldApi {
  validate: () => string | null
  focus: () => void
}

const uid = useId()
const length = ref<number | null>(null)
const width = ref<number | null>(null)
const lengthField = ref<FieldApi | null>(null)
const widthField = ref<FieldApi | null>(null)
const saving = ref(false)
const error = ref('')

/** چیزی تازه نوشته شده که هنوز ثبت نشده. */
const dirty = computed(() => {
  const row = props.row
  if (!row) return false
  return length.value !== row.finalLength || width.value !== row.finalWidth
})

const title = computed(() => message('I_BOARD_SIZE_TITLE', { serial: serialText(props.row?.serial) }))
const intake = computed(() => (props.row ? sizeText(props.row.intakeLength, props.row.intakeWidth) : ''))
const badge = computed(() => sizeChangeBadge(props.row?.sizeChangeCm))

function reset() {
  length.value = props.row?.finalLength ?? null
  width.value = props.row?.finalWidth ?? null
  error.value = ''
  if (props.open) void nextTick(() => lengthField.value?.focus())
}

watch(() => [props.row?.lineId, props.row?.rugVersion, props.open], reset, { immediate: true })

function onPair(values: [number, number]) {
  length.value = values[0]
  width.value = values[1]
}

async function run(size: FinalSizeInput, then: 'next' | 'prev' | 'close' | 'stay') {
  if (saving.value || props.locked) return
  saving.value = true
  error.value = ''
  try {
    const ok = await props.save(size)
    if (!ok) return
    if (then === 'next' && props.hasNext) emit('navigate', 1)
    else if (then === 'prev' && props.hasPrev) emit('navigate', -1)
    else if (then !== 'stay') close()
  } finally {
    saving.value = false
  }
}

/** «ثبت» یا Enter: هر دو لازم‌اند؛ اگر فقط طول نوشته شده فوکوس به عرض می‌رود. */
async function submit(then: 'next' | 'prev' | 'close' = 'next') {
  const problem = lengthField.value?.validate() ?? widthField.value?.validate() ?? null
  if (problem) return
  if (length.value === null && width.value === null) {
    error.value = message('E_SIZE_PAIR')
    lengthField.value?.focus()
    return
  }
  if (width.value === null) {
    widthField.value?.focus()
    return
  }
  if (length.value === null) {
    lengthField.value?.focus()
    return
  }
  await run({ kind: 'pair', length: length.value, width: width.value }, then)
}

function same() {
  void run({ kind: 'same' }, props.hasNext ? 'next' : 'close')
}

async function clear() {
  const ok = await confirmDialog({ title: message('Q_FINAL_SIZE_CLEAR'), confirmLabel: 'پاک کن', cancelLabel: 'نه', danger: true })
  if (ok) await run({ kind: 'clear' }, 'stay')
}

/** «قبلی» / «بعدی»: نوشتهٔ ثبت‌نشده اول ثبت می‌شود. */
function go(step: -1 | 1) {
  if (dirty.value && length.value !== null && width.value !== null) void submit(step === 1 ? 'next' : 'prev')
  else if (step === 1 ? props.hasNext : props.hasPrev) emit('navigate', step)
}

function close() {
  emit('update:open', false)
}

/** «=» هر جای برگه یعنی «همان اندازهٔ اول» (C.4). */
function onKeydown(event: KeyboardEvent) {
  if (event.key === '=' && !event.ctrlKey && !event.metaKey && !event.altKey) {
    event.preventDefault()
    same()
  } else if ((event.ctrlKey || event.metaKey) && (event.key === 'z' || event.key === 'Z')) {
    event.preventDefault()
    emit('undo')
  }
}
</script>

<template>
  <Drawer :open="open" :title="title" side="bottom" @update:open="(v) => emit('update:open', v)">
    <form v-if="row" class="mx-auto max-w-xl space-y-4" @submit.prevent="submit('next')" @keydown="onKeydown">
      <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-on-surface-variant" data-numeric>
        <span>{{ message('I_BOARD_INTAKE_SIZE', { size: intake }) }}</span>
        <span v-if="row.qty > 1">{{ faDigitsOf(row.qty) }} تخته</span>
        <span v-if="row.finalLength !== null" class="inline-flex items-center gap-1 text-on-surface">
          نهایی: <bdi dir="ltr">{{ faFixed(row.finalLength) }} × {{ faFixed(row.finalWidth) }}</bdi>
          <span
            v-if="badge"
            class="rounded-full bg-surface-container-high px-2 text-xs font-semibold"
            :aria-label="badge.label"
          >{{ badge.text }}</span>
        </span>
      </div>

      <div class="grid grid-cols-2 gap-3">
        <div>
          <label :for="`${uid}-length`" class="mb-1 block text-sm font-medium">طول</label>
          <NumberField
            :id="`${uid}-length`"
            ref="lengthField"
            v-model="length"
            kind="dim"
            label="طول"
            enterkeyhint="next"
            :placeholder="faFixed(row.intakeLength)"
            :disabled="locked"
            @pair="onPair"
          />
        </div>
        <div>
          <label :for="`${uid}-width`" class="mb-1 block text-sm font-medium">عرض</label>
          <NumberField
            :id="`${uid}-width`"
            ref="widthField"
            v-model="width"
            kind="dim"
            label="عرض"
            enterkeyhint="done"
            :placeholder="faFixed(row.intakeWidth)"
            :disabled="locked"
            @pair="onPair"
          />
        </div>
      </div>

      <p v-if="error" class="text-sm text-error" role="alert">{{ error }}</p>

      <div class="flex flex-wrap gap-2">
        <Button variant="primary" size="lg" type="submit" icon="check" :loading="saving" :disabled="locked">ثبت</Button>
        <Button size="lg" icon="ruler" :disabled="locked || saving" @click="same">همان اندازهٔ اول</Button>
        <Button v-if="row.finalLength !== null" variant="ghost" icon="trash" :disabled="locked || saving" @click="clear">پاک کردن</Button>
      </div>
    </form>

    <template #footer>
      <!-- نوار لمسی (H.6 ب): همان کارهای صفحه‌کلید -->
      <div class="grid w-full grid-cols-5 gap-2" role="group" aria-label="کارهای اندازه">
        <button type="button" class="min-h-14 rounded-lg bg-surface-container text-sm font-semibold text-on-surface" :disabled="locked || saving" @mousedown.prevent @click="same">
          همان اندازه
        </button>
        <button type="button" class="min-h-14 rounded-lg bg-surface-container text-sm text-on-surface" :disabled="!hasPrev || saving" @mousedown.prevent @click="go(-1)">
          <AppIcon name="arrow-right" class="mx-auto h-5 w-5" />
          قبلی
        </button>
        <button type="button" class="min-h-14 rounded-lg bg-primary text-sm font-semibold text-on-primary" :disabled="!hasNext || saving" @mousedown.prevent @click="go(1)">
          <AppIcon name="arrow-left" class="mx-auto h-5 w-5" />
          بعدی
        </button>
        <button type="button" class="min-h-14 rounded-lg bg-surface-container text-sm text-on-surface" :disabled="!canUndo || saving" @mousedown.prevent @click="emit('undo')">
          <AppIcon name="undo" class="mx-auto h-5 w-5" />
          برگرد
        </button>
        <button type="button" class="min-h-14 rounded-lg bg-surface-container text-sm text-on-surface" @mousedown.prevent @click="close">
          <AppIcon name="close" class="mx-auto h-5 w-5" />
          بستن
        </button>
      </div>
    </template>
  </Drawer>
</template>
