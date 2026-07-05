<script setup lang="ts">
/**
 * فرم ثبت/ویرایش فرش. اگر rugId داشته باشد حالت ویرایش است.
 * محاسبهٔ مساحت زنده، ورودی مبلغ با جداکننده، و فیلدهای سفارشی داینامیک (JSONB متادیتا).
 * منطق محاسبهٔ هزینه در بک‌اند است؛ اینجا فقط ورودی و پیش‌نمایش سادهٔ سود.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { api } from '@/lib/api'
import { formatThousands } from '@/lib/money'
import MoneyInput from '@/components/MoneyInput.vue'
import type { CustomFieldDefinition, Rug, WorkflowTemplate } from '@/lib/types'

const props = defineProps<{ rugId?: string }>()
const isEdit = computed(() => !!props.rugId)

const form = reactive({
  title: '', origin: '', pattern: '', material: '',
  knotDensity: undefined as number | undefined,
  widthMeters: undefined as number | undefined,
  lengthMeters: undefined as number | undefined,
  purchaseCost: undefined as number | undefined,
  targetSalePrice: undefined as number | undefined,
  notes: '',
  workflowTemplateId: '' as string,
})

const metadata = reactive<Record<string, string>>({})
const templates = ref<WorkflowTemplate[]>([])
const customFields = ref<CustomFieldDefinition[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const fieldErrors = reactive<Record<string, string>>({})

const area = computed(() => {
  const w = form.widthMeters ?? 0, l = form.lengthMeters ?? 0
  return w > 0 && l > 0 ? Math.round(w * l * 100) / 100 : 0
})
const margin = computed(() => (form.targetSalePrice ?? 0) - (form.purchaseCost ?? 0))

function optionsOf(f: CustomFieldDefinition): string[] {
  if (!f.optionsJson) return []
  try { return JSON.parse(f.optionsJson) as string[] } catch { return [] }
}

onMounted(async () => {
  try {
    const [tpl, cf] = await Promise.all([
      api.get<WorkflowTemplate[]>('/api/lookups/workflow-templates'),
      api.get<CustomFieldDefinition[]>('/api/lookups/custom-fields'),
    ])
    templates.value = tpl
    customFields.value = cf
    if (!isEdit.value) {
      form.workflowTemplateId = tpl.find((t) => t.isDefault)?.id ?? tpl[0]?.id ?? ''
    }
    if (props.rugId) {
      const rug = await api.get<Rug>(`/api/rugs/${props.rugId}`)
      Object.assign(form, {
        title: rug.title ?? '', origin: rug.origin ?? '', pattern: rug.pattern ?? '',
        material: rug.material ?? '', knotDensity: rug.knotDensity,
        widthMeters: rug.widthMeters, lengthMeters: rug.lengthMeters,
        purchaseCost: rug.purchaseCost, targetSalePrice: rug.targetSalePrice,
        notes: rug.notes ?? '',
      })
      if (rug.metadataJson) {
        try { Object.assign(metadata, JSON.parse(rug.metadataJson)) } catch { /* ignore */ }
      }
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
})

function validate(): boolean {
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
  if (!form.widthMeters || form.widthMeters <= 0) fieldErrors.widthMeters = 'عرض باید بزرگ‌تر از صفر باشد.'
  if (!form.lengthMeters || form.lengthMeters <= 0) fieldErrors.lengthMeters = 'طول باید بزرگ‌تر از صفر باشد.'
  for (const f of customFields.value) {
    if (f.isRequired && !metadata[f.key]) fieldErrors['cf_' + f.key] = `«${f.label}» الزامی است.`
  }
  return Object.keys(fieldErrors).length === 0
}

async function submit() {
  error.value = ''
  if (!validate()) return
  saving.value = true
  try {
    const payload = {
      title: form.title || null, origin: form.origin || null, pattern: form.pattern || null,
      material: form.material || null, knotDensity: form.knotDensity ?? null,
      widthMeters: form.widthMeters, lengthMeters: form.lengthMeters,
      purchaseCost: form.purchaseCost ?? null, targetSalePrice: form.targetSalePrice ?? null,
      imageUrl: null, notes: form.notes || null,
      metadataJson: Object.keys(metadata).length ? JSON.stringify(metadata) : null,
    }
    let rug: Rug
    if (isEdit.value) {
      rug = await api.put<Rug>(`/api/rugs/${props.rugId}`, { ...payload, status: null })
    } else {
      rug = await api.post<Rug>('/api/rugs', {
        ...payload,
        workflowTemplateId: form.workflowTemplateId || null,
        skippedOptionalStepIds: null, customSteps: null,
      })
    }
    window.location.href = `/Rugs/Details/${rug.id}`
  } catch (e) {
    error.value = (e as Error).message
    saving.value = false
  }
}
</script>

