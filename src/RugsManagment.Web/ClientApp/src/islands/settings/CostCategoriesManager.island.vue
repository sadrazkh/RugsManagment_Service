<script setup lang="ts">
/**
 * جزیرهٔ `cost-categories-manager` (`/settings/cost-categories`، فقط تنظیم‌کننده؛ I.4، I.5، B.7): نوع‌های هزینه و خرید
 * کارگاه با پیش‌فرض‌های ثبت (مبنا، نرخ، ارز، روش تقسیم، پایهٔ متراژ، دامنه)، نماد، ترتیب و نوع مرحلهٔ کارگاهِ هم‌ارز
 * (برای هشدار «دوبار حساب نشود؟»).
 *
 * قاعده‌های سرور (D.5 #25، B.13): گروهِ نوعی که به کار رفته عوض نمی‌شود (۴۰۹ با پیام فارسی) و حذفِ نوعِ به‌کاررفته آن را
 * غیرفعال می‌کند، نه پاک. پیش‌فرض‌ها فقط فرم ثبت تازه را پر می‌کنند و ثبت‌های قبلی دست نمی‌خورند. نسخهٔ کهنه ۴۰۹.
 */
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { AREA_BASIS_LABELS, BASIS_SEGMENTS, METHOD_LABELS, METHOD_ORDER, currencyWord, n, scopeLabel } from '@/components/costs/costText'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import Field from '@/components/ui/Field.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { api, errorMessage } from '@/lib/api'
import { message } from '@/lib/messages'
import {
  AllocationMethod,
  AreaBasis,
  CostBasis,
  CostGroup,
  CostScope,
  CurrencyUnit,
  type CostCategory,
  type SaveCostCategoryRequest,
} from '@/lib/types/costs'
import { confirmDialog, toast } from '@/lib/ui'

interface StepTypeOption {
  id: string
  nameFa: string
}

/** نمادهایی که در `icons.svg` هستند و برای نوع هزینه معنا دارند (H.8)، با نام فارسی برای صفحه‌خوان. */
const ICONS: readonly { name: string; label: string }[] = [
  { name: 'coins', label: 'سکه' },
  { name: 'receipt', label: 'رسید' },
  { name: 'droplet', label: 'قطره (شستشو)' },
  { name: 'needle', label: 'سوزن (رفو)' },
  { name: 'truck', label: 'کامیون' },
  { name: 'ship', label: 'کشتی' },
  { name: 'tape', label: 'نوار بسته‌بندی' },
  { name: 'stamp', label: 'مهر (گمرک)' },
  { name: 'warehouse', label: 'انبار' },
  { name: 'package', label: 'بسته' },
  { name: 'bale', label: 'عدل' },
  { name: 'dollar', label: 'دلار' },
  { name: 'handshake', label: 'دست دادن' },
  { name: 'ruler', label: 'متر' },
  { name: 'tag', label: 'برچسب' },
  { name: 'dots', label: 'سایر' },
]

const GROUPS = [
  { group: CostGroup.Expense, label: 'هزینه', hint: 'شستشو، رفوگری، کرایه، گمرک و … — بعد از خرید روی فرش‌ها می‌آید.' },
  { group: CostGroup.Purchase, label: 'خرید', hint: 'قیمت خرید، دلالی، خرید یکجا — بخشی از قیمت خرید فرش.' },
] as const

const loading = ref(true)
const error = ref('')
const categories = ref<CostCategory[]>([])
const stepTypes = ref<StepTypeOption[]>([])

/** شناسهٔ نوعی که ویرایشگرش باز است؛ «new» برای نوع تازه. */
const editingId = ref<string | null>(null)
const saving = ref(false)
const formError = ref('')
const nameInput = ref<HTMLInputElement | null>(null)

const draft = reactive<SaveCostCategoryRequest>(blank(CostGroup.Expense))

function blank(group: CostGroup): SaveCostCategoryRequest {
  return {
    name: '',
    group,
    icon: 'coins',
    sortOrder: nextSortOrder(group),
    isActive: true,
    defaultBasis: CostBasis.Total,
    defaultRate: null,
    defaultCurrency: null,
    defaultMethod: AllocationMethod.ByArea,
    defaultAreaBasis: AreaBasis.Effective,
    defaultScope: null,
    showInIntake: false,
    stepTypeId: null,
    version: null,
  }
}

