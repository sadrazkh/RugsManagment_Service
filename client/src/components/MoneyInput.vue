<script setup lang="ts">
import { computed } from 'vue'
import { formatMoneyInput, parseMoneyInput } from '@/utils/money'

const model = defineModel<number | undefined>()
const props = defineProps<{ placeholder?: string; class?: string }>()

const display = computed({
  get: () => formatMoneyInput(model.value),
  set: (v: string) => { model.value = parseMoneyInput(v) },
})
</script>

<template>
  <input
    v-model="display"
    type="text"
    inputmode="numeric"
    :placeholder="props.placeholder ?? '۰'"
    :class="props.class"
    dir="ltr"
    class="text-end w-full"
  />
</template>
