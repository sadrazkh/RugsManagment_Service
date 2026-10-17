<script setup lang="ts">
/**
 * خانهٔ عدد برای مبلغ، اندازه، دلار و درصد (H.4) — جای MoneyInput و `type="number"` در کد تازه.
 *
 * - هر شکل تایپی بازار را می‌پذیرد (`parseDecimal`، G.4): ارقام فارسی و عربی، «٫»، «۲/۵»، «۱۰م»، «۸۵ه»،
 *   «2,08» در اندازه. چیز مبهم پرسیده می‌شود با دو دکمهٔ ۴۴ پیکسلی (Enter = اولی، Esc = دومی):
 *   «منظورتان ۱۰٬۰۰۰ است؟»، «۲۰۸ ← ۲٫۰۸ متر؟»، «۳۰ متر؟ یا ۳۰ سانت؟».
 * - بیرون از فوکوس عدد با ارقام فارسی و جداکنندهٔ هزارگان؛ در فوکوس همان متن خام برای ویرایش.
 * - زیر هر مبلغ همان عدد به حروف («ده میلیون تومان») در ناحیهٔ `aria-live` تا صفر اضافه همان لحظه دیده شود.
 * - اندازه: «208×210» یا «۲ در ۳» هر دو خانه را پر می‌کند (`pair` می‌فرستد؛ والد خانهٔ دوم را پر کند).
 * - رسید ریالی در کارگاه تومانی (فقط حالت ساده و فقط وقتی والد `v-model:currency` بسته): کلید
 *   «تومان | ریال». طبق erratum E13 مبلغ همان ریالی که نوشته شده ثبت می‌شود و واحدش ریال است؛ تبدیل به
 *   تومان را سرور برای هر تخته انجام می‌دهد. سطر حروف هر دو را نشان می‌دهد.
 *
 * مقدار `v-model` عدد جاوااسکریپت است (برای JSON)، ولی خواندن و گرد کردن دقیق (`lib/num.ts`) انجام می‌شود.
 *
 * فقط نوشتهٔ کاربر خوانده می‌شود: فوکوس و خروج بی‌تایپ مقدار ذخیره‌شده را دوباره نمی‌خواند. وگرنه ضلع ۱۵ متریِ
 * یک فرش قدیمی با یک Tab «۱۵ متر؟ یا ۱۵ سانت؟» می‌شد و پاک می‌شد، و جواب «۲۵ متر» با خروج بعدی دوباره پرسیده و پاک
 * می‌شد (G.4: «متر یا سانت؟» یک بار پرسیده و جوابش برای همان عدد به خاطر سپرده می‌شود).
 */
import { computed, ref, useId, watch } from 'vue'
import { appContext, isSimpleUi } from '@/lib/context'
import { faDecimal, toPersianDigits } from '@/lib/format'
import { message } from '@/lib/messages'
import {
  looksLikeDimensionPair,
  normalizeNumericText,
  parseDecimal,
  parseDimensionPair,
  type DecimalKind,
  type ParsedDecimal,
} from '@/lib/money'
import { cmp, dec, mul, toNumber, toPlainString, tryDec } from '@/lib/num'
import { moneyWords } from '@/lib/numWords'
import { CurrencyUnit } from '@/lib/types/platform'

type FieldKind = 'money' | 'dim' | 'usd' | 'number' | 'percent' | 'integer'

