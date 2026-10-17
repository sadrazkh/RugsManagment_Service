<script setup lang="ts">
/**
 * پنل «ثبت هزینه» (نام: `cost-entry`؛ H.2، I.5) — مهم‌ترین صفحهٔ فاز هزینه.
 *
 * نمای ساده فقط این‌ها را نشان می‌دهد: «نوع هزینه» (کاشی‌های نوع‌های فعال گروه هزینه، با «+ نوع تازه»)، کلید سه‌تکهٔ
 * «هر متر | هر تخته | کل» درست بالای «مبلغ»، «برای» (تراشه‌هایی که همه تخته می‌شمارند) و یک جملهٔ فقط‌خواندنی که می‌گوید
 * مبلغ چطور تقسیم می‌شود. پایهٔ متراژ (اندازهٔ اول/نهایی) در نمای ساده هرگز کنترل نیست؛ پیش‌فرض نوع هزینه به کار می‌رود.
 * روش تقسیم با «[تغییر]» زیر جمله، و تاریخ، شمارهٔ رسید، کارگاه، دلار، مبلغ ثابت و توضیح زیر «بیشتر». نمای کامل همان
 * پنل است با «بیشتر» باز، روش و پایهٔ متراژ و جدول سهم‌ها.
 *
 * از کجا باز می‌شود (props = `CostPanelPreset`): خانه `{scope: 2, shipmentId}` یا `{}`، جدول ورود `{scope: 3, batchId}`،
 * نوار گروهی فهرست فرش‌ها `{rugIds}`، زبانهٔ هزینه‌های محموله، صفحهٔ فرش و دفتر هزینه‌ها (`entryId` برای ویرایش).
 *
 * ذخیره: `POST /api/costs` با `clientKey` (یا `PUT` با `version`). اگر نشد (اینترنت یا خطای سرور) پنل باز می‌ماند و
 * «دوباره بفرست» همان کلید را می‌فرستد، پس ثبت دوم ساخته نمی‌شود؛ فرم هم روی همین دستگاه پیش‌نویس می‌شود (G.7) و دفعهٔ
 * بعد پیشنهاد برگشت دارد. تا پاسخ نرسیده تازه‌سازی خودکار صفحه قفل است (`blockReload`).
 */
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, toRaw, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import AllocationPreviewTable from '@/components/costs/AllocationPreviewTable.vue'
import MethodChooser from '@/components/costs/MethodChooser.vue'
import TargetPicker from '@/components/costs/TargetPicker.vue'
import {
  amountLabel,
  BASIS_SEGMENTS,
  balesChipLabel,
  baseCurrency,
  baseCurrencyWord,
  batchChipLabel,
  buildRequest,
  categoryDefaults,
  costDraftKey,
  costSentence,
  costSummary,
  currencyWord,
  emptyForm,
  formFromRequest,
  formHasContent,
  freezes,
  methodPhrase,
  missingPart,
  n,
  parseCostDraft,
  pinsAllowed,
  piecesText,
  radioKeyTarget,
  rugsChipLabel,
  shipmentChipLabel,
  targetKey,
  visibleCategories,
  type CostDraft,
  type CostForm,
  type CostTarget,
} from '@/components/costs/costText'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { api, ApiError, errorMessage, isNetworkError } from '@/lib/api'
import { appContext, isSimpleUi } from '@/lib/context'
import { newClientKey } from '@/lib/ids'
import { message } from '@/lib/messages'
import { sub, toNumber } from '@/lib/num'
import {
  AllocationMethod,
  CostBasis,
  CostGroup,
  CostScope,
  CostSeedKey,
  COSTS_CHANGED_EVENT,
  CurrencyUnit,
  type AllocationPreview,
  type CostCategory,
  type CostEntry,
  type CostPanelResult,
} from '@/lib/types/costs'
import type { IntakeBatch } from '@/lib/types/grid'
import type { Shipment } from '@/lib/types/shipments'
import { blockReload, requestReload, toast, toastAfterReload } from '@/lib/ui'

const props = defineProps<{
  scope?: number
  shipmentId?: string | null
  batchId?: string | null
  baleIds?: string[] | null
  rugIds?: string[] | null
  categorySeedKey?: string | null
  entryId?: string | null
  reloadOnSave?: boolean
}>()

const emit = defineEmits<{ close: [result?: CostPanelResult] }>()

interface ProviderOption {
  id: string
  name: string
  isActive: boolean
}

const ctx = appContext()
const simple = isSimpleUi()
const tenantCurrency = baseCurrency()
const isEdit = computed(() => !!props.entryId)
const fromBatch = computed(() => !!props.batchId && (props.scope === undefined || props.scope === CostScope.IntakeBatch))

// ── وضعیت ────────────────────────────────────────────────────────────

const loading = ref(true)
const loadError = ref('')
const categories = ref<CostCategory[]>([])
const recentBatches = ref<IntakeBatch[]>([])
const presetShipment = ref<Shipment | null>(null)
const entry = ref<CostEntry | null>(null)
const providers = ref<ProviderOption[] | null>(null)

const form = reactive<CostForm>(emptyForm(ctx.today, tenantCurrency))
const clientKey = ref<string>(newClientKey())
const version = ref<number | null>(null)

