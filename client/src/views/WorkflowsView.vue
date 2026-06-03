<script setup lang="ts">
/** مدیریت قالب مسیر، انواع مرحله (فقط خواندن)، خدمات‌دهندگان */
import { onMounted, ref } from 'vue'
import api from '@/api/client'
import type { ProcessStepType, ServiceProvider, WorkflowTemplate } from '@/types'

const templates = ref<WorkflowTemplate[]>([])
const stepTypes = ref<ProcessStepType[]>([])
const providers = ref<ServiceProvider[]>([])
const tab = ref<'templates' | 'steps' | 'providers'>('templates')

const newTemplate = ref({ name: '', description: '', isDefault: false })
const newProvider = ref({ name: '', specialty: '', phone: '' })
const selectedSteps = ref<{ processStepTypeId: string; orderIndex: number; isOptional: boolean }[]>([])

onMounted(load)

async function load() {
  const [t, s, p] = await Promise.all([
    api.get<WorkflowTemplate[]>('/workflows/templates'),
    api.get<ProcessStepType[]>('/workflows/step-types'),
    api.get<ServiceProvider[]>('/workflows/providers'),
  ])
  templates.value = t.data
  stepTypes.value = s.data
  providers.value = p.data
}

function addStepToBuilder(typeId: string) {
  selectedSteps.value.push({
    processStepTypeId: typeId,
    orderIndex: selectedSteps.value.length,
    isOptional: false,
  })
}

async function createTemplate() {
  await api.post('/workflows/templates', {
    ...newTemplate.value,
    steps: selectedSteps.value,
  })
  newTemplate.value = { name: '', description: '', isDefault: false }
  selectedSteps.value = []
  await load()
}

async function createProvider() {
  await api.post('/workflows/providers', newProvider.value)
  newProvider.value = { name: '', specialty: '', phone: '' }
  await load()
}
</script>

<template>
  <div class="space-y-5">
    <header>
      <h2 class="text-2xl font-bold">مدیریت فرایندها</h2>
      <p class="text-sm text-on-surface-variant">قالب‌های مسیر، انواع مرحله و ارائه‌دهندگان خدمات</p>
    </header>

    <div class="flex gap-2 border-b border-outline-variant overflow-x-auto">
      <button
        v-for="t in [
          { id: 'templates', label: 'قالب‌ها' },
          { id: 'steps', label: 'انواع مرحله' },
          { id: 'providers', label: 'خدمات‌دهندگان' },
        ]"
        :key="t.id"
        type="button"
        class="px-4 py-2 text-sm border-b-2 -mb-px shrink-0"
        :class="tab === t.id ? 'border-primary text-primary font-medium' : 'border-transparent text-on-surface-variant'"
        @click="tab = t.id as typeof tab"
      >
        {{ t.label }}
      </button>
    </div>

    <div v-if="tab === 'templates'" class="grid lg:grid-cols-2 gap-6">
      <div class="space-y-3">
        <div
          v-for="tmpl in templates"
          :key="tmpl.id"
          class="p-4 rounded-xl border border-outline-variant bg-surface-container-lowest"
        >
          <div class="flex items-center justify-between">
            <h3 class="font-semibold">{{ tmpl.name }}</h3>
            <span v-if="tmpl.isDefault" class="text-xs px-2 py-0.5 rounded-full bg-secondary-container">پیش‌فرض</span>
          </div>
          <p class="text-xs text-on-surface-variant mt-1">{{ tmpl.description }}</p>
          <ol class="mt-3 flex flex-wrap gap-1">
            <li
              v-for="s in tmpl.steps"
              :key="s.id"
              class="text-xs px-2 py-1 rounded bg-surface-container"
            >
              {{ s.stepNameFa }}
              <span v-if="s.isOptional" class="text-on-surface-variant">(اختیاری)</span>
            </li>
          </ol>
        </div>
      </div>

      <form class="p-4 rounded-xl border border-outline-variant bg-surface-container-low space-y-3" @submit.prevent="createTemplate">
        <h3 class="font-semibold">قالب جدید</h3>
        <input v-model="newTemplate.name" placeholder="نام قالب" required class="w-full px-3 py-2 rounded-lg border border-outline-variant" />
        <textarea v-model="newTemplate.description" placeholder="توضیحات" rows="2" class="w-full px-3 py-2 rounded-lg border border-outline-variant" />
        <p class="text-xs font-medium">مراحل (به ترتیب انتخاب):</p>
        <div class="flex flex-wrap gap-1">
          <button
            v-for="st in stepTypes"
            :key="st.id"
            type="button"
            class="text-xs px-2 py-1 rounded-full border border-outline-variant hover:bg-surface-container-lowest"
            @click="addStepToBuilder(st.id)"
          >
            + {{ st.nameFa }}
          </button>
        </div>
        <ol v-if="selectedSteps.length" class="text-sm space-y-1">
          <li v-for="(s, i) in selectedSteps" :key="i" class="flex justify-between">
            <span>{{ stepTypes.find((t) => t.id === s.processStepTypeId)?.nameFa }}</span>
            <label class="text-xs flex items-center gap-1">
              <input v-model="s.isOptional" type="checkbox" /> اختیاری
            </label>
          </li>
        </ol>
        <button type="submit" class="w-full py-2 bg-primary text-on-primary rounded-full text-sm font-medium">
          ذخیره قالب
        </button>
      </form>
    </div>

    <ul v-else-if="tab === 'steps'" class="grid sm:grid-cols-2 gap-3">
      <li
        v-for="st in stepTypes"
        :key="st.id"
        class="flex items-center gap-3 p-4 rounded-xl border border-outline-variant bg-surface-container-lowest"
      >
        <span class="material-symbols-outlined text-primary text-2xl">{{ st.icon }}</span>
        <div>
          <p class="font-medium">{{ st.nameFa }}</p>
          <p class="text-xs font-mono text-on-surface-variant">{{ st.code }}</p>
        </div>
      </li>
    </ul>

    <div v-else class="grid lg:grid-cols-2 gap-6">
      <ul class="space-y-2">
        <li
          v-for="p in providers"
          :key="p.id"
          class="p-4 rounded-xl border border-outline-variant bg-surface-container-lowest"
        >
          <p class="font-medium">{{ p.name }}</p>
          <p class="text-sm text-on-surface-variant">{{ p.specialty }}</p>
          <p class="text-sm font-mono">{{ p.phone }}</p>
        </li>
      </ul>
      <form class="p-4 rounded-xl border border-outline-variant space-y-3" @submit.prevent="createProvider">
        <h3 class="font-semibold">خدمات‌دهنده جدید</h3>
        <input v-model="newProvider.name" placeholder="نام" required class="w-full px-3 py-2 rounded-lg border border-outline-variant" />
        <input v-model="newProvider.specialty" placeholder="تخصص (قالیشویی، رفوگری...)" class="w-full px-3 py-2 rounded-lg border border-outline-variant" />
        <input v-model="newProvider.phone" placeholder="تلفن" class="w-full px-3 py-2 rounded-lg border border-outline-variant" />
        <button type="submit" class="w-full py-2 bg-primary text-on-primary rounded-full text-sm">افزودن</button>
      </form>
    </div>
  </div>
</template>
