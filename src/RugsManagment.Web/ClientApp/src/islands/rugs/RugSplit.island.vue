<script setup lang="ts">
/**
 * جزیرهٔ `rug-split` (D.9): «جدا کردن از دسته» در صفحهٔ فرشی که دسته است (تعداد ۲ یا بیشتر).
 *
 * پیش از تأیید نتیجه را نشان می‌دهد: «۵۰ تخته ← ۳۰ + ۲۰ · شماره‌ها ۳۹۱۱ تا ۳۹۴۰ و ۳۹۴۱ تا ۳۹۶۰ [جدا کن] [نه]». برش
 * یک‌کلیکی برنمی‌گردد (سطرهای خرید، مراحل و سهم‌ها هم تقسیم می‌شوند)، پس همین پیش‌نمایش محافظ است.
 *
 * شماره‌ها خودکارند فقط وقتی بازهٔ دسته دقیقاً به اندازهٔ تعداد است (فرش تازه شماره‌های آخر را می‌گیرد)؛ دستهٔ بی‌شماره
 * بی‌شماره می‌ماند؛ در بقیهٔ حالت‌ها (بازهٔ بلندتر از تعداد، شمارهٔ متنی) هر دو بخش را خودِ کاربر می‌نویسد و سرور
 * همپوشانی را می‌سنجد. دستهٔ فروش‌دار جدا نمی‌شود (اول برش، بعد فروش).
 *
 * نسخهٔ فرش با ذخیرهٔ جزیره‌های دیگر همین صفحه (اندازهٔ نهایی، سطرهای خرید) جلو می‌رود؛ این جزیره با `rugs:rug-changed`
 * همراه می‌شود تا برش بعد از آن ذخیره‌ها ۴۰۹ دروغین نگیرد، و بعد از برش خودش هم خبر می‌دهد.
 *
 * خانه‌های شماره صفحه‌کلید متنی دارند (`inputmode="text"`، مثل خانهٔ شماره در جدول G.1): بازه «۳۹۱۱ - ۳۹۳۰» و
 * «۳۹۱۱ تا ۳۹۳۰» خط تیره یا حرف لازم دارد که صفحه‌کلید عددی آیفون ندارد.
 */
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { toPersianDigits } from '@/lib/format'
import { message } from '@/lib/messages'
import { requestReload, toastAfterReload } from '@/lib/ui'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import Modal from '@/components/ui/Modal.vue'
import NumberField from '@/components/ui/NumberField.vue'
import type { SplitRugResult } from '@/lib/types/rugs'
import { announceRugChanged, onRugChanged } from './rugEvents'

const props = withDefaults(
  defineProps<{
    rugId: string
    version: number
    quantity: number
    /** شکل نمایشی شمارهٔ دسته («3911 - 3960»)؛ null یعنی بی‌شماره */
    serial?: string | null
    /** فروش دارد (برش ممنوع) */
    sold?: boolean
  }>(),
  { serial: null, sold: false },
)

const SOURCE = 'rug-split'

const version = ref(props.version)
const open = ref(false)
const taken = ref<number | null>(null)
const keptSerialText = ref('')
const takenSerialText = ref('')
const saving = ref(false)
const error = ref('')
const errors = ref<Record<string, string>>({})

/** بازهٔ عددی شکل نمایشی («3911 - 3960» یا «3778»)؛ متن آزاد null. */
const range = computed<{ from: number; to: number } | null>(() => {
  const text = props.serial?.trim()
  if (!text) return null
  const match = /^(\d+)(?: - (\d+))?$/.exec(text)
  if (!match) return null
  const from = Number(match[1])
  const to = match[2] ? Number(match[2]) : from
  return Number.isSafeInteger(from) && Number.isSafeInteger(to) ? { from, to } : null
})

/** شماره‌ها خودکار است؟ (بی‌شماره هم «خودکار» است: هر دو بخش بی‌شماره می‌مانند.) */
const automatic = computed(() => {
  if (!props.serial) return true
  const r = range.value
  return r !== null && r.to - r.from + 1 === props.quantity
})

const kept = computed(() => (taken.value ? props.quantity - taken.value : null))
const validCount = computed(() => taken.value !== null && taken.value >= 1 && taken.value < props.quantity)

function rangeText(from: number, to: number): string {
  return from === to ? toPersianDigits(from) : `${toPersianDigits(from)} تا ${toPersianDigits(to)}`
}

