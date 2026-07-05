<script setup lang="ts">
/** مدیریت فیلدهای سفارشی کارگاه: افزودن/ویرایش/حذف. مقادیر در Rug.MetadataJson ذخیره می‌شوند. */
import { onMounted, reactive, ref } from 'vue'
import { api } from '@/lib/api'
import type { CustomFieldDefinition, CustomFieldType } from '@/lib/types'

const fields = ref<CustomFieldDefinition[]>([])
const loading = ref(true)
const error = ref('')
const saving = ref(false)

const typeLabels: Record<number, string> = { 0: 'متن', 1: 'عدد', 2: 'تاریخ', 3: 'انتخابی', 4: 'بله/خیر' }

const draft = reactive({
  key: '', label: '', fieldType: 0 as CustomFieldType, options: '', isRequired: false,
})

async function load() {
  loading.value = true
  try { fields.value = await api.get<CustomFieldDefinition[]>('/api/custom-fields') }
  catch (e) { error.value = (e as Error).message }
  finally { loading.value = false }
}

async function add() {
  error.value = ''
  if (!draft.key.trim() || !draft.label.trim()) { error.value = 'کلید و برچسب الزامی است.'; return }
  saving.value = true
  try {
    const optionsJson = draft.fieldType === 3 && draft.options.trim()
      ? JSON.stringify(draft.options.split(',').map((s) => s.trim()).filter(Boolean))
      : null
    await api.post('/api/custom-fields', {
      key: draft.key, label: draft.label, fieldType: draft.fieldType,
      optionsJson, isRequired: draft.isRequired, sortOrder: fields.value.length,
    })
    Object.assign(draft, { key: '', label: '', fieldType: 0, options: '', isRequired: false })
    await load()
  } catch (e) { error.value = (e as Error).message }
  finally { saving.value = false }
}

async function toggleActive(f: CustomFieldDefinition) {
  try {
    await api.put(`/api/custom-fields/${f.id}`, {
      label: f.label, fieldType: f.fieldType, optionsJson: f.optionsJson,
      isRequired: f.isRequired, sortOrder: f.sortOrder, isActive: !f.isActive,
    })
    await load()
  } catch (e) { error.value = (e as Error).message }
}

async function remove(f: CustomFieldDefinition) {
  if (!confirm(`حذف فیلد «${f.label}»؟`)) return
  try { await api.del(`/api/custom-fields/${f.id}`); await load() }
  catch (e) { error.value = (e as Error).message }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>

    <!-- افزودن -->
    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">فیلد جدید</h2>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <label class="block">
          <span class="mb-1 block text-sm">کلید (انگلیسی)</span>
          <input v-model="draft.key" dir="ltr" placeholder="background_color" class="fld" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">برچسب</span>
          <input v-model="draft.label" placeholder="رنگ زمینه" class="fld" />
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">نوع</span>
          <select v-model.number="draft.fieldType" class="fld">
            <option v-for="(lbl, val) in typeLabels" :key="val" :value="Number(val)">{{ lbl }}</option>
          </select>
        </label>
        <label v-if="draft.fieldType === 3" class="block">
          <span class="mb-1 block text-sm">گزینه‌ها (با , جدا)</span>
          <input v-model="draft.options" placeholder="قرمز, آبی, کرم" class="fld" />
        </label>
        <label class="flex items-center gap-2 pt-7 text-sm">
          <input v-model="draft.isRequired" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
          الزامی
        </label>
      </div>
      <button :disabled="saving" @click="add"
              class="mt-4 rounded-lg bg-primary px-5 py-2.5 font-semibold text-on-primary hover:opacity-90 disabled:opacity-60">
        افزودن فیلد
      </button>
    </section>

    <!-- فهرست -->
    <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">فیلدهای تعریف‌شده</h2>
      <div v-if="loading" class="py-6 text-center text-on-surface-variant">در حال بارگذاری…</div>
      <div v-else-if="fields.length === 0" class="py-6 text-center text-on-surface-variant">فیلدی تعریف نشده است.</div>
      <ul v-else class="divide-y divide-outline-variant">
        <li v-for="f in fields" :key="f.id" class="flex items-center justify-between gap-3 py-3">
          <div>
            <div class="font-medium">{{ f.label }} <span v-if="f.isRequired" class="text-xs text-error">(الزامی)</span></div>
            <div class="text-xs text-on-surface-variant" dir="ltr">{{ f.key }} · {{ typeLabels[f.fieldType] }}</div>
          </div>
          <div class="flex items-center gap-2">
            <button @click="toggleActive(f)"
                    class="rounded-lg border border-outline-variant px-3 py-1.5 text-xs hover:bg-surface-container">
              {{ f.isActive ? 'غیرفعال' : 'فعال' }}
            </button>
            <button @click="remove(f)" class="rounded-lg border border-outline-variant px-3 py-1.5 text-xs text-error hover:bg-error-container">حذف</button>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.fld {
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  padding: 0.625rem 0.75rem;
  outline: none;
}
.fld:focus { border-color: var(--color-primary); box-shadow: 0 0 0 1px var(--color-primary); }
</style>