const preview = ref<AllocationPreview | null>(null)
const previewError = ref('')
const previewBusy = ref(false)
/** شمار تخته/ردیف آخرین «برای» (برای برچسب تراشه و گزینهٔ «مساوی»)، حتی وقتی مبلغ هنوز نوشته نشده */
const counts = ref<{ key: string; pieces: number; lines: number } | null>(null)

const saving = ref(false)
const saveFailed = ref(false)
const saveError = ref('')
const problem = ref<{ field: 'category' | 'target' | 'amount' | 'fx'; text: string } | null>(null)

const showMore = ref(!simple)
const showMethod = ref(!simple)
const showPicker = ref(false)
const customTarget = ref<CostTarget | null>(null)
const newCategory = reactive({ open: false, name: '', saving: false, error: '' })
const draftOffer = ref<CostDraft | null>(null)

const categoryGroup = ref<HTMLElement | null>(null)
const targetGroup = ref<HTMLElement | null>(null)
const amountField = ref<InstanceType<typeof NumberField> | null>(null)
const fxField = ref<InstanceType<typeof NumberField> | null>(null)
const restoreButton = ref<HTMLElement | null>(null)
const newCategoryInput = ref<HTMLInputElement | null>(null)

// ── نوع هزینه ────────────────────────────────────────────────────────

const tiles = computed(() =>
  visibleCategories(categories.value, { simple, fromBatch: fromBatch.value, selectedId: form.categoryId }),
)
const selectedCategory = computed(() => categories.value.find((c) => c.id === form.categoryId) ?? null)

function tileLabel(c: CostCategory): string {
  return c.seedKey === CostSeedKey.PurchaseLump && fromBatch.value ? 'خرید یکجای این ورودی' : c.name
}

function selectCategory(c: CostCategory, focusAmount = true) {
  const changed = form.categoryId !== c.id
  form.categoryId = c.id
  problem.value = null
  if (changed) {
    const d = categoryDefaults(c, tenantCurrency)
    form.basis = d.basis
    form.method = d.method
    form.areaBasis = d.areaBasis
    form.currency = d.currency
    if (d.typed !== null && form.typed === null) form.typed = d.typed
    // «گمرک و ترخیص» پیش‌فرض محموله دارد: اگر تراشهٔ محموله هست و «برای» خالی است، همان (H.8)
    if (d.scope === CostScope.Shipment && !form.target) {
      const shipment = targetOptions.value.find((o) => o.target.kind === 'shipment')
      if (shipment) form.target = shipment.target
    }
  }
  if (focusAmount) void nextTick(() => amountField.value?.focus())
}

async function addCategory() {
  const name = newCategory.name.trim()
  if (!name) {
    newCategory.error = message('E_REQUIRED', { label: 'نام نوع' })
    newCategoryInput.value?.focus()
    return
  }
  newCategory.saving = true
  newCategory.error = ''
  try {
    const created = await api.post<CostCategory>('/api/cost-categories/quick', { name, group: CostGroup.Expense })
    categories.value = [...categories.value.filter((c) => c.id !== created.id), created]
    newCategory.open = false
    newCategory.name = ''
    toast.success(message('I_COST_NEW_CATEGORY_SAVED', { name: created.name }))
    selectCategory(created)
  } catch (e) {
    newCategory.error = errorMessage(e)
  } finally {
    newCategory.saving = false
  }
}

function openNewCategory() {
  newCategory.open = true
  newCategory.error = ''
  void nextTick(() => newCategoryInput.value?.focus())
}

// ── «برای» ───────────────────────────────────────────────────────────

const todayBatches = computed(() => recentBatches.value.filter((b) => b.receivedOn === ctx.today))

function countsFor(target: CostTarget): { pieces: number; lines: number } | null {
  return counts.value && counts.value.key === targetKey(target) ? counts.value : null
}

const targetOptions = computed<{ key: string; target: CostTarget }[]>(() => {
  const list: CostTarget[] = []
  if (props.rugIds?.length) {
    const t: CostTarget = { kind: 'rugs', rugIds: [...props.rugIds], label: '', scopeNote: null }
    t.label = rugsChipLabel(props.rugIds.length, countsFor(t)?.pieces ?? null)
    list.push(t)
  }
  if (props.baleIds?.length) {
    const bales = presetShipment.value?.bales.filter((b) => props.baleIds?.includes(b.id)) ?? []
    const label =
      bales.length === 1
        ? `${ctx.terms.baleOf} ${n(bales[0].number)} (${piecesText(bales[0].pieces)})`
        : balesChipLabel(props.baleIds.length, ctx.terms.bale)
    list.push({ kind: 'bales', shipmentId: props.shipmentId ?? null, baleIds: [...props.baleIds], label })
  }
  if (props.shipmentId) {
    list.push({ kind: 'shipment', shipmentId: props.shipmentId, label: shipmentChipLabel(presetShipment.value?.totals.pieces ?? null) })
  }
  if (props.batchId && !todayBatches.value.some((b) => b.id === props.batchId)) {
    const b = recentBatches.value.find((x) => x.id === props.batchId)
    list.push({ kind: 'batch', batchId: props.batchId, label: batchChipLabel(b?.sellerName ?? null, b?.pieces ?? null, false) })
  }
  // یک تراشه برای هر فروشندهٔ امروز (erratum E7)
  for (const b of todayBatches.value) list.push({ kind: 'batch', batchId: b.id, label: batchChipLabel(b.sellerName, b.pieces, true) })
  const custom = customTarget.value
  if (custom && !list.some((t) => targetKey(t) === targetKey(custom))) list.push(custom)
  return list.map((t) => ({ key: targetKey(t), target: t }))
})

