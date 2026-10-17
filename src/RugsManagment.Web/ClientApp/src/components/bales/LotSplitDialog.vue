<script setup lang="ts">
/**
 * «جدا کن» برای دسته (D.9) از صفحهٔ نگله‌بندی: وقتی شمارهٔ زده‌شده داخل یک دسته است، یا از منوی ردیف دسته.
 *
 * پیش از تأیید نتیجه را نشان می‌دهد: «این دسته ۵۰ تخته است. چند تخته جدا شود؟» → «۵۰ تخته ← ۳۰ + ۲۰ · شماره‌ها ۳۹۱۱ تا
 * ۳۹۴۰ و ۳۹۴۱ تا ۳۹۶۰ [جدا کن] [نه]». جدا کردن «برگردان» یک‌کلیکی ندارد (سطرهای خرید، مراحل و سهم‌ها هم تقسیم
 * می‌شوند)، پس همین پیش‌نمایش محافظ است. سرور (`POST /api/rugs/{id}/split`) دستهٔ فروخته یا قفل را رد می‌کند و اگر دسته در
 * نگله است، فرش تازه همان نگله در جای بعدی را می‌گیرد.
 *
 * شماره‌ها خودکارند فقط وقتی طول بازه دقیقاً همان تعداد است (فرش تازه شماره‌های آخر را می‌گیرد)؛ وگرنه هر دو بخش را
 * کاربر می‌نویسد. نسخهٔ فرش اگر داده نشده همین‌جا خوانده می‌شود.
 */
import { computed, ref, useId, watch } from 'vue'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import Modal from '@/components/ui/Modal.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { message } from '@/lib/messages'
import type { Rug, SplitRugResult } from '@/lib/types/rugs'
import { faDigitsOf } from './baleModel'

const props = withDefaults(
  defineProps<{
    open: boolean
    rugId: string | null
    /** شکل نمایشی شمارهٔ دسته («3911 - 3960»)؛ null یعنی بی‌شماره */
    serial?: string | null
    quantity?: number
    /** نسخهٔ فرش؛ نبودنش یعنی از سرور خوانده شود */
    version?: number | null
  }>(),
  { serial: null, quantity: 0, version: null },
)

const emit = defineEmits<{ 'update:open': [value: boolean]; done: [result: SplitRugResult] }>()

const uid = useId()
const version = ref<number | null>(null)
const qty = ref(0)
const serialText = ref<string | null>(null)
const taken = ref<number | null>(null)
const keptSerial = ref('')
const takenSerial = ref('')
const saving = ref(false)
const loading = ref(false)
const error = ref('')
const errors = ref<Record<string, string>>({})

