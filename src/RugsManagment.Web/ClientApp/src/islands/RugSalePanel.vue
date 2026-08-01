<script setup lang="ts">
/**
 * ثبت/ویرایش/لغو فروش یک فرش، و نمایش سود واقعی در برابر تخمینی.
 *
 * تا وقتی فروش ثبت نشده فقط «سود تخمینی» (بر پایهٔ قیمت هدف) داریم؛
 * بعد از ثبت، سود واقعی جای آن را می‌گیرد.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { api } from '@/lib/api'
import { faDate, faMoney } from '@/lib/format'
import { confirmDialog, toast } from '@/lib/ui'

interface Sale {
  id: string
  buyerName: string
  buyerPhone?: string
  salePrice: number
  discount: number
  netAmount: number
  receivedAmount: number
  outstandingAmount: number
  paymentMethod: number
  soldAt: string
  reference?: string
  notes?: string
  totalInvestment: number
  actualProfit: number
}

const props = defineProps<{ rugId: string; totalInvestment: number }>()

const PAYMENT_METHODS = [
  { value: 0, label: 'نقدی' },
  { value: 1, label: 'کارت' },
  { value: 2, label: 'حواله' },
  { value: 3, label: 'چک' },
  { value: 4, label: 'اقساطی' },
]

const sale = ref<Sale | null>(null)
const loading = ref(true)
const saving = ref(false)
const editing = ref(false)

const form = reactive({
  buyerName: '',
  buyerPhone: '',
  salePrice: 0,
  discount: 0,
  receivedAmount: 0,
  paymentMethod: 0,
  soldAt: today(),
  reference: '',
  notes: '',
})

function today(): string {
  return new Date().toISOString().slice(0, 10)
}

const methodLabel = (v: number) => PAYMENT_METHODS.find((m) => m.value === v)?.label ?? '—'

/** پیش‌نمایش زنده — کاربر قبل از ذخیره سود را می‌بیند. */
const preview = computed(() => {
  const net = Math.max(0, form.salePrice - form.discount)
  return { net, profit: net - props.totalInvestment }
})

