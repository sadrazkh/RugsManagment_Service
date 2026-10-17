<script setup lang="ts">
/**
 * جزیرهٔ `rug-final-size` (I.5، Floor): اندازهٔ نهایی فرش پیش از صادرات (C.4).
 *
 * - «همان اندازه»: اندازهٔ اول صریحاً به‌عنوان نهایی ثبت می‌شود (نه با خالی گذاشتن؛ قطعی کردن محموله اندازهٔ نهایی
 *   می‌خواهد).
 * - اندازهٔ تازه: طول و عرض (یا «208×210» در یک خانه)، با پرسش «متر یا سانت؟» خودِ NumberField.
 * - پاک کردن: اندازهٔ اول دوباره حساب می‌شود.
 * - نشانِ تغییر: بزرگ‌ترین تغییر طول یا عرض به سانت با علامتش: «↓۵ سانت».
 *
 * هر ذخیره نسخهٔ فرش را می‌فرستد (کهنه → ۴۰۹) و بعد صفحه تازه می‌شود، چون متراژ، بها هر متر و هر بخش دیگری که از
 * اندازهٔ مؤثر می‌خواند عوض شده است. قیمت خرید «هر متر» عوض نمی‌شود (روی اندازهٔ اول است). فرشِ قفل‌شده (صادرشده)
 * فقط نمایش است.
 *
 * تازه‌سازی ممکن است صبر کند (جزیرهٔ دیگری مثل سطرهای خرید کار ذخیره‌نشده دارد). پس پیش از آن، نسخهٔ تازه با
 * `rugs:rug-changed` به جزیره‌های دیگر خبر داده می‌شود و خودِ این جزیره نسخه و اندازهٔ ذخیره‌شده را از پاسخ برمی‌دارد؛
 * وگرنه هم این‌جا و هم ویرایشگر خرید با نسخهٔ کهنه ۴۰۹ دروغین «کس دیگری هم عوض کرد» می‌گرفتند.
 */
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { isSimpleUi } from '@/lib/context'
import { faDecimal, toPersianDigits } from '@/lib/format'
import { message } from '@/lib/messages'
import { cmp, dec, isZero, mul, pieceArea, sub, toNumber, tryDec, abs as decAbs } from '@/lib/num'
import { confirmDialog, drainPendingToast, requestReload, toastAfterReload } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import NumberField from '@/components/ui/NumberField.vue'
import type { Rug } from '@/lib/types/rugs'
import { announceRugChanged, onRugChanged } from './rugEvents'

const props = withDefaults(
  defineProps<{
    rugId: string
    version: number
    /** [طول، عرض] اندازهٔ اول */
    intake: [number, number]
    /** [طول، عرض] اندازهٔ نهایی؛ null یعنی ثبت نشده */
    final: [number, number] | null
    locked?: boolean
    quantity?: number
  }>(),
  { locked: false, quantity: 1 },
)

const SOURCE = 'rug-final-size'

const version = ref(props.version)
/** اندازهٔ نهایی ذخیره‌شده؛ از props شروع می‌شود و بعد از هر ذخیره از پاسخ سرور (تا اگر تازه‌سازی صبر کرد درست بماند). */
const current = ref<[number, number] | null>(props.final)
const editing = ref(false)
const saving = ref(false)
const error = ref('')
const length = ref<number | null>(null)
const width = ref<number | null>(null)
const lengthField = ref<InstanceType<typeof NumberField> | null>(null)
const widthField = ref<InstanceType<typeof NumberField> | null>(null)

const simple = isSimpleUi()

const sameAsIntake = computed(
  () => current.value !== null && cmp(current.value[0], props.intake[0]) === 0 && cmp(current.value[1], props.intake[1]) === 0,
)

/** بزرگ‌ترین تغییر طول یا عرض به سانت، با علامت (منفی = کوچک‌تر). */
const changeCm = computed(() => {
  if (!current.value) return null
  const dl = mul(sub(current.value[0], props.intake[0]), 100)
  const dw = mul(sub(current.value[1], props.intake[1]), 100)
  const larger = cmp(decAbs(dl), decAbs(dw)) >= 0 ? dl : dw
  return isZero(larger) ? null : toNumber(larger)
})

const finalArea = computed(() => (current.value ? toNumber(pieceArea(current.value[0], current.value[1])) : null))

function size(pair: [number, number]): string {
  return `${faDecimal(pair[0], 2, 2)} × ${faDecimal(pair[1], 2, 2)}`
}

function startEdit() {
  length.value = current.value?.[0] ?? null
  width.value = current.value?.[1] ?? null
  error.value = ''
  editing.value = true
  // H.7: فوکوس به اولین خانه‌ای که باید نوشته شود، نه ماندن روی بدنهٔ صفحه
  void nextTick(() => lengthField.value?.focus())
}

function onPair([l, w]: [number, number]) {
  length.value = l
  width.value = w
}

