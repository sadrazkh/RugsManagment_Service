<script setup lang="ts">
/**
 * ترجیحات کارگاه (`/settings/preferences`، فقط تنظیم‌کننده) — یک بار تنظیم می‌شود تا بازرگان دیگر با آن کار
 * نداشته باشد: شماره‌گذاری فرش، واژهٔ نگله/عدل و متراژ هدف، ارزش گمرکی و دلار، پیش‌فرض‌های هزینه، و
 * سربرگ صادرکننده.
 *
 * داده از `GET /api/settings/preferences` می‌آید و با `PUT` همان مسیر برمی‌گردد، همراه `version` خوانده‌شده؛
 * اگر کس دیگری در این فاصله ذخیره کرده باشد سرور ۴۰۹ می‌دهد و فرم پیشنهاد بارگذاری دوباره می‌دهد (نه
 * بازنویسی بی‌صدا). تعداد رقم اعشار مبلغ پس از ثبت اولین مبلغ قفل است (`moneyScaleLocked`).
 *
 * واژه‌ها این‌جا «زبان تنظیم‌کننده»اند (روش تقسیم، پایهٔ متراژ…) — صفحه فقط در حالت کامل و برای Configure است.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import {
  ALLOCATION_METHOD_LABELS,
  AREA_BASIS_LABELS,
  BALE_TERM_LABELS,
  DECLARED_VALUE_KEY_FIELDS,
  SERIAL_MODE_LABELS,
} from '@/lib/labels'
import { toast } from '@/lib/ui'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import Field from '@/components/ui/Field.vue'
import NumberField from '@/components/ui/NumberField.vue'
import type { CustomFieldDefinition } from '@/lib/types'
import type { TenantPreferences } from '@/lib/types/platform'

const ENDPOINT = '/api/settings/preferences'

const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const formError = ref('')
/** ۴۰۹: کسی دیگر ذخیره کرده؛ فقط بارگذاری دوباره */
const stale = ref(false)
const fieldErrors = reactive<Record<string, string>>({})

const prefs = ref<TenantPreferences | null>(null)
/** آنچه آخرین بار از سرور آمد — برای «تغییری داده‌اید؟» */
const savedJson = ref('')
const customFields = ref<CustomFieldDefinition[]>([])

const currency = appContext().currency.label

const dirty = computed(() => prefs.value !== null && JSON.stringify(prefs.value) !== savedJson.value)

const keyFieldOptions = computed(() => {
  const options = [
    ...DECLARED_VALUE_KEY_FIELDS,
    ...customFields.value
      .filter((f) => f.fieldType === 0 || f.fieldType === 3)
      .map((f) => ({ key: `rug.custom.${f.key}`, label: f.label })),
  ]
  const current = prefs.value?.declaredValueKeyField
  if (current && !options.some((o) => o.key === current)) options.push({ key: current, label: current })
  return options
})

