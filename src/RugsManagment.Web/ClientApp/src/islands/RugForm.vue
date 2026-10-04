<script setup lang="ts">
/**
 * فرم ثبت/ویرایش فرش. اگر rugId داشته باشد حالت ویرایش است.
 * مساحت زنده (دقیق، مثل اکسل: ROUND(طول×عرض، ۲))، خانه‌های عدد با ارقام فارسی و پرسش «سانت یا متر؟»،
 * مبلغ با سطر «به حروف»، و فیلدهای سفارشی داینامیک (JSONB متادیتا) با تاریخ شمسی.
 * منطق محاسبهٔ هزینه در بک‌اند است؛ اینجا فقط ورودی و پیش‌نمایش سادهٔ سود.
 *
 * مبلغ (قیمت خرید و قیمت فروش هدف) فقط برای Business است (A.5): برای انباردار این بخش نیست و هر دو null
 * فرستاده می‌شوند؛ سرور null را «دست نزن» می‌خواند و مقدار ذخیره‌شده می‌ماند (erratum E5).
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { moneyVisible } from '@/lib/context'
import { faDecimal, faMoney } from '@/lib/format'
import { isLargeArea } from '@/lib/money'
import { message } from '@/lib/messages'
import { pieceAreaNumber, sub, toNumber, tryDec } from '@/lib/num'
import Field from '@/components/ui/Field.vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
import NumberField from '@/components/ui/NumberField.vue'
import type { CustomFieldDefinition, Rug, WorkflowTemplate } from '@/lib/types'

const props = defineProps<{ rugId?: string }>()
const isEdit = computed(() => !!props.rugId)
const canSeeMoney = moneyVisible()

const form = reactive({
  title: '', origin: '', pattern: '', material: '',
  knotDensity: null as number | null,
  lengthMeters: null as number | null,
  widthMeters: null as number | null,
  purchaseCost: null as number | null,
  targetSalePrice: null as number | null,
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

const lengthField = ref<InstanceType<typeof NumberField> | null>(null)
const widthField = ref<InstanceType<typeof NumberField> | null>(null)

const area = computed(() => pieceAreaNumber(form.lengthMeters, form.widthMeters))
const largeArea = computed(() => area.value !== null && isLargeArea(area.value))
const margin = computed(() => {
  const target = tryDec(form.targetSalePrice)
  if (!target) return null
  return toNumber(sub(target, tryDec(form.purchaseCost) ?? 0))
})

function optionsOf(f: CustomFieldDefinition): string[] {
  if (!f.optionsJson) return []
  try { return JSON.parse(f.optionsJson) as string[] } catch { return [] }
}

/** «208×210» در هر خانهٔ اندازه: اولی طول، دومی عرض. */
function onPair([length, width]: [number, number]) {
  form.lengthMeters = length
  form.widthMeters = width
}

/** عدد خانهٔ سفارشی به متن (متادیتا همه رشته‌اند). */
function setMetaNumber(key: string, value: number | null) {
  metadata[key] = value === null ? '' : String(value)
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
        material: rug.material ?? '', knotDensity: rug.knotDensity ?? null,
        widthMeters: rug.widthMeters, lengthMeters: rug.lengthMeters,
        purchaseCost: rug.purchaseCost ?? null, targetSalePrice: rug.targetSalePrice ?? null,
        notes: rug.notes ?? '',
      })
      if (rug.metadataJson) {
        try { Object.assign(metadata, JSON.parse(rug.metadataJson)) } catch { /* ignore */ }
      }
    }
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
})

/** خانه‌های عددی دیگر (رجشمار، مبلغ‌ها): خطای خودشان را نگه می‌دارند و پیش از ارسال پرسیده می‌شوند. */
const otherNumberFields = new Set<{ validate: () => string | null }>()
function track(el: unknown) {
  if (el && typeof (el as { validate?: unknown }).validate === 'function') {
    otherNumberFields.add(el as { validate: () => string | null })
  }
}

