<script setup lang="ts">
/**
 * کاتالوگ انواع مرحلهٔ کارگاه.
 *
 * مرحله‌های سیستمی فقط دیده می‌شوند (قفل)، ولی کارگاه می‌تواند مرحلهٔ اختصاصی بسازد،
 * برایش «مدت معمول» تعیین کند (مبنای هشدار کهنگی) و یک فرم داینامیک برایش طراحی کند
 * که هنگام تکمیل آن مرحله پر می‌شود.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { api } from '@/lib/api'
import { faNumber } from '@/lib/format'
import { confirmDialog, toast } from '@/lib/ui'

interface StepField {
  key: string
  label: string
  type: number
  required: boolean
  options?: string[]
  hint?: string
}

interface StepType {
  id: string
  code: string
  nameFa: string
  nameEn: string
  icon: string
  sortOrder: number
  defaultPricingModel: number
  defaultUnitRate: number
  fieldSchemaJson?: string
  expectedDurationDays?: number
  isActive: boolean
  isSystem: boolean
}

const FIELD_TYPES = [
  { value: 0, label: 'متن' },
  { value: 1, label: 'عدد' },
  { value: 2, label: 'تاریخ' },
  { value: 3, label: 'انتخابی' },
  { value: 4, label: 'بله/خیر' },
]

const PRICING_MODELS = [
  { value: 0, label: 'مبلغ ثابت' },
  { value: 1, label: 'به ازای متر مربع' },
  { value: 4, label: 'به ازای طول' },
  { value: 5, label: 'به ازای عرض' },
  { value: 6, label: 'ترکیبی' },
]

const items = ref<StepType[]>([])
const loading = ref(true)
const saving = ref(false)
const editingId = ref<string | null>(null)
const showForm = ref(false)

const form = reactive({
  nameFa: '',
  nameEn: '',
  icon: 'workflow',
  sortOrder: 100,
  defaultPricingModel: 1,
  defaultUnitRate: 0,
  expectedDurationDays: null as number | null,
  isActive: true,
  fields: [] as StepField[],
})

const systemSteps = computed(() => items.value.filter((s) => s.isSystem))
const ownSteps = computed(() => items.value.filter((s) => !s.isSystem))

const typeLabel = (v: number) => FIELD_TYPES.find((t) => t.value === v)?.label ?? '—'
const pricingLabel = (v: number) => PRICING_MODELS.find((p) => p.value === v)?.label ?? '—'

function fieldCount(s: StepType): number {
  if (!s.fieldSchemaJson) return 0
  try {
    return (JSON.parse(s.fieldSchemaJson) as StepField[]).length
  } catch {
    return 0
  }
}

async function load() {
  loading.value = true
  try {
    items.value = await api.get<StepType[]>('/api/step-types?includeInactive=true')
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editingId.value = null
  Object.assign(form, {
    nameFa: '', nameEn: '', icon: 'workflow',
    // بعد از آخرین مرحله قرار می‌گیرد
    sortOrder: Math.max(0, ...items.value.map((s) => s.sortOrder)) + 10,
    defaultPricingModel: 1, defaultUnitRate: 0,
    expectedDurationDays: null, isActive: true, fields: [],
  })
  showForm.value = true
}

function startEdit(s: StepType) {
  editingId.value = s.id
  let fields: StepField[] = []
  try {
    if (s.fieldSchemaJson) fields = JSON.parse(s.fieldSchemaJson)
  } catch {
    /* اسکیمای خراب نباید فرم را بشکند */
  }

  Object.assign(form, {
    nameFa: s.nameFa, nameEn: s.nameEn, icon: s.icon, sortOrder: s.sortOrder,
    defaultPricingModel: s.defaultPricingModel, defaultUnitRate: s.defaultUnitRate,
    expectedDurationDays: s.expectedDurationDays ?? null,
    isActive: s.isActive, fields,
  })
  showForm.value = true
}

// ── طراح فرم داینامیک ──