<template>
  <div v-if="loading" class="rounded-xl border border-outline-variant bg-white p-8 text-center text-on-surface-variant">
    در حال بارگذاری…
  </div>
  <form v-else class="space-y-6" @submit.prevent="submit">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>

    <!-- مشخصات پایه -->
    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مشخصات فرش</h2>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <label class="block">
          <span class="mb-1 block text-sm">عنوان</span>
          <input v-model="form.title" class="input" placeholder="مثلاً تبریز لاکی" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">اصالت / منشأ</span>
          <input v-model="form.origin" class="input" placeholder="تبریز، کاشان…" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">طرح / نقشه</span>
          <input v-model="form.pattern" class="input" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">جنس</span>
          <input v-model="form.material" class="input" placeholder="پشم، ابریشم…" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">رجشمار</span>
          <input v-model.number="form.knotDensity" type="number" min="0" dir="ltr" class="input text-end" />
        </label>
      </div>
    </section>

    <!-- ابعاد (متر) -->
    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">ابعاد (متر)</h2>
      <div class="grid gap-4 sm:grid-cols-3">
        <label class="block">
          <span class="mb-1 block text-sm">عرض (متر)</span>
          <input v-model.number="form.widthMeters" type="number" step="0.01" min="0" dir="ltr" class="input text-end" />
          <span v-if="fieldErrors.widthMeters" class="mt-1 block text-xs text-error">{{ fieldErrors.widthMeters }}</span>
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">طول (متر)</span>
          <input v-model.number="form.lengthMeters" type="number" step="0.01" min="0" dir="ltr" class="input text-end" />
          <span v-if="fieldErrors.lengthMeters" class="mt-1 block text-xs text-error">{{ fieldErrors.lengthMeters }}</span>
        </label>
        <div class="block">
          <span class="mb-1 block text-sm">مساحت (متر مربع)</span>
          <div class="rounded-lg border border-dashed border-outline-variant bg-surface-container px-3 py-2.5 text-end font-semibold" dir="ltr">
            {{ area || '—' }}
          </div>
        </div>
      </div>
    </section>

    <!-- قیمت -->
    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">قیمت (تومان)</h2>
      <div class="grid gap-4 sm:grid-cols-3">
        <label class="block">
          <span class="mb-1 block text-sm">قیمت خرید</span>
          <MoneyInput v-model="form.purchaseCost" suffix="تومان" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">قیمت فروش هدف</span>
          <MoneyInput v-model="form.targetSalePrice" suffix="تومان" />
        </label>
        <div class="block">
          <span class="mb-1 block text-sm">سود ناخالص (تخمینی)</span>
          <div class="rounded-lg border border-dashed border-outline-variant px-3 py-2.5 text-end font-semibold" dir="ltr"
               :class="margin >= 0 ? 'text-success' : 'text-error'">
            {{ formatThousands(margin) }}
          </div>
        </div>
      </div>
    </section>

    <!-- فیلدهای سفارشی کارگاه -->
    <section v-if="customFields.length" class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">فیلدهای سفارشی</h2>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <label v-for="f in customFields" :key="f.id" class="block">
          <span class="mb-1 block text-sm">{{ f.label }}<span v-if="f.isRequired" class="text-error">*</span></span>
          <select v-if="f.fieldType === 3" v-model="metadata[f.key]" class="input">
            <option value="">—</option>
            <option v-for="o in optionsOf(f)" :key="o" :value="o">{{ o }}</option>
          </select>
          <label v-else-if="f.fieldType === 4" class="flex items-center gap-2">
            <input type="checkbox" :checked="metadata[f.key] === 'true'"
                   @change="metadata[f.key] = ($event.target as HTMLInputElement).checked ? 'true' : 'false'"
                   class="h-4 w-4 rounded border-outline-variant text-primary" />
            <span class="text-sm text-on-surface-variant">بله</span>
          </label>
          <input v-else v-model="metadata[f.key]"
                 :type="f.fieldType === 1 ? 'number' : f.fieldType === 2 ? 'date' : 'text'"
                 :dir="f.fieldType === 1 ? 'ltr' : undefined" class="input" />
          <span v-if="fieldErrors['cf_' + f.key]" class="mt-1 block text-xs text-error">{{ fieldErrors['cf_' + f.key] }}</span>
        </label>
      </div>
    </section>

    <!-- مسیر گردش کار (فقط هنگام ثبت) -->
    <section v-if="!isEdit" class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مسیر گردش کار</h2>
      <select v-model="form.workflowTemplateId" class="input max-w-md">
        <option value="">بدون مسیر (بعداً تعریف می‌شود)</option>
        <option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option>
      </select>
      <p class="mt-2 text-xs text-on-surface-variant">مسیر انتخابی برای این فرش کپی می‌شود و بعداً قابل ویرایش است.</p>
    </section>

    <!-- یادداشت -->
    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <label class="block">
        <span class="mb-1 block text-sm font-semibold text-primary">یادداشت</span>
        <textarea v-model="form.notes" rows="3" class="input"></textarea>
      </label>
    </section>

    <!-- اکشن‌ها (چسبان پایین در موبایل) -->
    <div class="sticky bottom-0 -mx-4 flex gap-3 border-t border-outline-variant bg-surface/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0 md:py-0">
      <a href="/Rugs" class="flex-1 rounded-lg border border-outline-variant px-4 py-3 text-center hover:bg-surface-container md:flex-none">انصراف</a>
      <button type="submit" :disabled="saving"
              class="flex-1 rounded-lg bg-primary px-6 py-3 font-semibold text-on-primary hover:opacity-90 disabled:opacity-60 md:flex-none">
        {{ saving ? 'در حال ذخیره…' : isEdit ? 'ذخیرهٔ تغییرات' : 'ثبت فرش' }}
      </button>
    </div>
  </form>
</template>

<style scoped>
.input {
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  padding: 0.625rem 0.75rem;
  outline: none;
}
.input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
</style>
