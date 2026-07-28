<script setup lang="ts">
/**
 * گزارش فروش و سود واقعی برای یک بازهٔ زمانی، با خروجی CSV و نسخهٔ چاپی.
 *
 * همهٔ اعداد اینجا «واقعی» هستند (از رکوردهای فروش)، نه تخمینی.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { api } from '@/lib/api'
import { faDate, faMoney, faNumber } from '@/lib/format'
import { toast } from '@/lib/ui'

interface Sale {
  rugId: string
  rugSku: string
  rugTitle?: string
  buyerName: string
  salePrice: number
  discount: number
  netAmount: number
  receivedAmount: number
  outstandingAmount: number
  paymentMethod: number
  soldAt: string
  totalInvestment: number
  actualProfit: number
}
interface Summary {
  saleCount: number
  grossTotal: number
  discountTotal: number
  netTotal: number
  receivedTotal: number
  outstandingTotal: number
  investmentTotal: number
  profitTotal: number
  marginPercent?: number
}

const METHODS = ['نقدی', 'کارت', 'حواله', 'چک', 'اقساطی']

const summary = ref<Summary | null>(null)
const sales = ref<Sale[]>([])
const loading = ref(true)

const filters = reactive({ from: '', to: '', onlyOutstanding: false })

/** رشتهٔ query مشترک بین گزارش، CSV و چاپ تا هر سه دقیقاً یک بازه را ببینند. */
const queryString = computed(() => {
  const p = new URLSearchParams()
  if (filters.from) p.set('from', filters.from)
  if (filters.to) p.set('to', filters.to)
  if (filters.onlyOutstanding) p.set('onlyOutstanding', 'true')
  return p.toString()
})

const methodLabel = (v: number) => METHODS[v] ?? '—'

