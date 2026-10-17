<script setup lang="ts">
/**
 * جزیرهٔ `intake-grid` (I.5): صفحهٔ «ورود فرش» (H.1).
 *
 * - سربرگ: «تاریخ رسیدن» (امروز به‌طور پیش‌فرض) و عنوانی که با تاریخ عوض می‌شود: «ورودی امروز» / «ورودی دیروز» /
 *   «ورودی ۱۴۰۵/۰۶/۱۰».
 * - «ورودی‌های اخیر»: چیپ هر ورودی هفت روز گذشته («۱۴۰۵/۰۶/۱۴ · حاج رضا · ۸ تخته») که همان روز را باز می‌کند تا غلط
 *   تایپی درست شود.
 * - جدول `intake` همهٔ ردیف‌های همان روز. «فروشنده» ستون چسبان است و ورودیِ هر ردیف هنگام ذخیره از (روز، فروشنده)
 *   پیدا یا ساخته می‌شود؛ عوض کردن فروشندهٔ یک ردیف فقط همان ردیف را جابه‌جا می‌کند و هیچ ردیفی از جدول نمی‌پرد.
 * - پانویس به تفکیک فروشنده (اگر چند تاست): «حاج رضا: ۱۲ تخته · ۳۴٫۵ متر · خرید ۴۵۰٬۰۰۰٬۰۰۰ تومان» (پول فقط Business)؛
 *   با دسته «۵۲ تخته (۳ ردیف)». هر گروه دکمهٔ خودش را دارد: «هزینه یا خرید یکجای این ورودی» (E7؛ وقتی پنل هزینه هست).
 * - «چاپ برچسب‌ها» برای فرش‌های ذخیره‌شدهٔ همین روز.
 *
 * روز عوض شود: اول هر چه مانده ذخیره و پیش‌نویسش نوشته می‌شود، بعد روز تازه بار می‌شود (و `?date=` در نشانی می‌ماند تا
 * تازه کردن صفحه همان روز را بیاورد).
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import DataGrid from '@/components/grid/DataGrid.vue'
import { KEY_SELLER, faDigits, faNum, fold, rowArea, rowPurchase, rowQty } from '@/components/grid/gridParse'
import { useGridController } from '@/components/grid/useGridController'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { addIsoDays, faJalali } from '@/lib/jalali'
import { message } from '@/lib/messages'
import { add, dec, type Dec } from '@/lib/num'
import { hasPanel, openPanel, toast } from '@/lib/ui'
import type { GridData, IntakeBatch, JsonValue } from '@/lib/types/grid'
import { SerialMode } from '@/lib/types/platform'

const props = withDefaults(
  defineProps<{
    /** روز ایرانِ جدول، ISO */
    date: string
    defaultSeller?: string | null
    serialMode?: number
    maxPlausibleSideM?: number
  }>(),
  { defaultSeller: null, serialMode: SerialMode.SuggestNext, maxPlausibleSideM: 12 },
)

const ctx = appContext()
const day = ref(props.date || ctx.today)
const dateField = ref<string | null>(day.value)
const loading = ref(true)
const loadError = ref('')
const recent = ref<IntakeBatch[]>([])
const defaultSeller = ref(props.defaultSeller)
const business = ctx.policies.business && ctx.moneyVisible

const ctrl = useGridController({
  gridId: 'intake',
  allowAdd: true,
  allowDelete: true,
  serialMode: () => props.serialMode as SerialMode,
  maxPlausibleSideM: () => props.maxPlausibleSideM,
  draftScope: () => ({ tenantId: ctx.tenantId, userId: ctx.userId, gridId: 'intake', contextId: day.value }),
  batchContext: () => ({ id: null, sellerName: null, receivedAt: day.value }),
  stickyDefaults: (): Record<string, JsonValue> => (defaultSeller.value ? { [KEY_SELLER]: defaultSeller.value } : {}),
  onSaved: () => scheduleRecent(),
  onRestored: () => void reload(),
})

// ── عنوان ───────────────────────────────────────────────────────────────

const title = computed(() => {
  if (day.value === ctx.today) return message('I_GRID_INTAKE_TODAY')
  if (day.value === addIsoDays(ctx.today, -1)) return message('I_GRID_INTAKE_YESTERDAY')
  return message('I_GRID_INTAKE_DAY', { date: faJalali(day.value) })
})

// ── بار کردن ────────────────────────────────────────────────────────────

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const data = await api.get<GridData>(`/api/grid/intake?date=${encodeURIComponent(day.value)}`)
    ctrl.load(data)
  } catch (error) {
    loadError.value = errorMessage(error) || message('E_GRID_LOAD')
  } finally {
    loading.value = false
  }
}

