<script setup lang="ts">
/**
 * مدیریت طرف‌های خدمات: افزودن/ویرایش مشخصات، نرخ توافقی هر نوع مرحله،
 * فعال/غیرفعال کردن، و نمایش مانده‌حساب هر طرف.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { api } from '@/lib/api'
import { faMoney, faNumber } from '@/lib/format'
import { confirmDialog, toast } from '@/lib/ui'

interface StepType { id: string; nameFa: string; defaultPricingModel: number; defaultUnitRate: number }
interface Rate { id?: string; processStepTypeId: string; stepNameFa?: string; pricingModel: number; unitRate: number; notes?: string }
interface Provider {
  id: string
  name: string
  specialty?: string
  phone?: string
  address?: string
  notes?: string
  isActive: boolean
  rates: Rate[]
}
interface Balance {
  providerId: string
  completedWorkTotal: number
  inProgressTotal: number
  paidTotal: number
  balance: number
  completedStepCount: number
}

/** نام مدل‌های قیمت‌گذاری — همان ترتیب enum سرور. */
const PRICING_MODELS: { value: number; label: string }[] = [
  { value: 0, label: 'مبلغ ثابت' },
  { value: 1, label: 'به ازای متر مربع' },
  { value: 4, label: 'به ازای متر طول' },
  { value: 5, label: 'به ازای متر عرض' },
  { value: 2, label: 'به ازای فوت مربع' },
]

const providers = ref<Provider[]>([])
const stepTypes = ref<StepType[]>([])
const balances = ref<Record<string, Balance>>({})
const loading = ref(true)
const saving = ref(false)
const editingId = ref<string | null>(null)
const showForm = ref(false)

const emptyForm = (): Provider => ({
  id: '', name: '', specialty: '', phone: '', address: '', notes: '', isActive: true, rates: [],
})
const form = reactive<Provider>(emptyForm())

const modelLabel = (value: number) =>
  PRICING_MODELS.find((m) => m.value === value)?.label ?? '—'

/** انواع مرحله‌ای که هنوز نرخی برایشان تعریف نشده. */
const availableStepTypes = computed(() =>
  stepTypes.value.filter((t) => !form.rates.some((r) => r.processStepTypeId === t.id)),
)

