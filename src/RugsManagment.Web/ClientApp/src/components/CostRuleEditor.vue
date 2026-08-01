<script setup lang="ts">
/**
 * ویرایشگر قاعدهٔ هزینه: ثابت / ×مساحت / ×طول / ×عرض / ترکیبی / دستی + تخفیف یا اضافه.
 * پیش‌نمایش «در بک‌اند» محاسبه می‌شود (POST /api/pricing/preview) تا با نتیجهٔ نهایی یکی باشد.
 * توضیحات فرمول از سرور می‌آید. با toPayload() مقدار نهایی برای ثبت مرحله به والد داده می‌شود.
 */
import { computed, reactive, ref, watch } from 'vue'
import { api } from '@/lib/api'
import { formatThousands } from '@/lib/money'
import MoneyInput from '@/components/MoneyInput.vue'

const props = defineProps<{ width: number; length: number }>()

interface CombinedRow { uid: number; model: number; rate?: number }
interface PreviewResult { base: number; adjustment: number; total: number; formula: string; components: { label: string; amount: number }[] }

const singleModels = [
  { v: 0, label: 'ثابت' },
  { v: 1, label: 'نرخ × مساحت' },
  { v: 4, label: 'نرخ × طول' },
  { v: 5, label: 'نرخ × عرض' },
]

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
  return state.combinedMode ? 6 : state.model
}

/** مقدار نهایی برای ثبت مرحله (والد این را در payload مرحله می‌گذارد). */
function toPayload() {
  if (state.manual) {
    return { manualCostOverride: state.manualCost ?? 0, pricingModel: null, unitRate: null, pricingConfigJson: null, adjustment: signedAdjustment() }
  }
  if (state.combinedMode) {
    return { manualCostOverride: null, pricingModel: 6, unitRate: null, pricingConfigJson: combinedJson(), adjustment: signedAdjustment() }
  }
  return { manualCostOverride: null, pricingModel: state.model, unitRate: state.unitRate ?? 0, pricingConfigJson: null, adjustment: signedAdjustment() }
}
defineExpose({ toPayload })

let timer: ReturnType<typeof setTimeout> | undefined
async function refresh() {
  previewing.value = true
  try {
    preview.value = await api.post<PreviewResult>('/api/pricing/preview', {
      widthMeters: props.width, lengthMeters: props.length,
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

watch([state, combined], schedule, { deep: true, immediate: true })

function addCombined(model: number) { combined.value.push({ uid: ++counter, model, rate: undefined }); if (!state.combinedMode) state.combinedMode = true }
function removeCombined(uid: number) { combined.value = combined.value.filter((c) => c.uid !== uid) }

const modeLabel = computed(() => (state.manual ? 'دستی' : state.combinedMode ? 'ترکیبی' : 'تک‌فرمول'))
</script>

<template>
  <div class="space-y-3">
    <div class="flex flex-wrap items-center gap-3 text-sm">
      <label class="flex items-center gap-1.5"><input v-model="state.manual" type="checkbox" class="h-4 w-4" /> مبلغ دستی</label>
      <label v-if="!state.manual" class="flex items-center gap-1.5"><input v-model="state.combinedMode" type="checkbox" class="h-4 w-4" /> ترکیبی</label>
      <span class="rounded-full bg-surface-container px-2 py-0.5 text-xs text-on-surface-variant">{{ modeLabel }}</span>
    </div>

    <!-- دستی -->
    <div v-if="state.manual">
      <span class="mb-1 block text-sm">مبلغ (تومان)</span>
      <MoneyInput v-model="state.manualCost" suffix="تومان" />
    </div>

    <!-- ترکیبی -->
    <div v-else-if="state.combinedMode" class="space-y-2">
      <div v-for="c in combined" :key="c.uid" class="flex items-center gap-2">
        <select v-model.number="c.model" class="fld flex-1">
          <option v-for="m in singleModels.filter(x => x.v !== 0)" :key="m.v" :value="m.v">{{ m.label }}</option>
          <option :value="0">ثابت</option>
        </select>
        <div class="flex-1"><MoneyInput v-model="c.rate" suffix="تومان" /></div>
        <button type="button" @click="removeCombined(c.uid)" class="text-error">✕</button>
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
      <label class="block"><span class="mb-1 block text-sm">نرخ واحد</span><MoneyInput v-model="state.unitRate" suffix="تومان" /></label>
    </div>

    <!-- تخفیف / اضافه -->
    <div class="grid grid-cols-2 gap-3">
      <label class="block"><span class="mb-1 block text-sm">تعدیل</span>
        <select v-model="state.adjustKind" class="fld"><option value="discount">تخفیف</option><option value="fee">هزینهٔ اضافه</option></select>
      </label>
      <label class="block"><span class="mb-1 block text-sm">مبلغ تعدیل</span><MoneyInput v-model="state.adjustment" suffix="تومان" /></label>
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
          <span>هزینهٔ نهایی</span><span dir="ltr">{{ formatThousands(preview.total) }} تومان</span>
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