const props = withDefaults(
  defineProps<{
    modelValue: number | null | undefined
    kind?: FieldKind
    /** رقم اعشار؛ پیش‌فرض: مبلغ = MoneyScale کارگاه، اندازه/دلار/درصد/عدد = ۲، عدد صحیح = ۰ */
    scale?: number
    /** پسوند؛ undefined = خودکار (تومان/ریال، دلار، متر، ٪)، رشتهٔ خالی = بی‌پسوند */
    suffix?: string
    allowNegative?: boolean
    /** سطر «به حروف»؛ پیش‌فرض فقط برای مبلغ */
    words?: boolean
    /** کلید «تومان | ریال» در حالت ساده (والد باید `v-model:currency` ببندد) */
    rialToggle?: boolean
    /** واحد مبلغ نوشته‌شده (فقط با rialToggle) */
    currency?: CurrencyUnit
    min?: number
    max?: number
    /**
     * اندازه: ضلع بزرگ‌تر از این پرسش «متر یا سانت؟» می‌آورد. پیش‌فرض: «بزرگ‌ترین ضلع معمول» کارگاه از app-context
     * (TenantPreferences.MaxPlausibleSideM)، نه عدد ثابت.
     */
    maxPlausibleSideM?: number
    id?: string
    describedBy?: string
    invalid?: boolean
    disabled?: boolean
    readonly?: boolean
    required?: boolean
    placeholder?: string
    ariaLabel?: string
    /** نام خانه برای پیام ««…» را بنویسید» (نمایش داده نمی‌شود؛ برچسب دیدنی کار Field است) */
    label?: string
    enterkeyhint?: 'enter' | 'done' | 'go' | 'next' | 'previous' | 'search' | 'send'
  }>(),
  {
    kind: 'number',
    scale: undefined,
    suffix: undefined,
    allowNegative: false,
    words: undefined,
    rialToggle: false,
    currency: undefined,
    min: undefined,
    max: undefined,
    maxPlausibleSideM: undefined,
    id: undefined,
    describedBy: undefined,
    invalid: false,
    disabled: false,
    readonly: false,
    required: false,
    placeholder: undefined,
    ariaLabel: undefined,
    label: undefined,
    enterkeyhint: undefined,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: number | null]
  'update:currency': [value: CurrencyUnit]
  /** «208×210» در خانهٔ اندازه: [این خانه، خانهٔ دوم] به متر */
  pair: [values: [number, number]]
  /** پس از هر ثبت قطعی (خروج یا Enter) با مقدار نهایی */
  commit: [value: number | null]
}>()

const ctx = appContext()
const uid = useId()
const inputId = computed(() => props.id ?? `${uid}-input`)
const wordsId = `${uid}-words`
const errorId = `${uid}-error`
const questionId = `${uid}-question`
const input = ref<HTMLInputElement | null>(null)

// ── تنظیمات بر اساس نوع ──────────────────────────────────────────────

const effectiveScale = computed(() => {
  if (props.scale !== undefined) return props.scale
  switch (props.kind) {
    case 'money':
      return ctx.moneyScale
    case 'integer':
      return 0
    default:
      return 2
  }
})

const parseKind = computed<DecimalKind>(() => (props.kind === 'money' ? 'money' : props.kind === 'dim' ? 'dim' : 'number'))

const showRialToggle = computed(
  () => props.rialToggle && props.kind === 'money' && isSimpleUi() && ctx.currency.code === 'IRT',
)
const isRial = computed(() => showRialToggle.value && props.currency === CurrencyUnit.Rial)

/** واحد مبلغی که همین حالا نوشته می‌شود. */
const unitLabel = computed(() => (props.kind === 'money' ? (isRial.value ? 'ریال' : ctx.currency.label) : ''))

const suffixText = computed(() => {
  if (props.suffix !== undefined) return props.suffix
  switch (props.kind) {
    case 'money':
      return unitLabel.value
    case 'usd':
      return 'دلار'
    case 'dim':
      return 'متر'
    case 'percent':
      return '٪'
    default:
      return ''
  }
})

const showWords = computed(() => props.words ?? props.kind === 'money')

const parseOptions = computed(() => ({
  scale: effectiveScale.value,
  allowNegative: props.allowNegative,
  currencyLabel: unitLabel.value || ctx.currency.label,
  maxPlausibleSideM: props.maxPlausibleSideM ?? ctx.maxPlausibleSideM,
}))

// ── وضعیت ────────────────────────────────────────────────────────────

interface Choice {
  label: string
  apply: () => void
}

const focused = ref(false)
const text = ref(editable(props.modelValue))
const errorText = ref<string | null>(null)
const pending = ref<{ message: string; choices: Choice[] } | null>(null)
/** مقدار معتبر متن جاری حین تایپ (برای سطر حروف) */
const liveValue = ref<string | null>(null)
/** آخرین مقداری که خودمان فرستادیم؛ تغییر بیرونی را از پژواک خودمان جدا می‌کند */
let lastEmitted: number | null | undefined = props.modelValue ?? null
/** کاربر از آخرین ثبت چیزی تایپ کرده؟ فقط آن‌وقت خروج یا Enter متن را دوباره می‌خواند. */
let dirty = false
/**
 * جواب‌های «متر یا سانت؟» همین خانه: متن تایپ‌شده (نرمال) → مقدار انتخاب‌شده. همان عدد دوباره پرسیده نمی‌شود.
 */
const unitAnswers = new Map<string, string>()

function answerKey(raw: string): string {
  return normalizeNumericText(raw)
}

