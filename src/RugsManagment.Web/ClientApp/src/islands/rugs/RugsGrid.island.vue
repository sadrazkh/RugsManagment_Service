<script setup lang="ts">
/**
 * جزیرهٔ `rugs-grid` (I.5): فهرست فرش‌های انبار به‌صورت جدول اکسل‌مانند، برای درست کردن چند فرش پشت سر هم بی باز
 * کردن تک‌تک صفحه‌ها. همان جست‌وجو و فیلترهای صفحهٔ «فرش‌ها» (`query`) به `GET /api/grid/rugs` می‌رود.
 *
 * ردیف تازه این‌جا ساخته نمی‌شود (جای ورود فرش «ورود فرش» است) و حذف هم از صفحهٔ هر فرش است؛ بقیهٔ رفتار جدول همان است:
 * ذخیرهٔ خودکار، پیش‌نویس روی دستگاه، تعارض، برگرد، چسباندن از اکسل. صفحه‌ها ۲۰۰ ردیفی و با رسیدن به ته جدول بار
 * می‌شوند.
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import DataGrid from '@/components/grid/DataGrid.vue'
import { faNum } from '@/components/grid/gridParse'
import { useGridController } from '@/components/grid/useGridController'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import type { GridData } from '@/lib/types/grid'
import { SerialMode } from '@/lib/types/platform'

const props = withDefaults(defineProps<{ query?: Record<string, string> }>(), { query: () => ({}) })

const PAGE = 200
const ctx = appContext()
const loading = ref(true)
const loadingMore = ref(false)
const loadError = ref('')

/** پیش‌نویس هر جست‌وجو جدا (ردیف‌ها با شناسهٔ فرش نشانده می‌شوند، پس فیلتر دیگر هم آسیبی نمی‌زند). */
const contextId = computed(() => {
  const keys = Object.keys(props.query).sort()
  return keys.length === 0 ? 'list' : `list?${keys.map((k) => `${k}=${props.query[k]}`).join('&')}`.slice(0, 200)
})

const ctrl = useGridController({
  gridId: 'rugs',
  allowAdd: false,
  allowDelete: false,
  serialMode: () => SerialMode.SuggestNext,
  maxPlausibleSideM: () => ctx.maxPlausibleSideM,
  draftScope: () => ({ tenantId: ctx.tenantId, userId: ctx.userId, gridId: 'rugs', contextId: contextId.value }),
})

function url(skip: number): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(props.query)) {
    if (value !== null && value !== undefined && value !== '') query.set(key, String(value))
  }
  query.set('skip', String(skip))
  query.set('take', String(PAGE))
  return `/api/grid/rugs?${query.toString()}`
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    ctrl.load(await api.get<GridData>(url(0)))
  } catch (error) {
    loadError.value = errorMessage(error) || message('E_GRID_LOAD')
  } finally {
    loading.value = false
  }
}

const total = computed(() => ctrl.totals?.rows ?? ctrl.rows.length)
const loadedCount = computed(() => ctrl.rows.length)

async function loadMore() {
  if (loadingMore.value || loadedCount.value >= total.value) return
  loadingMore.value = true
  try {
    ctrl.append(await api.get<GridData>(url(loadedCount.value)))
  } catch (error) {
    ctrl.notify(errorMessage(error), 'error')
  } finally {
    loadingMore.value = false
  }
}

const footer = computed(() => {
  const totals = ctrl.totals
  if (!totals) return ''
  const pieces =
    totals.pieces !== totals.rows
      ? message('I_GRID_PIECES_ROWS', { n: totals.pieces, rows: totals.rows })
      : message('I_GRID_PIECES', { n: totals.pieces })
  return `${pieces} · ${message('I_GRID_AREA', { area: faNum(totals.areaM2, 2) })}`
})

onMounted(() => void load())
onBeforeUnmount(() => ctrl.dispose())
</script>

<template>
  <div class="flex flex-col gap-3">
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

    <EmptyState v-else-if="ctrl.loaded && total === 0" :title="message('I_GRID_EMPTY_RUGS')" icon="search" />

    <DataGrid v-else :ctrl="ctrl" label="جدول فرش‌ها" @near-end="loadMore">
      <template #footer>
        <div class="flex flex-wrap items-center gap-3 text-sm text-on-surface">
          <AppIcon name="rug" class="h-5 w-5 text-on-surface-variant" />
          <span class="min-w-0 flex-1 tabular-nums">{{ footer }}</span>
          <Button v-if="loadedCount < total" icon="chevron-down" :loading="loadingMore" @click="loadMore">
            ردیف‌های بیشتر
          </Button>
        </div>
      </template>
    </DataGrid>
  </div>
</template>