function validate(): boolean {
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
  let numbersOk = true
  for (const field of otherNumberFields) if (field.validate() !== null) numbersOk = false
  if (!numbersOk) error.value = message('E_VALIDATION')
  const lengthProblem = lengthField.value?.validate() ?? null
  const widthProblem = widthField.value?.validate() ?? null
  if (lengthProblem) fieldErrors.lengthMeters = lengthProblem
  else if (!form.lengthMeters || form.lengthMeters <= 0) fieldErrors.lengthMeters = message('E_REQUIRED', { label: 'طول' })
  if (widthProblem) fieldErrors.widthMeters = widthProblem
  else if (!form.widthMeters || form.widthMeters <= 0) fieldErrors.widthMeters = message('E_REQUIRED', { label: 'عرض' })
  for (const f of customFields.value) {
    if (f.isRequired && !metadata[f.key]) fieldErrors['cf_' + f.key] = message('E_REQUIRED', { label: f.label })
  }
  return numbersOk && Object.keys(fieldErrors).length === 0
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
      // غیر Business هیچ مبلغی نمی‌فرستد (null = مقدار ذخیره‌شده بماند)
      purchaseCost: canSeeMoney ? form.purchaseCost ?? null : null,
      targetSalePrice: canSeeMoney ? form.targetSalePrice ?? null : null,
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
    error.value = errorMessage(e)
    if (e instanceof ApiError) {
      for (const key of ['lengthMeters', 'widthMeters', 'purchaseCost', 'targetSalePrice']) {
        const problem = e.fieldError(key)
        if (problem) fieldErrors[key] = problem
      }
    }
    saving.value = false
  }
}
</script>