/**
 * متن قابل ویرایش یک مقدار: ارقام فارسی، بی‌جداکنندهٔ هزارگان، با همان ممیز «.» نمایش (مثل `faNumber` و
 * سرور) تا با فوکوس شکل عدد عوض نشود. تایپ «٫» هم پذیرفته است.
 */
function editable(value: number | string | null | undefined): string {
  const d = tryDec(value)
  return d ? toPersianDigits(toPlainString(d)) : ''
}

/** نمایش بیرون از فوکوس. */
function formatted(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return ''
  const minDecimals = props.kind === 'dim' ? 2 : 0
  return faDecimal(value, effectiveScale.value, minDecimals)
}

const showRaw = computed(() => focused.value || pending.value !== null || errorText.value !== null)
const inputValue = computed(() => (showRaw.value ? text.value : formatted(props.modelValue)))

function emitValue(value: number | null) {
  lastEmitted = value
  emit('update:modelValue', value)
}

watch(
  () => props.modelValue,
  (value) => {
    if (value === lastEmitted || (value == null && lastEmitted == null)) return
    // مقدار از بیرون عوض شد (والد پر کرد یا پاک کرد): وضعیت محلی را کنار بگذار
    lastEmitted = value ?? null
    dirty = false
    pending.value = null
    errorText.value = null
    liveValue.value = null
    text.value = editable(value)
  },
)

/**
 * آیا خروج/Enter باید متن را بخواند: کاربر چیزی تایپ کرده، یا متن با آخرین مقدار ثبت‌شده نمی‌خواند (پرسش، خطا یا
 * متنی که هنوز ثبت نشده). متنِ دست‌نخورده‌ای که همان مقدار است دوباره خوانده نمی‌شود.
 */
function needsCommit(): boolean {
  return dirty || pending.value !== null || errorText.value !== null || text.value !== editable(lastEmitted)
}

function rangeError(value: string): string | null {
  if (props.min !== undefined && cmp(value, props.min) < 0) {
    return message('E_MIN', { min: faDecimal(props.min, effectiveScale.value) })
  }
  if (props.max !== undefined && cmp(value, props.max) > 0) {
    return message('E_MAX', { max: faDecimal(props.max, effectiveScale.value) })
  }
  return null
}

function parse(raw: string): ParsedDecimal {
  return parseDecimal(raw, parseKind.value, parseOptions.value)
}

function apply(value: string) {
  const outOfRange = rangeError(value)
  if (outOfRange) {
    errorText.value = outOfRange
    pending.value = null
    emitValue(null)
    emit('commit', null)
    return
  }
  pending.value = null
  errorText.value = null
  liveValue.value = null
  dirty = false
  text.value = editable(value)
  const n = toNumber(dec(value))
  emitValue(n)
  emit('commit', n)
}

function reject(code: string) {
  dirty = false
  pending.value = null
  errorText.value = message(code, { currency: unitLabel.value || ctx.currency.label })
  emitValue(null)
  emit('commit', null)
}

/** ثبت قطعی متن (خروج از خانه یا Enter). true اگر چیزی ماند که کاربر باید جواب دهد. */
function commit(): boolean {
  if (pending.value) return true
  const raw = text.value

  if (props.kind === 'dim' && looksLikeDimensionPair(raw)) {
    const r = parseDimensionPair(raw, parseOptions.value)
    if (!r) return false
    if (r.status === 'ok') {
      applyPair(r.values)
      return false
    }
    if (r.status === 'error') {
      reject(r.code)
      return true
    }
    pending.value = {
      message: message(r.code, r.tokens),
      choices: r.choices.map((c) => ({
        label: c.label,
        apply: () => (c.values ? applyPair(c.values) : reject('E_DIMENSION')),
      })),
    }
    emitValue(null)
    return true
  }

  const r = parse(raw)
  // همین عدد قبلاً «متر یا سانت؟» جواب گرفته: همان جواب، بی پرسش دوباره
  const remembered = r.status === 'ask' && r.code === 'Q_DIM_UNIT' ? unitAnswers.get(answerKey(raw)) : undefined
  if (remembered !== undefined) {
    apply(remembered)
    return errorText.value !== null
  }
  switch (r.status) {
    case 'empty':
      dirty = false
      pending.value = null
      errorText.value = null
      text.value = ''
      emitValue(null)
      emit('commit', null)
      return false
    case 'ok':
      apply(r.value)
      return errorText.value !== null
    case 'error':
      dirty = false
      pending.value = null
      errorText.value = message(r.code, r.tokens)
      emitValue(null)
      emit('commit', null)
      return true
    case 'ask': {
      const fallback = props.kind === 'dim' ? 'E_DIMENSION' : 'E_MONEY_DECIMALS'
      const key = answerKey(raw)
      dirty = false
      pending.value = {
        message: message(r.code, r.tokens),
        choices: r.choices.map((c) => ({
          label: c.label,
          apply: () => {
            if (c.value === null) return reject(fallback)
            if (r.code === 'Q_DIM_UNIT') unitAnswers.set(key, c.value)
            apply(c.value)
          },
        })),
      }
      emitValue(null)
      return true
    }
  }
}

