<script setup lang="ts">
/**
 * رندر نهایی یک برچسب با دادهٔ واقعی یک فرش — مشترک بین پیش‌نمایش طراح و چاپ.
 * عناصر در یک شبکهٔ CSS با ستون‌بندی قابل‌تنظیم چیده می‌شوند.
 */
import { resolveField, STATIC_FIELDS, type LabelElement, type LabelLayout } from '@/lib/labelFields'
import type { Rug } from '@/lib/types'
import QrImage from '@/components/QrImage.vue'
import BarcodeImage from '@/components/BarcodeImage.vue'

const props = defineProps<{ layout: LabelLayout; rug: Rug }>()

function fieldLabel(key: string): string {
  return STATIC_FIELDS.find((f) => f.key === key)?.label ?? key
}
function align(e: LabelElement) { return e.align ?? 'right' }
function styleOf(e: LabelElement) {
  return { fontSize: (e.fontSize ?? 12) + 'px', fontWeight: e.bold ? 700 : 400, textAlign: align(e) }
}
function fill(text: string) {
  return text.replace(/\{\{\s*([\w]+)\s*\}\}/g, (_, k: string) => resolveField(props.rug, k))
}
</script>

<template>
  <div class="lbl-grid" :style="{ gridTemplateColumns: `repeat(${layout.columns || 1}, 1fr)` }">
    <div v-for="e in layout.elements" :key="e.id" :style="{ gridColumn: `span ${Math.min(e.colSpan || 1, layout.columns || 1)}` }">
      <template v-if="e.type === 'divider'"><hr class="lbl-hr" /></template>
      <h3 v-else-if="e.type === 'heading'" :style="styleOf(e)" class="lbl-h">{{ fill(e.text || '') }}</h3>
      <div v-else-if="e.type === 'text'" :style="styleOf(e)">{{ fill(e.text || '') }}</div>
      <div v-else-if="e.type === 'field'" :style="styleOf(e)">
        <span v-if="e.prefix" class="lbl-key">{{ fieldLabel(e.field || '') }}: </span>{{ resolveField(rug, e.field || '') }}
      </div>
      <div v-else-if="e.type === 'qr'" :style="{ textAlign: align(e) }"><QrImage :value="fill(e.source || '{{sku}}')" :size="80" /></div>
      <div v-else-if="e.type === 'barcode'" :style="{ textAlign: align(e) }"><BarcodeImage :value="fill(e.source || '{{sku}}')" /></div>
      <div v-else-if="e.type === 'image'" :style="{ textAlign: align(e) }"><img v-if="e.src" :src="e.src" alt="logo" style="max-height:48px;max-width:100%" /></div>
      <table v-else-if="e.type === 'table'" class="lbl-table">
        <tbody>
          <tr v-for="k in (e.fields || [])" :key="k"><td class="lbl-key">{{ fieldLabel(k) }}</td><td>{{ resolveField(rug, k) }}</td></tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.lbl-grid { display: grid; gap: 4px 8px; width: 100%; direction: rtl; color: #111; }
.lbl-hr { border: 0; border-top: 1px solid #6b0008; margin: 2px 0; }
.lbl-h { margin: 0; color: #6b0008; }
.lbl-key { color: #555; }
.lbl-table { width: 100%; border-collapse: collapse; font-size: 11px; }
.lbl-table td { border: 1px solid #ddd; padding: 2px 4px; }
</style>
