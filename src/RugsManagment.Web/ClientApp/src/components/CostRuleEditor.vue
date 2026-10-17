<script setup lang="ts">
/**
 * ویرایشگر قاعدهٔ هزینه: ثابت / ×مساحت / ×فوت مربع / ×طول / ×عرض / ترکیبی / دستی + تخفیف یا اضافه.
 * پیش‌نمایش «در بک‌اند» محاسبه می‌شود (POST /api/pricing/preview) تا با نتیجهٔ نهایی یکی باشد.
 * توضیحات فرمول از سرور می‌آید. با toPayload() مقدار نهایی برای ثبت مرحله به والد داده می‌شود.
 *
 * قاعدهٔ مهم: تا کاربر به بخش قیمت‌گذاری دست نزده، toPayload برای روش/نرخ/مبلغ null می‌فرستد.
 * null یعنی «سرور تصمیم بگیرد»: قیمتی که قبلاً روی مرحله ذخیره شده می‌ماند (همان که اینجا پیش‌پر شده)؛
 * مرحلهٔ بدون قیمتِ ذخیره‌شده نرخ توافقیِ انجام‌دهنده، وگرنه نرخ پیش‌فرض نوع مرحله را می‌گیرد.
 * قبلاً همیشه «نرخ × مساحت» با نرخ ۰ فرستاده می‌شد؛ پس تکمیل مرحله بدون تایپ نرخ، هزینهٔ مرحله را
 * صفر می‌کرد و نرخ توافقیِ انجام‌دهنده هرگز اعمال نمی‌شد.
 *
 * مبلغ دستی: اگر مرحله مبلغ دستی دارد، ویرایشگر در حالت «دستی» با همان مبلغ باز می‌شود. خاموش کردن حالت
 * دستی و انتخاب روش/نرخ، clearManualOverride: true هم می‌فرستد؛ بدون آن مبلغ دستی (که بر هر روشی مقدم است)
 * می‌ماند و روش انتخاب‌شده بی‌اثر بود، در حالی که پیش‌نمایش هزینهٔ روش را نشان می‌داد.
 */
import { computed, reactive, ref, watch } from 'vue'
import { api } from '@/lib/api'
import { moneyVisible } from '@/lib/context'
import { currencyLabel } from '@/lib/format'
import { formatThousands } from '@/lib/money'
import AppIcon from '@/components/AppIcon.vue'
import MoneyInput from '@/components/MoneyInput.vue'

/**
 * مبلغ‌ها فقط برای Business (بازرگان و مدیر) است (A.5). برای انباردار ویرایشگر چیزی نشان نمی‌دهد،
 * پیش‌نمایش را از سرور نمی‌خواهد و toPayload همه را null می‌فرستد؛ سرور هم مبلغی از او نمی‌پذیرد و
 * نرخ توافقیِ انجام‌دهنده یا نرخ پیش‌فرض مرحله را می‌گذارد.
 */
const canSeeMoney = moneyVisible()
const unit = currencyLabel()

/**
 * قیمت‌گذاری فعلی مرحله (فیلدهای RugWorkflowStepDto) و پیش‌فرض نوع مرحله — فقط برای پر کردن اولیه،
 * تا ویرایشگر همان چیزی را نشان دهد که سرور اگر چیزی عوض نشود حساب می‌کند.
 */
interface StepPricing {
  /** سرور این enum را به‌صورت نام («PerSquareMeter») می‌فرستد؛ عدد هم پذیرفته می‌شود */
  appliedPricingModel?: string | number | null
  appliedUnitRate?: number | null
  pricingConfigJson?: string | null
  /** مبلغ دستیِ ذخیره‌شده؛ بر روش و نرخ مقدم است */
  manualCostOverride?: number | null
  adjustment?: number | null
  defaultPricingModel?: number | null
  defaultUnitRate?: number | null
}

/**
 * width/length اندازهٔ یک تخته و quantity تعداد تخته‌هایی است که مرحله رویشان قیمت می‌خورد (عکس قیمت‌گذاری مرحله، وگرنه
 * فرش). سرور قیمت یک تخته × تعداد را حساب می‌کند و تخفیف و کف صفر را روی کل ردیف می‌گذارد، مثل ثبت مرحله.
 */
const props = withDefaults(
  defineProps<{ width: number; length: number; quantity?: number; current?: StepPricing | null }>(),
  { quantity: 1 },
)

interface CombinedRow { uid: number; model: number; rate?: number }
interface PreviewResult { base: number; adjustment: number; total: number; formula: string; components: { label: string; amount: number }[] }