const selectedTargetKey = computed(() => targetKey(form.target))

function selectTarget(target: CostTarget) {
  form.target = target
  problem.value = null
  showPicker.value = false
}

function onPicked(target: CostTarget) {
  customTarget.value = target
  selectTarget(target)
  void nextTick(() => focusRadio(targetGroup.value, targetKey(target)))
}

// ── مبنا، مبلغ و روش ─────────────────────────────────────────────────

const amountKind = computed(() => (form.currency === CurrencyUnit.Usd ? 'usd' : 'money'))
const hasLot = computed(() => {
  const c = form.target ? countsFor(form.target) : null
  return c ? c.pieces !== c.lines : false
})
const methodApplies = computed(() => form.basis === CostBasis.Total)
const isFrozen = computed(() => freezes(form.basis, form.receiptNo, form.providerId))
const canPin = computed(() => pinsAllowed(form.basis, form.receiptNo, form.providerId))
const usd = computed({
  get: () => form.currency === CurrencyUnit.Usd,
  set: (value: boolean) => {
    form.currency = value ? CurrencyUnit.Usd : tenantCurrency
    if (!value) form.fxRate = null
  },
})

const currencyChoices = computed(() => {
  const other = tenantCurrency === CurrencyUnit.Toman ? CurrencyUnit.Rial : CurrencyUnit.Toman
  return [tenantCurrency, other, CurrencyUnit.Usd]
})

const pinsResidual = computed(() => {
  if (!preview.value || form.pins.length === 0) return null
  const pinned = form.pins.reduce((sum, p) => sum + p.amount, 0)
  return toNumber(sub(preview.value.baseAmount, pinned))
})

watch(
  () => form.method,
  (m) => {
    // «خودم سهم هر فرش را می‌نویسم»: جدول مبلغ ثابت همان لحظه باز شود
    if (m === AllocationMethod.Manual) showMore.value = true
  },
)

// ── جمله و پیش‌نمایش ─────────────────────────────────────────────────

const previewBody = computed(() =>
  JSON.stringify(buildRequest({ ...form, typed: form.typed ?? 0 }, { version: version.value, clientKey: null })),
)
/** پیش‌نمایش برای کدام بدنه گرفته شد؛ جملهٔ سرور فقط وقتی هنوز همان فرم است نشان داده می‌شود. */
const previewFor = ref('')

/**
 * جمله: وقتی پیش‌نمایش سرور برای همین فرم رسیده، جملهٔ خودِ سرور (که رسید ثابت‌شده را هم «رسید …» می‌گوید)؛ تا آن وقت
 * جملهٔ همین‌جا، تا تایپ بی‌درنگ دیده شود.
 */
const sentence = computed(() => {
  if (preview.value?.sentence && previewFor.value === previewBody.value && form.typed !== null) return preview.value.sentence
  return costSentence({
    basis: form.basis,
    method: form.method,
    areaBasis: form.areaBasis,
    category: selectedCategory.value?.name ?? null,
    typed: form.typed,
    currency: form.currency,
    preview: preview.value,
    hasTarget: !!form.target,
  })
})

/** خلاصهٔ یک‌خطی بالای جمله: «کل ۱٬۷۶۸٬۸۵۰ · به نسبت متراژ (اندازهٔ اول)». */
const summary = computed(() =>
  form.typed !== null && form.target ? costSummary(form.basis, form.typed, form.method, form.areaBasis) : '',
)

let previewSeq = 0
let previewTimer: ReturnType<typeof setTimeout> | undefined

function schedulePreview() {
  if (previewTimer !== undefined) clearTimeout(previewTimer)
  if (!form.categoryId || !form.target) {
    preview.value = null
    previewError.value = ''
    return
  }
  previewTimer = setTimeout(() => void runPreview(), 350)
}

async function runPreview() {
  const seq = ++previewSeq
  const typed = form.typed
  const target = form.target
  const bodyText = previewBody.value
  previewBusy.value = true
  try {
    const body = buildRequest({ ...form, typed: typed ?? 0 }, { version: version.value, clientKey: null })
    const result = await api.post<AllocationPreview>('/api/costs/preview', body)
    if (seq !== previewSeq) return
    if (target) counts.value = { key: targetKey(target), pieces: result.pieces, lines: result.lines }
    // بی مبلغ فقط شمارش‌ها به کار می‌آیند؛ جمله هنوز «مبلغ را بنویسید» است
    preview.value = typed === null ? null : result
    previewFor.value = bodyText
    previewError.value = ''
  } catch (e) {
    if (seq !== previewSeq) return
    preview.value = null
    // خطای پیش‌نمایش فقط وقتی مبلغی نوشته شده معنا دارد (مثلاً «جمع مبلغ‌های ثابت … بیشتر است»)
    previewError.value = typed === null || isNetworkError(e) ? '' : errorMessage(e)
  } finally {
    if (seq === previewSeq) previewBusy.value = false
  }
}

watch(previewBody, schedulePreview)

// ── پیش‌نویس (G.7) ────────────────────────────────────────────────────

const draftContext = computed(() => (isEdit.value ? `edit-${props.entryId}` : 'new'))

