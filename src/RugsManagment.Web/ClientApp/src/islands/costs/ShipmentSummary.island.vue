<script setup lang="ts">
/**
 * جزیرهٔ `shipment-summary` — زبانهٔ «حساب محموله» (H.1، D.6؛ Business):
 * - «فروش این محموله: [__] دلار × نرخ [__] = … تومان» → `PUT /api/shipments/{id}/revenue` با نسخهٔ محموله؛
 * - «قیمت تمام‌شدهٔ هر متر» و «سود» (فروش − جمع بهای فرش‌ها) از `GET …/profit` (`ShipmentProfitDto`)؛
 * - ریز بها: خرید، کارهای کارگاه، هزینه‌ها و جمع، با تخته، متراژ و دلار اظهار.
 *
 * فروش با قاعدهٔ D.3 یک بار به تومان گرد می‌شود (`round(مبلغ × نرخ، رقم اعشار کارگاه)`)؛ پیش‌نمایش همین‌جا با `lib/num.ts`
 * است و عدد نهایی همیشه از پاسخ سرور. محمولهٔ قطعی هم فروش و هزینهٔ دیر می‌پذیرد؛ فقط سود عوض می‌شود، نه اظهار.
 * پس از هر ثبت یا حذف هزینه (رویداد `rugs:costs-changed`) دوباره می‌خواند.
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { baseCurrency, baseCurrencyWord, currencyWord, n, piecesText } from '@/components/costs/costText'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import { mul, round, toNumber } from '@/lib/num'
import { COSTS_CHANGED_EVENT, CurrencyUnit, type SaveRevenueRequest, type ShipmentProfit } from '@/lib/types/costs'
import { ShipmentStatus, type Shipment } from '@/lib/types/shipments'
import { toast } from '@/lib/ui'

const props = defineProps<{ shipmentId: string }>()

const ctx = appContext()
const tenantCurrency = baseCurrency()

const loading = ref(true)
const error = ref('')
const profit = ref<ShipmentProfit | null>(null)
const shipment = ref<Shipment | null>(null)
const saving = ref(false)
const saveError = ref('')

const revenue = reactive({
  amount: null as number | null,
  currency: CurrencyUnit.Usd as CurrencyUnit,
  fx: null as number | null,
})

const isUsd = computed(() => revenue.currency === CurrencyUnit.Usd)
const finalized = computed(() => profit.value?.finalized ?? shipment.value?.status === ShipmentStatus.Finalized)

/** فروش به ارز کارگاه برای پیش‌نمایش (سرور همان را حساب می‌کند). */
const revenueBasePreview = computed<number | null>(() => {
  if (revenue.amount === null) return null
  if (!isUsd.value) return revenue.amount
  if (revenue.fx === null) return null
  return toNumber(round(mul(revenue.amount, revenue.fx), ctx.moneyScale))
})

function fillRevenue(p: ShipmentProfit, s: Shipment | null) {
  if (p.revenueAmount !== undefined && p.revenueAmount !== null) {
    revenue.amount = p.revenueAmount
    revenue.currency = p.revenueCurrency ?? CurrencyUnit.Usd
    revenue.fx = p.revenueFx ?? s?.usdRate ?? null
  } else if (revenue.fx === null) {
    revenue.fx = s?.usdRate ?? null
  }
}