function accept(data: TenantPreferences) {
  prefs.value = { ...data }
  savedJson.value = JSON.stringify(prefs.value)
  stale.value = false
  formError.value = ''
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [data, fields] = await Promise.all([
      api.get<TenantPreferences>(ENDPOINT),
      api.get<CustomFieldDefinition[]>('/api/lookups/custom-fields').catch(() => [] as CustomFieldDefinition[]),
    ])
    customFields.value = fields
    accept(data)
  } catch (e) {
    loadError.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

const SKU_PREFIX = /^[A-Za-z0-9]{1,12}$/

/**
 * خانه‌های عددی: هر کدام خطای خودش (بیرون از بازه، پرسش بی‌جواب) را نگه می‌دارد و null می‌فرستد؛ پیش از
 * ذخیره از همه می‌پرسیم تا null ناشی از خطا بی‌صدا مقدار ذخیره‌شده را پاک نکند.
 */
const numberFields = new Set<{ validate: () => string | null }>()
function track(el: unknown) {
  if (el && typeof (el as { validate?: unknown }).validate === 'function') numberFields.add(el as { validate: () => string | null })
}

function validate(p: TenantPreferences): boolean {
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
  let numbersOk = true
  for (const field of numberFields) if (field.validate() !== null) numbersOk = false
  p.skuPrefix = p.skuPrefix.trim()
  if (!SKU_PREFIX.test(p.skuPrefix)) fieldErrors.skuPrefix = message('E_SKU_PREFIX')
  if (!p.declaredValueKeyField) fieldErrors.declaredValueKeyField = message('E_REQUIRED', { label: 'فیلد ارزش گمرکی' })
  // این دو در سرور عدد اجباری‌اند (null یعنی خانه خالی مانده)
  if ((p.maxPlausibleSideM as number | null) === null) {
    fieldErrors.maxPlausibleSideM = message('E_REQUIRED', { label: 'بزرگ‌ترین ضلع معمول' })
  }
  if ((p.baleTargetTolerancePercent as number | null) === null) {
    fieldErrors.baleTargetTolerancePercent = message('E_REQUIRED', { label: 'بازهٔ مجاز اطراف هدف' })
  }
  return numbersOk && Object.keys(fieldErrors).length === 0
}

async function save() {
  const p = prefs.value
  if (!p || saving.value) return
  formError.value = ''
  if (!validate(p)) {
    formError.value = message('E_VALIDATION')
    return
  }

  saving.value = true
  try {
    const updated = await api.put<TenantPreferences | undefined>(ENDPOINT, p)
    if (updated && typeof updated === 'object' && 'version' in updated) accept(updated)
    else accept(await api.get<TenantPreferences>(ENDPOINT))
    toast.success(message('I_PREFERENCES_SAVED'))
  } catch (e) {
    if (e instanceof ApiError && e.status === 409 && !e.field) {
      // نسخهٔ کهنه: کس دیگری ذخیره کرده
      stale.value = true
      formError.value = e.message || message('E_PREFERENCES_STALE')
    } else if (e instanceof ApiError && e.status === 409 && e.field) {
      // تعارض یک خانه، مثل عوض کردن رقم اعشار مبلغ پس از ثبت اولین مبلغ (field: «moneyScale»)
      fieldErrors[e.field] = e.message || message('E_MONEY_LOCKED')
      formError.value = e.message || message('E_MONEY_LOCKED')
    } else if (e instanceof ApiError && Object.keys(e.errors).length > 0) {
      for (const [key, list] of Object.entries(e.errors)) if (list[0]) fieldErrors[key] = list[0]
      formError.value = e.message
    } else {
      formError.value = errorMessage(e)
    }
  } finally {
    saving.value = false
  }
}

function revert() {
  if (savedJson.value) accept(JSON.parse(savedJson.value) as TenantPreferences)
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div v-if="loading" class="skeleton h-96 w-full" aria-hidden="true"></div>

    <EmptyState v-else-if="loadError || !prefs" icon="warning" title="ترجیحات بارگذاری نشد" :text="loadError">
      <template #actions>
        <Button variant="primary" icon="undo" @click="load">دوباره امتحان کن</Button>
      </template>
    </EmptyState>

    <form v-else class="space-y-6" novalidate @submit.prevent="save">
      <div v-if="formError" class="flex flex-wrap items-center gap-3 rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">
        <span class="flex-1">{{ formError }}</span>
        <Button v-if="stale" size="md" icon="undo" @click="load">بارگذاری دوباره</Button>
      </div>

      <!-- ── شماره‌گذاری ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-1 font-semibold text-primary">شمارهٔ فرش</h2>
        <p class="mb-4 text-sm text-on-surface-variant">شماره همان عددی است که بازرگان روی فرش می‌نویسد، مثل ۳۷۷۸ یا دستهٔ «۳۹۱۱ - ۳۹۶۰».</p>

        <fieldset class="mb-4">
          <legend class="mb-2 text-sm font-medium">شیوهٔ شماره‌گذاری</legend>
          <div class="grid gap-2 sm:grid-cols-3">
            <label
              v-for="(option, value) in SERIAL_MODE_LABELS"
              :key="value"
              class="flex min-h-11 cursor-pointer items-start gap-2 rounded-lg border p-3"
              :class="prefs.serialMode === Number(value) ? 'border-primary bg-primary/5' : 'border-outline-variant hover:bg-surface-container'"
            >
              <input v-model.number="prefs.serialMode" type="radio" name="serial-mode" :value="Number(value)" class="mt-1 h-4 w-4 accent-primary" />
              <span>
                <span class="block font-medium">{{ option.label }}</span>
                <span class="block text-xs text-on-surface-variant">{{ option.hint }}</span>
              </span>
            </label>
          </div>
        </fieldset>

        <div class="grid gap-4 sm:grid-cols-2">
          <Field label="شمارهٔ شروع" hint="شمارهٔ بعدی هیچ‌وقت از این کمتر پیشنهاد نمی‌شود؛ خالی یعنی از ۱." :error="fieldErrors.serialStart" v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="prefs.serialStart" kind="integer" :min="1" :described-by="f.describedBy" :invalid="f.invalid" placeholder="مثلاً ۳۷۰۰" />
          </Field>
          <Field label="پیشوند کد سامانه" hint="کد خودکار هر فرش با این شروع می‌شود (۱ تا ۱۲ حرف انگلیسی یا رقم)." :error="fieldErrors.skuPrefix" required v-slot="f">
            <input
              :id="f.id"
              v-model="prefs.skuPrefix"
              dir="ltr"
              maxlength="12"
              autocomplete="off"
              :aria-describedby="f.describedBy"
              :aria-invalid="f.invalid || undefined"
              class="fld font-mono"
            />
          </Field>
        </div>
      </section>

      <!-- ── نگله و محموله ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-4 font-semibold text-primary">نگله و محموله</h2>

        <fieldset class="mb-4">
          <legend class="mb-2 text-sm font-medium">به بسته‌ها چه می‌گویید؟</legend>
          <div class="flex flex-wrap gap-2">
            <label
              v-for="(option, value) in BALE_TERM_LABELS"
              :key="value"
              class="flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border px-4 py-2"
              :class="prefs.baleTerm === Number(value) ? 'border-primary bg-primary/5' : 'border-outline-variant hover:bg-surface-container'"
            >
              <input v-model.number="prefs.baleTerm" type="radio" name="bale-term" :value="Number(value)" class="h-4 w-4 accent-primary" />
              <span class="font-medium">{{ option.label }}</span>
              <span class="text-xs text-on-surface-variant">({{ option.example }})</span>
            </label>
          </div>
        </fieldset>

        <div class="grid gap-4 sm:grid-cols-3">
          <Field label="متراژ هدف هر نگله" hint="مثلاً ۱۳ متر؛ خالی یعنی بی‌هدف." :error="fieldErrors.defaultBaleTargetAreaM2" v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="prefs.defaultBaleTargetAreaM2" kind="number" :scale="2" :min="0" :max="9999.99" suffix="متر" :described-by="f.describedBy" :invalid="f.invalid" />
          </Field>
          <Field label="بازهٔ مجاز اطراف هدف" :error="fieldErrors.baleTargetTolerancePercent" required v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="prefs.baleTargetTolerancePercent" kind="integer" :min="0" :max="100" suffix="٪" :described-by="f.describedBy" :invalid="f.invalid" />
          </Field>
          <Field label="بزرگ‌ترین ضلع معمول" hint="ضلع بزرگ‌تر از این «متر یا سانت؟» پرسیده می‌شود." :error="fieldErrors.maxPlausibleSideM" required v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="prefs.maxPlausibleSideM" kind="number" :scale="2" :min="1" :max="30" suffix="متر" :described-by="f.describedBy" :invalid="f.invalid" />
          </Field>
        </div>

        <label class="mt-4 flex min-h-11 items-start gap-3">
          <input v-model="prefs.requireFinalSizeToFinalize" type="checkbox" class="mt-1 h-5 w-5 rounded border-outline-variant accent-primary" />
          <span>
            <span class="block font-medium">بدون اندازهٔ نهایی همهٔ فرش‌ها، محموله قطعی نشود</span>
            <span class="block text-xs text-on-surface-variant">با «همان اندازه» می‌شود اندازهٔ اول را نهایی کرد.</span>
          </span>
        </label>
      </section>

      <!-- ── ارزش گمرکی و دلار ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-4 font-semibold text-primary">ارزش گمرکی و دلار</h2>
        <div class="grid gap-4 sm:grid-cols-3">
          <Field label="ارزش گمرکی بر اساس" hint="نرخ دلار هر متر از جدول، با مقدار همین فیلد پیدا می‌شود." :error="fieldErrors.declaredValueKeyField" required v-slot="f">
            <select :id="f.id" v-model="prefs.declaredValueKeyField" :aria-describedby="f.describedBy" class="fld">
              <option v-for="o in keyFieldOptions" :key="o.key" :value="o.key">{{ o.label }}</option>
            </select>
          </Field>
          <Field label="ارزش گمرکی وقتی در جدول نیست" hint="خالی یعنی فرش بی‌نرخ می‌ماند و جلوی قطعی شدن را می‌گیرد." :error="fieldErrors.declaredValueFallbackUsd" v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="prefs.declaredValueFallbackUsd" kind="usd" :min="0" :described-by="f.describedBy" :invalid="f.invalid" />
          </Field>
          <Field :label="`نرخ پیش‌فرض دلار (${currency})`" hint="برای محمولهٔ تازه پیشنهاد می‌شود." :error="fieldErrors.defaultUsdFx" v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="prefs.defaultUsdFx" kind="number" :scale="6" :min="0" :suffix="currency" :described-by="f.describedBy" :invalid="f.invalid" />
          </Field>
        </div>
      </section>

      <!-- ── هزینه‌ها ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-4 font-semibold text-primary">هزینه‌ها</h2>
        <div class="grid gap-4 sm:grid-cols-3">
          <Field label="تقسیم هزینهٔ «کل»" hint="روش پیش‌فرض؛ در هر هزینه عوض‌شدنی است." :error="fieldErrors.defaultMethod" v-slot="f">
            <select :id="f.id" v-model.number="prefs.defaultMethod" :aria-describedby="f.describedBy" class="fld">
              <option v-for="(label, value) in ALLOCATION_METHOD_LABELS" :key="value" :value="Number(value)">{{ label }}</option>
            </select>
          </Field>
          <Field label="متراژ برای تقسیم" :error="fieldErrors.defaultAreaBasis" v-slot="f">
            <select :id="f.id" v-model.number="prefs.defaultAreaBasis" :aria-describedby="f.describedBy" class="fld">
              <option v-for="(label, value) in AREA_BASIS_LABELS" :key="value" :value="Number(value)">{{ label }}</option>
            </select>
          </Field>
          <Field
            label="رقم اعشار مبلغ‌ها"
            :hint="prefs.moneyScaleLocked ? message('E_MONEY_LOCKED') : `مبلغ ${currency} معمولاً اعشار ندارد.`"
            :error="fieldErrors.moneyScale"
            v-slot="f"
          >
            <select :id="f.id" v-model.number="prefs.moneyScale" :disabled="prefs.moneyScaleLocked" :aria-describedby="f.describedBy" class="fld">
              <option :value="0">بدون اعشار</option>
              <option :value="1">یک رقم</option>
              <option :value="2">دو رقم</option>
            </select>
          </Field>
        </div>
      </section>

      <!-- ── صادرکننده ── -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <h2 class="mb-1 font-semibold text-primary">صادرکننده</h2>
        <p class="mb-4 text-sm text-on-surface-variant">سربرگ اظهارنامه و خروجی‌ها از این‌ها پر می‌شود.</p>
        <div class="grid gap-4 sm:grid-cols-2">
          <Field label="نام صادرکننده" :error="fieldErrors.exporterName" v-slot="f">
            <input :id="f.id" v-model="prefs.exporterName" :aria-describedby="f.describedBy" class="fld" />
          </Field>
          <Field label="کد بازرگانی" :error="fieldErrors.exporterCode" v-slot="f">
            <input :id="f.id" v-model="prefs.exporterCode" dir="ltr" :aria-describedby="f.describedBy" class="fld" />
          </Field>
          <Field label="تلفن" :error="fieldErrors.exporterPhone" v-slot="f">
            <input :id="f.id" v-model="prefs.exporterPhone" dir="ltr" inputmode="tel" :aria-describedby="f.describedBy" class="fld" />
          </Field>
          <Field label="گمرک" :error="fieldErrors.customsOffice" v-slot="f">
            <input :id="f.id" v-model="prefs.customsOffice" :aria-describedby="f.describedBy" class="fld" />
          </Field>
          <Field label="نشانی" :error="fieldErrors.exporterAddress" class="sm:col-span-2" v-slot="f">
            <textarea :id="f.id" v-model="prefs.exporterAddress" rows="2" :aria-describedby="f.describedBy" class="fld py-2"></textarea>
          </Field>
        </div>
      </section>

      <!-- ذخیره (چسبان پایین) -->
      <div class="sticky bottom-0 z-10 -mx-4 flex flex-wrap items-center gap-3 border-t border-outline-variant bg-surface/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0 md:py-0">
        <span v-if="dirty" class="flex-1 text-sm text-on-surface-variant" role="status">تغییرها هنوز ذخیره نشده‌اند.</span>
        <span v-else class="flex-1"></span>
        <Button :disabled="!dirty || saving" icon="undo" @click="revert">برگرداندن</Button>
        <Button type="submit" variant="primary" icon="check" :loading="saving" :disabled="!dirty || stale">ذخیره</Button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.fld {
  min-height: 2.75rem;
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  color: var(--color-on-surface);
  padding: 0 0.75rem;
  outline: none;
}
.fld:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
.fld:disabled {
  opacity: 0.6;
}
</style>