function storage(): Storage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    return null
  }
}

function draftStorageKey(): string | null {
  return costDraftKey(ctx.tenantId, ctx.userId, draftContext.value)
}

function writeDraft() {
  const key = draftStorageKey()
  if (!key || draftOffer.value || loading.value || !formHasContent(form)) return
  try {
    const payload: CostDraft = { v: 1, savedAt: Date.now(), clientKey: clientKey.value, form: JSON.parse(JSON.stringify(toRaw(form))) as CostForm }
    storage()?.setItem(key, JSON.stringify(payload))
  } catch {
    /* حالت خصوصی یا سهمیهٔ پر؛ پیش‌نویس فقط کمک است */
  }
}

function clearDraft() {
  const key = draftStorageKey()
  if (!key) return
  try {
    storage()?.removeItem(key)
  } catch {
    /* storage بسته */
  }
}

let draftTimer: ReturnType<typeof setTimeout> | undefined
watch(
  () => JSON.stringify(form),
  () => {
    if (draftTimer !== undefined) clearTimeout(draftTimer)
    draftTimer = setTimeout(writeDraft, 300)
  },
)

function restoreDraft() {
  const draft = draftOffer.value
  if (!draft) return
  draftOffer.value = null
  Object.assign(form, draft.form)
  // همان کلید ارسال: اگر ارسال قبلی در واقع رسیده بود، ذخیره همان ثبت را برمی‌گرداند (ثبت دوم ساخته نمی‌شود)
  clientKey.value = draft.clientKey
  if (draft.form.target && !targetOptions.value.some((o) => o.key === targetKey(draft.form.target))) {
    customTarget.value = draft.form.target
  }
  void nextTick(() => amountField.value?.focus())
}

function discardDraft() {
  draftOffer.value = null
  clearDraft()
  void nextTick(focusFirst)
}

// ── بار اول ──────────────────────────────────────────────────────────

async function loadProviders() {
  if (providers.value !== null) return
  try {
    const list = await api.get<ProviderOption[]>('/api/providers')
    providers.value = list.map((p) => ({ id: p.id, name: p.name, isActive: p.isActive }))
  } catch {
    providers.value = []
  }
}

watch(showMore, (open) => {
  if (open) void loadProviders()
})

function editTargetLabel(e: CostEntry, shipment: Shipment | null): string {
  const r = e.request
  switch (r.scope) {
    case CostScope.Shipment:
      return shipment ? `محمولهٔ ${n(shipment.number)} (${piecesText(shipment.totals.pieces)})` : 'این محموله'
    case CostScope.Bale:
      return balesChipLabel(r.baleIds?.length ?? 1, ctx.terms.bale)
    case CostScope.IntakeBatch:
      return r.batchId ? batchChipLabel(null, e.result.pieces, false) : rugsChipLabel(r.rugIds?.length ?? 0, e.result.pieces)
    default:
      return rugsChipLabel(r.rugIds?.length ?? 0, e.result.pieces)
  }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [cats, batches, shipment, existing] = await Promise.all([
      api.get<CostCategory[]>('/api/cost-categories'),
      api.get<IntakeBatch[]>('/api/batches/recent?days=7').catch(() => [] as IntakeBatch[]),
      props.shipmentId ? api.get<Shipment>(`/api/shipments/${props.shipmentId}`).catch(() => null) : Promise.resolve(null),
      props.entryId ? api.get<CostEntry>(`/api/costs/${props.entryId}`) : Promise.resolve(null),
    ])
    categories.value = cats
    recentBatches.value = batches
    presetShipment.value = shipment

    if (existing) {
      entry.value = existing
      version.value = existing.version
      clientKey.value = existing.request.clientKey ?? clientKey.value
      let shipmentForLabel = shipment
      if (!shipmentForLabel && existing.request.shipmentId && existing.request.scope === CostScope.Shipment) {
        shipmentForLabel = await api.get<Shipment>(`/api/shipments/${existing.request.shipmentId}`).catch(() => null)
      }
      Object.assign(form, formFromRequest(existing.request, editTargetLabel(existing, shipmentForLabel)))
      customTarget.value = form.target
      counts.value = form.target ? { key: targetKey(form.target), pieces: existing.result.pieces, lines: existing.result.lines } : null
      preview.value = existing.result
      if (existing.request.serviceProviderId || existing.request.receiptNo || existing.request.pins?.length || existing.request.description) {
        showMore.value = true
      }
    } else {
      applyPreset()
      draftOffer.value = parseCostDraft(readDraftRaw())
    }
  } catch (e) {
    loadError.value = errorMessage(e)
  } finally {
    loading.value = false
  }
  await nextTick()
  if (draftOffer.value) restoreButton.value?.focus()
  else focusFirst()
}

function readDraftRaw(): string | null {
  const key = draftStorageKey()
  if (!key) return null
  try {
    return storage()?.getItem(key) ?? null
  } catch {
    return null
  }
}

