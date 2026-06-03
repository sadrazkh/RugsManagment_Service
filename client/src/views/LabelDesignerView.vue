<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { VueDraggable } from 'vue-draggable-plus'
import { DEFAULT_LABEL_BLOCKS, LABEL_FIELDS, type LabelBlock } from '@/utils/labelRender'

const blocks = ref<LabelBlock[]>([...DEFAULT_LABEL_BLOCKS])
const name = ref('برچسب پیش‌فرض')
const saving = ref(false)

function addDivider() {
  blocks.value = [...blocks.value, { id: crypto.randomUUID(), kind: 'divider' }]
}

function addRow(cols: number) {
  const children: LabelBlock[] = Array.from({ length: cols }, (_, i) => ({
    id: crypto.randomUUID(),
    kind: 'field' as const,
    field: LABEL_FIELDS[i % LABEL_FIELDS.length]!.key,
    fontSize: 10,
  }))
  blocks.value = [...blocks.value, { id: crypto.randomUUID(), kind: 'row', columns: cols, children }]
}

function addField(key: string) {
  blocks.value = [...blocks.value, { id: crypto.randomUUID(), kind: 'field', field: key, fontSize: 11 }]
}

function removeBlock(id: string) {
  blocks.value = blocks.value.filter((b) => b.id !== id)
}

const STORAGE_KEY = 'rug_label_template'

function save() {
  saving.value = true
  localStorage.setItem(STORAGE_KEY, JSON.stringify({ name: name.value, blocks: blocks.value }))
  saving.value = false
  alert('قالب برچسب ذخیره شد (مرورگر)')
}

onMounted(() => {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (raw) {
    try {
      const data = JSON.parse(raw) as { name: string; blocks: LabelBlock[] }
      name.value = data.name
      blocks.value = data.blocks
    } catch { /* default */ }
  }
})
</script>

<template>
  <div class="space-y-4 max-w-3xl">
    <header>
      <h2 class="text-2xl font-bold">طراح برچسب</h2>
      <p class="text-sm text-on-surface-variant">دیوایدر، چیدمان ۲ تا ۵ ستونه، فیلدهای داینامیک</p>
    </header>

    <input v-model="name" class="w-full px-3 py-2 border rounded-lg" placeholder="نام قالب" />

    <div class="flex flex-wrap gap-2">
      <button type="button" class="text-xs px-3 py-1.5 border rounded-full" @click="addDivider">+ خط جدا</button>
      <button v-for="n in [2,3,4,5]" :key="n" type="button" class="text-xs px-3 py-1.5 border rounded-full" @click="addRow(n)">+ {{ n }} ستون</button>
      <button v-for="f in LABEL_FIELDS" :key="f.key" type="button" class="text-xs px-2 py-1 border rounded-full" @click="addField(f.key)">{{ f.label }}</button>
    </div>

    <div class="border-2 border-primary/30 rounded-xl p-4 bg-white min-h-[120px] space-y-2">
      <template v-for="b in blocks" :key="b.id">
        <hr v-if="b.kind === 'divider'" class="border-primary/40" />
        <div v-else-if="b.kind === 'row'" class="grid gap-1" :style="{ gridTemplateColumns: `repeat(${b.columns ?? 2}, 1fr)` }">
          <div v-for="c in b.children" :key="c.id" class="text-[10px] border border-dashed p-1 rounded">{{ c.field || c.text }}</div>
        </div>
        <p v-else class="text-xs border border-dashed p-1 rounded inline-block">{{ b.field || b.text }}</p>
      </template>
    </div>

    <VueDraggable v-model="blocks" item-key="id" handle=".drag" class="space-y-1">
      <div v-for="b in blocks" :key="b.id" class="flex items-center gap-2 text-sm p-2 border rounded-lg bg-surface-container-lowest">
        <span class="drag material-symbols-outlined cursor-grab text-sm">drag_indicator</span>
        <span class="flex-1">{{ b.kind === 'row' ? `ردیف ${b.columns} ستونه` : b.kind === 'divider' ? 'خط جدا' : (b.field || b.text) }}</span>
        <button type="button" class="text-error text-xs" @click="removeBlock(b.id)">حذف</button>
      </div>
    </VueDraggable>

    <button type="button" class="w-full py-3 bg-primary text-on-primary rounded-full font-medium" :disabled="saving" @click="save">ذخیره قالب</button>
  </div>
</template>