async function send(body: Record<string, unknown>, done: string) {
  saving.value = true
  error.value = ''
  try {
    const saved = await api.put<Rug>(`/api/rugs/${props.rugId}/final-size`, { ...body, version: version.value })
    version.value = saved.version
    current.value = saved.finalLength !== null && saved.finalWidth !== null ? [saved.finalLength, saved.finalWidth] : null
    editing.value = false
    announceRugChanged({ rugId: props.rugId, version: saved.version, source: SOURCE })
    toastAfterReload(done)
    // تازه‌سازی صبر کرد: پیام «ثبت شد» همین حالا، نه بعد از تازه‌سازیِ بعدی
    if (!requestReload()) drainPendingToast()
  } catch (e) {
    error.value = errorMessage(e)
    if (e instanceof ApiError) {
      const field = e.fieldError('rug.finalLength') ?? e.fieldError('rug.finalWidth')
      if (field) error.value = field
    }
  } finally {
    saving.value = false
  }
}

async function saveTyped() {
  const lengthProblem = lengthField.value?.validate() ?? null
  const widthProblem = widthField.value?.validate() ?? null
  if (lengthProblem || widthProblem) {
    error.value = lengthProblem ?? widthProblem ?? ''
    return
  }
  if (!tryDec(length.value) || !tryDec(width.value)) {
    error.value = message('E_SIZE_PAIR')
    return
  }
  await send({ length: toNumber(dec(length.value!)), width: toNumber(dec(width.value!)) }, message('I_FINAL_SIZE_SAVED'))
}

async function sameAsFirst() {
  await send({ sameAsIntake: true }, message('I_FINAL_SIZE_SAVED'))
}

async function clear() {
  const ok = await confirmDialog({
    title: message('Q_FINAL_SIZE_CLEAR'),
    confirmLabel: 'پاک کن',
    cancelLabel: 'نه',
    danger: true,
  })
  if (ok) await send({ clear: true }, message('I_FINAL_SIZE_CLEARED'))
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
  <div aria-labelledby="final-size-title">
    <div class="flex flex-wrap items-center justify-between gap-2">
      <h4 id="final-size-title" class="text-sm font-semibold text-on-surface-variant">اندازهٔ نهایی</h4>
      <span v-if="locked" class="inline-flex items-center gap-1 text-xs text-on-surface-variant">
        <AppIcon name="lock" class="h-3.5 w-3.5" />
        قفل است
      </span>
    </div>

    <!-- نمایش -->
    <div v-if="!editing" class="mt-2 flex flex-wrap items-center gap-3" data-numeric>
      <template v-if="current">
        <span class="text-base font-semibold">
          <bdi dir="ltr">{{ size(current) }}</bdi>
          <span class="text-sm font-normal text-on-surface-variant"> متر</span>
        </span>
        <span v-if="sameAsIntake" class="rounded-full bg-surface-container px-2.5 py-1 text-xs">همان اندازهٔ اول</span>
        <span
          v-else-if="changeCm !== null"
          class="inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold"
          :class="changeCm < 0 ? 'bg-warning/15 text-warning' : 'bg-tertiary/12 text-tertiary'"
          :aria-label="`${toPersianDigits(Math.abs(changeCm))} سانت ${changeCm < 0 ? 'کوچک‌تر' : 'بزرگ‌تر'} شده`"
        >
          {{ changeCm < 0 ? '↓' : '↑' }}{{ toPersianDigits(Math.abs(changeCm)) }} سانت
        </span>
        <span v-if="!simple || !sameAsIntake" class="text-sm text-on-surface-variant">
          متراژ {{ faDecimal(finalArea, 2, 2) }}
          <template v-if="quantity > 1"> × {{ toPersianDigits(quantity) }}</template>
        </span>
      </template>
      <span v-else class="text-sm text-on-surface-variant">هنوز ثبت نشده؛ اندازهٔ اول حساب می‌شود.</span>

      <div v-if="!locked" class="ms-auto flex flex-wrap gap-2">
        <Button v-if="!current" variant="primary" icon="check" :loading="saving" @click="sameAsFirst">همان اندازه</Button>
        <Button icon="ruler" :disabled="saving" @click="startEdit">{{ current ? 'ویرایش' : 'اندازهٔ تازه' }}</Button>
        <Button v-if="current" variant="ghost" icon="close" :disabled="saving" @click="clear">پاک کردن</Button>
      </div>
    </div>

    <!-- ویرایش -->
    <form v-else class="mt-2 space-y-3" novalidate @submit.prevent="saveTyped">
      <div class="grid gap-3 sm:grid-cols-2">
        <Field label="طول نهایی" v-slot="f">
          <NumberField
            :id="f.id"
            ref="lengthField"
            v-model="length"
            kind="dim"
            label="طول نهایی"
            :placeholder="faDecimal(intake[0], 2, 2)"
            :described-by="f.describedBy"
            enterkeyhint="next"
            @pair="onPair"
          />
        </Field>
        <Field label="عرض نهایی" v-slot="f">
          <NumberField
            :id="f.id"
            ref="widthField"
            v-model="width"
            kind="dim"
            label="عرض نهایی"
            :placeholder="faDecimal(intake[1], 2, 2)"
            :described-by="f.describedBy"
            enterkeyhint="done"
            @pair="onPair"
          />
        </Field>
      </div>
      <div class="flex flex-wrap gap-2">
        <Button type="submit" variant="primary" icon="check" :loading="saving">ثبت</Button>
        <Button icon="copy" :disabled="saving" @click="sameAsFirst">همان اندازهٔ اول</Button>
        <Button variant="ghost" :disabled="saving" @click="editing = false">انصراف</Button>
      </div>
    </form>

    <p v-if="error" class="mt-2 rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">{{ error }}</p>
  </div>
</template>