/** «برای» از جای باز شدن (A.4): انتخاب صریح، وگرنه محموله، وگرنه تنها ورودی امروز؛ با هیچ‌کدام خالی و فوکوس‌دار. */
function applyPreset() {
  const options = targetOptions.value
  const pick = (kind: CostTarget['kind']) => options.find((o) => o.target.kind === kind)?.target ?? null
  let target: CostTarget | null = null
  if (props.rugIds?.length) target = pick('rugs')
  else if (props.baleIds?.length) target = pick('bales')
  else if (props.batchId) target = options.find((o) => o.target.kind === 'batch' && o.target.batchId === props.batchId)?.target ?? null
  else if (props.shipmentId) target = pick('shipment')
  else if (todayBatches.value.length === 1) target = pick('batch')
  form.target = target

  const seed = props.categorySeedKey
  const preset = seed ? categories.value.find((c) => c.seedKey === seed && c.isActive) : undefined
  if (preset) selectCategory(preset, false)
}

/** فوکوس آغاز: با «برای» آماده، کاشی نوع؛ وگرنه «برای». */
function focusFirst() {
  if (loadError.value) return
  if (form.target && !form.categoryId) {
    categoryGroup.value?.querySelector<HTMLElement>('[role="radio"]')?.focus()
  } else if (!form.target) {
    const first = targetGroup.value?.querySelector<HTMLElement>('[role="radio"]')
    if (first) first.focus()
    else targetGroup.value?.querySelector<HTMLElement>('button')?.focus()
  } else {
    amountField.value?.focus()
  }
}

function focusRadio(group: HTMLElement | null, key: string) {
  group?.querySelector<HTMLElement>(`[role="radio"][data-key="${CSS.escape(key)}"]`)?.focus()
}

/** کلیدهای جهت در گروه‌های رادیویی (کاشی‌ها، بخش‌های مبنا، تراشه‌ها). */
function onRadioKey(event: KeyboardEvent, index: number, count: number, select: (i: number) => void) {
  const next = radioKeyTarget(event.key, index, count)
  if (next === null) return
  event.preventDefault()
  select(next)
  const group = (event.currentTarget as HTMLElement).closest('[role="radiogroup"]')
  group?.querySelectorAll<HTMLElement>('[role="radio"]')[next]?.focus()
}

// ── ذخیره ────────────────────────────────────────────────────────────

function showProblem(field: 'category' | 'target' | 'amount' | 'fx', code: string) {
  problem.value = { field, text: message(code) }
  void nextTick(() => {
    if (field === 'category') categoryGroup.value?.querySelector<HTMLElement>('[role="radio"]')?.focus()
    else if (field === 'target') targetGroup.value?.querySelector<HTMLElement>('button')?.focus()
    else if (field === 'amount') amountField.value?.focus()
    else {
      showMore.value = true
      void nextTick(() => fxField.value?.focus())
    }
  })
}

async function save() {
  if (saving.value) return
  saveError.value = ''
  const amountProblem = amountField.value?.validate() ?? null
  if (amountProblem) {
    problem.value = { field: 'amount', text: amountProblem }
    amountField.value?.focus()
    return
  }
  const missing = missingPart(form)
  if (missing) {
    const field = missing === 'E_COST_CATEGORY_REQUIRED' ? 'category' : missing === 'E_COST_TARGET_REQUIRED' ? 'target' : missing === 'E_FX_REQUIRED' ? 'fx' : 'amount'
    showProblem(field, missing)
    return
  }
  problem.value = null

  saving.value = true
  saveFailed.value = false
  const release = blockReload('ثبت هزینه')
  writeDraft()
  try {
    const body = buildRequest(form, { version: version.value, clientKey: clientKey.value })
    const saved = isEdit.value
      ? await api.put<CostEntry>(`/api/costs/${props.entryId}`, body)
      : await api.post<CostEntry>('/api/costs', body)
    clearDraft()
    clientKey.value = newClientKey()
    announceSaved(saved)
    window.dispatchEvent(new CustomEvent(COSTS_CHANGED_EVENT, { detail: { entryId: saved.id } }))
    emit('close', { saved: true, entry: saved })
  } catch (e) {
    // اینترنت یا خطای سرور: پنل با همهٔ نوشته‌ها باز می‌ماند و «دوباره بفرست» همان کلید را می‌فرستد
    if (isNetworkError(e) || (e instanceof ApiError && e.status >= 500)) saveFailed.value = true
    else saveError.value = errorMessage(e)
  } finally {
    saving.value = false
    release()
  }
}

function announceSaved(saved: CostEntry) {
  const r = saved.result
  const text = isEdit.value
    ? message('I_COST_UPDATED', { number: n(saved.number) })
    : r.state === 2
      ? message('I_COST_SAVED_UNALLOCATED', { amount: n(r.baseAmount), currency: baseCurrencyWord() })
      : message('I_COST_SAVED', { amount: n(r.baseAmount), currency: baseCurrencyWord(), pieces: n(r.pieces) })
  if (props.reloadOnSave) {
    toastAfterReload(text, 'success')
    requestReload()
  } else {
    toast.success(text)
  }
}

function cancel() {
  // «انصراف» آگاهانه است: پیش‌نویس هم برود. بستن با Esc یا پس‌زمینه پیش‌نویس را نگه می‌دارد (بستن اتفاقی).
  clearDraft()
  emit('close')
}

const title = computed(() => (entry.value ? `هزینهٔ شمارهٔ ${n(entry.value.number)}` : 'ثبت هزینه'))
const saveLabel = computed(() => (isEdit.value ? 'ذخیره' : 'ثبت هزینه'))

onMounted(() => void load())
onBeforeUnmount(() => {
  if (previewTimer !== undefined) clearTimeout(previewTimer)
  if (draftTimer !== undefined) {
    clearTimeout(draftTimer)
    if (!saving.value) writeDraft()
  }
})
</script>