const preview = computed(() => {
  if (!validCount.value || kept.value === null || taken.value === null) return ''
  const counts = { total: props.quantity, kept: kept.value, taken: taken.value }
  if (automatic.value) {
    const r = range.value
    if (!r || !props.serial) return message('I_SPLIT_PREVIEW_NO_SERIAL', counts)
    return message('I_SPLIT_PREVIEW', {
      ...counts,
      keptSerial: rangeText(r.from, r.to - taken.value),
      takenSerial: rangeText(r.to - taken.value + 1, r.to),
    })
  }
  if (!keptSerialText.value.trim() || !takenSerialText.value.trim()) return message('I_SPLIT_PREVIEW_NO_SERIAL', counts)
  return message('I_SPLIT_PREVIEW', {
    ...counts,
    keptSerial: toPersianDigits(keptSerialText.value.trim()),
    takenSerial: toPersianDigits(takenSerialText.value.trim()),
  })
})

function start() {
  taken.value = null
  keptSerialText.value = ''
  takenSerialText.value = ''
  error.value = props.sold ? message('E_SPLIT_SOLD') : ''
  errors.value = {}
  open.value = true
}

async function confirmSplit() {
  error.value = ''
  errors.value = {}
  if (!validCount.value) {
    errors.value = { quantity: message('E_SPLIT_COUNT', { max: props.quantity - 1 }) }
    return
  }
  saving.value = true
  try {
    const result = await api.post<SplitRugResult>(`/api/rugs/${props.rugId}/split`, {
      quantity: taken.value,
      serialFrom: automatic.value ? null : keptSerialText.value.trim() || null,
      serialTo: automatic.value ? null : takenSerialText.value.trim() || null,
      version: version.value,
    })
    version.value = result.original.version
    announceRugChanged({ rugId: props.rugId, version: result.original.version, source: SOURCE })
    toastAfterReload(message('I_SPLIT_DONE', { taken: taken.value ?? 0 }))
    requestReload()
  } catch (e) {
    error.value = errorMessage(e)
    if (e instanceof ApiError) {
      const next: Record<string, string> = {}
      for (const key of ['quantity', 'serialFrom', 'serialTo']) {
        const problem = e.fieldError(key)
        if (problem) next[key] = problem
      }
      if (e.field === 'rug.serial') next.serialTo = e.message
      errors.value = next
    }
  } finally {
    saving.value = false
  }
}

let stop: (() => void) | null = null
onMounted(() => {
  stop = onRugChanged(props.rugId, SOURCE, (detail) => {
    version.value = detail.version
  })
})
onUnmounted(() => stop?.())
</script>

<template>
  <div>
    <Button icon="split" @click="start">جدا کردن از دسته</Button>

    <Modal v-model:open="open" title="جدا کردن از دسته" size="md" :close-on-backdrop="false">
      <div class="space-y-4">
        <p class="text-sm text-on-surface-variant" data-numeric>
          این دسته {{ toPersianDigits(quantity) }} تخته دارد<template v-if="serial">
            با شمارهٔ <bdi dir="ltr">{{ toPersianDigits(serial) }}</bdi></template>.
        </p>

        <Field label="چند تخته جدا شود؟" :error="errors.quantity" required v-slot="f">
          <NumberField
            :id="f.id"
            v-model="taken"
            kind="integer"
            :min="1"
            :max="quantity - 1"
            suffix="تخته"
            label="تعداد جداشده"
            :described-by="f.describedBy"
            :invalid="f.invalid"
          />
        </Field>

        <div v-if="!automatic" class="grid gap-3 sm:grid-cols-2">
          <Field label="شمارهٔ بخشی که می‌ماند" :error="errors.serialFrom" required v-slot="f">
            <input
              :id="f.id"
              v-model="keptSerialText"
              dir="ltr"
              inputmode="text"
              autocomplete="off"
              autocapitalize="off"
              spellcheck="false"
              placeholder="3911 - 3940"
              class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end outline-none focus:border-primary"
              :aria-describedby="f.describedBy"
              :aria-invalid="f.invalid"
            />
          </Field>
          <Field label="شمارهٔ بخش جداشده" :error="errors.serialTo" required v-slot="f">
            <input
              :id="f.id"
              v-model="takenSerialText"
              dir="ltr"
              inputmode="text"
              autocomplete="off"
              autocapitalize="off"
              spellcheck="false"
              placeholder="3941 - 3960"
              class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end outline-none focus:border-primary"
              :aria-describedby="f.describedBy"
              :aria-invalid="f.invalid"
            />
          </Field>
        </div>

        <p
          v-if="preview"
          class="rounded-lg bg-surface-container px-4 py-3 text-base font-semibold"
          data-numeric
          aria-live="polite"
        >
          {{ preview }}
        </p>
        <p class="text-xs text-on-surface-variant">{{ message('I_SPLIT_NO_UNDO') }}</p>

        <p v-if="error" class="rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">{{ error }}</p>
      </div>

      <template #footer="{ close }">
        <Button variant="primary" icon="split" :loading="saving" :disabled="sold || !validCount" @click="confirmSplit">
          جدا کن
        </Button>
        <Button variant="ghost" :disabled="saving" @click="close">نه</Button>
      </template>
    </Modal>
  </div>
</template>