function addField() {
  if (form.fields.length >= 15) {
    toast.warning('حداکثر ۱۵ فیلد برای هر مرحله.')
    return
  }
  form.fields.push({ key: `field_${form.fields.length + 1}`, label: '', type: 0, required: false })
}

function removeField(index: number) {
  form.fields.splice(index, 1)
}

/** گزینه‌های «انتخابی» به‌صورت متن با کاما وارد می‌شوند. */
function optionsText(f: StepField): string {
  return (f.options ?? []).join('، ')
}

function setOptions(f: StepField, text: string) {
  f.options = text
    .split(/[،,]/)
    .map((o) => o.trim())
    .filter(Boolean)
}

async function save() {
  if (!form.nameFa.trim()) {
    toast.warning('نام مرحله الزامی است.')
    return
  }

  // اعتبارسنجی محلی تا کاربر خطای سرور نگیرد
  const keys = new Set<string>()
  for (const f of form.fields) {
    if (!f.key.trim() || !f.label.trim()) {
      toast.warning('همهٔ فیلدها باید کلید و برچسب داشته باشند.')
      return
    }
    if (!/^[A-Za-z0-9_]+$/.test(f.key)) {
      toast.warning(`کلید «${f.key}» فقط می‌تواند حروف انگلیسی، عدد و زیرخط داشته باشد.`)
      return
    }
    if (keys.has(f.key.toLowerCase())) {
      toast.warning(`کلید «${f.key}» تکراری است.`)
      return
    }
    keys.add(f.key.toLowerCase())
    if (f.type === 3 && (!f.options || f.options.length === 0)) {
      toast.warning(`فیلد «${f.label}» از نوع انتخابی است و باید گزینه داشته باشد.`)
      return
    }
  }

  saving.value = true
  try {
    const payload = {
      nameFa: form.nameFa,
      nameEn: form.nameEn || null,
      icon: form.icon,
      sortOrder: form.sortOrder,
      defaultPricingModel: form.defaultPricingModel,
      defaultUnitRate: form.defaultUnitRate,
      expectedDurationDays: form.expectedDurationDays,
      fieldSchemaJson: form.fields.length ? JSON.stringify(form.fields) : null,
      isActive: form.isActive,
    }

    if (editingId.value) await api.put(`/api/step-types/${editingId.value}`, payload)
    else await api.post('/api/step-types', payload)

    toast.success(editingId.value ? 'مرحله ویرایش شد.' : 'مرحله ساخته شد.')
    showForm.value = false
    await load()
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    saving.value = false
  }
}