<template>
  <form class="flex min-h-full flex-col gap-5" novalidate data-cost-panel @submit.prevent="save">
    <div class="flex items-center gap-2">
      <AppIcon name="coins" class="h-6 w-6 text-primary" />
      <h2 class="text-xl font-bold" data-numeric>{{ title }}</h2>
    </div>

    <div v-if="loading" class="space-y-3" aria-hidden="true">
      <div class="skeleton h-24 w-full" />
      <div class="skeleton h-16 w-full" />
      <div class="skeleton h-12 w-2/3" />
    </div>

    <p v-else-if="loadError" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-on-error-container" role="alert">
      <AppIcon name="error" class="mt-0.5 h-4 w-4" />
      {{ loadError }}
    </p>

    <template v-else>
      <!-- پیش‌نویس جامانده -->
      <div v-if="draftOffer" class="space-y-2 rounded-lg border border-secondary/40 bg-secondary-container/40 p-3" role="status">
        <p class="text-sm font-medium">{{ message('I_COST_DRAFT_FOUND') }}</p>
        <div class="flex flex-wrap gap-2">
          <button
            ref="restoreButton"
            type="button"
            class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
            @click="restoreDraft"
          >
            <AppIcon name="undo" class="h-4 w-4" />
            برگرداندن
          </button>
          <Button variant="outline" @click="discardDraft">پاک کردن</Button>
        </div>
      </div>

      <!-- ۱) نوع هزینه -->
      <section class="space-y-2">
        <p id="cost-category-label" class="text-sm font-semibold">نوع هزینه</p>
        <div
          ref="categoryGroup"
          role="radiogroup"
          aria-labelledby="cost-category-label"
          :aria-describedby="problem?.field === 'category' ? 'cost-problem' : undefined"
          class="grid grid-cols-2 gap-2 sm:grid-cols-3"
        >
          <button
            v-for="(c, i) in tiles"
            :key="c.id"
            type="button"
            role="radio"
            :aria-checked="c.id === form.categoryId"
            :tabindex="c.id === form.categoryId || (!form.categoryId && i === 0) ? 0 : -1"
            :data-key="c.id"
            :data-seed="c.seedKey ?? undefined"
            class="flex min-h-14 items-center gap-2 rounded-xl border px-3 text-start text-sm"
            :class="c.id === form.categoryId
              ? 'border-primary bg-primary/10 font-semibold text-on-surface'
              : 'border-outline-variant bg-surface-container-lowest text-on-surface hover:bg-surface-container'"
            @click="selectCategory(c)"
            @keydown="onRadioKey($event, i, tiles.length, (j) => selectCategory(tiles[j], false))"
          >
            <AppIcon :name="c.icon || 'coins'" class="h-5 w-5 shrink-0 text-primary" />
            <span class="min-w-0 flex-1 truncate">{{ tileLabel(c) }}</span>
            <AppIcon v-if="c.id === form.categoryId" name="check" class="h-4 w-4 shrink-0 text-primary" />
          </button>
          <button
            v-if="!newCategory.open"
            type="button"
            class="flex min-h-14 items-center gap-2 rounded-xl border border-dashed border-outline-variant px-3 text-start text-sm text-primary hover:bg-surface-container"
            @click="openNewCategory"
          >
            <AppIcon name="plus" class="h-5 w-5 shrink-0" />
            نوع تازه
          </button>
        </div>
        <div v-if="newCategory.open" class="flex flex-wrap items-start gap-2 rounded-lg border border-outline-variant p-3">
          <label class="min-w-0 flex-1">
            <span class="sr-only">نام نوع تازه</span>
            <input
              ref="newCategoryInput"
              v-model="newCategory.name"
              type="text"
              maxlength="120"
              placeholder="مثلاً «کرایهٔ وانت»"
              class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary focus:ring-1 focus:ring-primary"
              @keydown.enter.prevent="addCategory"
              @keydown.esc.stop.prevent="newCategory.open = false"
            />
            <span v-if="newCategory.error" class="mt-1 block text-xs text-error">{{ newCategory.error }}</span>
          </label>
          <Button variant="primary" icon="plus" :loading="newCategory.saving" @click="addCategory">افزودن</Button>
          <Button variant="ghost" @click="newCategory.open = false">نه</Button>
        </div>
      </section>

      <!-- ۲) مبنا و مبلغ -->
      <section class="space-y-2">
        <div role="radiogroup" aria-label="مبلغ برای" class="grid grid-cols-3 rounded-xl border border-outline-variant p-1">
          <button
            v-for="(s, i) in BASIS_SEGMENTS"
            :key="s.basis"
            type="button"
            role="radio"
            :aria-checked="s.basis === form.basis"
            :tabindex="s.basis === form.basis ? 0 : -1"
            :data-basis="s.basis"
            class="inline-flex min-h-11 items-center justify-center gap-1 rounded-lg text-sm"
            :class="s.basis === form.basis ? 'bg-primary font-semibold text-on-primary' : 'text-on-surface-variant hover:bg-surface-container'"
            @click="form.basis = s.basis"
            @keydown="onRadioKey($event, i, BASIS_SEGMENTS.length, (j) => (form.basis = BASIS_SEGMENTS[j].basis))"
          >
            <AppIcon v-if="s.basis === form.basis" name="check" class="h-4 w-4" />
            {{ s.label }}
          </button>
        </div>
        <Field :label="amountLabel(form.basis)" :error="problem?.field === 'amount' ? problem.text : null" v-slot="f">
          <NumberField
            :id="f.id"
            ref="amountField"
            v-model="form.typed"
            v-model:currency="form.currency"
            :kind="amountKind"
            :rial-toggle="!usd"
            :described-by="f.describedBy"
            :invalid="f.invalid"
            :label="amountLabel(form.basis)"
            allow-negative
            enterkeyhint="done"
          />
        </Field>
      </section>

      <!-- ۳) برای -->
      <section class="space-y-2">
        <p id="cost-target-label" class="text-sm font-semibold">برای</p>
        <div
          ref="targetGroup"
          role="radiogroup"
          aria-labelledby="cost-target-label"
          :aria-describedby="problem?.field === 'target' ? 'cost-problem' : undefined"
          class="flex flex-wrap gap-2"
          data-numeric
        >
          <button
            v-for="(o, i) in targetOptions"
            :key="o.key"
            type="button"
            role="radio"
            :aria-checked="o.key === selectedTargetKey"
            :tabindex="o.key === selectedTargetKey || (!selectedTargetKey && i === 0) ? 0 : -1"
            :data-key="o.key"
            class="inline-flex min-h-11 items-center gap-2 rounded-full border px-4 text-sm"
            :class="o.key === selectedTargetKey
              ? 'border-primary bg-primary/10 font-semibold text-on-surface'
              : 'border-outline-variant bg-surface-container-lowest hover:bg-surface-container'"
            @click="selectTarget(o.target)"
            @keydown="onRadioKey($event, i, targetOptions.length, (j) => selectTarget(targetOptions[j].target))"
          >
            <AppIcon v-if="o.key === selectedTargetKey" name="check" class="h-4 w-4 text-primary" />
            {{ o.target.label }}
          </button>
        </div>
        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-full border border-dashed border-outline-variant px-4 text-sm text-primary hover:bg-surface-container"
          :aria-expanded="showPicker"
          @click="showPicker = !showPicker"
        >
          <AppIcon name="search" class="h-4 w-4" />
          فرش‌های دیگر…
        </button>
        <TargetPicker v-if="showPicker" :allow-bales="!simple" @pick="onPicked" @cancel="showPicker = false" />
      </section>

      <p v-if="problem" id="cost-problem" class="flex items-start gap-2 text-sm text-error" role="alert">
        <AppIcon name="error" class="mt-0.5 h-4 w-4" />
        {{ problem.text }}
      </p>

      <!-- ۴) جمله -->
      <section class="space-y-3 rounded-xl bg-surface-container-low p-4" aria-label="چطور تقسیم می‌شود">
        <p v-if="summary" class="flex flex-wrap items-center gap-x-2 font-semibold" data-numeric data-cost-summary>
          <span>{{ summary }}</span>
          <button
            v-if="methodApplies"
            type="button"
            class="inline-flex min-h-11 items-center rounded-lg px-2 text-sm font-semibold text-primary underline-offset-4 hover:underline"
            :aria-expanded="showMethod"
            @click="showMethod = !showMethod"
          >
            [تغییر]
          </button>
        </p>
        <p class="text-base leading-7" aria-live="polite" data-numeric data-cost-sentence>{{ sentence }}</p>
        <p v-if="previewBusy" class="text-xs text-on-surface-variant">در حال حساب…</p>
        <p v-if="isFrozen" class="flex items-start gap-2 text-xs text-on-surface-variant">
          <AppIcon name="lock" class="mt-0.5 h-3.5 w-3.5" />
          {{ message('I_COST_FREEZES') }}
        </p>
        <MethodChooser
          v-if="showMethod && methodApplies"
          v-model="form.method"
          v-model:area-basis="form.areaBasis"
          :group="selectedCategory?.group ?? null"
          :has-lot="hasLot"
          :show-area-basis="!simple"
        />
        <p v-if="!simple && !methodApplies" class="text-xs text-on-surface-variant">
          {{ methodPhrase(form.basis === CostBasis.PerM2 ? AllocationMethod.ByArea : AllocationMethod.ByCount, form.areaBasis) }}
        </p>
        <ul v-if="preview?.warnings.length" class="space-y-1" data-cost-warnings>
          <li
            v-for="w in preview.warnings"
            :key="w.code"
            class="flex items-start gap-2 rounded-lg bg-warning/10 px-3 py-2 text-sm text-warning"
            :data-warning="w.code"
          >
            <AppIcon name="warning" class="mt-0.5 h-4 w-4 shrink-0" />
            <span>{{ w.message }}</span>
          </li>
        </ul>
        <p v-if="previewError" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">
          <AppIcon name="error" class="mt-0.5 h-4 w-4 shrink-0" />
          {{ previewError }}
        </p>
      </section>

      <!-- بیشتر -->
      <section class="rounded-xl border border-outline-variant">
        <button
          type="button"
          class="flex min-h-11 w-full items-center justify-between gap-2 px-4 text-sm font-semibold"
          :aria-expanded="showMore"
          aria-controls="cost-more"
          @click="showMore = !showMore"
        >
          بیشتر
          <AppIcon :name="showMore ? 'chevron-up' : 'chevron-down'" class="h-4 w-4" />
        </button>
        <div v-if="showMore" id="cost-more" class="space-y-4 border-t border-outline-variant p-4">
          <Field label="تاریخ" v-slot="f">
            <JalaliDateField :id="f.id" v-model="form.date" :described-by="f.describedBy" label="تاریخ" required />
          </Field>

          <div class="grid gap-4 sm:grid-cols-2">
            <Field label="شمارهٔ رسید" v-slot="f">
              <input
                :id="f.id"
                v-model="form.receiptNo"
                type="text"
                maxlength="60"
                dir="auto"
                class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary focus:ring-1 focus:ring-primary"
              />
            </Field>
            <Field label="کارگاه / طرف حساب" v-slot="f">
              <select
                :id="f.id"
                v-model="form.providerId"
                class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary focus:ring-1 focus:ring-primary"
              >
                <option :value="null">—</option>
                <option
                  v-for="p in (providers ?? []).filter((x) => x.isActive || x.id === form.providerId)"
                  :key="p.id"
                  :value="p.id"
                >{{ p.name }}</option>
              </select>
            </Field>
          </div>

          <!-- ارز -->
          <div v-if="simple" class="space-y-2">
            <label class="flex min-h-11 items-center gap-2 text-sm">
              <input v-model="usd" type="checkbox" class="h-5 w-5 rounded border-outline-variant text-primary" />
              به دلار؟
            </label>
          </div>
          <div v-else role="radiogroup" aria-label="ارز مبلغ" class="flex flex-wrap gap-2">
            <button
              v-for="(c, i) in currencyChoices"
              :key="c"
              type="button"
              role="radio"
              :aria-checked="c === form.currency"
              :tabindex="c === form.currency ? 0 : -1"
              class="inline-flex min-h-11 items-center gap-2 rounded-lg border px-3 text-sm"
              :class="c === form.currency ? 'border-primary bg-primary/10 font-semibold' : 'border-outline-variant hover:bg-surface-container'"
              @click="form.currency = c"
              @keydown="onRadioKey($event, i, currencyChoices.length, (j) => (form.currency = currencyChoices[j]))"
            >
              <AppIcon v-if="c === form.currency" name="check" class="h-4 w-4" />
              {{ currencyWord(c) }}
            </button>
          </div>
          <Field
            v-if="usd"
            :label="`نرخ ارز (${currencyWord(tenantCurrency)} برای هر دلار)`"
            :error="problem?.field === 'fx' ? problem.text : null"
            v-slot="f"
          >
            <NumberField
              :id="f.id"
              ref="fxField"
              v-model="form.fxRate"
              kind="number"
              :scale="4"
              :suffix="currencyWord(tenantCurrency)"
              :described-by="f.describedBy"
              :invalid="f.invalid"
              label="نرخ ارز"
            />
          </Field>

          <!-- مبلغ ثابت -->
          <div class="space-y-2">
            <p class="text-sm font-semibold">مبلغ ثابت برای یک فرش</p>
            <p v-if="!canPin" class="text-xs text-on-surface-variant">{{ message('E_PINS_BASIS') }}</p>
            <template v-else-if="preview && preview.shares.length">
              <AllocationPreviewTable v-model:pins="form.pins" :preview="preview" pin-editable />
              <p v-if="pinsResidual !== null" class="text-xs text-on-surface-variant" data-numeric>
                {{ message('I_COST_PINS_LEFT', { amount: n(pinsResidual), currency: baseCurrencyWord() }) }}
              </p>
            </template>
            <p v-else class="text-xs text-on-surface-variant">{{ message('I_COST_SENTENCE_NO_AMOUNT') }}</p>
          </div>

          <Field label="توضیح" v-slot="f">
            <textarea
              :id="f.id"
              v-model="form.description"
              rows="2"
              maxlength="500"
              class="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 outline-none focus:border-primary focus:ring-1 focus:ring-primary"
            />
          </Field>

          <div v-if="!canPin && preview && preview.shares.length" class="space-y-2">
            <p class="text-sm font-semibold">سهم هر فرش</p>
            <AllocationPreviewTable :preview="preview" />
          </div>
        </div>
      </section>

      <!-- ذخیره نشد -->
      <div v-if="saveFailed" class="flex flex-wrap items-center gap-3 rounded-lg bg-error-container px-3 py-2 text-on-error-container" role="alert" data-cost-save-failed>
        <AppIcon name="error" class="h-5 w-5" />
        <span class="flex-1 text-sm">{{ message('I_COST_SAVE_FAILED') }}</span>
        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
          :disabled="saving"
          @click="save"
        >
          <AppIcon name="undo" class="h-4 w-4" />
          دوباره بفرست
        </button>
      </div>
      <p v-if="saveError" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">
        <AppIcon name="error" class="mt-0.5 h-4 w-4 shrink-0" />
        {{ saveError }}
      </p>

      <div class="sticky bottom-0 -mx-5 mt-auto flex flex-wrap gap-2 border-t border-outline-variant bg-surface-container-lowest px-5 py-3">
        <Button type="submit" variant="primary" size="lg" icon="check" :loading="saving" class="flex-1" data-cost-save>
          {{ saveLabel }}
        </Button>
        <Button size="lg" variant="ghost" :disabled="saving" @click="cancel">انصراف</Button>
      </div>
    </template>
  </form>
</template>
