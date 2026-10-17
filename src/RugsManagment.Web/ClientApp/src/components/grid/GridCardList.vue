<script setup lang="ts">
/**
 * نمای کارتی جدول برای صفحهٔ کمتر از ۶۴۰ پیکسل (G.1): هر ردیف یک کارت و هر خانه یک برچسب و یک مقدار. همان کنترل‌گر
 * جدول را می‌راند، پس پرسش‌ها، خانه‌های چسبان، شمارهٔ پیشنهادی، برگرد و ذخیره همان است؛ فقط Enter به خانهٔ بعدیِ همان
 * کارت می‌رود (و ته کارت به کارت بعد). کارت‌ها چهل‌تا چهل‌تا رندر می‌شوند و کارت فعال همیشه در پنجره است.
 */
import { computed, ref, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { message } from '@/lib/messages'
import GridCell from './GridCell.vue'
import type { GridRowState } from './gridModel'
import type { GridController } from './useGridController'

const props = defineProps<{ ctrl: GridController; label: string }>()
const emit = defineEmits<{ menu: [row: GridRowState, event: MouseEvent] }>()

const PAGE = 40
const count = computed(() => props.ctrl.rows.length)
/** ردیف تازه پایین است؛ پس پیش‌فرض آخرین کارت‌ها دیده می‌شوند */
const start = ref(Math.max(0, count.value - PAGE))
const end = ref(count.value)

watch(count, (n, before) => {
  // ردیف تازه آمد: پنجره تا ته جدول کش بیاید
  if (end.value >= (before ?? 0)) end.value = n
  if (start.value > n) start.value = Math.max(0, n - PAGE)
})

watch(
  () => props.ctrl.nav.row,
  (row) => {
    if (row < start.value) start.value = Math.max(0, row - 5)
    if (row >= end.value) end.value = Math.min(count.value, row + 5)
  },
  { immediate: true },
)

const indices = computed(() => {
  const list: number[] = []
  for (let i = start.value; i < Math.min(end.value, count.value); i++) list.push(i)
  return list
})

/** خانه‌های بلند (یادداشت، جمع خرید، متراژ دسته) کل پهنای کارت را می‌گیرند. */
function wide(type: string, key: string): boolean {
  return type === 'text' || key === 'rug.purchaseTotal' || key === 'rug.areaM2' || type === 'money+unit'
}

function title(row: GridRowState): string {
  if (row.placeholder) return 'فرش تازه'
  return props.ctrl.serialLabel(row) || 'بی‌شماره'
}

function tone(row: GridRowState): string {
  if (row.status === 'conflict') return 'border-warning'
  if (row.status === 'invalid') return 'border-error'
  return 'border-outline-variant'
}
</script>

<template>
  <div :aria-label="label" role="list" class="flex flex-col gap-3">
    <button
      v-if="start > 0"
      type="button"
      class="min-h-11 rounded-lg border border-dashed border-outline-variant text-sm text-on-surface-variant"
      @click="start = Math.max(0, start - PAGE)"
    >
      ردیف‌های قبلی
    </button>

    <div
      v-for="i in indices"
      :key="ctrl.rows[i].id"
      role="listitem"
      class="rounded-xl border bg-surface-container-lowest p-3"
      :class="[tone(ctrl.rows[i]), ctrl.nav.row === i ? 'ring-2 ring-primary/40' : '', ctrl.rows[i].placeholder ? 'border-dashed' : '']"
    >
      <header class="mb-2 flex items-center gap-2">
        <h3 class="min-w-0 flex-1 truncate font-semibold text-on-surface">
          <bdi dir="ltr">{{ title(ctrl.rows[i]) }}</bdi>
        </h3>
        <span v-if="ctrl.rows[i].saving" class="text-xs text-on-surface-variant">{{ message('I_SAVING') }}</span>
        <span v-else-if="ctrl.rows[i].status === 'invalid'" class="flex items-center gap-1 text-xs text-error">
          <AppIcon name="error" class="h-4 w-4" />{{ ctrl.rows[i].rowError ?? message('E_VALIDATION') }}
        </span>
        <span v-else-if="ctrl.rows[i].status === 'conflict'" class="flex items-center gap-1 text-xs text-warning">
          <AppIcon name="warning" class="h-4 w-4" />{{ message('E_GRID_CONFLICT') }}
        </span>
        <span v-else-if="ctrl.rows[i].dirty.length > 0 && !ctrl.rows[i].placeholder" class="h-2 w-2 rounded-full bg-secondary" aria-hidden="true"></span>
        <button
          v-if="!ctrl.rows[i].placeholder"
          type="button"
          data-row-menu
          class="grid h-11 w-11 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
          :aria-label="`کارهای ردیف ${ctrl.serialLabel(ctrl.rows[i])}`"
          aria-haspopup="menu"
          @mousedown.prevent
          @click="emit('menu', ctrl.rows[i], $event)"
        >
          <AppIcon name="dots" class="h-5 w-5" />
        </button>
      </header>

      <div class="grid grid-cols-2 gap-x-3 gap-y-2">
        <div
          v-for="(column, c) in ctrl.columns"
          :key="column.key"
          class="flex min-w-0 flex-col gap-1"
          :class="wide(column.type, column.key) ? 'col-span-2' : ''"
        >
          <span class="text-xs text-on-surface-variant">
            {{ column.label }}<span v-if="column.required" class="text-error" aria-hidden="true"> *</span>
          </span>
          <GridCell :ctrl="ctrl" :row="ctrl.rows[i]" :row-index="i" :column="column" :col-index="c" variant="card" />
        </div>
      </div>
    </div>

    <button
      v-if="end < count"
      type="button"
      class="min-h-11 rounded-lg border border-dashed border-outline-variant text-sm text-on-surface-variant"
      @click="end = Math.min(count, end + PAGE)"
    >
      ردیف‌های بعدی
    </button>
  </div>
</template>
