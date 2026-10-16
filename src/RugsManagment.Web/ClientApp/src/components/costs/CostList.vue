<script setup lang="ts">
/**
 * فهرست ثبت‌های هزینه (H.1 «Costs tab and /Costs»): دفتر «هزینه‌ها» و زبانهٔ «هزینه‌ها»ی محموله هر دو همین را نشان
 * می‌دهند. `GET /api/costs` با فیلتر؛ تراشهٔ «تقسیم‌نشده» هر ثبتی را که همهٔ مبلغش تقسیم نشده نشان می‌دهد.
 *
 * هر ردیف: شماره و تاریخ، نوع با نماد، «برای»، مبلغ (به ارز کارگاه؛ دلاری/ریالی با مبلغ اصلی)، وضعیت تقسیم با نماد و
 * نوشته (نه فقط رنگ) و هشدارها. لمس ردیف پنل را در حالت ویرایش باز می‌کند. حذف بی‌پرسش است چون برمی‌گردد: نوار
 * «هزینهٔ شمارهٔ ۱۲ حذف شد [برگردان]» تا ۸ ثانیه (`POST /api/costs/{id}/restore`).
 *
 * پس از هر ثبت، ویرایش، حذف یا بازگردانی (هر جای صفحه، رویداد `rugs:costs-changed`) دوباره می‌خواند.
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
import { api, errorMessage } from '@/lib/api'
import { faJalali } from '@/lib/jalali'
import { message } from '@/lib/messages'
import {
  AllocationState,
  COSTS_CHANGED_EVENT,
  CostGroup,
  type CostCategory,
  type CostEntryListItem,
  type CostPage,
  type CostPanelPreset,
} from '@/lib/types/costs'
import { openPanel, toast } from '@/lib/ui'
import { baseCurrencyWord, currencyWord, n, stateChipText, WARNING_SHORT } from './costText'

const props = withDefaults(
  defineProps<{
    shipmentId?: string | null
    batchId?: string | null
    categoryId?: string | null
    /** فیلترهای نوع و تاریخ (دفتر هزینه‌ها؛ زبانهٔ محموله فقط تراشهٔ «تقسیم‌نشده») */
    showFilters?: boolean
    /** پنل ثبت تازه با این «برای» باز می‌شود */
    preset?: CostPanelPreset
    /** دکمهٔ «ثبت هزینه» بالای فهرست */
    showCreate?: boolean
    /** فقط ثبت‌هایی که این فرش را دارند (صفحهٔ فرش) */
    rugId?: string | null
    /** نمای فشرده (صفحهٔ فرش): بی تراشهٔ «تقسیم‌نشده»، بی حذف، ده ردیف */
    compact?: boolean
    /** از اول فقط «تقسیم‌نشده»‌ها (`/Costs?unallocated=1`) */
    initialUnallocated?: boolean
  }>(),
  {
    shipmentId: null,
    batchId: null,
    categoryId: null,
    showFilters: false,
    preset: () => ({}),
    showCreate: true,
    rugId: null,
    compact: false,
    initialUnallocated: false,
  },
)

const emit = defineEmits<{ changed: [] }>()

const filters = reactive({
  categoryId: props.categoryId ?? '',
  unallocatedOnly: props.initialUnallocated,
  from: null as string | null,
  to: null as string | null,
  page: 1,
})

const loading = ref(true)
const error = ref('')
const page = ref<CostPage<CostEntryListItem> | null>(null)
const unallocatedCount = ref<number | null>(null)
const categories = ref<CostCategory[]>([])

const undo = ref<{ id: string; number: number } | null>(null)
let undoTimer: ReturnType<typeof setTimeout> | undefined
const busyId = ref<string | null>(null)

const items = computed(() => page.value?.items ?? [])
const totalPages = computed(() => {
  const p = page.value
  if (!p) return 1
  return p.totalPages ?? Math.max(1, Math.ceil(p.totalCount / Math.max(1, p.pageSize)))
})
const hasFilters = computed(() => !!filters.categoryId || filters.unallocatedOnly || !!filters.from || !!filters.to)