/** بدنهٔ قیمت‌گذاری برای advance/pricing — null یعنی «دست نزن / سرور تصمیم بگیرد». */
interface CostPayload {
  manualCostOverride: number | null
  pricingModel: number | null
  unitRate: number | null
  pricingConfigJson: string | null
  adjustment: number | null
  /** true فقط وقتی مرحله مبلغ دستی دارد و کاربر به روش/نرخ برگشته؛ null یعنی «مبلغ دستی را نگه دار» */
  clearManualOverride: boolean | null
}

const singleModels = [
  { v: 0, label: 'ثابت' },
  { v: 1, label: 'نرخ × مساحت' },
  { v: 2, label: 'نرخ × فوت مربع' },
  { v: 4, label: 'نرخ × طول' },
  { v: 5, label: 'نرخ × عرض' },
]
const singleCodes = new Set(singleModels.map((m) => m.v))

/** نام‌های enum سمت سرور (StepPricingModel) → کد عددی */
const MODEL_CODES: Record<string, number> = {
  Fixed: 0, PerSquareMeter: 1, PerSquareFoot: 2, CustomFormula: 3, PerLength: 4, PerWidth: 5, Combined: 6,
}
const COMBINED = 6

function toModelCode(v: string | number | null | undefined): number | null {
  if (v === null || v === undefined || v === '') return null
  if (typeof v === 'number') return v
  const named = MODEL_CODES[v]
  if (named !== undefined) return named
  const n = Number(v)
  return Number.isInteger(n) ? n : null
}

const state = reactive({
  manual: false,
  combinedMode: false,
  model: 1,
  unitRate: undefined as number | undefined,
  manualCost: undefined as number | undefined,
  adjustment: undefined as number | undefined,
  adjustKind: 'discount' as 'discount' | 'fee',
})
const combined = ref<CombinedRow[]>([])
let counter = 0

/** جزءهای ترکیبی ذخیره‌شده؛ سرور کلیدها را بدون حساسیت به بزرگی حرف می‌خواند، اینجا هم همین‌طور. */
function parseCombined(json: string | null | undefined): CombinedRow[] {
  if (!json) return []
  try {
    const parsed = JSON.parse(json) as Record<string, unknown>
    const items = parsed.items ?? parsed.Items
    if (!Array.isArray(items)) return []
    return items.map((raw) => {
      const it = (raw ?? {}) as Record<string, unknown>
      const rate = Number(it.rate ?? it.Rate)
      return { uid: ++counter, model: Number(it.model ?? it.Model ?? 0), rate: Number.isFinite(rate) ? rate : undefined }
    })
  } catch {
    return []
  }
}

/**
 * پر کردن اولیه با همان اولویت سرور: مبلغ دستی، وگرنه مقدار اعمال‌شده روی مرحله، وگرنه پیش‌فرض نوع مرحله.
 *
 * روش و نرخِ «تک‌فرمول» همیشه پر می‌شوند — حتی زیر حالت ترکیبی یا دستی — تا اگر کاربر تیک ترکیبی/دستی را
 * بردارد، همان عددی را ببیند و بفرستد که پیش‌نمایش حساب می‌کند (نه فیلد خالی‌ای که سرور با نرخ کهنه پر کند).
 */
function prefill(c: StepPricing | null | undefined) {
  if (!c) return
  const applied = toModelCode(c.appliedPricingModel)
  const rows = applied === COMBINED ? parseCombined(c.pricingConfigJson) : []

  // ترکیبیِ دارای جزء: زیرش پیش‌فرض نوع مرحله. «ترکیبیِ بدون جزء» و «فرمول سفارشی» در موتور هزینه
  // مثل «ثابت» حساب می‌شوند، پس همان‌طور نشان داده می‌شوند.
  const single = applied === null || rows.length > 0 ? c.defaultPricingModel ?? null : applied
  if (single !== null) state.model = singleCodes.has(single) ? single : 0
  const rate = c.appliedUnitRate ?? c.defaultUnitRate
  state.unitRate = rate ?? undefined

  if (rows.length > 0) {
    state.combinedMode = true
    combined.value = rows
  }

  if (c.manualCostOverride !== null && c.manualCostOverride !== undefined) {
    state.manual = true
    state.manualCost = c.manualCostOverride
  }

  if (c.adjustment) {
    state.adjustKind = c.adjustment < 0 ? 'discount' : 'fee'
    state.adjustment = Math.abs(c.adjustment)
  }
}

prefill(props.current)

/** مرحله همین حالا مبلغ دستی دارد؟ (ویرایشگر با هر بار باز شدن مودال از نو ساخته می‌شود) */
const hasStoredManual = props.current?.manualCostOverride !== null && props.current?.manualCostOverride !== undefined
/** قیمتی صریح روی مرحله ذخیره شده؟ — فقط برای متن راهنما */
const hasStoredPricing = hasStoredManual
  || (props.current?.appliedPricingModel ?? null) !== null
  || (props.current?.appliedUnitRate ?? null) !== null
  || !!props.current?.pricingConfigJson

