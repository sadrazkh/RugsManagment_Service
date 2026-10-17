<script setup lang="ts">
import { ref } from 'vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
const props = defineProps<{ name: string; value?: string | null; label: string }>()
const date = ref(props.value ?? null)
</script>
<template>
  <div>
    <span class="mb-1 block text-sm">{{ label }}</span>
    <JalaliDateField v-model="date" :aria-label="label" :chips="false" />
    <input type="hidden" :name="name" :value="date ?? ''" />
  </div>
</template>