function nextSortOrder(group: CostGroup): number {
  const orders = categories.value.filter((c) => c.group === group).map((c) => c.sortOrder)
  return orders.length ? Math.max(...orders) + 10 : 10
}

const byGroup = computed(() =>
  GROUPS.map((g) => ({
    ...g,
    items: categories.value
      .filter((c) => c.group === g.group)
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name, 'fa')),
  })),
)

const basisChoices = computed(() => {
  const list = BASIS_SEGMENTS.map((s) => ({ basis: s.basis, label: s.label }))
  if (draft.group === CostGroup.Purchase) list.push({ basis: CostBasis.Percent, label: 'درصد' })
  return list
})

const methodChoices = computed(() =>
  METHOD_ORDER.filter((m) => !(draft.group === CostGroup.Purchase && m === AllocationMethod.ByPurchaseCost)),
)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [cats, steps] = await Promise.all([
      api.get<CostCategory[]>('/api/cost-categories?includeInactive=true'),
      api.get<StepTypeOption[]>('/api/lookups/step-types').catch(() => [] as StepTypeOption[]),
    ])
    categories.value = cats
    stepTypes.value = steps
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

function summary(c: CostCategory): string {
  const basis = c.defaultBasis === CostBasis.Percent ? 'درصد' : (BASIS_SEGMENTS.find((s) => s.basis === c.defaultBasis)?.label ?? 'کل')
  const rate = c.defaultRate !== null ? ` ${n(c.defaultRate)}${c.defaultBasis === CostBasis.Percent ? '٪' : ''}` : ''
  const method = c.defaultBasis === CostBasis.Total ? ` · ${METHOD_LABELS[c.defaultMethod]}` : ''
  const area = c.defaultMethod === AllocationMethod.ByArea || c.defaultBasis === CostBasis.PerM2 ? ` · ${AREA_BASIS_LABELS[c.defaultAreaBasis]}` : ''
  return `${basis}${rate}${method}${area}`
}

function startNew(group: CostGroup) {
  Object.assign(draft, blank(group))
  editingId.value = 'new'
  formError.value = ''
  void nextTick(() => nameInput.value?.focus())
}

function startEdit(c: CostCategory) {
  Object.assign(draft, {
    name: c.name,
    group: c.group,
    icon: c.icon,
    sortOrder: c.sortOrder,
    isActive: c.isActive,
    defaultBasis: c.defaultBasis,
    defaultRate: c.defaultRate,
    defaultCurrency: c.defaultCurrency,
    defaultMethod: c.defaultMethod,
    defaultAreaBasis: c.defaultAreaBasis,
    defaultScope: c.defaultScope,
    showInIntake: c.showInIntake,
    stepTypeId: c.stepTypeId,
    version: c.version,
  } satisfies SaveCostCategoryRequest)
  editingId.value = c.id
  formError.value = ''
  void nextTick(() => nameInput.value?.focus())
}

function cancelEdit() {
  editingId.value = null
  formError.value = ''
}