// دست‌خوردگی بعد از پر کردن اولیه ردیابی می‌شود؛ پس خودِ prefill «تغییر کاربر» حساب نمی‌شود
const pricingTouched = ref(false)
const adjustmentTouched = ref(false)
watch(() => [state.manual, state.combinedMode, state.model, state.unitRate, state.manualCost], () => { pricingTouched.value = true })
watch(combined, () => { pricingTouched.value = true }, { deep: true })
watch(() => [state.adjustment, state.adjustKind], () => { adjustmentTouched.value = true })

const preview = ref<PreviewResult | null>(null)
const previewing = ref(false)

function signedAdjustment(): number {
  const a = state.adjustment ?? 0
  return state.adjustKind === 'discount' ? -a : a
}

function combinedJson(): string | null {
  if (!state.combinedMode) return null
  return JSON.stringify({ items: combined.value.map((c) => ({ model: c.model, rate: c.rate ?? 0 })) })
}

function currentModel(): number {
  return state.combinedMode ? COMBINED : state.model
}

/**
 * مقدار نهایی برای ثبت مرحله (والد این را در payload مرحله می‌گذارد).
 * بخش دست‌نخورده null می‌فرستد تا قیمتِ ذخیره‌شده بماند (یا تزریق نرخ توافقی/پیش‌فرض در سرور اجرا شود) و
 * تعدیلِ فعلی بماند. اگر کاربر به قیمت‌گذاری دست زده باشد، دقیقاً «همان چیزی که پیش‌نمایش حساب می‌کند»
 * فرستاده می‌شود: روش و نرخ با هم، و نرخِ خالی همان ۰ِ پیش‌نمایش — null بود و سرور روشِ تازه را با نرخِ
 * کهنه یا پیش‌فرض ترکیب می‌کرد (پیش‌نمایش ۰، هزینهٔ ذخیره‌شده چند میلیون).
 */
function toPayload(): CostPayload {
  const adjustment = adjustmentTouched.value ? signedAdjustment() : null
  const untouched = { manualCostOverride: null, pricingModel: null, unitRate: null, pricingConfigJson: null, clearManualOverride: null }

  // غیر Business هیچ عضو پولی نمی‌فرستد (null یعنی «سرور تصمیم بگیرد»؛ مقدار غیر null ۴۰۳ می‌گیرد)
  if (!canSeeMoney) return { ...untouched, adjustment: null }

  if (!pricingTouched.value) {
    return { ...untouched, adjustment }
  }
  if (state.manual) {
    return { ...untouched, manualCostOverride: state.manualCost ?? 0, adjustment }
  }

  // کاربر از حالت دستی بیرون آمده: مبلغ دستیِ ذخیره‌شده باید صریحاً پاک شود، وگرنه بر روش انتخاب‌شده مقدم می‌ماند
  const clearManualOverride = hasStoredManual ? true : null
  if (state.combinedMode) {
    return { ...untouched, pricingModel: COMBINED, pricingConfigJson: combinedJson(), adjustment, clearManualOverride }
  }
  return { ...untouched, pricingModel: state.model, unitRate: state.unitRate ?? 0, adjustment, clearManualOverride }
}
defineExpose({ toPayload })

let timer: ReturnType<typeof setTimeout> | undefined
async function refresh() {
  previewing.value = true
  try {
    preview.value = await api.post<PreviewResult>('/api/pricing/preview', {
      widthMeters: props.width, lengthMeters: props.length, quantity: Math.max(1, Math.trunc(props.quantity)),
      model: currentModel(),
      unitRate: state.manual ? null : state.unitRate ?? 0,
      combinedJson: combinedJson(),
      manualCost: state.manual ? state.manualCost ?? 0 : null,
      adjustment: signedAdjustment(),
    })
  } catch { /* پیش‌نمایش خطا را بی‌صدا رد کن */ }
  finally { previewing.value = false }
}
function schedule() { clearTimeout(timer); timer = setTimeout(refresh, 250) }

if (canSeeMoney) watch([state, combined], schedule, { deep: true, immediate: true })

function addCombined(model: number) { combined.value.push({ uid: ++counter, model, rate: undefined }); if (!state.combinedMode) state.combinedMode = true }
function removeCombined(uid: number) { combined.value = combined.value.filter((c) => c.uid !== uid) }

const modeLabel = computed(() => (state.manual ? 'دستی' : state.combinedMode ? 'ترکیبی' : 'تک‌فرمول'))
</script>

