<script setup lang="ts">
/**
 * گزارش‌های عملیاتی: کهنگی (گلوگاه)، شکست هزینه به تفکیک مرحله، و روند ماهانه.
 *
 * سه بخش عمداً از هم جدا نگه داشته شده‌اند چون سه سؤال متفاوت را جواب می‌دهند:
 *   • کجا کار گیر کرده؟   • پول کجا خرج می‌شود؟   • روند به کدام سمت است؟
 */
import { computed, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import BarChart from '@/components/BarChart.vue'
import LineChart from '@/components/LineChart.vue'
import { api } from '@/lib/api'
import { faMoney, faNumber } from '@/lib/format'
import { toast } from '@/lib/ui'

interface AgingItem {
  rugId: string
  sku: string
  title?: string
  stepName: string
  daysInStep: number
  serviceProviderName?: string
  severity: number
}
interface StepBreakdown {
  stepName: string
  completedCount: number
  totalCost: number
  averageCost: number
  averageDurationDays?: number
  inProgressCount: number
}
interface TrendPoint {
  label: string
  rugsAdded: number
  rugsSold: number
  salesNet: number
  profit: number
}
interface Report {
  aging: { items: AgingItem[]; warningCount: number; seriousCount: number; criticalCount: number }
  stepBreakdown: StepBreakdown[]
  trend: TrendPoint[]
}

/** وضعیت همیشه با آیکون + برچسب می‌آید، نه فقط رنگ. */
const SEVERITY = [
  { label: 'عادی', icon: 'info', css: 'bg-surface-container-high text-on-surface-variant', color: 'var(--color-on-surface-variant)' },
  { label: 'نیاز به پیگیری', icon: 'info', css: 'bg-warning/12 text-warning', color: 'var(--color-aging-warning)' },
  { label: 'طولانی', icon: 'warning', css: 'bg-warning/20 text-warning', color: 'var(--color-aging-serious)' },
  { label: 'بحرانی', icon: 'error', css: 'bg-error-container text-on-error-container', color: 'var(--color-aging-critical)' },
]

const report = ref<Report | null>(null)
const loading = ref(true)
const months = ref(12)
/** روند: مالی یا تعدادی — واحدشان فرق دارد پس در یک نمودار جمع نمی‌شوند */
const trendMode = ref<'money' | 'count'>('money')

async function load() {
  loading.value = true
  try {
    report.value = await api.get<Report>(`/api/analytics?months=${months.value}`)
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

const costBars = computed(() =>
  (report.value?.stepBreakdown ?? [])
    .filter((s) => s.totalCost > 0)
    .map((s) => ({
      label: s.stepName,
      value: s.totalCost,
      display: faMoney(s.totalCost),
      hint:
        `${faNumber(s.completedCount)} بار انجام شده · میانگین ${faMoney(s.averageCost)}` +
        (s.averageDurationDays != null ? ` · میانگین ${faNumber(s.averageDurationDays, 1)} روز` : ''),
    })),
)

const trendLabels = computed(() => (report.value?.trend ?? []).map((t) => t.label))

const trendSeries = computed(() => {
  const trend = report.value?.trend ?? []
  return trendMode.value === 'money'
    ? [
        { name: 'فروش خالص', values: trend.map((t) => t.salesNet), color: 'var(--color-chart-3)' },
        { name: 'سود واقعی', values: trend.map((t) => t.profit), color: 'var(--color-chart-4)' },
      ]
    : [
        { name: 'فرش ثبت‌شده', values: trend.map((t) => t.rugsAdded), color: 'var(--color-chart-1)' },
        { name: 'فرش فروخته‌شده', values: trend.map((t) => t.rugsSold), color: 'var(--color-chart-2)' },
      ]
})

const trendFormat = computed(() =>
  trendMode.value === 'money' ? (v: number) => faMoney(v) : (v: number) => faNumber(v),
)

const hasTrendData = computed(() =>
  (report.value?.trend ?? []).some((t) => t.rugsAdded > 0 || t.rugsSold > 0),
)

/** بیشترین روز ماندن — برای مقیاس نوار کهنگی */
const maxDays = computed(() => Math.max(1, ...(report.value?.aging.items ?? []).map((i) => i.daysInStep)))

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div v-if="loading" class="skeleton h-72 w-full" aria-hidden="true"></div>

    <template v-else-if="report">
      <!-- ══ کهنگی / گلوگاه ══ -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 class="flex items-center gap-2 text-sm font-semibold text-primary">
              <AppIcon name="warning" class="h-4 w-4" />
              فرش‌های گیرکرده
            </h2>
            <p class="text-xs text-on-surface-variant">
              فرش‌هایی که بیش از یک هفته در مرحلهٔ جاری‌شان مانده‌اند
            </p>
          </div>

          <div v-if="report.aging.items.length" class="flex flex-wrap gap-2 text-xs">
            <span v-if="report.aging.criticalCount" class="inline-flex items-center gap-1 rounded-full bg-error-container px-2.5 py-1 text-on-error-container">
              <AppIcon name="error" class="h-3.5 w-3.5" />
              بحرانی: {{ faNumber(report.aging.criticalCount) }}
            </span>
            <span v-if="report.aging.seriousCount" class="inline-flex items-center gap-1 rounded-full bg-warning/20 px-2.5 py-1 text-warning">
              <AppIcon name="warning" class="h-3.5 w-3.5" />
              طولانی: {{ faNumber(report.aging.seriousCount) }}
            </span>
            <span v-if="report.aging.warningCount" class="inline-flex items-center gap-1 rounded-full bg-warning/12 px-2.5 py-1 text-warning">
              <AppIcon name="info" class="h-3.5 w-3.5" />
              پیگیری: {{ faNumber(report.aging.warningCount) }}
            </span>
          </div>
        </div>

        <div v-if="report.aging.items.length === 0"
             class="flex items-center justify-center gap-2 rounded-lg bg-success/10 px-4 py-6 text-sm text-success">
          <AppIcon name="success" class="h-5 w-5" />
          هیچ فرشی گیر نکرده است.
        </div>

        <ul v-else class="divide-y divide-outline-variant">
          <li v-for="item in report.aging.items" :key="item.rugId">
            <a :href="`/Rugs/Details/${item.rugId}`"
               class="-mx-2 flex items-center gap-3 rounded-lg px-2 py-2.5 hover:bg-surface-container">
              <span class="inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-xs"
                    :class="SEVERITY[item.severity].css">
                <AppIcon :name="SEVERITY[item.severity].icon" class="h-3.5 w-3.5" />
                {{ SEVERITY[item.severity].label }}
              </span>

              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-medium">{{ item.title || 'بدون عنوان' }}</span>
                <span class="block truncate text-xs text-on-surface-variant">
                  <span class="font-mono" dir="ltr">{{ item.sku }}</span>
                  · {{ item.stepName }}<template v-if="item.serviceProviderName"> · {{ item.serviceProviderName }}</template>
                </span>
              </span>

              <!-- نوار طول ماندن: طول میله همان عدد را نشان می‌دهد، رنگ فقط شدت را -->
              <span class="hidden w-28 shrink-0 sm:block">
                <span class="block h-1.5 overflow-hidden rounded-full bg-surface-container">
                  <span class="block h-full rounded-full"
                        :style="{ width: (item.daysInStep / maxDays * 100) + '%', background: SEVERITY[item.severity].color }"></span>
                </span>
              </span>

              <span class="shrink-0 whitespace-nowrap text-sm font-semibold" data-numeric>
                {{ faNumber(item.daysInStep) }} روز
              </span>
            </a>
          </li>
        </ul>
      </section>

      <div class="grid gap-6 lg:grid-cols-2">
        <!-- ══ شکست هزینه به تفکیک مرحله ══ -->
        <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <h2 class="mb-1 flex items-center gap-2 text-sm font-semibold text-primary">
            <AppIcon name="chart" class="h-4 w-4" />
            هزینه به تفکیک مرحله
          </h2>
          <p class="mb-4 text-xs text-on-surface-variant">مجموع هزینهٔ مراحل تکمیل‌شده در کل کارگاه</p>

          <p v-if="costBars.length === 0" class="py-8 text-center text-sm text-on-surface-variant">
            هنوز هزینه‌ای برای مراحل ثبت نشده است.
          </p>
          <BarChart v-else :bars="costBars" />
        </section>

        <!-- ══ بار کاری جاری ══ -->
        <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <h2 class="mb-1 flex items-center gap-2 text-sm font-semibold text-primary">
            <AppIcon name="workflow" class="h-4 w-4" />
            زمان و بار هر مرحله
          </h2>
          <p class="mb-4 text-xs text-on-surface-variant">میانگین مدت انجام و تعداد فرش در حال انجام</p>

          <div v-if="report.stepBreakdown.length === 0" class="py-8 text-center text-sm text-on-surface-variant">
            هنوز مرحله‌ای ثبت نشده است.
          </div>
          <div v-else class="overflow-x-auto">
            <table class="w-full text-sm">
              <caption class="sr-only">میانگین مدت و بار کاری هر نوع مرحله</caption>
              <thead class="text-on-surface-variant">
                <tr class="border-b border-outline-variant">
                  <th scope="col" class="py-2 text-right font-medium">مرحله</th>
                  <th scope="col" class="py-2 text-right font-medium">انجام‌شده</th>
                  <th scope="col" class="py-2 text-right font-medium">میانگین مدت</th>
                  <th scope="col" class="py-2 text-right font-medium">در جریان</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="s in report.stepBreakdown" :key="s.stepName" class="border-b border-outline-variant last:border-0">
                  <td class="py-2">{{ s.stepName }}</td>
                  <td class="py-2" data-numeric>{{ faNumber(s.completedCount) }}</td>
                  <td class="py-2" data-numeric>
                    {{ s.averageDurationDays != null ? faNumber(s.averageDurationDays, 1) + ' روز' : '—' }}
                  </td>
                  <td class="py-2" data-numeric>
                    <span v-if="s.inProgressCount" class="rounded-full bg-secondary-container px-2 py-0.5 text-xs text-on-secondary-container">
                      {{ faNumber(s.inProgressCount) }}
                    </span>
                    <span v-else class="text-on-surface-variant">—</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <!-- ══ روند ماهانه ══ -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 class="flex items-center gap-2 text-sm font-semibold text-primary">
              <AppIcon name="chart" class="h-4 w-4" />
              روند ماهانه
            </h2>
            <p class="text-xs text-on-surface-variant">بر پایهٔ تقویم شمسی</p>
          </div>

          <div class="flex flex-wrap gap-2">
            <!-- واحد مالی و تعدادی جدا انتخاب می‌شوند؛ در یک محور جمع نمی‌شوند -->
            <div class="flex rounded-lg border border-outline-variant p-0.5" role="group" aria-label="نوع داده">
              <button type="button" class="min-h-11 rounded px-3 text-xs"
                      :class="trendMode === 'money' ? 'bg-primary text-on-primary font-semibold' : 'text-on-surface-variant'"
                      @click="trendMode = 'money'">مالی</button>
              <button type="button" class="min-h-11 rounded px-3 text-xs"
                      :class="trendMode === 'count' ? 'bg-primary text-on-primary font-semibold' : 'text-on-surface-variant'"
                      @click="trendMode = 'count'">تعداد</button>
            </div>

            <label class="inline-flex items-center gap-2 text-xs text-on-surface-variant">
              بازه
              <select v-model.number="months" class="min-h-11 rounded-lg border border-outline-variant bg-surface-container-lowest px-2 text-xs"
                      @change="load">
                <option :value="6">۶ ماه</option>
                <option :value="12">۱۲ ماه</option>
                <option :value="24">۲۴ ماه</option>
              </select>
            </label>
          </div>
        </div>

        <p v-if="!hasTrendData" class="py-8 text-center text-sm text-on-surface-variant">
          در این بازه فعالیتی ثبت نشده است.
        </p>
        <LineChart v-else :labels="trendLabels" :series="trendSeries" :format="trendFormat" />
      </section>
    </template>
  </div>
</template>
