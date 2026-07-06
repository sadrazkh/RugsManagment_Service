<script setup lang="ts">
/**
 * طراح برچسب — سبک page-builder:
 *  • حالت بصری: شبکهٔ چند‌ستونه + عناصر قابل کشیدن (متن، عنوان، فیلد، QR، بارکد، لوگو، جداکننده، جدول)
 *  • حالت HTML پیشرفته: چسباندن HTML با placeholder مثل {{sku}}
 *  • پیش‌نمایش با دادهٔ واقعی یک فرش نمونه، ذخیرهٔ per-tenant، و چاپ
 */
import { computed, nextTick, onMounted, ref } from 'vue'
import { VueDraggable } from 'vue-draggable-plus'
import { api } from '@/lib/api'
import { STATIC_FIELDS, fillPlaceholders, type ElementType, type LabelElement, type LabelLayout } from '@/lib/labelFields'
import type { Rug } from '@/lib/types'
import LabelRender from '@/components/LabelRender.vue'

const props = defineProps<{ templateId?: string }>()

const meta = ref({ name: 'برچسب جدید', widthMm: 90, heightMm: 50, mode: 0 as 0 | 1 })
const layout = ref<LabelLayout>({ columns: 2, elements: [] })
const html = ref('<div style="text-align:center">\n  <h3>{{title}}</h3>\n  <p>{{sku}} — {{dimensions}}</p>\n</div>')
const selectedId = ref<string | null>(null)
const rugs = ref<Rug[]>([])
const sampleRugId = ref('')
const customFields = ref<{ key: string; label: string }[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const previewRef = ref<HTMLElement | null>(null)
let counter = 0

/** ساخت رشتهٔ placeholder بدون نوشتن {{ }} در متن template (که کامپایلر Vue را گیج می‌کند). */
const ph = (k: string) => '{{' + k + '}}'

const allFields = computed(() => [...STATIC_FIELDS, ...customFields.value])
const selected = computed(() => layout.value.elements.find((e) => e.id === selectedId.value) ?? null)
const sampleRug = computed(() => rugs.value.find((r) => r.id === sampleRugId.value) ?? rugs.value[0] ?? null)

const palette: { type: ElementType; label: string }[] = [
  { type: 'heading', label: 'عنوان' }, { type: 'text', label: 'متن' }, { type: 'field', label: 'فیلد' },
  { type: 'qr', label: 'QR' }, { type: 'barcode', label: 'بارکد' }, { type: 'image', label: 'لوگو' },
  { type: 'divider', label: 'جداکننده' }, { type: 'table', label: 'جدول' },
]

function add(type: ElementType) {
  const cols = layout.value.columns
  const base: LabelElement = { id: 'e' + ++counter, type, colSpan: 1, fontSize: 12, align: 'right' }
  if (type === 'heading') Object.assign(base, { text: 'عنوان برچسب', fontSize: 16, bold: true, align: 'center', colSpan: cols })
  if (type === 'text') Object.assign(base, { text: 'متن' })
  if (type === 'field') Object.assign(base, { field: 'sku', prefix: true })
  if (type === 'divider') Object.assign(base, { colSpan: cols })
  if (type === 'qr') Object.assign(base, { source: '{{sku}}', align: 'center' })
  if (type === 'barcode') Object.assign(base, { source: '{{sku}}', colSpan: cols, align: 'center' })
  if (type === 'table') Object.assign(base, { fields: ['sku', 'dimensions', 'pattern'], colSpan: cols })
  layout.value.elements.push(base)
  selectedId.value = base.id
}
function remove(id: string) {
  layout.value.elements = layout.value.elements.filter((e) => e.id !== id)
  if (selectedId.value === id) selectedId.value = null
}
function descriptor(e: LabelElement): string {
  switch (e.type) {
    case 'field': return 'فیلد: ' + (allFields.value.find((f) => f.key === e.field)?.label ?? e.field)
    case 'heading': case 'text': return (e.text || '').slice(0, 20)
    case 'qr': return 'QR ' + e.source
    case 'barcode': return 'بارکد ' + e.source
    case 'image': return 'لوگو'
    case 'divider': return '──────'
    case 'table': return 'جدول (' + (e.fields?.length ?? 0) + ')'
    default: return ''
  }
}

async function load() {
  try {
    const [rugList, cf] = await Promise.all([
      api.get<Rug[]>('/api/rugs'),
      api.get<{ key: string; label: string }[]>('/api/lookups/custom-fields'),
    ])
    rugs.value = rugList
    sampleRugId.value = rugList[0]?.id ?? ''
    customFields.value = cf.map((f) => ({ key: f.key, label: f.label }))
    if (props.templateId) {
      const t = await api.get<{ name: string; widthMm: number; heightMm: number; mode: number; elementsJson?: string; htmlContent?: string }>(`/api/labels/${props.templateId}`)
      meta.value = { name: t.name, widthMm: t.widthMm, heightMm: t.heightMm, mode: (t.mode as 0 | 1) }
      if (t.elementsJson) { try { layout.value = JSON.parse(t.elementsJson) } catch { /* */ } }
      if (t.htmlContent) html.value = t.htmlContent
    } else {
      layout.value.elements = [
        { id: 'e' + ++counter, type: 'heading', colSpan: 2, text: '{{title}}', fontSize: 16, bold: true, align: 'center' },
        { id: 'e' + ++counter, type: 'divider', colSpan: 2 },
        { id: 'e' + ++counter, type: 'field', colSpan: 1, field: 'sku', prefix: true, fontSize: 12, align: 'right' },
        { id: 'e' + ++counter, type: 'field', colSpan: 1, field: 'dimensions', prefix: true, fontSize: 12, align: 'right' },
        { id: 'e' + ++counter, type: 'qr', colSpan: 2, source: '{{sku}}', align: 'center' },
      ]
    }
  } catch (e) { error.value = (e as Error).message }
  finally { loading.value = false }
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    const payload = {
      name: meta.value.name, widthMm: meta.value.widthMm, heightMm: meta.value.heightMm,
      mode: meta.value.mode, elementsJson: JSON.stringify(layout.value), htmlContent: html.value,
    }
    if (props.templateId) await api.put(`/api/labels/${props.templateId}`, payload)
    else await api.post('/api/labels', payload)
    window.location.href = '/Labels'
  } catch (e) { error.value = (e as Error).message; saving.value = false }
}