<template>
  <div v-if="canSeeMoney" class="space-y-3">
    <div class="flex flex-wrap items-center gap-3 text-sm">
      <label class="flex items-center gap-1.5"><input v-model="state.manual" type="checkbox" class="h-4 w-4" /> مبلغ دستی</label>
      <label v-if="!state.manual" class="flex items-center gap-1.5"><input v-model="state.combinedMode" type="checkbox" class="h-4 w-4" /> ترکیبی</label>
      <span class="rounded-full bg-surface-container px-2 py-0.5 text-xs text-on-surface-variant">{{ modeLabel }}</span>
    </div>
    <p v-if="!pricingTouched" class="text-xs text-on-surface-variant">
      <template v-if="hasStoredPricing">
        اگر چیزی را تغییر ندهید، همین قیمتِ ذخیره‌شده می‌ماند (مگر انجام‌دهنده را عوض کنید؛ آن‌وقت نرخ توافقیِ او اعمال می‌شود).
      </template>
      <template v-else>
        اگر چیزی را تغییر ندهید، نرخ توافقیِ انجام‌دهنده (اگر تعریف شده باشد) و گرنه همین نرخ اعمال می‌شود.
      </template>
    </p>

    <!-- دستی -->
    <div v-if="state.manual">
      <span class="mb-1 block text-sm">مبلغ</span>
      <MoneyInput v-model="state.manualCost" :suffix="unit" />
    </div>

    <!-- ترکیبی -->
    <div v-else-if="state.combinedMode" class="space-y-2">
      <div v-for="c in combined" :key="c.uid" class="flex items-center gap-2">
        <select v-model.number="c.model" class="fld flex-1">
          <option v-for="m in singleModels.filter(x => x.v !== 0)" :key="m.v" :value="m.v">{{ m.label }}</option>
          <option :value="0">ثابت</option>
        </select>
        <div class="flex-1"><MoneyInput v-model="c.rate" :suffix="unit" /></div>
        <button type="button" @click="removeCombined(c.uid)"
                class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-error hover:bg-error-container">
          <AppIcon name="close" class="h-4 w-4" label="حذف این بند" />
        </button>
      </div>
      <div class="flex flex-wrap gap-1.5">
        <button v-for="m in singleModels" :key="m.v" type="button" @click="addCombined(m.v)"
                class="rounded-lg border border-outline-variant px-2.5 py-1 text-xs hover:bg-surface-container">+ {{ m.label }}</button>
      </div>
    </div>

    <!-- تک‌فرمول -->
    <div v-else class="grid grid-cols-2 gap-3">
      <label class="block"><span class="mb-1 block text-sm">روش</span>
        <select v-model.number="state.model" class="fld">
          <option v-for="m in singleModels" :key="m.v" :value="m.v">{{ m.label }}</option>
        </select>
      </label>
      <label class="block"><span class="mb-1 block text-sm">نرخ واحد</span><MoneyInput v-model="state.unitRate" :suffix="unit" /></label>
    </div>

    <!-- تخفیف / اضافه -->
    <div class="grid grid-cols-2 gap-3">
      <label class="block"><span class="mb-1 block text-sm">تعدیل</span>
        <select v-model="state.adjustKind" class="fld"><option value="discount">تخفیف</option><option value="fee">هزینهٔ اضافه</option></select>
      </label>
      <label class="block"><span class="mb-1 block text-sm">مبلغ تعدیل</span><MoneyInput v-model="state.adjustment" :suffix="unit" /></label>
    </div>

    <!-- پیش‌نمایش بک‌اند -->
    <div class="rounded-lg border border-dashed border-outline-variant bg-surface-container/40 px-4 py-3 text-sm">
      <div v-if="preview" class="space-y-1">
        <div v-for="(c, i) in preview.components" :key="i" class="flex justify-between text-on-surface-variant">
          <span>{{ c.label }}</span><span dir="ltr">{{ formatThousands(c.amount) }}</span>
        </div>
        <div v-if="preview.adjustment !== 0" class="flex justify-between text-on-surface-variant">
          <span>{{ preview.adjustment < 0 ? 'تخفیف' : 'اضافه' }}</span><span dir="ltr">{{ formatThousands(preview.adjustment) }}</span>
        </div>
        <div class="flex justify-between border-t border-outline-variant pt-1 font-bold">
          <span>هزینهٔ نهایی</span><span dir="ltr">{{ formatThousands(preview.total) }} {{ unit }}</span>
        </div>
      </div>
      <div v-else class="text-center text-on-surface-variant">{{ previewing ? 'در حال محاسبه…' : 'مقادیر را وارد کنید' }}</div>
    </div>
  </div>
</template>

<style scoped>
.fld { width: 100%; border-radius: 0.5rem; border: 1px solid var(--color-outline-variant); padding: 0.5rem 0.75rem; outline: none; }
.fld:focus { border-color: var(--color-primary); box-shadow: 0 0 0 1px var(--color-primary); }
</style>