async function load() {
  loading.value = true
  try {
    const data = await api.get<Sale | undefined>(`/api/sales/rug/${props.rugId}`)
    sale.value = data ?? null
    if (data) fillForm(data)
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

function fillForm(s: Sale) {
  Object.assign(form, {
    buyerName: s.buyerName,
    buyerPhone: s.buyerPhone ?? '',
    salePrice: s.salePrice,
    discount: s.discount,
    receivedAmount: s.receivedAmount,
    paymentMethod: s.paymentMethod,
    soldAt: s.soldAt.slice(0, 10),
    reference: s.reference ?? '',
    notes: s.notes ?? '',
  })
}

function startSale() {
  editing.value = true
  if (!sale.value) {
    // پیش‌فرض معقول: فروش نقدی امروز، کل مبلغ دریافت‌شده
    Object.assign(form, {
      buyerName: '', buyerPhone: '', salePrice: 0, discount: 0, receivedAmount: 0,
      paymentMethod: 0, soldAt: today(), reference: '', notes: '',
    })
  }
}

/** در فروش نقدی، «دریافتی» را با مبلغ خالص هم‌گام نگه می‌داریم تا کاربر دوباره تایپ نکند. */
function syncReceived() {
  if (form.paymentMethod !== 4 && form.paymentMethod !== 3) {
    form.receivedAmount = Math.max(0, form.salePrice - form.discount)
  }
}

async function save() {
  if (!form.buyerName.trim()) {
    toast.warning('نام خریدار الزامی است.')
    return
  }
  if (form.salePrice <= 0) {
    toast.warning('مبلغ فروش باید بزرگ‌تر از صفر باشد.')
    return
  }

  saving.value = true
  try {
    const saved = await api.put<Sale>(`/api/sales/rug/${props.rugId}`, {
      buyerName: form.buyerName,
      buyerPhone: form.buyerPhone || null,
      salePrice: form.salePrice,
      discount: form.discount,
      receivedAmount: form.receivedAmount,
      paymentMethod: form.paymentMethod,
      soldAt: form.soldAt ? new Date(form.soldAt).toISOString() : null,
      reference: form.reference || null,
      notes: form.notes || null,
    })
    sale.value = saved
    fillForm(saved)
    editing.value = false
    toast.success('فروش ثبت شد.')
    // وضعیت فرش در سربرگ صفحه عوض شده — بارگذاری دوباره تا همه‌جا هماهنگ باشد
    window.setTimeout(() => window.location.reload(), 600)
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    saving.value = false
  }
}

async function cancelSale() {
  const ok = await confirmDialog({
    title: 'فروش این فرش لغو شود؟',
    message: 'رکورد فروش حذف می‌شود و فرش به وضعیت «آمادهٔ فروش» برمی‌گردد.',
    confirmLabel: 'لغو فروش',
    danger: true,
  })
  if (!ok) return

  try {
    await api.del(`/api/sales/rug/${props.rugId}`)
    toast.success('فروش لغو شد.')
    window.setTimeout(() => window.location.reload(), 600)
  } catch (e) {
    toast.error((e as Error).message)
  }
}

onMounted(load)
</script>

<template>
  <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
    <div class="mb-4 flex items-center justify-between gap-3">
      <h2 class="flex items-center gap-2 text-sm font-semibold text-primary">
        <AppIcon name="check" class="h-4 w-4" />
        فروش
      </h2>
      <button v-if="!loading && !editing && !sale" type="button"
              class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
              @click="startSale">
        <AppIcon name="plus" class="h-4 w-4" />
        ثبت فروش
      </button>
    </div>

    <div v-if="loading" class="skeleton h-32 w-full" aria-hidden="true"></div>

    <!-- فرم ثبت/ویرایش -->
    <template v-else-if="editing">
      <div class="grid gap-3 sm:grid-cols-2">
        <label class="block">
          <span class="mb-1 block text-sm">نام خریدار <span class="text-error">*</span></span>
          <input v-model="form.buyerName" class="fld" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">تلفن خریدار</span>
          <input v-model="form.buyerPhone" dir="ltr" class="fld" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">مبلغ فروش (تومان) <span class="text-error">*</span></span>
          <MoneyInput v-model="form.salePrice" @update:modelValue="syncReceived" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">تخفیف (تومان)</span>
          <MoneyInput v-model="form.discount" @update:modelValue="syncReceived" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">نحوهٔ پرداخت</span>
          <select v-model.number="form.paymentMethod" class="fld" @change="syncReceived">
            <option v-for="m in PAYMENT_METHODS" :key="m.value" :value="m.value">{{ m.label }}</option>
          </select>
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">مبلغ دریافتی (تومان)</span>
          <MoneyInput v-model="form.receivedAmount" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">تاریخ فروش</span>
          <input v-model="form.soldAt" type="date" dir="ltr" class="fld" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">شمارهٔ فاکتور</span>
          <input v-model="form.reference" dir="ltr" class="fld" />
        </label>
        <label class="block sm:col-span-2">
          <span class="mb-1 block text-sm">توضیح</span>
          <input v-model="form.notes" class="fld" />
        </label>
      </div>

      <!-- پیش‌نمایش سود پیش از ذخیره -->
      <dl class="mt-4 grid grid-cols-3 gap-2 rounded-lg bg-surface-container px-3 py-2 text-center text-sm" data-numeric>
        <div>
          <dt class="text-xs text-on-surface-variant">فروش خالص</dt>
          <dd>{{ faMoney(preview.net) }}</dd>
        </div>
        <div>
          <dt class="text-xs text-on-surface-variant">سرمایه‌گذاری</dt>
          <dd>{{ faMoney(totalInvestment) }}</dd>
        </div>
        <div>
          <dt class="text-xs text-on-surface-variant">سود</dt>
          <dd class="font-bold" :class="preview.profit >= 0 ? 'text-success' : 'text-error'">
            {{ faMoney(preview.profit) }}
          </dd>
        </div>
      </dl>

      <div class="mt-4 flex justify-end gap-3">
        <button type="button" class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-4 hover:bg-surface-container"
                @click="editing = false">انصراف</button>
        <button type="button" :disabled="saving"
                class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
                @click="save">
          <AppIcon name="check" class="h-4 w-4" />
          ذخیره
        </button>
      </div>
    </template>

    <!-- نمایش فروش ثبت‌شده -->
    <template v-else-if="sale">
      <dl class="space-y-2 text-sm" data-numeric>
        <div class="flex justify-between gap-3">
          <dt class="text-on-surface-variant">خریدار</dt>
          <dd>
            {{ sale.buyerName }}
            <span v-if="sale.buyerPhone" class="font-mono text-xs text-on-surface-variant" dir="ltr">
              ({{ sale.buyerPhone }})
            </span>
          </dd>
        </div>
        <div class="flex justify-between gap-3">
          <dt class="text-on-surface-variant">تاریخ فروش</dt>
          <dd>{{ faDate(sale.soldAt) }}</dd>
        </div>
        <div class="flex justify-between gap-3">
          <dt class="text-on-surface-variant">مبلغ فروش</dt>
          <dd>{{ faMoney(sale.salePrice) }}</dd>
        </div>
        <div v-if="sale.discount > 0" class="flex justify-between gap-3">
          <dt class="text-on-surface-variant">تخفیف</dt>
          <dd class="text-error">−{{ faMoney(sale.discount) }}</dd>
        </div>
        <div class="flex justify-between gap-3 border-t border-outline-variant pt-2 font-semibold">
          <dt>فروش خالص</dt>
          <dd>{{ faMoney(sale.netAmount) }}</dd>
        </div>
        <div class="flex justify-between gap-3">
          <dt class="text-on-surface-variant">نحوهٔ پرداخت</dt>
          <dd>{{ methodLabel(sale.paymentMethod) }}</dd>
        </div>
        <div v-if="sale.outstandingAmount > 0" class="flex justify-between gap-3 text-warning">
          <dt>باقی‌ماندهٔ طلب</dt>
          <dd>{{ faMoney(sale.outstandingAmount) }}</dd>
        </div>
        <div class="flex justify-between gap-3 border-t border-outline-variant pt-2 font-bold"
             :class="sale.actualProfit >= 0 ? 'text-success' : 'text-error'">
          <dt>سود واقعی</dt>
          <dd>{{ faMoney(sale.actualProfit) }}</dd>
        </div>
      </dl>

      <p v-if="sale.reference || sale.notes" class="mt-3 rounded-lg bg-surface-container px-3 py-2 text-xs text-on-surface-variant">
        <span v-if="sale.reference">فاکتور <span class="font-mono" dir="ltr">{{ sale.reference }}</span></span>
        <span v-if="sale.reference && sale.notes"> · </span>
        <span v-if="sale.notes">{{ sale.notes }}</span>
      </p>

      <div class="mt-4 flex gap-2">
        <button type="button"
                class="inline-flex min-h-11 flex-1 items-center justify-center gap-2 rounded-lg border border-outline-variant text-sm hover:bg-surface-container"
                @click="startSale">
          <AppIcon name="edit" class="h-4 w-4" />
          ویرایش
        </button>
        <button type="button"
                class="inline-flex min-h-11 items-center justify-center gap-2 rounded-lg border border-outline-variant px-4 text-sm text-error hover:bg-error-container"
                @click="cancelSale">
          <AppIcon name="close" class="h-4 w-4" />
          لغو فروش
        </button>
      </div>
    </template>

    <p v-else class="py-4 text-center text-sm text-on-surface-variant">
      این فرش هنوز فروخته نشده است.
    </p>
  </section>
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
