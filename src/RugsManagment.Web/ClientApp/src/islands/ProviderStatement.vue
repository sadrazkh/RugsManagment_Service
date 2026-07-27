<script setup lang="ts">
/**
 * صورت‌حساب یک طرف خدمات: مانده، کارهای انجام‌شده و پرداخت‌ها + ثبت تسویه.
 *
 * مانده هرگز در دیتابیس ذخیره نمی‌شود؛ سرور آن را از «کار تکمیل‌شده − پرداخت‌ها»
 * محاسبه می‌کند، پس هیچ‌وقت با واقعیت اختلاف پیدا نمی‌کند.
 */
import { onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { api } from '@/lib/api'
import { faDate, faMoney, faNumber } from '@/lib/format'
import { confirmDialog, toast } from '@/lib/ui'

interface Balance {
  providerId: string
  providerName: string
  completedWorkTotal: number
  inProgressTotal: number
  paidTotal: number
  balance: number
  completedStepCount: number
  inProgressStepCount: number
  lastPaymentAt?: string
}
interface WorkItem {
  stepId: string
  rugId: string
  rugSku: string
  rugTitle?: string
  stepNameFa: string
  completedAt?: string
  status: number
  cost: number
}
interface Payment { id: string; amount: number; paidAt: string; reference?: string; notes?: string }

const props = defineProps<{ providerId: string; providerName: string }>()

const balance = ref<Balance | null>(null)
const work = ref<WorkItem[]>([])
const payments = ref<Payment[]>([])
const loading = ref(true)
const saving = ref(false)

const payment = reactive({ amount: 0, paidAt: today(), reference: '', notes: '' })

function today(): string {
  return new Date().toISOString().slice(0, 10)
}

async function load() {
  loading.value = true
  try {
    const data = await api.get<{ balance: Balance; work: WorkItem[]; payments: Payment[] }>(
      `/api/providers/${props.providerId}/statement`,
    )
    balance.value = data.balance
    work.value = data.work
    payments.value = data.payments
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

/** پر کردن مبلغ با کل مانده — پرکاربردترین حالت تسویه. */
function payFull() {
  if (balance.value && balance.value.balance > 0) payment.amount = balance.value.balance
}

async function submitPayment() {
  if (payment.amount <= 0) {
    toast.warning('مبلغ پرداخت باید بزرگ‌تر از صفر باشد.')
    return
  }

  saving.value = true
  try {
    await api.post(`/api/providers/${props.providerId}/payments`, {
      amount: payment.amount,
      // تاریخ انتخابی کاربر یک «روز» است؛ به لحظهٔ UTC تبدیل می‌شود
      paidAt: payment.paidAt ? new Date(payment.paidAt).toISOString() : null,
      reference: payment.reference || null,
      notes: payment.notes || null,
    })
    toast.success('پرداخت ثبت شد.')
    Object.assign(payment, { amount: 0, paidAt: today(), reference: '', notes: '' })
    await load()
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    saving.value = false
  }
}

async function removePayment(p: Payment) {
  const ok = await confirmDialog({
    title: `پرداخت ${faMoney(p.amount)} حذف شود؟`,
    message: 'مانده‌حساب دوباره محاسبه می‌شود.',
    confirmLabel: 'حذف پرداخت',
    danger: true,
  })
  if (!ok) return

  try {
    await api.del(`/api/providers/${props.providerId}/payments/${p.id}`)
    toast.success('پرداخت حذف شد.')
    await load()
  } catch (e) {
    toast.error((e as Error).message)
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <div v-if="loading" class="skeleton h-64 w-full" aria-hidden="true"></div>

    <template v-else-if="balance">
      <!-- کارت‌های خلاصه -->
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <div class="text-sm text-on-surface-variant">کار تکمیل‌شده</div>
          <div class="mt-1 text-xl font-bold" data-numeric>{{ faMoney(balance.completedWorkTotal) }}</div>
          <div class="text-xs text-on-surface-variant" data-numeric>
            {{ faNumber(balance.completedStepCount) }} مرحله
          </div>
        </div>

        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <div class="text-sm text-on-surface-variant">در جریان (هنوز بدهی قطعی نیست)</div>
          <div class="mt-1 text-xl font-bold text-secondary" data-numeric>{{ faMoney(balance.inProgressTotal) }}</div>
          <div class="text-xs text-on-surface-variant" data-numeric>
            {{ faNumber(balance.inProgressStepCount) }} مرحله
          </div>
        </div>

        <div class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <div class="text-sm text-on-surface-variant">پرداخت‌شده</div>
          <div class="mt-1 text-xl font-bold text-success" data-numeric>{{ faMoney(balance.paidTotal) }}</div>
          <div v-if="balance.lastPaymentAt" class="text-xs text-on-surface-variant">
            آخرین: {{ faDate(balance.lastPaymentAt) }}
          </div>
        </div>

        <div class="rounded-xl border p-5 shadow-sm"
             :class="balance.balance > 0
               ? 'border-error/40 bg-error-container'
               : 'border-success/40 bg-success/10'">
          <div class="text-sm" :class="balance.balance > 0 ? 'text-on-error-container' : 'text-success'">
            {{ balance.balance >= 0 ? 'مانده (بدهی ما)' : 'پرداخت اضافی' }}
          </div>
          <div class="mt-1 text-2xl font-bold" data-numeric
               :class="balance.balance > 0 ? 'text-on-error-container' : 'text-success'">
            {{ faMoney(Math.abs(balance.balance)) }}
          </div>
        </div>
      </div>

      <!-- ثبت پرداخت -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-4 flex items-center gap-2 text-sm font-semibold text-primary">
          <AppIcon name="check" class="h-4 w-4" />
          ثبت تسویه
        </h2>

        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label class="block">
            <span class="mb-1 block text-sm">مبلغ (تومان) <span class="text-error">*</span></span>
            <MoneyInput v-model="payment.amount" />
          </label>
          <label class="block">
            <span class="mb-1 block text-sm">تاریخ پرداخت</span>
            <input v-model="payment.paidAt" type="date" dir="ltr" class="fld" />
          </label>
          <label class="block">
            <span class="mb-1 block text-sm">شمارهٔ فیش / چک</span>
            <input v-model="payment.reference" dir="ltr" class="fld" />
          </label>
          <label class="block">
            <span class="mb-1 block text-sm">توضیح</span>
            <input v-model="payment.notes" class="fld" />
          </label>
        </div>

        <div class="mt-4 flex flex-wrap gap-3">
          <button v-if="balance.balance > 0" type="button"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
                  @click="payFull">
            تسویهٔ کامل ({{ faMoney(balance.balance) }})
          </button>
          <div class="flex-1"></div>
          <button type="button" :disabled="saving"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
                  @click="submitPayment">
            <AppIcon name="check" class="h-4 w-4" />
            ثبت پرداخت
          </button>
        </div>
      </section>

      <div class="grid gap-5 lg:grid-cols-2">
        <!-- کارهای انجام‌شده -->
        <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <h2 class="mb-4 text-sm font-semibold text-primary">کارهای انجام‌شده</h2>

          <p v-if="work.length === 0" class="py-6 text-center text-sm text-on-surface-variant">
            هنوز مرحله‌ای به این طرف نسبت داده نشده است.
          </p>

          <ul v-else class="divide-y divide-outline-variant">
            <li v-for="item in work" :key="item.stepId" class="flex items-center justify-between gap-3 py-2.5">
              <a :href="`/Rugs/Details/${item.rugId}`" class="min-w-0 flex-1">
                <div class="truncate text-sm font-medium hover:text-primary">
                  {{ item.stepNameFa }} — {{ item.rugTitle || 'بدون عنوان' }}
                </div>
                <div class="truncate text-xs text-on-surface-variant">
                  <span class="font-mono" dir="ltr">{{ item.rugSku }}</span>
                  <span v-if="item.completedAt"> · {{ faDate(item.completedAt) }}</span>
                  <span v-else class="text-secondary"> · در جریان</span>
                </div>
              </a>
              <span class="shrink-0 whitespace-nowrap text-sm" data-numeric>{{ faMoney(item.cost) }}</span>
            </li>
          </ul>
        </section>

        <!-- پرداخت‌ها -->
        <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
          <h2 class="mb-4 text-sm font-semibold text-primary">پرداخت‌ها</h2>

          <p v-if="payments.length === 0" class="py-6 text-center text-sm text-on-surface-variant">
            هنوز پرداختی ثبت نشده است.
          </p>

          <ul v-else class="divide-y divide-outline-variant">
            <li v-for="p in payments" :key="p.id" class="flex items-center justify-between gap-3 py-2.5">
              <div class="min-w-0">
                <div class="text-sm font-medium" data-numeric>{{ faMoney(p.amount) }}</div>
                <div class="truncate text-xs text-on-surface-variant">
                  {{ faDate(p.paidAt) }}
                  <span v-if="p.reference"> · فیش <span class="font-mono" dir="ltr">{{ p.reference }}</span></span>
                  <span v-if="p.notes"> · {{ p.notes }}</span>
                </div>
              </div>
              <button type="button"
                      class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-error hover:bg-error-container"
                      @click="removePayment(p)">
                <AppIcon name="trash" class="h-4 w-4" label="حذف این پرداخت" />
              </button>
            </li>
          </ul>
        </section>
      </div>
    </template>
  </div>
</template>

<style scoped>
.fld {
  min-height: 2.75rem;
  width: 100%;
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
