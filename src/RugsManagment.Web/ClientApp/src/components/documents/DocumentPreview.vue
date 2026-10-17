<script setup lang="ts">
import { toPersianDigits } from '@/lib/format'
import type { RenderDocument } from '@/lib/types/documents'
defineProps<{ document: RenderDocument | null }>()
</script>
<template>
  <div v-if="document" class="space-y-4" role="region" aria-label="پیش‌نمایش سند">
    <p v-if="document.isDraft" class="rounded-lg bg-surface-container-high p-3 font-bold">پیش‌نویس</p>
    <p v-for="warning in document.warnings" :key="warning.code + warning.group" role="status">{{ warning.message }}</p>
    <section v-for="sheet in document.sheets" :key="sheet.name" :aria-label="sheet.name" tabindex="0" class="overflow-x-auto rounded-lg border border-outline-variant">
      <h2 class="p-3 font-bold">{{ sheet.name }}</h2>
      <table class="w-full border-collapse text-sm"><tbody>
        <tr v-for="row in sheet.rows" :key="row.rowId" :class="['footer', 'totals', 'title', 'grandTotal'].includes(row.kind) ? 'bg-surface-container-high font-bold' : ''">
          <td v-for="cell in row.cells" :key="cell.col" :colspan="cell.colSpan" :rowspan="cell.rowSpan" class="border border-outline-variant p-2 text-center" :title="cell.formula ?? undefined">{{ toPersianDigits(cell.display) }}</td>
        </tr>
      </tbody></table>
    </section>
    <p v-if="document.truncated" class="text-sm text-on-surface-variant">پیش‌نمایش کوتاه شده است؛ فایل اکسل و چاپ همهٔ ردیف‌ها را دارند.</p>
  </div>
</template>