async function remove(s: StepType) {
  const ok = await confirmDialog({
    title: `مرحلهٔ «${s.nameFa}» حذف شود؟`,
    message: 'اگر این مرحله در قالبی یا روی فرشی استفاده شده باشد، به‌جای حذف غیرفعال می‌شود تا تاریخچه از بین نرود.',
    confirmLabel: 'حذف مرحله',
    danger: true,
  })
  if (!ok) return

  try {
    await api.del(`/api/step-types/${s.id}`)
    toast.success('انجام شد.')
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
      <!-- ── فرم ساخت/ویرایش ── -->
      <section v-if="showForm" class="rounded-xl border border-primary/40 bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-4 flex items-center gap-2 text-sm font-semibold text-primary">
          <AppIcon name="workflow" class="h-4 w-4" />
          {{ editingId ? 'ویرایش مرحله' : 'مرحلهٔ جدید' }}
        </h2>

        <div class="grid gap-4 sm:grid-cols-2">
          <label class="block">
            <span class="mb-1 block text-sm font-medium">نام مرحله <span class="text-error">*</span></span>
            <input v-model="form.nameFa" class="fld" placeholder="مثلاً رنگرزی" />
          </label>

          <label class="block">
            <span class="mb-1 block text-sm font-medium">ترتیب نمایش</span>
            <input v-model.number="form.sortOrder" type="number" dir="ltr" class="fld" />
          </label>

          <label class="block">
            <span class="mb-1 block text-sm font-medium">روش قیمت‌گذاری پیش‌فرض</span>
            <select v-model.number="form.defaultPricingModel" class="fld">
              <option v-for="p in PRICING_MODELS" :key="p.value" :value="p.value">{{ p.label }}</option>
            </select>
          </label>

          <label class="block">
            <span class="mb-1 block text-sm font-medium">نرخ پیش‌فرض</span>
            <MoneyInput v-model="form.defaultUnitRate" />
          </label>

          <label class="block">
            <span class="mb-1 block text-sm font-medium">مدت معمول انجام (روز)</span>
            <input v-model.number="form.expectedDurationDays" type="number" min="1" max="365" dir="ltr"
                   class="fld" placeholder="اختیاری" />
            <span class="mt-1 block text-xs text-on-surface-variant">
              اگر فرشی بیشتر از این بماند، در گزارش «فرش‌های گیرکرده» علامت می‌خورد.
            </span>
          </label>

          <label class="flex min-h-11 items-center gap-2 self-end text-sm">
            <input v-model="form.isActive" type="checkbox" class="h-5 w-5 rounded border-outline-variant text-primary" />
            فعال (در فهرست انتخاب مرحله بیاید)
          </label>
        </div>

        <!-- طراح فرم داینامیک -->
        <div class="mt-6 border-t border-outline-variant pt-4">
          <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 class="text-sm font-semibold">فرم این مرحله</h3>
              <p class="text-xs text-on-surface-variant">
                هنگام تکمیل این مرحله از اپراتور پرسیده می‌شود — مثلاً «کد رنگ» یا «دمای آب».
              </p>
            </div>
            <button type="button"
                    class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container"
                    @click="addField">
              <AppIcon name="plus" class="h-4 w-4" />
              افزودن فیلد
            </button>
          </div>

          <p v-if="form.fields.length === 0" class="rounded-lg bg-surface-container px-4 py-6 text-center text-sm text-on-surface-variant">
            فیلدی تعریف نشده — هنگام تکمیل این مرحله فقط هزینه و توضیح پرسیده می‌شود.
          </p>

          <div v-else class="space-y-2">
            <div v-for="(f, i) in form.fields" :key="i"
                 class="grid gap-2 rounded-lg border border-outline-variant p-3 sm:grid-cols-12">
              <label class="block sm:col-span-3">
                <span class="mb-1 block text-xs">برچسب</span>
                <input v-model="f.label" class="fld" placeholder="کد رنگ" />
              </label>

              <label class="block sm:col-span-3">
                <span class="mb-1 block text-xs">کلید (انگلیسی)</span>
                <input v-model="f.key" dir="ltr" class="fld font-mono text-xs" />
              </label>

              <label class="block sm:col-span-2">
                <span class="mb-1 block text-xs">نوع</span>
                <select v-model.number="f.type" class="fld">
                  <option v-for="t in FIELD_TYPES" :key="t.value" :value="t.value">{{ t.label }}</option>
                </select>
              </label>

              <label v-if="f.type === 3" class="block sm:col-span-3">
                <span class="mb-1 block text-xs">گزینه‌ها (با کاما)</span>
                <input :value="optionsText(f)" class="fld" placeholder="قرمز، آبی، سبز"
                       @input="setOptions(f, ($event.target as HTMLInputElement).value)" />
              </label>

              <label class="flex min-h-11 items-center gap-1 self-end text-xs sm:col-span-2">
                <input v-model="f.required" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
                الزامی
              </label>

              <div class="flex items-end justify-end sm:col-span-1">
                <button type="button"
                        class="grid h-11 w-11 place-items-center rounded-lg text-error hover:bg-error-container"
                        @click="removeField(i)">
                  <AppIcon name="trash" class="h-4 w-4" :label="`حذف فیلد ${f.label || i + 1}`" />
                </button>
              </div>
            </div>
          </div>
        </div>

        <div class="mt-5 flex justify-end gap-3 border-t border-outline-variant pt-4">
          <button type="button"
                  class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-4 hover:bg-surface-container"
                  @click="showForm = false">انصراف</button>
          <button type="button" :disabled="saving"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
                  @click="save">
            <AppIcon name="check" class="h-4 w-4" />
            ذخیره
          </button>
        </div>
      </section>

      <!-- ── مرحله‌های اختصاصی کارگاه ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 class="flex items-center gap-2 text-sm font-semibold text-primary">
              <AppIcon name="workflow" class="h-4 w-4" />
              مرحله‌های اختصاصی کارگاه
            </h2>
            <p class="text-xs text-on-surface-variant">مرحله‌هایی که خودتان تعریف کرده‌اید</p>
          </div>
          <button v-if="!showForm" type="button"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
                  @click="startCreate">
            <AppIcon name="plus" class="h-4 w-4" />
            مرحلهٔ جدید
          </button>
        </div>

        <p v-if="ownSteps.length === 0" class="rounded-lg bg-surface-container px-4 py-8 text-center text-sm text-on-surface-variant">
          هنوز مرحلهٔ اختصاصی نساخته‌اید. مرحله‌های پیش‌فرض سامانه پایین آمده‌اند.
        </p>

        <ul v-else class="divide-y divide-outline-variant">
          <li v-for="s in ownSteps" :key="s.id" class="flex flex-wrap items-center gap-3 py-3">
            <span class="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-secondary-container text-on-secondary-container">
              <AppIcon :name="s.icon || 'workflow'" class="h-4 w-4" />
            </span>

            <div class="min-w-0 flex-1">
              <p class="flex flex-wrap items-center gap-2 font-medium">
                {{ s.nameFa }}
                <span v-if="!s.isActive" class="rounded-full bg-surface-container-high px-2 py-0.5 text-xs text-on-surface-variant">
                  غیرفعال
                </span>
              </p>
              <p class="text-xs text-on-surface-variant" data-numeric>
                {{ pricingLabel(s.defaultPricingModel) }}
                <template v-if="s.expectedDurationDays"> · مدت معمول {{ faNumber(s.expectedDurationDays) }} روز</template>
                <template v-if="fieldCount(s)"> · {{ faNumber(fieldCount(s)) }} فیلد فرم</template>
              </p>
            </div>

            <div class="flex gap-1.5">
              <button type="button"
                      class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant px-3 text-xs hover:bg-surface-container"
                      @click="startEdit(s)">
                <AppIcon name="edit" class="h-4 w-4" />
                ویرایش
              </button>
              <button type="button"
                      class="grid h-11 w-11 place-items-center rounded-lg border border-outline-variant text-error hover:bg-error-container"
                      @click="remove(s)">
                <AppIcon name="trash" class="h-4 w-4" :label="`حذف ${s.nameFa}`" />
              </button>
            </div>
          </li>
        </ul>
      </section>

      <!-- ── مرحله‌های سیستمی (فقط خواندنی) ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-1 flex items-center gap-2 text-sm font-semibold text-primary">
          <AppIcon name="info" class="h-4 w-4" />
          مرحله‌های پیش‌فرض سامانه
        </h2>
        <p class="mb-4 text-xs text-on-surface-variant">
          این‌ها برای همهٔ کارگاه‌ها مشترک‌اند و قابل ویرایش نیستند. برای نسخهٔ دلخواه خودتان، مرحلهٔ اختصاصی بسازید.
        </p>

        <ul class="flex flex-wrap gap-2">
          <li v-for="s in systemSteps" :key="s.id"
              class="inline-flex items-center gap-1.5 rounded-full bg-surface-container px-3 py-1.5 text-sm">
            <AppIcon :name="s.icon || 'workflow'" class="h-4 w-4 text-on-surface-variant" />
            {{ s.nameFa }}
          </li>
        </ul>
      </section>
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