async function load(first = false) {
  if (first) loading.value = true
  error.value = ''
  try {
    const [p, s] = await Promise.all([
      api.get<ShipmentProfit>(`/api/shipments/${props.shipmentId}/profit`),
      api.get<Shipment>(`/api/shipments/${props.shipmentId}`),
    ])
    profit.value = p
    shipment.value = s
    if (first) fillRevenue(p, s)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

async function saveRevenue() {
  saveError.value = ''
  if (isUsd.value && revenue.amount !== null && !revenue.fx) {
    saveError.value = message('E_REVENUE_FX_REQUIRED')
    return
  }
  if (!shipment.value) return
  saving.value = true
  try {
    const body: SaveRevenueRequest = {
      amount: revenue.amount,
      currency: revenue.currency,
      fx: isUsd.value ? revenue.fx : null,
      version: shipment.value.version,
    }
    profit.value = await api.put<ShipmentProfit>(`/api/shipments/${props.shipmentId}/revenue`, body)
    // نسخهٔ تازهٔ محموله برای ذخیرهٔ بعدی
    shipment.value = await api.get<Shipment>(`/api/shipments/${props.shipmentId}`)
    toast.success(message('I_REVENUE_SAVED'))
  } catch (e) {
    saveError.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

function money(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${n(value)} ${baseCurrencyWord()}`
}

const rows = computed(() => {
  const p = profit.value
  if (!p) return []
  return [
    { key: 'purchase', label: 'خرید', value: p.purchase },
    { key: 'steps', label: 'کارهای کارگاه', value: p.steps },
    { key: 'expenses', label: 'هزینه‌ها', value: p.expenses },
  ]
})

function onChanged() {
  void load()
}

onMounted(() => {
  window.addEventListener(COSTS_CHANGED_EVENT, onChanged)
  void load(true)
})
onBeforeUnmount(() => window.removeEventListener(COSTS_CHANGED_EVENT, onChanged))
</script>

<template>
  <div class="space-y-5" :data-shipment-summary="shipmentId" data-numeric>
    <div v-if="loading" class="space-y-3" aria-hidden="true">
      <div class="skeleton h-24 w-full" />
      <div class="skeleton h-40 w-full" />
    </div>

    <p v-else-if="error" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-on-error-container" role="alert">
      <AppIcon name="error" class="mt-0.5 h-4 w-4" />
      {{ error }}
    </p>

    <template v-else-if="profit">
      <!-- فروش -->
      <section class="space-y-3 rounded-xl border border-outline-variant bg-surface-container-lowest p-5" aria-labelledby="revenue-title">
        <h2 id="revenue-title" class="flex items-center gap-2 text-base font-semibold text-primary">
          <AppIcon name="dollar" class="h-5 w-5" />
          فروش این محموله
        </h2>
        <div role="radiogroup" aria-label="ارز فروش" class="flex flex-wrap gap-2">
          <button
            v-for="c in [CurrencyUnit.Usd, tenantCurrency]"
            :key="c"
            type="button"
            role="radio"
            :aria-checked="revenue.currency === c"
            :tabindex="revenue.currency === c ? 0 : -1"
            class="inline-flex min-h-11 items-center gap-2 rounded-lg border px-3 text-sm"
            :class="revenue.currency === c ? 'border-primary bg-primary/10 font-semibold' : 'border-outline-variant hover:bg-surface-container'"
            @click="revenue.currency = c"
            @keydown.left.prevent="revenue.currency = revenue.currency === CurrencyUnit.Usd ? tenantCurrency : CurrencyUnit.Usd"
            @keydown.right.prevent="revenue.currency = revenue.currency === CurrencyUnit.Usd ? tenantCurrency : CurrencyUnit.Usd"
          >
            <AppIcon v-if="revenue.currency === c" name="check" class="h-4 w-4" />
            {{ currencyWord(c) }}
          </button>
        </div>
        <div class="grid items-start gap-3 sm:grid-cols-[1fr_auto_1fr]">
          <Field label="مبلغ فروش" v-slot="f">
            <NumberField :id="f.id" v-model="revenue.amount" :kind="isUsd ? 'usd' : 'money'" :described-by="f.describedBy" label="مبلغ فروش" />
          </Field>
          <span v-if="isUsd" class="hidden pt-9 text-lg text-on-surface-variant sm:block" aria-hidden="true">×</span>
          <Field v-if="isUsd" :label="`نرخ دلار (${currencyWord(tenantCurrency)})`" v-slot="f">
            <NumberField :id="f.id" v-model="revenue.fx" kind="number" :scale="4" :suffix="currencyWord(tenantCurrency)" :described-by="f.describedBy" label="نرخ دلار" />
          </Field>
        </div>
        <p class="text-base" aria-live="polite" data-revenue-preview>
          = <strong>{{ money(revenueBasePreview) }}</strong>
        </p>
        <p v-if="saveError" class="flex items-start gap-2 text-sm text-error" role="alert">
          <AppIcon name="error" class="mt-0.5 h-4 w-4" />
          {{ saveError }}
        </p>
        <Button variant="primary" icon="check" :loading="saving" @click="saveRevenue">ذخیرهٔ فروش</Button>
      </section>

      <!-- هر متر و سود -->
      <div class="grid gap-4 sm:grid-cols-2">
        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5">
          <p class="text-sm text-on-surface-variant">قیمت تمام‌شدهٔ هر متر</p>
          <p class="mt-1 text-2xl font-bold" data-cost-per-m2>{{ money(profit.costPerM2) }}</p>
        </div>
        <div
          class="rounded-xl border p-5"
          :class="profit.profit === null ? 'border-outline-variant bg-surface-container-lowest' : profit.profit >= 0 ? 'border-success/40 bg-success/10' : 'border-error/40 bg-error-container'"
        >
          <p class="flex items-center gap-1 text-sm" :class="profit.profit !== null && profit.profit < 0 ? 'text-on-error-container' : 'text-on-surface-variant'">
            <AppIcon v-if="profit.profit !== null" :name="profit.profit >= 0 ? 'chart' : 'warning'" class="h-4 w-4" />
            {{ profit.profit === null ? 'سود' : profit.profit >= 0 ? 'سود' : 'زیان' }}
          </p>
          <p class="mt-1 text-2xl font-bold" :class="profit.profit !== null && profit.profit < 0 ? 'text-on-error-container' : ''" data-profit>
            {{ profit.profit === null ? 'فروش را بنویسید' : money(Math.abs(profit.profit)) }}
          </p>
        </div>
      </div>

      <!-- ریز بها -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5" aria-labelledby="cost-breakdown-title">
        <h2 id="cost-breakdown-title" class="mb-3 text-base font-semibold text-primary">بهای تمام‌شدهٔ محموله</h2>
        <p class="mb-3 text-sm text-on-surface-variant">
          {{ piecesText(profit.pieces) }} · {{ n(profit.areaM2) }} متر · {{ n(profit.usdTotal) }} دلار اظهار
        </p>
        <dl class="space-y-2 text-sm">
          <div v-for="r in rows" :key="r.key" class="flex justify-between gap-3">
            <dt class="text-on-surface-variant">{{ r.label }}</dt>
            <dd>{{ money(r.value) }}</dd>
          </div>
          <div class="flex justify-between gap-3 border-t border-outline-variant pt-2 font-semibold">
            <dt>جمع</dt>
            <dd>{{ money(profit.total) }}</dd>
          </div>
          <div v-if="profit.revenueBase !== null" class="flex justify-between gap-3">
            <dt class="text-on-surface-variant">فروش</dt>
            <dd>{{ money(profit.revenueBase) }}</dd>
          </div>
        </dl>
        <p v-if="finalized" class="mt-3 flex items-start gap-2 text-xs text-on-surface-variant">
          <AppIcon name="lock" class="mt-0.5 h-3.5 w-3.5" />
          {{ message('W_LATE_COST') }}
        </p>
      </section>
    </template>
  </div>
</template>