function query(extra: Record<string, string> = {}): string {
  const q = new URLSearchParams()
  if (props.shipmentId) q.set('shipmentId', props.shipmentId)
  if (props.batchId) q.set('batchId', props.batchId)
  if (props.rugId) q.set('rugId', props.rugId)
  if (props.compact) q.set('pageSize', '10')
  if (filters.categoryId) q.set('categoryId', filters.categoryId)
  if (filters.unallocatedOnly) q.set('notFullyAllocated', 'true')
  if (filters.from) q.set('from', filters.from)
  if (filters.to) q.set('to', filters.to)
  q.set('page', String(filters.page))
  for (const [k, v] of Object.entries(extra)) q.set(k, v)
  return q.toString()
}

let loadSeq = 0
async function load() {
  const seq = ++loadSeq
  loading.value = true
  error.value = ''
  try {
    const [list, open] = await Promise.all([
      api.get<CostPage<CostEntryListItem>>(`/api/costs?${query()}`),
      // شمار «تقسیم‌نشده» همین دامنه، برای خط خلاصه (یک ردیف بس است؛ totalCount مهم است)
      api
        .get<CostPage<CostEntryListItem>>(
          `/api/costs?${new URLSearchParams({
            ...(props.shipmentId ? { shipmentId: props.shipmentId } : {}),
            ...(props.batchId ? { batchId: props.batchId } : {}),
            ...(props.rugId ? { rugId: props.rugId } : {}),
            notFullyAllocated: 'true',
            pageSize: '1',
          }).toString()}`,
        )
        .catch(() => null),
    ])
    if (seq !== loadSeq) return
    page.value = list
    unallocatedCount.value = open?.totalCount ?? null
  } catch (e) {
    if (seq === loadSeq) error.value = errorMessage(e)
  } finally {
    if (seq === loadSeq) loading.value = false
  }
}

async function loadCategories() {
  if (!props.showFilters) return
  try {
    categories.value = await api.get<CostCategory[]>('/api/cost-categories')
  } catch {
    categories.value = []
  }
}

watch(
  () => [filters.categoryId, filters.unallocatedOnly, filters.from, filters.to],
  () => {
    filters.page = 1
    void load()
  },
)

function goPage(p: number) {
  filters.page = Math.min(Math.max(1, p), totalPages.value)
  void load()
}

function clearFilters() {
  filters.categoryId = ''
  filters.unallocatedOnly = false
  filters.from = null
  filters.to = null
}

async function create() {
  await openPanel('cost-entry', { ...props.preset })
}

async function edit(item: CostEntryListItem) {
  await openPanel('cost-entry', { entryId: item.id, ...(props.preset.reloadOnSave ? { reloadOnSave: true } : {}) })
}

async function remove(item: CostEntryListItem) {
  busyId.value = item.id
  try {
    const v = item.version !== undefined ? `?version=${item.version}` : ''
    await api.del(`/api/costs/${item.id}${v}`)
    showUndo(item)
    notifyChanged()
  } catch (e) {
    toast.error(errorMessage(e))
  } finally {
    busyId.value = null
  }
}

function showUndo(item: CostEntryListItem) {
  if (undoTimer !== undefined) clearTimeout(undoTimer)
  undo.value = { id: item.id, number: item.number }
  undoTimer = setTimeout(() => (undo.value = null), 8000)
}

async function restore() {
  const u = undo.value
  if (!u) return
  undo.value = null
  if (undoTimer !== undefined) clearTimeout(undoTimer)
  try {
    await api.post(`/api/costs/${u.id}/restore`)
    toast.success(message('I_COST_RESTORED', { number: n(u.number) }))
    notifyChanged()
  } catch (e) {
    toast.error(errorMessage(e))
  }
}

function notifyChanged() {
  window.dispatchEvent(new CustomEvent(COSTS_CHANGED_EVENT))
}

function onChanged() {
  void load()
  emit('changed')
}

function amountText(item: CostEntryListItem): string {
  return `${n(item.baseAmount)} ${baseCurrencyWord()}`
}

function originalText(item: CostEntryListItem): string | null {
  return item.originalAmount === null ? null : `${n(item.originalAmount)} ${currencyWord(item.currency)}`
}

function stateClass(state: number): string {
  if (state === AllocationState.Unallocated) return 'bg-error-container text-on-error-container'
  if (state === AllocationState.Partial) return 'bg-warning/12 text-warning'
  return 'bg-success/12 text-success'
}

