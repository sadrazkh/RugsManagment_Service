<script setup lang="ts">
/**
 * جزیرهٔ `ledger-health` (`/settings/ledger-health`، فقط تنظیم‌کننده): «سلامت حساب‌ها» (D.4 «Recovery»، L.4 #9).
 *
 * - با باز شدن صفحه `GET /api/diagnostics/cost-consistency`: سرور همه‌چیز را با همان قاعده‌ها از نو حساب و با
 *   عددهای ذخیره‌شده می‌سنجد. فهرست خالی یعنی سالم: «همهٔ حساب‌ها درست است؛ N فرش بررسی شد.»
 * - هر ناهمخوانی یک سطر: کدام فرش (شماره و کد)، کدام بررسی، عدد ذخیره‌شده و عدد درست.
 * - «ساختن دوبارهٔ حساب‌ها» (`POST /api/diagnostics/ledger-rebuild`) همهٔ ردیف‌های مشتق‌شده را از نو می‌نویسد و بعد
 *   دوباره می‌سنجد. هیچ داده‌ای که کاربر نوشته عوض نمی‌شود؛ فقط عددهای حساب‌شده.
 */
import { onMounted, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { faDecimal, faNumber, toPersianDigits } from '@/lib/format'
import { message } from '@/lib/messages'
import { confirmDialog, toast } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import { CostMismatchCheck, type CostConsistencyReport, type LedgerRebuildResult } from '@/lib/types/rugs'

const loading = ref(true)
const rebuilding = ref(false)
const error = ref('')
const report = ref<CostConsistencyReport | null>(null)

/** نام فارسی هر بررسی؛ کد ناشناختهٔ فاز بعد همان‌طور که هست نشان داده می‌شود. */
const CHECK_LABELS: Record<string, string> = {
  [CostMismatchCheck.MissingTotal]: 'بها حساب نشده',
  [CostMismatchCheck.Total]: 'جمع بها',
  [CostMismatchCheck.PurchaseLine]: 'مبلغ سطر خرید',
  [CostMismatchCheck.EffectiveSize]: 'اندازهٔ مؤثر',
  [CostMismatchCheck.StaleVersion]: 'قاعدهٔ کهنه',
}

async function verify() {
  loading.value = true
  error.value = ''
  try {
    report.value = await api.get<CostConsistencyReport>('/api/diagnostics/cost-consistency')
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

async function rebuild() {
  const ok = await confirmDialog({
    title: 'ساختن دوبارهٔ حساب‌ها',
    message: message('Q_LEDGER_REBUILD'),
    confirmLabel: 'بساز',
    cancelLabel: 'نه',
  })
  if (!ok) return

  rebuilding.value = true
  error.value = ''
  try {
    const result = await api.post<LedgerRebuildResult>('/api/diagnostics/ledger-rebuild')
    toast.success(message('I_LEDGER_REBUILT', { n: result.rugs }))
    await verify()
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    rebuilding.value = false
  }
}

function amount(value: number | null): string {
  return value === null ? '—' : faDecimal(value, 4)
}

onMounted(verify)
</script>

<template>
  <section class="space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <div class="min-w-0">
        <p v-if="loading" class="text-sm text-on-surface-variant">در حال سنجیدن حساب‌ها…</p>
        <p
          v-else-if="report && report.mismatches.length === 0"
          class="flex items-center gap-2 font-semibold text-success"
          role="status"
        >
          <AppIcon name="success" class="h-5 w-5" />
          {{ message('I_LEDGER_OK', { n: report.checked }) }}
        </p>
        <p v-else-if="report" class="flex items-center gap-2 font-semibold text-warning" role="status">
          <AppIcon name="warning" class="h-5 w-5" />
          {{ message('W_LEDGER_MISMATCH', { n: report.mismatches.length }) }}
          <span class="font-normal text-on-surface-variant">({{ faNumber(report.checked) }} فرش بررسی شد)</span>
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <Button icon="undo" :disabled="loading || rebuilding" @click="verify">دوباره بسنج</Button>
        <Button variant="primary" icon="calculator" :loading="rebuilding" :disabled="loading" @click="rebuild">
          ساختن دوبارهٔ حساب‌ها
        </Button>
      </div>
    </div>

    <p v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">{{ error }}</p>

    <div
      v-if="report && report.mismatches.length > 0"
      class="relative overflow-x-auto rounded-xl border border-outline-variant bg-surface-container-lowest"
    >
      <table class="w-full min-w-[40rem] text-sm" data-numeric>
        <caption class="sr-only">ناهمخوانی‌های حساب</caption>
        <thead class="bg-surface-container text-on-surface-variant">
          <tr>
            <th scope="col" class="p-3 text-right font-medium">فرش</th>
            <th scope="col" class="p-3 text-right font-medium">بررسی</th>
            <th scope="col" class="p-3 text-right font-medium">ذخیره‌شده</th>
            <th scope="col" class="p-3 text-right font-medium">درست</th>
            <th scope="col" class="p-3 text-right font-medium">توضیح</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, i) in report.mismatches" :key="`${row.rugId}-${row.check}-${i}`" class="border-t border-outline-variant">
            <td class="p-3">
              <a :href="`/Rugs/Details/${row.rugId}`" class="hover:text-primary hover:underline">
                <bdi v-if="row.serialNo" dir="ltr" class="font-semibold">{{ toPersianDigits(row.serialNo) }}</bdi>
                <span class="block font-mono text-xs text-on-surface-variant" dir="ltr">{{ row.sku }}</span>
              </a>
            </td>
            <td class="p-3">{{ CHECK_LABELS[row.check] ?? row.check }}</td>
            <td class="p-3"><bdi dir="ltr">{{ amount(row.stored) }}</bdi></td>
            <td class="p-3"><bdi dir="ltr">{{ amount(row.expected) }}</bdi></td>
            <td class="p-3 text-on-surface-variant">{{ row.detail ?? '' }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
