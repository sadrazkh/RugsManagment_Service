<script setup lang="ts">
/**
 * ثبت فرش: یا قالب (با امکان رد مراحل اختیاری) یا مسیر سفارشی
 * POST /api/rugs
 */
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/api/client'
import type { ProcessStepType, WorkflowTemplate } from '@/types'

const router = useRouter()
const templates = ref<WorkflowTemplate[]>([])
const stepTypes = ref<ProcessStepType[]>([])
const mode = ref<'template' | 'custom'>('template')
const selectedTemplate = ref('')
const skippedSteps = ref<string[]>([])
const customStepIds = ref<string[]>([])
const loading = ref(false)

const form = ref({
  title: '',
  origin: '',
  pattern: '',
  material: '',
  knotDensity: undefined as number | undefined,
  widthMeters: 2,
  lengthMeters: 3,
  purchaseCost: undefined as number | undefined,
  targetSalePrice: undefined as number | undefined,
  notes: '',
})

onMounted(async () => {
  const [t, s] = await Promise.all([
    api.get<WorkflowTemplate[]>('/workflows/templates'),
    api.get<ProcessStepType[]>('/workflows/step-types'),
  ])
  templates.value = t.data
  stepTypes.value = s.data
  const def = t.data.find((x) => x.isDefault)
  if (def) selectedTemplate.value = def.id
})

function toggleSkip(stepId: string) {
  if (skippedSteps.value.includes(stepId)) {
    skippedSteps.value = skippedSteps.value.filter((id) => id !== stepId)
  } else {
    skippedSteps.value.push(stepId)
  }
}

function toggleCustomStep(id: string) {
  if (customStepIds.value.includes(id)) {
    customStepIds.value = customStepIds.value.filter((x) => x !== id)
  } else {
    customStepIds.value.push(id)
  }
}

const currentTemplate = () => templates.value.find((t) => t.id === selectedTemplate.value)

async function submit() {
  loading.value = true
  try {
    const payload: Record<string, unknown> = { ...form.value }
    if (mode.value === 'template' && selectedTemplate.value) {
      payload.workflowTemplateId = selectedTemplate.value
      payload.skippedOptionalStepIds = skippedSteps.value
    } else if (mode.value === 'custom') {
      payload.customSteps = customStepIds.value.map((id) => ({
        processStepTypeId: id,
        isOptional: false,
      }))
    }
    const { data } = await api.post('/rugs', payload)
    router.push(`/rugs/${data.id}`)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="max-w-2xl mx-auto space-y-6">
    <header>
      <h2 class="text-2xl font-bold">ثبت فرش جدید</h2>
      <p class="text-sm text-on-surface-variant">اطلاعات پایه و مسیر فرایند را تعیین کنید</p>
    </header>

    <form class="space-y-5" @submit.prevent="submit">
      <div class="grid sm:grid-cols-2 gap-4">
        <div class="sm:col-span-2">
          <label class="text-sm font-medium">عنوان</label>
          <input v-model="form.title" class="input-field" />
        </div>
        <div>
          <label class="text-sm font-medium">منشأ</label>
          <input v-model="form.origin" class="input-field" placeholder="مثلاً تبریز" />
        </div>
        <div>
          <label class="text-sm font-medium">طرح</label>
          <input v-model="form.pattern" class="input-field" />
        </div>
        <div>
          <label class="text-sm font-medium">عرض (متر)</label>
          <input v-model.number="form.widthMeters" type="number" step="0.01" required class="input-field" />
        </div>
        <div>
          <label class="text-sm font-medium">طول (متر)</label>
          <input v-model.number="form.lengthMeters" type="number" step="0.01" required class="input-field" />
        </div>
        <div>
          <label class="text-sm font-medium">قیمت خرید</label>
          <input v-model.number="form.purchaseCost" type="number" class="input-field" />
        </div>
        <div>
          <label class="text-sm font-medium">قیمت هدف فروش</label>
          <input v-model.number="form.targetSalePrice" type="number" class="input-field" />
        </div>
      </div>

      <fieldset class="border border-outline-variant rounded-xl p-4 space-y-3">
        <legend class="px-2 text-sm font-semibold">مسیر فرایند</legend>
        <div class="flex gap-2">
          <button
            type="button"
            class="flex-1 py-2 rounded-lg text-sm border"
            :class="mode === 'template' ? 'bg-secondary-container border-secondary' : 'border-outline-variant'"
            @click="mode = 'template'"
          >
            از قالب
          </button>
          <button
            type="button"
            class="flex-1 py-2 rounded-lg text-sm border"
            :class="mode === 'custom' ? 'bg-secondary-container border-secondary' : 'border-outline-variant'"
            @click="mode = 'custom'"
          >
            مسیر سفارشی
          </button>
        </div>

        <div v-if="mode === 'template'" class="space-y-3">
          <select v-model="selectedTemplate" class="input-field">
            <option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option>
          </select>
          <div v-if="currentTemplate()" class="space-y-2">
            <p class="text-xs text-on-surface-variant">مراحل اختیاری — در صورت نیاز رد کنید:</p>
            <label
              v-for="step in currentTemplate()!.steps.filter((s) => s.isOptional)"
              :key="step.id"
              class="flex items-center gap-2 text-sm p-2 rounded-lg bg-surface-container-low cursor-pointer"
            >
              <input type="checkbox" :checked="skippedSteps.includes(step.id)" @change="toggleSkip(step.id)" />
              رد کردن {{ step.stepNameFa }}
            </label>
          </div>
        </div>

        <div v-else class="grid grid-cols-2 gap-2">
          <label
            v-for="st in stepTypes"
            :key="st.id"
            class="flex items-center gap-2 p-2 rounded-lg border text-sm cursor-pointer"
            :class="customStepIds.includes(st.id) ? 'border-primary bg-primary/5' : 'border-outline-variant'"
          >
            <input type="checkbox" :checked="customStepIds.includes(st.id)" @change="toggleCustomStep(st.id)" />
            <span class="material-symbols-outlined text-base">{{ st.icon }}</span>
            {{ st.nameFa }}
          </label>
        </div>
      </fieldset>

      <button
        type="submit"
        :disabled="loading"
        class="w-full py-3 bg-primary text-on-primary rounded-full font-semibold disabled:opacity-60"
      >
        {{ loading ? 'در حال ثبت...' : 'ثبت فرش' }}
      </button>
    </form>
  </div>
</template>

<style scoped>
.input-field {
  width: 100%;
  margin-top: 0.25rem;
  padding: 0.625rem 0.75rem;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
}
.input-field:focus {
  outline: none;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 25%, transparent);
}
</style>
