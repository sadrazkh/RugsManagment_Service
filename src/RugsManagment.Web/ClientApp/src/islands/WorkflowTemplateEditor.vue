<script setup lang="ts">
/** ویرایشگر قالب گردش کار با کشیدن‌ورهاکردن (drag & drop) مراحل. */
import { onMounted, reactive, ref } from 'vue'
import { VueDraggable } from 'vue-draggable-plus'
import { api } from '@/lib/api'
import type { WorkflowTemplate } from '@/lib/types'

const props = defineProps<{ templateId?: string }>()

interface StepType { id: string; nameFa: string; icon: string }
interface Row { uid: number; processStepTypeId: string; stepNameFa: string; isOptional: boolean }

const stepTypes = ref<StepType[]>([])
const steps = ref<Row[]>([])
const meta = reactive({ name: '', description: '', isDefault: false })
const loading = ref(true)
const saving = ref(false)
const error = ref('')
let counter = 0

function addStep(t: StepType) {
  steps.value.push({ uid: ++counter, processStepTypeId: t.id, stepNameFa: t.nameFa, isOptional: false })
}
function removeStep(uid: number) {
  steps.value = steps.value.filter((s) => s.uid !== uid)
}

onMounted(async () => {
  try {
    stepTypes.value = await api.get<StepType[]>('/api/lookups/step-types')
    if (props.templateId) {
      const all = await api.get<WorkflowTemplate[]>('/api/workflows/templates')
      const t = all.find((x) => x.id === props.templateId)
      if (t) {
        meta.name = t.name
        meta.description = t.description ?? ''
        meta.isDefault = t.isDefault
        steps.value = t.steps.map((s) => ({
          uid: ++counter, processStepTypeId: s.processStepTypeId,
          stepNameFa: s.stepNameFa, isOptional: s.isOptional,
        }))
      }
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
})

async function save() {
  error.value = ''
  if (!meta.name.trim()) { error.value = 'نام قالب الزامی است.'; return }
  if (steps.value.length === 0) { error.value = 'حداقل یک مرحله اضافه کنید.'; return }
  saving.value = true
  try {
    const payloadSteps = steps.value.map((s, i) => ({
      processStepTypeId: s.processStepTypeId, orderIndex: i,
      isOptional: s.isOptional, defaultServiceProviderId: null,
      overridePricingModel: null, overrideUnitRate: null,
    }))
    if (props.templateId) {
      await api.put(`/api/workflows/templates/${props.templateId}`, {
        name: meta.name, description: meta.description || null,
        isDefault: meta.isDefault, isActive: true, steps: payloadSteps,
      })
    } else {
      await api.post('/api/workflows/templates', {
        name: meta.name, description: meta.description || null,
        isDefault: meta.isDefault, steps: payloadSteps,
      })
    }
    window.location.href = '/Workflows'
  } catch (e) {
    error.value = (e as Error).message
    saving.value = false
  }
}
</script>

<template>
  <div v-if="loading" class="rounded-xl border border-outline-variant bg-white p-8 text-center text-on-surface-variant">در حال بارگذاری…</div>
  <div v-else class="space-y-5">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>

    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <div class="grid gap-4 sm:grid-cols-2">
        <label class="block">
          <span class="mb-1 block text-sm">نام قالب</span>
          <input v-model="meta.name" class="fld" placeholder="مسیر کامل" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">توضیح</span>
          <input v-model="meta.description" class="fld" />
        </label>
      </div>
      <label class="mt-3 flex items-center gap-2 text-sm">
        <input v-model="meta.isDefault" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
        قالب پیش‌فرض هنگام ثبت فرش
      </label>
    </section>

    <div class="grid gap-5 md:grid-cols-3">
      <!-- پالت انواع مرحله -->
      <section class="rounded-xl border border-outline-variant bg-white p-4 shadow-sm">
        <h3 class="mb-3 text-sm font-semibold text-primary">افزودن مرحله</h3>
        <div class="flex flex-wrap gap-2">
          <button v-for="t in stepTypes" :key="t.id" type="button" @click="addStep(t)"
                  class="rounded-lg border border-outline-variant px-3 py-1.5 text-sm hover:bg-surface-container">
            + {{ t.nameFa }}
          </button>
        </div>
      </section>

      <!-- مراحل قابل مرتب‌سازی -->
      <section class="rounded-xl border border-outline-variant bg-white p-4 shadow-sm md:col-span-2">
        <h3 class="mb-3 text-sm font-semibold text-primary">ترتیب مراحل (بکشید و جابه‌جا کنید)</h3>
        <p v-if="steps.length === 0" class="py-6 text-center text-sm text-on-surface-variant">
          از پالت کنار، مرحله اضافه کنید.
        </p>
        <VueDraggable v-model="steps" :animation="150" handle=".drag-handle" class="space-y-2">
          <div v-for="(s, i) in steps" :key="s.uid"
               class="flex items-center gap-3 rounded-lg border border-outline-variant bg-surface-container/40 px-3 py-2.5">
            <span class="drag-handle cursor-grab select-none text-on-surface-variant">⠿</span>
            <span class="grid h-7 w-7 place-items-center rounded-full bg-primary/10 text-sm text-primary">{{ i + 1 }}</span>
            <span class="flex-1 font-medium">{{ s.stepNameFa }}</span>
            <label class="flex items-center gap-1 text-xs text-on-surface-variant">
              <input v-model="s.isOptional" type="checkbox" class="h-3.5 w-3.5 rounded border-outline-variant text-primary" />
              اختیاری
            </label>
            <button type="button" @click="removeStep(s.uid)" class="text-error hover:opacity-70">✕</button>
          </div>
        </VueDraggable>
      </section>
    </div>

    <div class="sticky bottom-0 -mx-4 flex gap-3 border-t border-outline-variant bg-surface/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0 md:py-0">
      <a href="/Workflows" class="flex-1 rounded-lg border border-outline-variant px-4 py-3 text-center hover:bg-surface-container md:flex-none">انصراف</a>
      <button :disabled="saving" @click="save"
              class="flex-1 rounded-lg bg-primary px-6 py-3 font-semibold text-on-primary hover:opacity-90 disabled:opacity-60 md:flex-none">
        {{ saving ? 'در حال ذخیره…' : 'ذخیرهٔ قالب' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.fld { width: 100%; border-radius: 0.5rem; border: 1px solid var(--color-outline-variant); padding: 0.625rem 0.75rem; outline: none; }
.fld:focus { border-color: var(--color-primary); box-shadow: 0 0 0 1px var(--color-primary); }
</style>