async function prepare() {
  version.value = props.version
  qty.value = props.quantity
  serialText.value = props.serial
  taken.value = null
  keptSerial.value = ''
  takenSerial.value = ''
  error.value = ''
  errors.value = {}
  if (!props.rugId || (version.value !== null && qty.value > 1)) return
  loading.value = true
  try {
    const rug = await api.get<Rug>(`/api/rugs/${props.rugId}`)
    version.value = rug.version
    qty.value = rug.quantity
    serialText.value = rug.serialNo
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

watch(
  () => [props.open, props.rugId],
  () => {
    if (props.open) void prepare()
  },
  { immediate: true },
)

const range = computed<{ from: number; to: number } | null>(() => {
  const text = serialText.value?.trim()
  if (!text) return null
  const match = /^(\d+)(?:\s*-\s*(\d+))?$/.exec(text)
  if (!match) return null
  const from = Number(match[1])
  const to = match[2] ? Number(match[2]) : from
  return Number.isSafeInteger(from) && Number.isSafeInteger(to) ? { from, to } : null
})

/** شماره‌ها خودکار (یا بی‌شماره، که هر دو بخش بی‌شماره می‌مانند). */
const automatic = computed(() => {
  if (!serialText.value) return true
  const r = range.value
  return r !== null && r.to - r.from + 1 === qty.value
})

const kept = computed(() => (taken.value ? qty.value - taken.value : null))
const validCount = computed(() => taken.value !== null && taken.value >= 1 && taken.value < qty.value)

function rangeText(from: number, to: number): string {
  return from === to ? faDigitsOf(from) : `${faDigitsOf(from)} تا ${faDigitsOf(to)}`
}

const preview = computed(() => {
  if (!validCount.value || kept.value === null || taken.value === null) return ''
  const counts = { total: qty.value, kept: kept.value, taken: taken.value }
  if (automatic.value) {
    const r = range.value
    if (!r || !serialText.value) return message('I_SPLIT_PREVIEW_NO_SERIAL', counts)
    return message('I_SPLIT_PREVIEW', {
      ...counts,
      keptSerial: rangeText(r.from, r.to - taken.value),
      takenSerial: rangeText(r.to - taken.value + 1, r.to),
    })
  }
  if (!keptSerial.value.trim() || !takenSerial.value.trim()) return message('I_SPLIT_PREVIEW_NO_SERIAL', counts)
  return message('I_SPLIT_PREVIEW', {
    ...counts,
    keptSerial: faDigitsOf(keptSerial.value.trim()),
    takenSerial: faDigitsOf(takenSerial.value.trim()),
  })
})

async function confirmSplit() {
  error.value = ''
  errors.value = {}
  if (!props.rugId || version.value === null) return
  if (!validCount.value) {
    errors.value = { quantity: message('E_SPLIT_COUNT', { max: Math.max(1, qty.value - 1) }) }
    return
  }
  saving.value = true
  try {
    const result = await api.post<SplitRugResult>(`/api/rugs/${props.rugId}/split`, {
      quantity: taken.value,
      serialFrom: automatic.value ? null : keptSerial.value.trim() || null,
      serialTo: automatic.value ? null : takenSerial.value.trim() || null,
      version: version.value,
    })
    emit('done', result)
    emit('update:open', false)
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
</script>

<template>
  <Modal :open="open" title="جدا کردن از دسته" size="md" :close-on-backdrop="false" @update:open="(v) => emit('update:open', v)">
    <div class="space-y-4">
      <p class="text-sm text-on-surface-variant" data-numeric>
        {{ message('I_BOARD_SPLIT_ASK', { n: qty }) }}
        <template v-if="serialText"> (<bdi dir="ltr">{{ faDigitsOf(serialText) }}</bdi>)</template>
      </p>

      <Field label="چند تخته جدا شود؟" :error="errors.quantity" required v-slot="f">
        <NumberField
          :id="f.id"
          v-model="taken"
          kind="integer"
          :min="1"
          :max="Math.max(1, qty - 1)"
          suffix="تخته"
          label="تعداد جداشده"
          :described-by="f.describedBy"
          :invalid="f.invalid"
          :disabled="loading"
        />
      </Field>

      <div v-if="!automatic" class="grid gap-3 sm:grid-cols-2">
        <Field label="شمارهٔ بخشی که می‌ماند" :error="errors.serialFrom" required v-slot="f">
          <input
            :id="f.id"
            v-model="keptSerial"
            dir="ltr"
            inputmode="text"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end outline-none focus:border-primary"
            :aria-describedby="f.describedBy"
            :aria-invalid="f.invalid"
          />
        </Field>
        <Field label="شمارهٔ بخش جداشده" :error="errors.serialTo" required v-slot="f">
          <input
            :id="f.id"
            v-model="takenSerial"
            dir="ltr"
            inputmode="text"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end outline-none focus:border-primary"
            :aria-describedby="f.describedBy"
            :aria-invalid="f.invalid"
          />
        </Field>
      </div>

      <p v-if="preview" :id="`${uid}-preview`" class="rounded-lg bg-surface-container px-4 py-3 text-base font-semibold" data-numeric aria-live="polite">
        {{ preview }}
      </p>
      <p class="text-xs text-on-surface-variant">{{ message('I_SPLIT_NO_UNDO') }}</p>
      <p v-if="error" class="rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">{{ error }}</p>
    </div>

    <template #footer="{ close }">
      <Button variant="primary" icon="split" :loading="saving" :disabled="loading || !validCount" @click="confirmSplit">جدا کن</Button>
      <Button variant="ghost" :disabled="saving" @click="close">نه</Button>
    </template>
  </Modal>
</template>
