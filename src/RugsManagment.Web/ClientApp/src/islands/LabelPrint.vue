<script setup lang="ts">
/**
 * صفحهٔ چاپ برچسب برای یک یا چند فرش.
 *
 * برخلاف دکمهٔ چاپِ داخل طراح (که پنجرهٔ موقت باز می‌کرد)، اینجا خودِ صفحه چاپ می‌شود:
 * آدرس قابل اشتراک است، پیش‌نمایش دقیقاً همان چیزی است که چاپ می‌شود، و مرورگر
 * برچسب‌ها را طبق @page به‌درستی بین صفحه‌ها می‌شکند.
 */
import { computed, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import LabelRender from '@/components/LabelRender.vue'
import { api } from '@/lib/api'
import { faNumber } from '@/lib/format'
import { fillPlaceholders, type LabelLayout } from '@/lib/labelFields'
import { toast } from '@/lib/ui'
import type { Rug } from '@/lib/types'

interface Template {
  id: string
  name: string
  widthMm: number
  heightMm: number
  mode: number
  elementsJson?: string
  htmlContent?: string
}

const props = defineProps<{
  rugIds: string[]
  templateId: string
  templates: Template[]
}>()

const rugs = ref<Rug[]>([])
const loading = ref(true)
const selectedId = ref(props.templateId)
/** چند نسخه از هر برچسب چاپ شود (مثلاً یکی برای فرش، یکی برای پرونده). */
const copies = ref(1)

const template = computed(
  () => props.templates.find((t) => t.id === selectedId.value) ?? props.templates[0],
)

const layout = computed<LabelLayout>(() => {
  const raw = template.value?.elementsJson
  if (!raw) return { columns: 2, elements: [] }
  try {
    return JSON.parse(raw) as LabelLayout
  } catch {
    return { columns: 2, elements: [] }
  }
})

/** هر فرش × تعداد نسخه — لیست نهایی برچسب‌هایی که چاپ می‌شوند. */
const printItems = computed(() =>
  rugs.value.flatMap((rug) =>
    Array.from({ length: copies.value }, (_, copy) => ({ rug, key: `${rug.id}-${copy}` })),
  ),
)

/** اندازهٔ کاغذ برچسب به میلی‌متر — به @page تزریق می‌شود. */
const pageStyle = computed(() => {
  const t = template.value
  return `@page { size: ${t?.widthMm ?? 90}mm ${t?.heightMm ?? 50}mm; margin: 3mm; }`
})

async function load() {
  loading.value = true
  try {
    // هر فرش جدا گرفته می‌شود تا دادهٔ کامل (مراحل و متادیتا) برای فیلدهای برچسب موجود باشد
    const loaded = await Promise.all(
      props.rugIds.map((id) => api.get<Rug>(`/api/rugs/${id}`).catch(() => null)),
    )
    rugs.value = loaded.filter((r): r is Rug => r !== null)

    const missing = props.rugIds.length - rugs.value.length
    if (missing > 0) toast.warning(`${faNumber(missing)} فرش پیدا نشد و در چاپ نمی‌آید.`)
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

function doPrint() {
  window.print()
}

onMounted(load)
</script>

<template>
  <!-- اندازهٔ کاغذ برچسب باید در سطح سند اعمال شود -->
  <component :is="'style'">{{ pageStyle }}</component>

  <div class="space-y-4">
    <!-- نوار تنظیمات — در چاپ حذف می‌شود -->
    <div
      v-if="!loading"
      class="flex flex-wrap items-end gap-3 rounded-xl border border-outline-variant bg-surface-container-lowest p-4 shadow-sm"
      data-no-print
    >
      <label class="block">
        <span class="mb-1 block text-sm">قالب برچسب</span>
        <select
          v-model="selectedId"
          class="min-h-11 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary"
        >
          <option v-for="t in templates" :key="t.id" :value="t.id">
            {{ t.name }} ({{ faNumber(t.widthMm) }}×{{ faNumber(t.heightMm) }} م‌م)
          </option>
        </select>
      </label>

      <label class="block">
        <span class="mb-1 block text-sm">تعداد نسخه از هر فرش</span>
        <input
          v-model.number="copies"
          type="number"
          min="1"
          max="10"
          dir="ltr"
          class="min-h-11 w-24 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary"
        />
      </label>

      <div class="flex-1"></div>

      <p class="text-sm text-on-surface-variant" data-numeric>
        {{ faNumber(printItems.length) }} برچسب از {{ faNumber(rugs.length) }} فرش
      </p>

      <button
        type="button"
        class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
        @click="doPrint"
      >
        <AppIcon name="printer" class="h-5 w-5" />
        چاپ
      </button>
    </div>

    <div v-if="loading" class="skeleton h-64 w-full" aria-hidden="true"></div>

    <div
      v-else-if="rugs.length === 0"
      class="rounded-xl border border-dashed border-outline-variant p-10 text-center text-on-surface-variant"
      data-no-print
    >
      فرشی برای چاپ پیدا نشد.
    </div>

    <!-- برچسب‌ها: روی صفحه شبکه، در چاپ هر کدام یک صفحه -->
    <div v-else class="print-sheet grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <div
        v-for="item in printItems"
        :key="item.key"
        class="label-card overflow-hidden rounded-lg border border-outline-variant bg-white p-2"
        :style="{ width: template.widthMm + 'mm', minHeight: template.heightMm + 'mm' }"
      >
        <div
          v-if="template.mode === 1"
          v-html="fillPlaceholders(item.rug, template.htmlContent || '')"
        ></div>
        <LabelRender v-else :layout="layout" :rug="item.rug" />
      </div>
    </div>
  </div>
</template>

<style scoped>
/* روی صفحه، برچسب‌ها کنار هم دیده می‌شوند تا کاربر پیش‌نمایش بگیرد. */
.label-card {
  color: #111;
  box-sizing: border-box;
}

@media print {
  /* در چاپ هر برچسب یک صفحهٔ مستقل است تا روی کاغذ برچسب درست بنشیند */
  .print-sheet {
    display: block;
    gap: 0;
  }

  .label-card {
    border: 0;
    padding: 0;
    margin: 0;
    break-after: page;
    page-break-after: always;
  }

  .label-card:last-child {
    break-after: auto;
    page-break-after: auto;
  }
}
</style>
