<script setup lang="ts">
/**
 * فرم داینامیک یک مرحله — از روی اسکیمای همان نوع مرحله ساخته می‌شود.
 *
 * مقادیر به‌صورت رشته نگه‌داری می‌شوند و سرور آن‌ها را بر اساس نوع فیلد
 * تبدیل و اعتبارسنجی می‌کند؛ اعتبارسنجی اینجا فقط برای بازخورد سریع است.
 */
import { computed, ref, watch } from 'vue'

export interface StepField {
  key: string
  label: string
  type: number
  required: boolean
  options?: string[]
  hint?: string
}

const props = defineProps<{
  /** اسکیمای JSON از ProcessStepType.fieldSchemaJson */
  schemaJson?: string | null
  /** مقادیر قبلی (هنگام ویرایش) */
  valuesJson?: string | null
}>()

const values = ref<Record<string, string>>({})

const fields = computed<StepField[]>(() => {
  if (!props.schemaJson) return []
  try {
    const parsed = JSON.parse(props.schemaJson)
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
})

// مقادیر قبلی را در فرم بنشان
watch(
  () => props.valuesJson,
  (raw) => {
    if (!raw) return
    try {
      const parsed = JSON.parse(raw) as Record<string, unknown>
      for (const [k, v] of Object.entries(parsed)) values.value[k] = String(v ?? '')
    } catch {
      /* مقادیر خراب نباید فرم را بشکند */
    }
  },
  { immediate: true },
)

/** فیلدهای الزامیِ خالی — والد قبل از ارسال چک می‌کند. */
function missingRequired(): string[] {
  return fields.value
    .filter((f) => f.required && !String(values.value[f.key] ?? '').trim())
    .map((f) => f.label)
}

/** JSON آمادهٔ ارسال؛ null وقتی فرمی وجود ندارد یا همه خالی‌اند. */
function toPayload(): string | null {
  const out: Record<string, string> = {}
  for (const f of fields.value) {
    const v = String(values.value[f.key] ?? '').trim()
    if (v) out[f.key] = v
  }
  return Object.keys(out).length ? JSON.stringify(out) : null
}

defineExpose({ toPayload, missingRequired, hasFields: computed(() => fields.value.length > 0) })
</script>

<template>
  <div v-if="fields.length" class="space-y-3 rounded-lg border border-outline-variant bg-surface-container p-3">
    <p class="text-xs font-medium text-on-surface-variant">اطلاعات این مرحله</p>

    <div class="grid gap-3 sm:grid-cols-2">
      <label v-for="f in fields" :key="f.key" class="block">
        <span class="mb-1 block text-sm">
          {{ f.label }}
          <span v-if="f.required" class="text-error">*</span>
        </span>

        <!-- انتخابی -->
        <select v-if="f.type === 3" v-model="values[f.key]" class="fld">
          <option value="">— انتخاب کنید —</option>
          <option v-for="o in f.options ?? []" :key="o" :value="o">{{ o }}</option>
        </select>

        <!-- بله/خیر -->
        <select v-else-if="f.type === 4" v-model="values[f.key]" class="fld">
          <option value="">—</option>
          <option value="true">بله</option>
          <option value="false">خیر</option>
        </select>

        <!-- تاریخ -->
        <input v-else-if="f.type === 2" v-model="values[f.key]" type="date" dir="ltr" class="fld" />

        <!-- عدد -->
        <input v-else-if="f.type === 1" v-model="values[f.key]" type="text" inputmode="decimal" dir="ltr" class="fld" />

        <!-- متن -->
        <input v-else v-model="values[f.key]" type="text" class="fld" />

        <span v-if="f.hint" class="mt-1 block text-xs text-on-surface-variant">{{ f.hint }}</span>
      </label>
    </div>
  </div>
</template>

<style scoped>
.fld {
  min-height: 2.75rem;
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  padding: 0 0.75rem;
  outline: none;
}
.fld:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
</style>