function applyPair(values: [string, string]) {
  apply(values[0])
  if (errorText.value === null) emit('pair', [toNumber(dec(values[0])), toNumber(dec(values[1]))])
}

function choose(index: number) {
  pending.value?.choices[index]?.apply()
}

// ── رویدادها ─────────────────────────────────────────────────────────

function onFocus() {
  focused.value = true
  if (pending.value === null && errorText.value === null) text.value = editable(props.modelValue)
  // همهٔ متن انتخاب شود تا تایپ تازه جایش بنشیند (مثل خانهٔ اکسل)
  requestAnimationFrame(() => {
    if (document.activeElement === input.value) input.value?.select()
  })
}

function onInput(event: Event) {
  text.value = (event.target as HTMLInputElement).value
  dirty = true
  pending.value = null
  errorText.value = null

  if (props.kind === 'dim' && looksLikeDimensionPair(text.value)) {
    // «۲۰۸×۲۱۰» هنگام خروج دو خانه را پر می‌کند؛ تا آن وقت مقدار این خانه معلوم نیست
    liveValue.value = null
    if (lastEmitted !== null) emitValue(null)
    return
  }
  const r = parse(text.value)
  if (r.status === 'ok' && rangeError(r.value) === null) {
    liveValue.value = r.value
    emitValue(toNumber(dec(r.value)))
  } else {
    // خالی، نادرست یا مبهم («۲۰۸» هنوز سانت یا متر؟): مقدار قبلی نماند، وگرنه مثلاً مساحت زندهٔ والد با
    // «۲» حساب می‌شد در حالی که کاربر «۲۰۸» می‌بیند. پیام و پرسش هنگام خروج یا Enter می‌آیند.
    liveValue.value = null
    if (lastEmitted !== null) emitValue(null)
  }
}

function onBlur() {
  focused.value = false
  if (needsCommit()) commit()
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter') {
    if (pending.value) {
      event.preventDefault()
      choose(0)
      return
    }
    // اگر پرسش یا خطایی ماند، فرم ارسال نشود؛ متن دست‌نخورده دوباره خوانده نمی‌شود
    if (needsCommit() && commit()) event.preventDefault()
  } else if (event.key === 'Escape' && pending.value) {
    event.preventDefault()
    event.stopPropagation()
    choose(1)
  }
}

function setCurrency(unit: CurrencyUnit) {
  if (props.currency !== unit) emit('update:currency', unit)
}

// ── سطر حروف ─────────────────────────────────────────────────────────

const wordsText = computed(() => {
  if (!showWords.value) return ''
  const source = focused.value ? liveValue.value : props.modelValue
  const d = tryDec(source)
  if (!d) return ''
  if (props.kind !== 'money') return moneyWords(d, suffixText.value)
  if (isRial.value) {
    const toman = mul(d, '0.1')
    return `${moneyWords(d, 'ریال')} · ${moneyWords(toman, 'تومان')}`
  }
  return moneyWords(d, unitLabel.value)
})

const ariaDescribedBy = computed(
  () =>
    [
      props.describedBy,
      errorText.value ? errorId : null,
      pending.value ? questionId : null,
      wordsText.value ? wordsId : null,
    ]
      .filter(Boolean)
      .join(' ') || undefined,
)

/** پیام مانع ثبت (خطا یا پرسش بی‌جواب) یا null؛ والد پیش از ارسال صدا می‌زند. */
function validate(): string | null {
  // خروج از خانه خودش ثبت کرده؛ فقط متنِ در حال تایپ را ثبت کن
  if (focused.value && needsCommit()) commit()
  if (pending.value) return pending.value.message
  if (errorText.value) return errorText.value
  if (props.required && (props.modelValue === null || props.modelValue === undefined)) {
    const label = props.label ?? props.ariaLabel
    return label ? message('E_REQUIRED', { label }) : message('E_VALIDATION')
  }
  return null
}