<template>
  <div v-if="loading" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center text-on-surface-variant">
    در حال بارگذاری…
  </div>
  <form v-else class="space-y-6" novalidate @submit.prevent="submit">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">{{ error }}</div>

    <!-- مشخصات پایه -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
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
        <Field label="رجشمار" v-slot="f">
          <NumberField :id="f.id" :ref="track" v-model="form.knotDensity" kind="integer" :described-by="f.describedBy" label="رجشمار" />
        </Field>
      </div>
    </section>

    <!-- ابعاد (متر) -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-1 text-sm font-semibold text-primary">اندازه (متر)</h2>
      <p class="mb-4 text-xs text-on-surface-variant">به متر بنویسید، مثل ۲٫۰۸؛ «۲۰۸×۲۱۰» هر دو خانه را با هم پر می‌کند.</p>
      <div class="grid gap-4 sm:grid-cols-3">
        <Field label="طول" :error="fieldErrors.lengthMeters" required v-slot="f">
          <NumberField
            :id="f.id"
            ref="lengthField"
            v-model="form.lengthMeters"
            kind="dim"
            label="طول"
            required
            :described-by="f.describedBy"
            :invalid="f.invalid"
            @pair="onPair"
            @update:model-value="delete fieldErrors.lengthMeters"
          />
        </Field>
        <Field label="عرض" :error="fieldErrors.widthMeters" required v-slot="f">
          <NumberField
            :id="f.id"
            ref="widthField"
            v-model="form.widthMeters"
            kind="dim"
            label="عرض"
            required
            :described-by="f.describedBy"
            :invalid="f.invalid"
            @pair="onPair"
            @update:model-value="delete fieldErrors.widthMeters"
          />
        </Field>
        <div class="block">
          <span class="mb-1 block text-sm font-medium">مساحت</span>
          <div class="min-h-11 rounded-lg border border-dashed border-outline-variant bg-surface-container px-3 py-2.5 text-end font-semibold" data-numeric>
            {{ area !== null ? `${faDecimal(area, 2, 2)} متر` : '—' }}
          </div>
          <p v-if="largeArea" class="mt-1 flex items-start gap-1 text-xs text-warning" role="status">
            <svg class="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" focusable="false"><use :href="'/icons.svg#warning'" /></svg>
            {{ message('W_LARGE_AREA', { area: faDecimal(area, 2) }) }}
          </p>
        </div>
      </div>
    </section>

    <!-- قیمت (فقط بازرگان و مدیر) -->
    <section v-if="canSeeMoney" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">قیمت</h2>
      <div class="grid gap-4 sm:grid-cols-3">
        <Field label="قیمت خرید" :error="fieldErrors.purchaseCost" v-slot="f">
          <NumberField :id="f.id" :ref="track" v-model="form.purchaseCost" kind="money" label="قیمت خرید" :described-by="f.describedBy" :invalid="f.invalid" />
        </Field>
        <Field label="قیمت فروش هدف" :error="fieldErrors.targetSalePrice" v-slot="f">
          <NumberField :id="f.id" :ref="track" v-model="form.targetSalePrice" kind="money" label="قیمت فروش هدف" :described-by="f.describedBy" :invalid="f.invalid" />
        </Field>
        <div class="block">
          <span class="mb-1 block text-sm font-medium">سود ناخالص (تخمینی)</span>
          <div class="min-h-11 rounded-lg border border-dashed border-outline-variant px-3 py-2.5 text-end font-semibold" data-numeric
               :class="margin === null ? 'text-on-surface-variant' : margin >= 0 ? 'text-success' : 'text-error'">
            {{ margin === null ? '—' : faMoney(margin) }}
          </div>
        </div>
      </div>
    </section>

    <!-- فیلدهای سفارشی کارگاه -->
    <section v-if="customFields.length" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مشخصات بیشتر</h2>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Field v-for="f in customFields" :key="f.id" :label="f.label" :required="f.isRequired" :error="fieldErrors['cf_' + f.key]" v-slot="slot">
          <select v-if="f.fieldType === 3" :id="slot.id" v-model="metadata[f.key]" class="input" :aria-describedby="slot.describedBy">
            <option value="">—</option>
            <option v-for="o in optionsOf(f)" :key="o" :value="o">{{ o }}</option>
          </select>
          <label v-else-if="f.fieldType === 4" class="flex min-h-11 items-center gap-2">
            <input :id="slot.id" type="checkbox" :checked="metadata[f.key] === 'true'"
                   @change="metadata[f.key] = ($event.target as HTMLInputElement).checked ? 'true' : 'false'"
                   class="h-5 w-5 rounded border-outline-variant text-primary" />
            <span class="text-sm text-on-surface-variant">بله</span>
          </label>
          <JalaliDateField
            v-else-if="f.fieldType === 2"
            :id="slot.id"
            :model-value="metadata[f.key] || null"
            :described-by="slot.describedBy"
            :required="f.isRequired"
            @update:model-value="metadata[f.key] = $event ?? ''"
          />
          <NumberField
            v-else-if="f.fieldType === 1"
            :id="slot.id"
            :model-value="tryDec(metadata[f.key]) ? Number(metadata[f.key]) : null"
            kind="number"
            :scale="4"
            allow-negative
            :label="f.label"
            :described-by="slot.describedBy"
            @update:model-value="setMetaNumber(f.key, $event)"
          />
          <input v-else :id="slot.id" v-model="metadata[f.key]" class="input" :aria-describedby="slot.describedBy" />
        </Field>
      </div>
    </section>

    <!-- مسیر گردش کار (فقط هنگام ثبت) -->
    <section v-if="!isEdit" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مسیر گردش کار</h2>
      <select v-model="form.workflowTemplateId" class="input max-w-md" aria-label="مسیر گردش کار">
        <option value="">بدون مسیر (بعداً تعریف می‌شود)</option>
        <option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option>
      </select>
      <p class="mt-2 text-xs text-on-surface-variant">مسیر انتخابی برای این فرش کپی می‌شود و بعداً قابل ویرایش است.</p>
    </section>

    <!-- یادداشت -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <label class="block">
        <span class="mb-1 block text-sm font-semibold text-primary">یادداشت</span>
        <textarea v-model="form.notes" rows="3" class="input"></textarea>
      </label>
    </section>

    <!-- اکشن‌ها (چسبان پایین در موبایل) -->
    <div class="sticky bottom-0 -mx-4 flex gap-3 border-t border-outline-variant bg-surface/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0 md:py-0">
      <a href="/Rugs" class="flex min-h-11 flex-1 items-center justify-center rounded-lg border border-outline-variant px-4 text-center hover:bg-surface-container md:flex-none">انصراف</a>
      <button type="submit" :disabled="saving"
              class="min-h-11 flex-1 rounded-lg bg-primary px-6 font-semibold text-on-primary hover:bg-primary-hover disabled:opacity-60 md:flex-none">
        {{ saving ? message('I_SAVING') : isEdit ? 'ذخیرهٔ تغییرات' : 'ثبت فرش' }}
      </button>
    </div>
  </form>
</template>

<style scoped>
.input {
  min-height: 2.75rem;
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  color: var(--color-on-surface);
  padding: 0.625rem 0.75rem;
  outline: none;
}
.input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
</style>