async function load() {
  loading.value = true
  try {
    const data = await api.get<{ summary: Summary; sales: Sale[] }>(
      `/api/sales/report${queryString.value ? '?' + queryString.value : ''}`,
    )
    summary.value = data.summary
    sales.value = data.sales
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

/** میان‌بُرهای بازه — پرکاربردترین انتخاب‌ها بدون تایپ تاریخ. */
function quickRange(days: number) {
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - days)
  filters.from = from.toISOString().slice(0, 10)
  filters.to = to.toISOString().slice(0, 10)
  void load()
}

function clearRange() {
  filters.from = ''
  filters.to = ''
  filters.onlyOutstanding = false
  void load()
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <!-- فیلترها -->
    <form class="flex flex-wrap items-end gap-3 rounded-xl border border-outline-variant bg-surface-container-lowest p-4 shadow-sm"
          data-no-print @submit.prevent="load">
      <label class="block">
        <span class="mb-1 block text-sm">از تاریخ</span>
        <input v-model="filters.from" type="date" dir="ltr" class="fld" />
      </label>
      <label class="block">
        <span class="mb-1 block text-sm">تا تاریخ</span>
        <input v-model="filters.to" type="date" dir="ltr" class="fld" />
      </label>
      <label class="flex min-h-11 items-center gap-2 text-sm">
        <input v-model="filters.onlyOutstanding" type="checkbox"
               class="h-5 w-5 rounded border-outline-variant text-primary" @change="load" />
        فقط تسویه‌نشده‌ها
      </label>

      <button type="submit"
              class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover">
        <AppIcon name="filter" class="h-4 w-4" />
        اعمال
      </button>

      <div class="flex flex-wrap gap-1.5">
        <button type="button" class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-3 text-xs hover:bg-surface-container"
                @click="quickRange(30)">۳۰ روز اخیر</button>
        <button type="button" class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-3 text-xs hover:bg-surface-container"
                @click="quickRange(90)">۹۰ روز اخیر</button>
        <button type="button" class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-3 text-xs hover:bg-surface-container"
                @click="clearRange">همهٔ زمان‌ها</button>
      </div>

      <div class="flex-1"></div>

      <a :href="`/Sales/ExportCsv?${queryString}`"
         class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container">
        <AppIcon name="download" class="h-4 w-4" />
        خروجی اکسل (CSV)
      </a>
      <a :href="`/Sales/Print?${queryString}`" target="_blank" rel="noopener"
         class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container">
        <AppIcon name="printer" class="h-4 w-4" />
        چاپ / PDF
      </a>
    </form>

    <div v-if="loading" class="skeleton h-64 w-full" aria-hidden="true"></div>

    <template v-else-if="summary">
      <!-- کارت‌های خلاصه -->
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <div class="text-sm text-on-surface-variant">تعداد فروش</div>
          <div class="mt-1 text-3xl font-bold" data-numeric>{{ faNumber(summary.saleCount) }}</div>
        </div>
        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <div class="text-sm text-on-surface-variant">فروش خالص</div>
          <div class="mt-1 text-xl font-bold" data-numeric>{{ faMoney(summary.netTotal) }}</div>
          <div v-if="summary.discountTotal > 0" class="text-xs text-on-surface-variant" data-numeric>
            پس از {{ faMoney(summary.discountTotal) }} تخفیف
          </div>
        </div>
        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <div class="text-sm text-on-surface-variant">سرمایه‌گذاری</div>
          <div class="mt-1 text-xl font-bold" data-numeric>{{ faMoney(summary.investmentTotal) }}</div>
        </div>
        <div class="rounded-xl border p-5 shadow-sm"
             :class="summary.profitTotal >= 0 ? 'border-success/40 bg-success/10' : 'border-error/40 bg-error-container'">
          <div class="text-sm" :class="summary.profitTotal >= 0 ? 'text-success' : 'text-on-error-container'">
            سود واقعی
          </div>
          <div class="mt-1 text-2xl font-bold" data-numeric
               :class="summary.profitTotal >= 0 ? 'text-success' : 'text-on-error-container'">
            {{ faMoney(summary.profitTotal) }}
          </div>
          <div v-if="summary.marginPercent != null" class="text-xs" data-numeric
               :class="summary.profitTotal >= 0 ? 'text-success' : 'text-on-error-container'">
            حاشیه {{ faNumber(summary.marginPercent, 1) }}٪
          </div>
        </div>
      </div>

      <div v-if="summary.outstandingTotal > 0"
           class="flex items-center gap-2 rounded-lg bg-warning/10 px-4 py-3 text-sm text-warning">
        <AppIcon name="warning" class="h-5 w-5" />
        <span data-numeric>طلب تسویه‌نشده از خریداران: {{ faMoney(summary.outstandingTotal) }}</span>
      </div>

      <!-- جدول فروش -->
      <div v-if="sales.length === 0"
           class="rounded-xl border border-dashed border-outline-variant bg-surface-container-lowest p-12 text-center">
        <span class="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-full bg-surface-container text-on-surface-variant">
          <AppIcon name="rug" class="h-7 w-7" />
        </span>
        <p class="font-medium text-on-surface">در این بازه فروشی ثبت نشده است</p>
        <p class="mx-auto mt-1 max-w-sm text-sm text-on-surface-variant">
          بازهٔ دیگری انتخاب کنید، یا از صفحهٔ جزئیات یک فرش، فروش آن را ثبت کنید.
        </p>
      </div>

      <div v-else class="overflow-x-auto rounded-xl border border-outline-variant bg-surface-container-lowest">
        <table class="w-full min-w-[52rem] text-sm">
          <caption class="sr-only">فهرست فروش‌ها با مبلغ، سرمایه‌گذاری و سود واقعی</caption>
          <thead class="bg-surface-container text-on-surface-variant">
            <tr>
              <th scope="col" class="p-3 text-right font-medium">کد</th>
              <th scope="col" class="p-3 text-right font-medium">فرش</th>
              <th scope="col" class="p-3 text-right font-medium">خریدار</th>
              <th scope="col" class="p-3 text-right font-medium">تاریخ</th>
              <th scope="col" class="p-3 text-right font-medium">فروش خالص</th>
              <th scope="col" class="p-3 text-right font-medium">باقی‌مانده</th>
              <th scope="col" class="p-3 text-right font-medium">سرمایه‌گذاری</th>
              <th scope="col" class="p-3 text-right font-medium">سود</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in sales" :key="s.rugId"
                class="border-t border-outline-variant even:bg-surface-container-low/60 hover:bg-surface-container/70">
              <td class="p-3 font-mono text-xs" dir="ltr">{{ s.rugSku }}</td>
              <td class="p-3">
                <a :href="`/Rugs/Details/${s.rugId}`" class="flex min-h-11 items-center font-medium hover:text-primary hover:underline">
                  {{ s.rugTitle || 'بدون عنوان' }}
                </a>
              </td>
              <td class="p-3">
                {{ s.buyerName }}
                <span class="block text-xs text-on-surface-variant">{{ methodLabel(s.paymentMethod) }}</span>
              </td>
              <td class="p-3 whitespace-nowrap">{{ faDate(s.soldAt) }}</td>
              <td class="p-3 whitespace-nowrap" data-numeric>{{ faMoney(s.netAmount) }}</td>
              <td class="p-3 whitespace-nowrap" data-numeric
                  :class="s.outstandingAmount > 0 ? 'text-warning' : 'text-on-surface-variant'">
                {{ s.outstandingAmount > 0 ? faMoney(s.outstandingAmount) : '—' }}
              </td>
              <td class="p-3 whitespace-nowrap" data-numeric>{{ faMoney(s.totalInvestment) }}</td>
              <td class="p-3 whitespace-nowrap font-semibold" data-numeric
                  :class="s.actualProfit >= 0 ? 'text-success' : 'text-error'">
                {{ faMoney(s.actualProfit) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
.fld {
  min-height: 2.75rem;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  padding: 0 0.75rem;
  outline: none;
}
.fld:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
</style>