defineExpose({ validate, focus: () => input.value?.focus() })
</script>

<template>
  <div>
    <div class="flex items-stretch gap-2">
      <div class="relative min-w-0 flex-1">
        <input
          :id="inputId"
          ref="input"
          type="text"
          :inputmode="kind === 'integer' ? 'numeric' : 'decimal'"
          dir="ltr"
          autocomplete="off"
          spellcheck="false"
          :value="inputValue"
          :placeholder="placeholder"
          :disabled="disabled"
          :readonly="readonly"
          :required="required"
          :aria-label="ariaLabel"
          :aria-invalid="invalid || errorText !== null || undefined"
          :aria-describedby="ariaDescribedBy"
          :enterkeyhint="enterkeyhint"
          data-numeric
          class="min-h-11 w-full rounded-lg border bg-surface-container-lowest px-3 text-end text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary disabled:opacity-60"
          :class="[
            invalid || errorText ? 'border-error' : 'border-outline-variant',
            suffixText && !showRialToggle ? 'pl-16' : '',
          ]"
          @focus="onFocus"
          @input="onInput"
          @blur="onBlur"
          @keydown="onKeydown"
        />
        <span
          v-if="suffixText && !showRialToggle"
          class="pointer-events-none absolute inset-y-0 left-3 flex items-center text-sm text-on-surface-variant"
          aria-hidden="true"
        >{{ suffixText }}</span>
      </div>

      <!-- رسید ریالی در کارگاه تومانی -->
      <div
        v-if="showRialToggle"
        role="radiogroup"
        aria-label="واحد مبلغ"
        class="flex shrink-0 rounded-lg border border-outline-variant p-0.5"
      >
        <button
          v-for="option in [{ unit: CurrencyUnit.Toman, label: 'تومان' }, { unit: CurrencyUnit.Rial, label: 'ریال' }]"
          :key="option.unit"
          type="button"
          role="radio"
          :aria-checked="(option.unit === CurrencyUnit.Rial) === isRial"
          :tabindex="(option.unit === CurrencyUnit.Rial) === isRial ? 0 : -1"
          :disabled="disabled || readonly"
          class="min-h-10 min-w-11 rounded-md px-3 text-sm"
          :class="(option.unit === CurrencyUnit.Rial) === isRial ? 'bg-primary font-semibold text-on-primary' : 'text-on-surface-variant'"
          @click="setCurrency(option.unit)"
          @keydown.left.prevent="setCurrency(isRial ? CurrencyUnit.Toman : CurrencyUnit.Rial)"
          @keydown.right.prevent="setCurrency(isRial ? CurrencyUnit.Toman : CurrencyUnit.Rial)"
        >{{ option.label }}</button>
      </div>
    </div>

    <!-- پرسش: دو دکمهٔ ۴۴ پیکسلی؛ راهنمای Enter/Esc فقط با موس -->
    <div
      v-if="pending"
      :id="questionId"
      role="group"
      :aria-label="pending.message"
      class="mt-2 rounded-lg border border-secondary/40 bg-secondary-container/40 p-3"
    >
      <p class="text-sm font-medium text-on-surface" aria-live="assertive">{{ pending.message }}</p>
      <div class="mt-2 flex flex-wrap gap-2">
        <button
          v-for="(choice, index) in pending.choices"
          :key="index"
          type="button"
          class="min-h-11 rounded-lg px-4 text-sm"
          :class="index === 0 ? 'bg-primary font-semibold text-on-primary hover:bg-primary-hover' : 'border border-outline-variant bg-surface-container-lowest text-on-surface hover:bg-surface-container'"
          @mousedown.prevent
          @click="choose(index)"
        >
          {{ choice.label }}
          <kbd class="ms-1 hidden rounded border border-current/30 px-1 text-xs opacity-70 pointer-fine:inline" dir="ltr" aria-hidden="true">{{ index === 0 ? 'Enter' : 'Esc' }}</kbd>
        </button>
      </div>
    </div>

    <p v-if="errorText" :id="errorId" class="mt-1 flex items-start gap-1 text-xs text-error">
      <svg class="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" focusable="false"><use :href="'/icons.svg#error'" /></svg>
      <span>{{ errorText }}</span>
    </p>

    <p
      v-if="showWords"
      :id="wordsId"
      aria-live="polite"
      class="mt-1 min-h-5 text-xs leading-5 text-on-surface-variant"
    >{{ wordsText }}</p>
  </div>
</template>
