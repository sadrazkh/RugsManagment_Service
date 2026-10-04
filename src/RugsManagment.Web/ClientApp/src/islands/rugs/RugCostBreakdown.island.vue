<script setup lang="ts">
/**
 * جزیرهٔ `rug-cost-breakdown` (I.5، فقط Business): «این عدد از کجا آمد؟» — بهای تمام‌شدهٔ فرش سطر به سطر (D.1):
 * سطرهای خرید، کارهای کارگاه (هر مرحله با سهمش؛ فقط انجام‌شده و در حال انجام، هر کدام کف صفر)، سهم ثبت‌های هزینه
 * (از فاز هزینه‌ها) و جمع. همه از `GET /api/rugs/{id}/cost-breakdown`، که همان عددهای ذخیره‌شدهٔ پاس سازگاری را
 * می‌دهد؛ پس جمع ستون‌ها با کارت «بهای تمام‌شده» یکی است.
 *
 * بسته شروع می‌شود (صفحهٔ بازرگان شلوغ نشود) و فقط با باز شدن می‌خواند؛ اگر جزیرهٔ دیگری روی همین فرش ذخیره کند و
 * این بخش باز باشد، دوباره می‌خواند.
 */
import { onMounted, onUnmounted, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { faDate, faDecimal, faMoney, toPersianDigits } from '@/lib/format'
import { stepStatus } from '@/lib/labels'
import { message } from '@/lib/messages'
import AppIcon from '@/components/AppIcon.vue'
import { CostBasis, CostGroup, WorkflowStepStatus, type RugCostBreakdown } from '@/lib/types/rugs'
import { onRugChanged } from './rugEvents'

const props = defineProps<{ rugId: string }>()

const open = ref(false)
const loading = ref(false)
const error = ref('')
const data = ref<RugCostBreakdown | null>(null)

const BASIS_LABELS: Record<number, string> = {
  [CostBasis.Total]: 'کل',
  [CostBasis.PerM2]: 'هر متر',
  [CostBasis.PerPiece]: 'هر تخته',
  [CostBasis.Percent]: 'درصد',
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await api.get<RugCostBreakdown>(`/api/rugs/${props.rugId}/cost-breakdown`)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

function toggle() {
  open.value = !open.value
  if (open.value && !data.value) void load()
}

function rateText(basis: CostBasis, rate: number): string {
  return basis === CostBasis.Percent ? `${faDecimal(rate, 4)}٪` : `${BASIS_LABELS[basis]} ${faMoney(rate)}`
}

function snapshotText(length: number | null, width: number | null, quantity: number | null): string | null {
  if (length === null || width === null) return null
  const size = `${faDecimal(length, 2, 2)} × ${faDecimal(width, 2, 2)}`
  return quantity && quantity > 1 ? `${size} × ${toPersianDigits(quantity)} تخته` : size
}

let stop: (() => void) | null = null
onMounted(() => {
  stop = onRugChanged(props.rugId, 'rug-cost-breakdown', () => {
    if (open.value) void load()
    else data.value = null
  })
})
onUnmounted(() => stop?.())
</script>

<template>
  <div>
    <button
      type="button"
      class="flex min-h-11 w-full items-center justify-between gap-2 rounded-lg px-2 text-sm text-primary hover:bg-surface-container"
      :aria-expanded="open"
      aria-controls="rug-cost-breakdown-body"
      @click="toggle"
    >
      <span class="flex items-center gap-2">
        <AppIcon name="calculator" class="h-4 w-4" />
        این عدد از کجا آمد؟
      </span>
      <AppIcon :name="open ? 'chevron-up' : 'chevron-down'" class="h-4 w-4" />
    </button>

    <div v-if="open" id="rug-cost-breakdown-body" class="mt-2 space-y-4 text-sm" data-numeric>
      <p v-if="loading" class="text-on-surface-variant">در حال بارگذاری…</p>
      <p v-else-if="error" class="rounded-lg bg-error-container px-3 py-2 text-on-error-container" role="alert">{{ error }}</p>

      <template v-else-if="data">
        <p
          v-if="!data.summary.computed"
          class="flex items-start gap-2 rounded-lg bg-warning/10 px-3 py-2 text-xs text-warning"
          role="status"
        >
          <AppIcon name="warning" class="mt-0.5 h-4 w-4 shrink-0" />
          {{ message('I_COST_NOT_COMPUTED') }}
        </p>

        <!-- خرید -->
        <section>
          <h4 class="mb-1 font-semibold">قیمت خرید</h4>
          <ul v-if="data.purchase.length" class="divide-y divide-outline-variant">
            <li v-for="line in data.purchase" :key="line.id ?? line.label" class="flex justify-between gap-3 py-1.5">
              <span>
                {{ line.label }}
                <span class="text-xs text-on-surface-variant">({{ rateText(line.basis, line.rate) }})</span>
              </span>
              <span>{{ faMoney(line.amount) }}</span>
            </li>
          </ul>
          <p v-else class="text-on-surface-variant">سطر خریدی نیست.</p>
          <p
            v-if="data.summary.purchaseCost !== data.purchase.reduce((s, l) => s + l.amount, 0)"
            class="mt-1 text-xs text-on-surface-variant"
          >
            با سهم خریدهای یکجا: {{ faMoney(data.summary.purchaseCost) }}
          </p>
        </section>

        <!-- کارهای کارگاه -->
        <section>
          <h4 class="mb-1 font-semibold">کارهای کارگاه</h4>
          <ul v-if="data.steps.length" class="divide-y divide-outline-variant">
            <li v-for="step in data.steps" :key="step.stepId" class="flex justify-between gap-3 py-1.5">
              <span class="min-w-0">
                {{ step.stepNameFa }}
                <span class="ms-1 inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs" :class="stepStatus(step.status).css">
                  {{ stepStatus(step.status).label }}
                </span>
                <span v-if="step.serviceProviderName" class="block text-xs text-on-surface-variant">{{ step.serviceProviderName }}</span>
                <span
                  v-if="snapshotText(step.pricedLength, step.pricedWidth, step.pricedQuantity)"
                  class="block text-xs text-on-surface-variant"
                >
                  قیمت‌گذاری روی <bdi dir="ltr">{{ snapshotText(step.pricedLength, step.pricedWidth, step.pricedQuantity) }}</bdi>
                </span>
              </span>
              <span class="shrink-0 text-end">
                {{ faMoney(step.countedCost) }}
                <span
                  v-if="step.countedCost !== step.effectiveCost"
                  class="block text-xs text-on-surface-variant"
                >
                  {{
                    step.status === WorkflowStepStatus.Completed || step.status === WorkflowStepStatus.InProgress
                      ? `ثبت‌شده ${faMoney(step.effectiveCost)}؛ کمتر از صفر حساب نمی‌شود`
                      : 'در بها حساب نمی‌شود'
                  }}
                </span>
              </span>
            </li>
          </ul>
          <p v-else class="text-on-surface-variant">کار کارگاهی ثبت نشده.</p>
        </section>

        <!-- سهم ثبت‌های هزینه (از فاز هزینه‌ها) -->
        <section v-if="data.allocations.length">
          <h4 class="mb-1 font-semibold">سهم هزینه‌ها</h4>
          <ul class="divide-y divide-outline-variant">
            <li v-for="a in data.allocations" :key="a.entryId" class="flex justify-between gap-3 py-1.5">
              <span>
                {{ a.category }}
                <span class="text-xs text-on-surface-variant">
                  ({{ a.group === CostGroup.Purchase ? 'خرید' : 'هزینه' }} · {{ faDate(a.date) }})
                </span>
              </span>
              <span>{{ faMoney(a.amount) }}</span>
            </li>
          </ul>
        </section>

        <!-- جمع -->
        <section class="border-t border-outline-variant pt-2">
          <div class="flex justify-between gap-3 font-semibold">
            <span>بهای تمام‌شده</span>
            <span>{{ faMoney(data.summary.totalInvestment) }}</span>
          </div>
          <div v-if="data.summary.costPerM2 !== null" class="flex justify-between gap-3 text-on-surface-variant">
            <span>هر متر</span>
            <span>{{ faMoney(data.summary.costPerM2) }}</span>
          </div>
          <div class="flex justify-between gap-3 text-on-surface-variant">
            <span>هر تخته</span>
            <span>{{ faMoney(data.summary.costPerPiece) }}</span>
          </div>
        </section>
      </template>
    </div>
  </div>
</template>