/** بار دوباره بی از دست دادن ردیف‌های ذخیره‌نشده (پس از «برگردان»). */
async function reload() {
  try {
    const data = await api.get<GridData>(`/api/grid/intake?date=${encodeURIComponent(day.value)}`)
    ctrl.load(data, { merge: true })
  } catch {
    /* جدول فعلی می‌ماند */
  }
}

async function loadRecent() {
  try {
    recent.value = await api.get<IntakeBatch[]>('/api/batches/recent?days=7')
  } catch {
    /* نوار «ورودی‌های اخیر» اختیاری است */
  }
}

let recentTimer: ReturnType<typeof setTimeout> | null = null
function scheduleRecent() {
  if (recentTimer) clearTimeout(recentTimer)
  recentTimer = setTimeout(() => void loadRecent(), 2000)
}

/** روز دیگر: اول هر چه مانده ذخیره شود، بعد روز تازه. */
async function switchDay(iso: string | null) {
  if (!iso || iso === day.value) return
  ctrl.flushDrafts()
  await ctrl.waitForIdle()
  await ctrl.flush()
  ctrl.flushDrafts()
  if (ctrl.unsavedCount > 0 || ctrl.saveState.inFlight) {
    dateField.value = day.value
    toast.warning('اول ردیف‌های این روز را ذخیره یا درست کنید.')
    return
  }
  loading.value = true
  try {
    const data = await api.get<GridData>(`/api/grid/intake?date=${encodeURIComponent(iso)}`)
    day.value = iso
    dateField.value = iso
    defaultSeller.value = null
    ctrl.load(data)
    syncUrl()
  } catch (error) {
    dateField.value = day.value
    loadError.value = errorMessage(error)
  } finally {
    loading.value = false
  }
}

function onDateChange(iso: string | null) {
  dateField.value = iso
  if (iso) void switchDay(iso)
}

function syncUrl() {
  try {
    const url = new URL(window.location.href)
    if (day.value === ctx.today) url.searchParams.delete('date')
    else url.searchParams.set('date', day.value)
    window.history.replaceState(window.history.state, '', url.toString())
  } catch {
    /* نشانی دست نخورد؛ مهم نیست */
  }
}

// ── ورودی‌های اخیر ──────────────────────────────────────────────────────

function chipText(batch: IntakeBatch): string {
  const date = batch.receivedOnJalali ? faDigits(batch.receivedOnJalali) : faJalali(batch.receivedOn)
  return message('I_GRID_RECENT_CHIP', {
    date,
    seller: batch.sellerName || message('I_GRID_NO_SELLER'),
    pieces: batch.pieces,
  })
}

// ── پانویس به تفکیک فروشنده ─────────────────────────────────────────────

interface FooterGroup {
  key: string
  seller: string | null
  rows: number
  pieces: number
  area: Dec
  purchase: Dec | null
  batchId: string | null
}

const groups = computed<FooterGroup[]>(() => {
  const map = new Map<string, FooterGroup>()
  for (const row of ctrl.rows) {
    if (row.placeholder) continue
    const seller = typeof row.values[KEY_SELLER] === 'string' ? (row.values[KEY_SELLER] as string) : null
    const key = fold(seller)
    let group = map.get(key)
    if (!group) {
      group = { key, seller, rows: 0, pieces: 0, area: dec(0), purchase: null, batchId: null }
      map.set(key, group)
    }
    group.rows++
    group.pieces += rowQty(row.values)
    const area = rowArea(row.values)
    if (area) group.area = add(group.area, area.total)
    if (business) {
      const derived = row.derived['rug.purchaseTotal']
      const local = rowPurchase(row.values, ctrl.columns, ctx.moneyScale)
      const amount = row.dirty.length === 0 && typeof derived === 'number' ? dec(derived) : local?.total ?? (typeof derived === 'number' ? dec(derived) : null)
      if (amount) group.purchase = add(group.purchase ?? dec(0), amount)
    }
  }
  // شناسهٔ ورودی هر گروه از جمع‌های سرور (یا شناسهٔ ورودی ردیف‌ها)
  for (const server of ctrl.totals?.groups ?? []) {
    const group = map.get(fold(server.sellerName))
    if (group && server.batchId) group.batchId = server.batchId
  }
  for (const row of ctrl.rows) {
    const batchId = row.derived['batch.id']
    const group = map.get(fold(typeof row.values[KEY_SELLER] === 'string' ? (row.values[KEY_SELLER] as string) : null))
    if (group && !group.batchId && typeof batchId === 'string') group.batchId = batchId
  }
  return [...map.values()]
})