async function print() {
  await nextTick()
  const inner = meta.value.mode === 1
    ? (sampleRug.value ? fillPlaceholders(sampleRug.value, html.value) : html.value)
    : previewRef.value?.innerHTML ?? ''
  const w = window.open('', '_blank', 'width=480,height=360')
  if (!w) return
  w.document.write(`<!doctype html><html dir="rtl"><head><meta charset="utf-8">
    <style>
      @page { size: ${meta.value.widthMm}mm ${meta.value.heightMm}mm; margin: 3mm; }
      body { font-family: Tahoma, sans-serif; margin: 0; }
      .lbl-grid { display: grid; gap: 4px 8px; direction: rtl; }
      .lbl-hr { border: 0; border-top: 1px solid #6b0008; margin: 2px 0; }
      .lbl-h { margin: 0; color: #6b0008; }
      .lbl-key { color: #555; }
      .lbl-table { width: 100%; border-collapse: collapse; font-size: 11px; }
      .lbl-table td { border: 1px solid #ddd; padding: 2px 4px; }
    </style></head><body>${inner}</body></html>`)
  w.document.close()
  w.focus()
  setTimeout(() => { w.print(); }, 300)
}
onMounted(load)
</script>

<template>
  <div v-if="loading" class="rounded-xl border border-outline-variant bg-white p-8 text-center text-on-surface-variant">در حال بارگذاری…</div>
  <div v-else class="space-y-4">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>

    <!-- نوار تنظیمات -->
    <div class="flex flex-wrap items-end gap-3 rounded-xl border border-outline-variant bg-white p-4 shadow-sm">
      <label class="block"><span class="mb-1 block text-xs">نام</span><input v-model="meta.name" class="fld w-40" /></label>
      <label class="block"><span class="mb-1 block text-xs">عرض (mm)</span><input v-model.number="meta.widthMm" type="number" class="fld w-24" dir="ltr" /></label>
      <label class="block"><span class="mb-1 block text-xs">ارتفاع (mm)</span><input v-model.number="meta.heightMm" type="number" class="fld w-24" dir="ltr" /></label>
      <label v-if="meta.mode === 0" class="block"><span class="mb-1 block text-xs">ستون‌ها</span>
        <select v-model.number="layout.columns" class="fld w-20"><option v-for="n in 5" :key="n" :value="n">{{ n }}</option></select>
      </label>
      <div class="flex rounded-lg border border-outline-variant p-0.5 text-sm">
        <button :class="meta.mode === 0 ? 'bg-primary text-on-primary' : ''" class="rounded px-3 py-1" @click="meta.mode = 0">بصری</button>
        <button :class="meta.mode === 1 ? 'bg-primary text-on-primary' : ''" class="rounded px-3 py-1" @click="meta.mode = 1">HTML</button>
      </div>
      <div class="flex-1"></div>
      <button @click="print" class="rounded-lg border border-outline-variant px-4 py-2 text-sm hover:bg-surface-container">چاپ</button>
      <button :disabled="saving" @click="save" class="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-on-primary disabled:opacity-60">ذخیره</button>
    </div>

    <div class="grid gap-4 lg:grid-cols-3">
      <!-- طراحی -->
      <div class="space-y-4 lg:col-span-2">
        <template v-if="meta.mode === 0">
          <!-- پالت -->
          <div class="flex flex-wrap gap-2 rounded-xl border border-outline-variant bg-white p-3 shadow-sm">
            <button v-for="p in palette" :key="p.type" @click="add(p.type)"
                    class="rounded-lg border border-outline-variant px-3 py-1.5 text-sm hover:bg-surface-container">+ {{ p.label }}</button>
          </div>
          <!-- شبکهٔ عناصر (قابل کشیدن) -->
          <div class="rounded-xl border border-outline-variant bg-white p-3 shadow-sm">
            <p v-if="layout.elements.length === 0" class="py-6 text-center text-sm text-on-surface-variant">از پالت بالا عنصر اضافه کنید.</p>
            <VueDraggable v-model="layout.elements" :animation="150" handle=".dh"
                          class="grid gap-2" :style="{ gridTemplateColumns: `repeat(${layout.columns}, 1fr)` }">
              <div v-for="e in layout.elements" :key="e.id" @click="selectedId = e.id"
                   :style="{ gridColumn: `span ${Math.min(e.colSpan, layout.columns)}` }"
                   class="cursor-pointer rounded-lg border px-2 py-1.5 text-xs"
                   :class="selectedId === e.id ? 'border-primary bg-primary/5' : 'border-outline-variant bg-surface-container/40'">
                <div class="flex items-center justify-between gap-1">
                  <span class="dh cursor-grab text-on-surface-variant">⠿</span>
                  <span class="flex-1 truncate">{{ descriptor(e) }}</span>
                  <button @click.stop="remove(e.id)" class="text-error">✕</button>
                </div>
              </div>
            </VueDraggable>
          </div>
          <!-- ویژگی‌های عنصر انتخاب‌شده -->
          <div v-if="selected" class="space-y-3 rounded-xl border border-outline-variant bg-white p-4 shadow-sm">
            <h3 class="text-sm font-semibold text-primary">ویژگی‌ها</h3>
            <div class="grid grid-cols-2 gap-3">
              <label v-if="['heading','text'].includes(selected.type)" class="col-span-2 block"><span class="mb-1 block text-xs">متن (مثال placeholder: {{ ph('field') }})</span><input v-model="selected.text" class="fld" /></label>
              <label v-if="selected.type === 'field'" class="block"><span class="mb-1 block text-xs">فیلد</span>
                <select v-model="selected.field" class="fld"><option v-for="f in allFields" :key="f.key" :value="f.key">{{ f.label }}</option></select>
              </label>
              <label v-if="selected.type === 'field'" class="flex items-center gap-2 pt-6 text-sm"><input v-model="selected.prefix" type="checkbox" class="h-4 w-4" /> نمایش برچسب</label>
              <label v-if="['qr','barcode'].includes(selected.type)" class="col-span-2 block"><span class="mb-1 block text-xs">مقدار (placeholder مجاز)</span><input v-model="selected.source" dir="ltr" class="fld" /></label>
              <label v-if="selected.type === 'image'" class="col-span-2 block"><span class="mb-1 block text-xs">آدرس تصویر لوگو</span><input v-model="selected.src" dir="ltr" class="fld" placeholder="https://…" /></label>
              <label class="block"><span class="mb-1 block text-xs">پهنا (ستون)</span>
                <select v-model.number="selected.colSpan" class="fld"><option v-for="n in layout.columns" :key="n" :value="n">{{ n }}</option></select>
              </label>
              <label v-if="!['divider','image','qr','barcode','table'].includes(selected.type)" class="block"><span class="mb-1 block text-xs">اندازهٔ فونت</span><input v-model.number="selected.fontSize" type="number" class="fld" dir="ltr" /></label>
              <label v-if="!['divider','qr','barcode','image'].includes(selected.type)" class="block"><span class="mb-1 block text-xs">چینش</span>
                <select v-model="selected.align" class="fld"><option value="right">راست</option><option value="center">وسط</option><option value="left">چپ</option></select>
              </label>
              <label v-if="!['divider','qr','barcode','image','table'].includes(selected.type)" class="flex items-center gap-2 pt-6 text-sm"><input v-model="selected.bold" type="checkbox" class="h-4 w-4" /> ضخیم</label>
            </div>
          </div>
          <!-- فیلدهای در دسترس -->
          <div class="rounded-xl border border-outline-variant bg-white p-3 text-xs text-on-surface-variant shadow-sm">
            placeholderها: <code v-for="f in allFields" :key="f.key" class="mx-1" dir="ltr">{{ ph(f.key) }}</code>
          </div>
        </template>

        <!-- حالت HTML -->
        <template v-else>
          <textarea v-model="html" dir="ltr" spellcheck="false" rows="16"
                    class="w-full rounded-xl border border-outline-variant bg-white p-3 font-mono text-sm shadow-sm outline-none focus:border-primary"></textarea>
          <p class="text-xs text-on-surface-variant">از placeholderهایی مثل <code dir="ltr">{{ ph('sku') }}</code> استفاده کنید.</p>
        </template>
      </div>

      <!-- پیش‌نمایش زنده -->
      <div class="space-y-3">
        <div class="flex items-center justify-between">
          <h3 class="text-sm font-semibold text-primary">پیش‌نمایش</h3>
          <select v-model="sampleRugId" class="rounded-lg border border-outline-variant px-2 py-1 text-xs">
            <option v-for="r in rugs" :key="r.id" :value="r.id">{{ r.title || r.sku }}</option>
          </select>
        </div>
        <div class="overflow-auto rounded-xl border border-outline-variant bg-surface-container p-4">
          <div class="mx-auto bg-white shadow" :style="{ width: meta.widthMm + 'mm', minHeight: meta.heightMm + 'mm', padding: '3mm' }">
            <div v-if="!sampleRug" class="py-8 text-center text-sm text-on-surface-variant">برای پیش‌نمایش، ابتدا یک فرش ثبت کنید.</div>
            <div v-else-if="meta.mode === 1" ref="previewRef" v-html="fillPlaceholders(sampleRug, html)"></div>
            <div v-else ref="previewRef"><LabelRender :layout="layout" :rug="sampleRug" /></div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.fld { width: 100%; border-radius: 0.5rem; border: 1px solid var(--color-outline-variant); padding: 0.4rem 0.6rem; outline: none; }
.fld:focus { border-color: var(--color-primary); box-shadow: 0 0 0 1px var(--color-primary); }
</style>