function stateIcon(state: number): string {
  return state === AllocationState.Allocated ? 'check' : 'warning'
}

onMounted(() => {
  window.addEventListener(COSTS_CHANGED_EVENT, onChanged)
  void load()
  void loadCategories()
})
onBeforeUnmount(() => {
  window.removeEventListener(COSTS_CHANGED_EVENT, onChanged)
  if (undoTimer !== undefined) clearTimeout(undoTimer)
})

defineExpose({ reload: load, create })
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-center gap-2">
      <Button v-if="showCreate" variant="primary" :size="compact ? 'md' : 'lg'" icon="plus" data-cost-new @click="create">
        {{ compact ? 'ثبت هزینه برای این فرش' : 'ثبت هزینه' }}
      </Button>
      <button
        v-if="!compact"
        type="button"
        :aria-pressed="filters.unallocatedOnly"
        class="inline-flex min-h-11 items-center gap-2 rounded-full border px-4 text-sm"
        :class="filters.unallocatedOnly ? 'border-primary bg-primary/10 font-semibold' : 'border-outline-variant hover:bg-surface-container'"
        data-filter-unallocated
        @click="filters.unallocatedOnly = !filters.unallocatedOnly"
      >
        <AppIcon :name="filters.unallocatedOnly ? 'check' : 'filter'" class="h-4 w-4" />
        {{ message('I_COST_UNALLOCATED') }}
        <span v-if="unallocatedCount" class="rounded-full bg-error-container px-2 text-xs text-on-error-container" data-numeric>
          {{ n(unallocatedCount) }}
        </span>
      </button>
    </div>

    <div v-if="showFilters" class="grid gap-3 rounded-xl border border-outline-variant bg-surface-container-lowest p-4 sm:grid-cols-3">
      <label class="block">
        <span class="mb-1 block text-sm font-medium">نوع هزینه</span>
        <select
          v-model="filters.categoryId"
          class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary focus:ring-1 focus:ring-primary"
        >
          <option value="">همه</option>
          <optgroup label="هزینه">
            <option v-for="c in categories.filter((x) => x.group === CostGroup.Expense)" :key="c.id" :value="c.id">{{ c.name }}</option>
          </optgroup>
          <optgroup label="خرید">
            <option v-for="c in categories.filter((x) => x.group === CostGroup.Purchase)" :key="c.id" :value="c.id">{{ c.name }}</option>
          </optgroup>
        </select>
      </label>
      <div>
        <span class="mb-1 block text-sm font-medium">از تاریخ</span>
        <JalaliDateField v-model="filters.from" :chips="false" aria-label="از تاریخ" />
      </div>
      <div>
        <span class="mb-1 block text-sm font-medium">تا تاریخ</span>
        <JalaliDateField v-model="filters.to" :chips="false" aria-label="تا تاریخ" />
      </div>
    </div>

    <p
      v-if="unallocatedCount && !filters.unallocatedOnly && !compact"
      class="flex items-start gap-2 rounded-lg bg-warning/10 px-3 py-2 text-sm text-warning"
      role="status"
      data-numeric
    >
      <AppIcon name="warning" class="mt-0.5 h-4 w-4 shrink-0" />
      {{ message('I_COST_UNALLOCATED_COUNT', { n: unallocatedCount }) }}
    </p>

    <div v-if="loading && !page" class="space-y-2" aria-hidden="true">
      <div v-for="i in 4" :key="i" class="skeleton h-16 w-full" />
    </div>

    <p v-else-if="error" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-on-error-container" role="alert">
      <AppIcon name="error" class="mt-0.5 h-4 w-4" />
      {{ error }}
    </p>

    <template v-else-if="items.length === 0">
      <EmptyState v-if="hasFilters" icon="filter" :title="message('I_COST_EMPTY_FILTERED')">
        <template #actions>
          <Button @click="clearFilters">پاک کردن فیلترها</Button>
        </template>
      </EmptyState>
      <p v-else-if="compact" class="text-sm text-on-surface-variant">هنوز هزینه‌ای برای این فرش ثبت نشده.</p>
      <EmptyState v-else icon="coins" title="هزینه‌ای ثبت نشده" :text="message('I_COST_EMPTY')">
        <template #actions>
          <Button variant="primary" icon="plus" @click="create">ثبت هزینه</Button>
        </template>
      </EmptyState>
    </template>

    <ul v-else class="divide-y divide-outline-variant rounded-xl border border-outline-variant bg-surface-container-lowest" :aria-busy="loading || undefined" data-numeric>
      <li v-for="item in items" :key="item.id" class="flex flex-wrap items-start gap-3 p-3 sm:flex-nowrap" :data-cost-entry="item.number">
        <button
          type="button"
          class="flex min-h-11 min-w-0 flex-1 items-start gap-3 rounded-lg p-1 text-start hover:bg-surface-container"
          :aria-label="`هزینهٔ شمارهٔ ${n(item.number)}، ${item.category}`"
          @click="edit(item)"
        >
          <span class="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-surface-container">
            <AppIcon :name="item.categoryIcon || 'coins'" class="h-5 w-5 text-primary" />
          </span>
          <span class="min-w-0 flex-1 space-y-1">
            <span class="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span class="font-semibold">{{ item.category }}</span>
              <span class="text-xs text-on-surface-variant">#{{ n(item.number) }} · {{ faJalali(item.date) }}</span>
              <AppIcon v-if="item.frozen" name="lock" class="h-3.5 w-3.5 text-on-surface-variant" label="رسید ثابت" />
            </span>
            <span class="block truncate text-sm text-on-surface-variant">برای {{ item.targetsText }}</span>
            <span v-if="item.description" class="block truncate text-xs text-on-surface-variant">{{ item.description }}</span>
            <span class="flex flex-wrap gap-1">
              <span class="inline-flex min-h-6 items-center gap-1 rounded-full px-2 text-xs font-medium" :class="stateClass(item.state)" :data-state="item.state">
                <AppIcon :name="stateIcon(item.state)" class="h-3.5 w-3.5" />
                {{ stateChipText(item.state, item.unallocated) }}
              </span>
              <span
                v-for="code in item.warningCodes ?? []"
                :key="code"
                class="inline-flex min-h-6 items-center gap-1 rounded-full bg-warning/12 px-2 text-xs text-warning"
                :data-warning="code"
              >
                <AppIcon name="warning" class="h-3.5 w-3.5" />
                {{ WARNING_SHORT[code] ?? 'هشدار' }}
              </span>
            </span>
          </span>
          <span class="shrink-0 text-end">
            <span class="block font-bold">{{ amountText(item) }}</span>
            <span v-if="originalText(item)" class="block text-xs text-on-surface-variant">{{ originalText(item) }}</span>
          </span>
        </button>
        <button
          v-if="!compact"
          type="button"
          class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-error hover:bg-error-container"
          :disabled="busyId === item.id"
          :aria-label="`حذف هزینهٔ شمارهٔ ${n(item.number)}`"
          @click="remove(item)"
        >
          <AppIcon name="trash" class="h-4 w-4" />
        </button>
      </li>
    </ul>

    <nav v-if="totalPages > 1" class="flex items-center justify-center gap-2" aria-label="صفحه‌ها" data-numeric>
      <Button icon="chevron-right" :disabled="filters.page <= 1" @click="goPage(filters.page - 1)">قبلی</Button>
      <span class="text-sm text-on-surface-variant">{{ n(filters.page) }} از {{ n(totalPages) }}</span>
      <Button icon-end="chevron-left" :disabled="filters.page >= totalPages" @click="goPage(filters.page + 1)">بعدی</Button>
    </nav>

    <!-- «هزینهٔ شمارهٔ ۱۲ حذف شد [برگردان]» -->
    <div
      v-if="undo"
      class="fixed inset-x-4 bottom-20 z-50 mx-auto flex max-w-md items-center gap-3 rounded-xl bg-on-surface px-4 py-2 text-surface shadow-lg md:bottom-6"
      role="status"
      data-cost-undo
    >
      <span class="flex-1 text-sm" data-numeric>{{ message('I_COST_DELETED', { number: n(undo.number) }) }}</span>
      <button type="button" class="inline-flex min-h-11 items-center gap-1 rounded-lg px-3 text-sm font-semibold underline-offset-4 hover:underline" @click="restore">
        <AppIcon name="undo" class="h-4 w-4" />
        برگردان
      </button>
    </div>
  </div>
</template>