function groupText(group: FooterGroup): string {
  const parts = [
    group.pieces !== group.rows
      ? message('I_GRID_PIECES_ROWS', { n: group.pieces, rows: group.rows })
      : message('I_GRID_PIECES', { n: group.pieces }),
    message('I_GRID_AREA', { area: faNum(group.area, 2) }),
  ]
  if (business && group.purchase) {
    parts.push(message('I_GRID_PURCHASE', { amount: faNum(group.purchase, ctx.moneyScale), currency: ctx.currency.label }))
  }
  const text = parts.join(' · ')
  if (groups.value.length < 2) return text
  return `${group.seller || message('I_GRID_NO_SELLER')}: ${text}`
}

const costPanel = business && hasPanel('cost-entry')

function openCost(group: FooterGroup) {
  if (!group.batchId) return
  // CostScope.IntakeBatch = 3 (B.10)
  void openPanel('cost-entry', { scope: 3, batchId: group.batchId })
}

const savedRugIds = computed(() => ctrl.rows.filter((r) => !r.placeholder && r.rugId).map((r) => r.rugId as string))
const labelsHref = computed(() => {
  if (savedRugIds.value.length === 0) return null
  return `/Labels/Print?date=${encodeURIComponent(day.value)}`
})

onMounted(() => {
  void load()
  void loadRecent()
})

onBeforeUnmount(() => {
  if (recentTimer) clearTimeout(recentTimer)
  ctrl.dispose()
})
</script>

<template>
  <div class="flex flex-col gap-4">
    <!-- سربرگ -->
    <header class="flex flex-wrap items-end justify-between gap-4">
      <h2 class="text-2xl font-bold text-on-surface">{{ title }}</h2>
      <div class="w-full max-w-64">
        <Field v-slot="f" label="تاریخ رسیدن">
          <JalaliDateField
            :id="f.id"
            :described-by="f.describedBy"
            :model-value="dateField"
            :max="ctx.today"
            label="تاریخ رسیدن"
            @update:model-value="onDateChange"
          />
        </Field>
      </div>
    </header>

    <!-- ورودی‌های اخیر -->
    <nav v-if="recent.length > 0" aria-label="ورودی‌های اخیر" class="flex flex-col gap-2">
      <h3 class="text-sm font-medium text-on-surface-variant">ورودی‌های اخیر</h3>
      <ul class="flex flex-wrap gap-2">
        <li v-for="batch in recent" :key="batch.id">
          <button
            type="button"
            class="inline-flex min-h-11 items-center gap-1.5 rounded-full border px-4 text-sm"
            :class="batch.receivedOn === day ? 'border-primary bg-primary/10 font-semibold text-primary' : 'border-outline-variant text-on-surface hover:bg-surface-container'"
            :aria-pressed="batch.receivedOn === day"
            @click="switchDay(batch.receivedOn)"
          >
            <AppIcon v-if="batch.receivedOn === day" name="check" class="h-4 w-4" />
            <AppIcon v-else name="calendar" class="h-4 w-4" />
            {{ chipText(batch) }}
          </button>
        </li>
      </ul>
    </nav>

    <!-- خطای بار -->
    <div
      v-if="loadError"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-error/40 bg-error-container/40 px-4 py-3 text-sm text-on-surface"
      role="alert"
    >
      <AppIcon name="error" class="h-5 w-5 text-error" />
      <p class="min-w-0 flex-1">{{ loadError }}</p>
      <Button icon="undo" @click="load">دوباره</Button>
    </div>

    <div v-else-if="loading && !ctrl.loaded" class="skeleton h-96 w-full" aria-hidden="true"></div>

    <DataGrid v-else :ctrl="ctrl" :label="title" autofocus>
      <template #toolbar>
        <Button v-if="labelsHref" icon="printer" :href="labelsHref">چاپ برچسب‌ها</Button>
        <Button v-else icon="printer" disabled>چاپ برچسب‌ها</Button>
      </template>

      <template #footer>
        <p v-if="ctrl.counts.rows === 0" class="text-sm text-on-surface-variant">{{ message('I_GRID_EMPTY_DAY') }}</p>
        <ul v-else class="flex flex-col gap-2" aria-label="جمع ورودی‌ها">
          <li
            v-for="group in groups"
            :key="group.key"
            class="flex flex-wrap items-center gap-3 rounded-xl bg-surface-container-low px-4 py-2 text-sm text-on-surface"
          >
            <AppIcon name="package" class="h-5 w-5 shrink-0 text-on-surface-variant" />
            <span class="min-w-0 flex-1 tabular-nums">{{ groupText(group) }}</span>
            <Button v-if="costPanel && group.batchId" icon="coins" @click="openCost(group)">هزینه یا خرید یکجای این ورودی</Button>
          </li>
        </ul>
      </template>
    </DataGrid>
  </div>
</template>