async function save() {
  if (!draft.name.trim()) {
    formError.value = message('E_REQUIRED', { label: 'نام' })
    nameInput.value?.focus()
    return
  }
  if (draft.group === CostGroup.Expense) {
    if (draft.defaultBasis === CostBasis.Percent) draft.defaultBasis = CostBasis.Total
    draft.showInIntake = false
  }
  saving.value = true
  formError.value = ''
  try {
    const body: SaveCostCategoryRequest = { ...draft, name: draft.name.trim() }
    const saved =
      editingId.value === 'new'
        ? await api.post<CostCategory>('/api/cost-categories', body)
        : await api.put<CostCategory>(`/api/cost-categories/${editingId.value}`, body)
    categories.value = [...categories.value.filter((c) => c.id !== saved.id), saved]
    editingId.value = null
    toast.success(message('I_COST_CATEGORY_SAVED'))
  } catch (e) {
    formError.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

async function remove(c: CostCategory) {
  const ok = await confirmDialog({
    title: `نوع «${c.name}» حذف شود؟`,
    message: 'اگر با آن هزینه یا قیمت خرید ثبت شده باشد حذف نمی‌شود و فقط غیرفعال می‌شود.',
    confirmLabel: 'حذف کن',
    cancelLabel: 'نه',
    danger: true,
  })
  if (!ok) return
  try {
    await api.del(`/api/cost-categories/${c.id}?version=${c.version}`)
    const fresh = await api.get<CostCategory[]>('/api/cost-categories?includeInactive=true')
    categories.value = fresh
    const still = fresh.find((x) => x.id === c.id)
    toast.success(still ? message('I_COST_CATEGORY_DEACTIVATED', { name: c.name }) : message('I_COST_CATEGORY_DELETED', { name: c.name }))
    if (editingId.value === c.id) editingId.value = null
  } catch (e) {
    toast.error(errorMessage(e))
  }
}

/** جابه‌جایی ترتیب با همسایه (دو ذخیره؛ هر کدام با نسخهٔ خودش). */
async function move(c: CostCategory, direction: -1 | 1) {
  const list = byGroup.value.find((g) => g.group === c.group)?.items ?? []
  const index = list.findIndex((x) => x.id === c.id)
  const other = list[index + direction]
  if (!other) return
  try {
    const a = await api.put<CostCategory>(`/api/cost-categories/${c.id}`, toRequest(c, { sortOrder: other.sortOrder === c.sortOrder ? c.sortOrder + direction : other.sortOrder }))
    const b = await api.put<CostCategory>(`/api/cost-categories/${other.id}`, toRequest(other, { sortOrder: c.sortOrder }))
    categories.value = categories.value.map((x) => (x.id === a.id ? a : x.id === b.id ? b : x))
  } catch (e) {
    toast.error(errorMessage(e))
    await load()
  }
}

function toRequest(c: CostCategory, patch: Partial<SaveCostCategoryRequest> = {}): SaveCostCategoryRequest {
  return {
    name: c.name,
    group: c.group,
    icon: c.icon,
    sortOrder: c.sortOrder,
    isActive: c.isActive,
    defaultBasis: c.defaultBasis,
    defaultRate: c.defaultRate,
    defaultCurrency: c.defaultCurrency,
    defaultMethod: c.defaultMethod,
    defaultAreaBasis: c.defaultAreaBasis,
    defaultScope: c.defaultScope,
    showInIntake: c.showInIntake,
    stepTypeId: c.stepTypeId,
    version: c.version,
    ...patch,
  }
}

const currencyOptions = [
  { value: null, label: 'ارز کارگاه' },
  { value: CurrencyUnit.Toman, label: currencyWord(CurrencyUnit.Toman) },
  { value: CurrencyUnit.Rial, label: currencyWord(CurrencyUnit.Rial) },
  { value: CurrencyUnit.Usd, label: currencyWord(CurrencyUnit.Usd) },
] as const

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div v-if="loading" class="space-y-3" aria-hidden="true">
      <div v-for="i in 5" :key="i" class="skeleton h-14 w-full" />
    </div>

    <p v-else-if="error" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-on-error-container" role="alert">
      <AppIcon name="error" class="mt-0.5 h-4 w-4" />
      {{ error }}
    </p>

    <template v-else>
      <section v-for="g in byGroup" :key="g.group" class="space-y-3" :aria-labelledby="`cat-group-${g.group}`">
        <div class="flex flex-wrap items-end justify-between gap-2">
          <div>
            <h2 :id="`cat-group-${g.group}`" class="text-lg font-bold">{{ g.label }}</h2>
            <p class="text-sm text-on-surface-variant">{{ g.hint }}</p>
          </div>
          <Button icon="plus" @click="startNew(g.group)">نوع تازهٔ {{ g.label }}</Button>
        </div>

        <EmptyState v-if="g.items.length === 0 && editingId !== 'new'" compact icon="coins" title="هنوز نوعی نیست" />

        <ul class="divide-y divide-outline-variant rounded-xl border border-outline-variant bg-surface-container-lowest" data-numeric>
          <template v-for="(c, i) in g.items" :key="c.id">
            <li class="flex flex-wrap items-center gap-3 p-3" :class="c.isActive ? '' : 'opacity-70'" :data-category="c.seedKey ?? c.id">
              <span class="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-surface-container">
                <AppIcon :name="c.icon || 'coins'" class="h-5 w-5 text-primary" />
              </span>
              <span class="min-w-0 flex-1">
                <span class="flex flex-wrap items-center gap-2 font-semibold">
                  {{ c.name }}
                  <span v-if="!c.isActive" class="rounded-full bg-surface-container-high px-2 py-0.5 text-xs font-normal">غیرفعال</span>
                  <span v-if="c.showInIntake" class="rounded-full bg-secondary-container px-2 py-0.5 text-xs font-normal text-on-secondary-container">ستون جدول ورود</span>
                </span>
                <span class="block text-sm text-on-surface-variant">{{ summary(c) }}</span>
              </span>
              <span class="flex shrink-0 items-center gap-1">
                <button type="button" class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container" :disabled="i === 0" :aria-label="`بالاتر: ${c.name}`" @click="move(c, -1)">
                  <AppIcon name="chevron-up" class="h-4 w-4" />
                </button>
                <button type="button" class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container" :disabled="i === g.items.length - 1" :aria-label="`پایین‌تر: ${c.name}`" @click="move(c, 1)">
                  <AppIcon name="chevron-down" class="h-4 w-4" />
                </button>
                <button type="button" class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container" :aria-label="`ویرایش ${c.name}`" :aria-expanded="editingId === c.id" @click="editingId === c.id ? cancelEdit() : startEdit(c)">
                  <AppIcon name="edit" class="h-4 w-4" />
                </button>
                <button type="button" class="grid h-11 w-11 place-items-center rounded-lg text-error hover:bg-error-container" :aria-label="`حذف ${c.name}`" @click="remove(c)">
                  <AppIcon name="trash" class="h-4 w-4" />
                </button>
              </span>
            </li>
          </template>
        </ul>

        <!-- ویرایشگر: نوع تازهٔ همین گروه، یا نوع در حال ویرایش همین گروه -->
        <form
          v-if="(editingId === 'new' && draft.group === g.group) || (editingId && editingId !== 'new' && g.items.some((x) => x.id === editingId))"
          class="space-y-4 rounded-xl border border-primary/40 bg-surface-container-low p-4"
          novalidate
          data-category-editor
          @submit.prevent="save"
        >
          <h3 class="font-semibold">{{ editingId === 'new' ? `نوع تازهٔ ${g.label}` : `ویرایش «${draft.name}»` }}</h3>

          <div class="grid gap-4 sm:grid-cols-2">
            <Field label="نام" required v-slot="f">
              <input
                :id="f.id"
                ref="nameInput"
                v-model="draft.name"
                type="text"
                maxlength="120"
                class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary focus:ring-1 focus:ring-primary"
              />
            </Field>
            <fieldset>
              <legend class="mb-1 text-sm font-medium">گروه</legend>
              <div class="flex gap-2">
                <label v-for="opt in GROUPS" :key="opt.group" class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm">
                  <input v-model="draft.group" type="radio" :value="opt.group" class="h-5 w-5" />
                  {{ opt.label }}
                </label>
              </div>
            </fieldset>
          </div>

          <fieldset>
            <legend class="mb-1 text-sm font-medium">نماد</legend>
            <div class="flex flex-wrap gap-2">
              <label
                v-for="icon in ICONS"
                :key="icon.name"
                class="grid h-11 w-11 cursor-pointer place-items-center rounded-lg border focus-within:ring-2 focus-within:ring-primary"
                :class="draft.icon === icon.name ? 'border-primary bg-primary/10' : 'border-outline-variant hover:bg-surface-container'"
              >
                <input v-model="draft.icon" type="radio" :value="icon.name" class="sr-only" :aria-label="icon.label" />
                <AppIcon :name="icon.name" class="h-5 w-5" />
              </label>
            </div>
          </fieldset>

          <div class="grid gap-4 sm:grid-cols-2">
            <fieldset>
              <legend class="mb-1 text-sm font-medium">مبنای پیش‌فرض</legend>
              <div class="flex flex-wrap gap-2">
                <label v-for="b in basisChoices" :key="b.basis" class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm">
                  <input v-model="draft.defaultBasis" type="radio" :value="b.basis" class="h-5 w-5" />
                  {{ b.label }}
                </label>
              </div>
            </fieldset>
            <Field :label="draft.defaultBasis === CostBasis.Percent ? 'درصد پیش‌فرض' : 'نرخ یا مبلغ پیش‌فرض'" hint="خالی یعنی بازرگان خودش می‌نویسد." v-slot="f">
              <NumberField
                :id="f.id"
                v-model="draft.defaultRate"
                :kind="draft.defaultBasis === CostBasis.Percent ? 'percent' : 'money'"
                :described-by="f.describedBy"
                label="نرخ پیش‌فرض"
              />
            </Field>
          </div>

          <div class="grid gap-4 sm:grid-cols-3">
            <Field label="ارز پیش‌فرض" v-slot="f">
              <select :id="f.id" v-model="draft.defaultCurrency" class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3">
                <option v-for="o in currencyOptions" :key="String(o.value)" :value="o.value">{{ o.label }}</option>
              </select>
            </Field>
            <Field label="روش تقسیم «کل»" v-slot="f">
              <select :id="f.id" v-model="draft.defaultMethod" class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3">
                <option v-for="m in methodChoices" :key="m" :value="m">{{ METHOD_LABELS[m] }}</option>
              </select>
            </Field>
            <Field label="متراژ کدام اندازه" v-slot="f">
              <select :id="f.id" v-model="draft.defaultAreaBasis" class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3">
                <option :value="AreaBasis.Intake">{{ AREA_BASIS_LABELS[AreaBasis.Intake] }}</option>
                <option :value="AreaBasis.Effective">{{ AREA_BASIS_LABELS[AreaBasis.Effective] }}</option>
              </select>
            </Field>
          </div>

          <div class="grid gap-4 sm:grid-cols-2">
            <Field label="معمولاً برای" v-slot="f">
              <select :id="f.id" v-model="draft.defaultScope" class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3">
                <option :value="null">—</option>
                <option v-for="s in [CostScope.Shipment, CostScope.Bale, CostScope.IntakeBatch, CostScope.Selection]" :key="s" :value="s">{{ scopeLabel(s) }}</option>
              </select>
            </Field>
            <Field label="همان کار کارگاه" hint="اگر فرشی این کار را با هزینه در «کارهای کارگاه» هم دارد، هشدار دوبار حساب شدن می‌آید." v-slot="f">
              <select :id="f.id" v-model="draft.stepTypeId" :aria-describedby="f.describedBy" class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3">
                <option :value="null">—</option>
                <option v-for="s in stepTypes" :key="s.id" :value="s.id">{{ s.nameFa }}</option>
              </select>
            </Field>
          </div>

          <div class="flex flex-wrap gap-4">
            <label v-if="draft.group === CostGroup.Purchase" class="inline-flex min-h-11 items-center gap-2 text-sm">
              <input v-model="draft.showInIntake" type="checkbox" class="h-5 w-5" />
              ستون در جدول ورود فرش
            </label>
            <label class="inline-flex min-h-11 items-center gap-2 text-sm">
              <input v-model="draft.isActive" type="checkbox" class="h-5 w-5" />
              فعال
            </label>
          </div>

          <p v-if="formError" class="flex items-start gap-2 text-sm text-error" role="alert">
            <AppIcon name="error" class="mt-0.5 h-4 w-4" />
            {{ formError }}
          </p>

          <div class="flex flex-wrap gap-2">
            <Button type="submit" variant="primary" icon="check" :loading="saving">ذخیره</Button>
            <Button variant="ghost" :disabled="saving" @click="cancelEdit">انصراف</Button>
          </div>
        </form>
      </section>
    </template>
  </div>
</template>