async function load() {
  loading.value = true
  try {
    const [list, types, balanceList] = [
      await api.get<Provider[]>('/api/providers'),
      await api.get<StepType[]>('/api/lookups/step-types'),
      await api.get<Balance[]>('/api/providers/balances'),
    ]
    providers.value = list
    stepTypes.value = types
    balances.value = Object.fromEntries(balanceList.map((b) => [b.providerId, b]))
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  Object.assign(form, emptyForm())
  editingId.value = null
  showForm.value = true
}

function startEdit(p: Provider) {
  Object.assign(form, {
    ...p,
    specialty: p.specialty ?? '',
    phone: p.phone ?? '',
    address: p.address ?? '',
    notes: p.notes ?? '',
    // کپی می‌گیریم تا انصراف، فهرست اصلی را تغییر ندهد
    rates: p.rates.map((r) => ({ ...r })),
  })
  editingId.value = p.id
  showForm.value = true
}

function cancelEdit() {
  showForm.value = false
  editingId.value = null
}

function addRate(stepTypeId: string) {
  const type = stepTypes.value.find((t) => t.id === stepTypeId)
  if (!type) return
  // نرخ پیش‌فرض نوع مرحله نقطهٔ شروع خوبی است؛ کاربر آن را تغییر می‌دهد
  form.rates.push({
    processStepTypeId: type.id,
    stepNameFa: type.nameFa,
    pricingModel: type.defaultPricingModel,
    unitRate: type.defaultUnitRate,
  })
}

function removeRate(index: number) {
  form.rates.splice(index, 1)
}

function stepName(rate: Rate): string {
  return rate.stepNameFa || stepTypes.value.find((t) => t.id === rate.processStepTypeId)?.nameFa || '—'
}

async function save() {
  if (!form.name.trim()) {
    toast.warning('نام طرف خدمات الزامی است.')
    return
  }

  saving.value = true
  try {
    const payload = {
      name: form.name,
      specialty: form.specialty || null,
      phone: form.phone || null,
      address: form.address || null,
      notes: form.notes || null,
      isActive: form.isActive,
      rates: form.rates.map((r) => ({
        processStepTypeId: r.processStepTypeId,
        pricingModel: r.pricingModel,
        unitRate: r.unitRate,
        notes: r.notes || null,
      })),
    }

    if (editingId.value) await api.put(`/api/providers/${editingId.value}`, payload)
    else await api.post('/api/providers', payload)

    toast.success(editingId.value ? 'طرف خدمات به‌روز شد.' : 'طرف خدمات اضافه شد.')
    cancelEdit()
    await load()
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    saving.value = false
  }
}

async function toggleActive(p: Provider) {
  try {
    await api.put(`/api/providers/${p.id}`, {
      name: p.name, specialty: p.specialty, phone: p.phone, address: p.address, notes: p.notes,
      isActive: !p.isActive,
      rates: p.rates.map((r) => ({
        processStepTypeId: r.processStepTypeId, pricingModel: r.pricingModel,
        unitRate: r.unitRate, notes: r.notes ?? null,
      })),
    })
    toast.success(p.isActive ? 'طرف خدمات غیرفعال شد.' : 'طرف خدمات فعال شد.')
    await load()
  } catch (e) {
    toast.error((e as Error).message)
  }
}

async function remove(p: Provider) {
  const ok = await confirmDialog({
    title: `«${p.name}» حذف شود؟`,
    message: 'اگر سابقهٔ کار یا پرداخت داشته باشد حذف نمی‌شود و باید غیرفعالش کنید.',
    confirmLabel: 'حذف',
    danger: true,
  })
  if (!ok) return

  try {
    await api.del(`/api/providers/${p.id}`)
    toast.success('طرف خدمات حذف شد.')
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

    <template v-else>
      <!-- فرم افزودن/ویرایش -->
      <section v-if="showForm" class="rounded-xl border border-primary/40 bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-4 text-sm font-semibold text-primary">
          {{ editingId ? 'ویرایش طرف خدمات' : 'طرف خدمات جدید' }}
        </h2>

        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label class="block">
            <span class="mb-1 block text-sm">نام <span class="text-error">*</span></span>
            <input v-model="form.name" class="fld" placeholder="قالیشویی نوری" />
          </label>
          <label class="block">
            <span class="mb-1 block text-sm">تخصص</span>
            <input v-model="form.specialty" class="fld" placeholder="قالیشویی و لکه‌گیری" />
          </label>
          <label class="block">
            <span class="mb-1 block text-sm">تلفن</span>
            <input v-model="form.phone" dir="ltr" class="fld" />
          </label>
          <label class="block">
            <span class="mb-1 block text-sm">نشانی</span>
            <input v-model="form.address" class="fld" />
          </label>
        </div>

        <label class="mt-3 block">
          <span class="mb-1 block text-sm">یادداشت</span>
          <input v-model="form.notes" class="fld" placeholder="شرایط همکاری، مهلت تسویه و…" />
        </label>

        <!-- نرخ‌های توافقی -->
        <div class="mt-5 border-t border-outline-variant pt-4">
          <h3 class="mb-1 text-sm font-semibold">نرخ‌های توافقی</h3>
          <p class="mb-3 text-xs text-on-surface-variant">
            برای هر نوع مرحله‌ای که این طرف انجام می‌دهد یک نرخ تعریف کنید. هنگام ثبت مرحله،
            اگر مبلغ دستی وارد نشود همین نرخ خودکار اعمال می‌شود.
          </p>

          <div v-if="form.rates.length === 0" class="rounded-lg border border-dashed border-outline-variant px-4 py-3 text-sm text-on-surface-variant">
            هنوز نرخی تعریف نشده — از فهرست پایین یک مرحله اضافه کنید.
          </div>

          <ul v-else class="space-y-2">
            <li v-for="(rate, index) in form.rates" :key="rate.processStepTypeId"
                class="flex flex-wrap items-end gap-2 rounded-lg border border-outline-variant p-2">
              <span class="min-w-28 flex-1 font-medium">{{ stepName(rate) }}</span>

              <label class="block">
                <span class="mb-1 block text-xs text-on-surface-variant">روش</span>
                <select v-model.number="rate.pricingModel" class="fld">
                  <option v-for="m in PRICING_MODELS" :key="m.value" :value="m.value">{{ m.label }}</option>
                </select>
              </label>

              <label class="block">
                <span class="mb-1 block text-xs text-on-surface-variant">نرخ (تومان)</span>
                <MoneyInput v-model="rate.unitRate" />
              </label>

              <button type="button"
                      class="grid h-11 w-11 place-items-center rounded-lg text-error hover:bg-error-container"
                      @click="removeRate(index)">
                <AppIcon name="trash" class="h-4 w-4" :label="`حذف نرخ ${stepName(rate)}`" />
              </button>
            </li>
          </ul>

          <div v-if="availableStepTypes.length" class="mt-3 flex flex-wrap gap-1.5">
            <button v-for="t in availableStepTypes" :key="t.id" type="button"
                    class="inline-flex min-h-11 items-center gap-1 rounded-lg border border-outline-variant px-3 text-xs hover:bg-surface-container"
                    @click="addRate(t.id)">
              <AppIcon name="plus" class="h-3.5 w-3.5" />
              {{ t.nameFa }}
            </button>
          </div>
        </div>

        <div class="mt-5 flex flex-wrap items-center gap-3 border-t border-outline-variant pt-4">
          <label class="flex min-h-11 items-center gap-2 text-sm">
            <input v-model="form.isActive" type="checkbox" class="h-5 w-5 rounded border-outline-variant text-primary" />
            فعال
          </label>
          <div class="flex-1"></div>
          <button type="button" class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-4 hover:bg-surface-container"
                  @click="cancelEdit">انصراف</button>
          <button type="button" :disabled="saving"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
                  @click="save">
            <AppIcon name="check" class="h-4 w-4" />
            ذخیره
          </button>
        </div>
      </section>

      <div v-else class="flex justify-end">
        <button type="button"
                class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 font-semibold text-on-primary hover:bg-primary-hover"
                @click="startCreate">
          <AppIcon name="plus" class="h-5 w-5" />
          طرف خدمات جدید
        </button>
      </div>

      <!-- حالت خالی -->
      <div v-if="providers.length === 0"
           class="rounded-xl border border-dashed border-outline-variant bg-surface-container-lowest p-12 text-center">
        <span class="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-full bg-surface-container text-on-surface-variant">
          <AppIcon name="users" class="h-7 w-7" />
        </span>
        <p class="font-medium text-on-surface">هنوز طرف خدماتی ثبت نشده است</p>
        <p class="mx-auto mt-1 max-w-md text-sm text-on-surface-variant">
          قالیشوی، رفوگر یا دارکشی که با آن کار می‌کنید را اضافه کنید تا هزینهٔ مراحل و
          مانده‌حسابشان خودکار محاسبه شود.
        </p>
      </div>

      <!-- فهرست طرف‌ها -->
      <div v-else class="grid gap-4 lg:grid-cols-2">
        <article v-for="p in providers" :key="p.id"
                 class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm"
                 :class="{ 'opacity-70': !p.isActive }">
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <h2 class="flex flex-wrap items-center gap-2 font-bold">
                {{ p.name }}
                <span v-if="!p.isActive" class="rounded-full bg-surface-container-high px-2 py-0.5 text-xs font-normal text-on-surface-variant">
                  غیرفعال
                </span>
              </h2>
              <p v-if="p.specialty" class="text-sm text-on-surface-variant">{{ p.specialty }}</p>
              <p v-if="p.phone" class="font-mono text-xs text-on-surface-variant" dir="ltr">{{ p.phone }}</p>
            </div>

            <div class="flex shrink-0 gap-1">
              <button type="button" class="grid h-11 w-11 place-items-center rounded-lg border border-outline-variant hover:bg-surface-container"
                      @click="startEdit(p)">
                <AppIcon name="edit" class="h-4 w-4" :label="`ویرایش ${p.name}`" />
              </button>
              <button type="button" class="grid h-11 w-11 place-items-center rounded-lg border border-outline-variant hover:bg-surface-container"
                      @click="toggleActive(p)">
                <AppIcon :name="p.isActive ? 'close' : 'check'" class="h-4 w-4"
                         :label="p.isActive ? `غیرفعال کردن ${p.name}` : `فعال کردن ${p.name}`" />
              </button>
              <button type="button" class="grid h-11 w-11 place-items-center rounded-lg border border-outline-variant text-error hover:bg-error-container"
                      @click="remove(p)">
                <AppIcon name="trash" class="h-4 w-4" :label="`حذف ${p.name}`" />
              </button>
            </div>
          </div>

          <!-- نرخ‌ها -->
          <ul v-if="p.rates.length" class="mt-3 flex flex-wrap gap-1.5">
            <li v-for="r in p.rates" :key="r.id"
                class="rounded-lg bg-surface-container px-2.5 py-1 text-xs" data-numeric>
              {{ stepName(r) }}:
              <span class="font-medium">{{ faMoney(r.unitRate) }}</span>
              <span class="text-on-surface-variant"> ({{ modelLabel(r.pricingModel) }})</span>
            </li>
          </ul>
          <p v-else class="mt-3 text-xs text-on-surface-variant">نرخی تعریف نشده — نرخ پیش‌فرض نوع مرحله استفاده می‌شود.</p>

          <!-- خلاصهٔ مالی -->
          <dl v-if="balances[p.id]" class="mt-4 grid grid-cols-3 gap-2 border-t border-outline-variant pt-3 text-center text-sm" data-numeric>
            <div>
              <dt class="text-xs text-on-surface-variant">کار انجام‌شده</dt>
              <dd>{{ faMoney(balances[p.id].completedWorkTotal) }}</dd>
            </div>
            <div>
              <dt class="text-xs text-on-surface-variant">پرداختی</dt>
              <dd>{{ faMoney(balances[p.id].paidTotal) }}</dd>
            </div>
            <div>
              <dt class="text-xs text-on-surface-variant">مانده</dt>
              <dd class="font-bold" :class="balances[p.id].balance > 0 ? 'text-error' : 'text-success'">
                {{ faMoney(balances[p.id].balance) }}
              </dd>
            </div>
          </dl>

          <a :href="`/Providers/Statement/${p.id}`"
             class="mt-3 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-lg border border-outline-variant text-sm hover:bg-surface-container">
            <AppIcon name="download" class="h-4 w-4" />
            صورت‌حساب و تسویه
          </a>
        </article>
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
