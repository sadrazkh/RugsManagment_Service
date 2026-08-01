<script setup lang="ts">
/** تولید بارکد CODE128 به‌صورت data URL. */
import { onMounted, ref, watch } from 'vue'
import JsBarcode from 'jsbarcode'

const props = defineProps<{ value: string }>()
const src = ref('')

function gen() {
  try {
    const canvas = document.createElement('canvas')
    JsBarcode(canvas, props.value || '0', { format: 'CODE128', displayValue: true, height: 40, margin: 0, fontSize: 12 })
    src.value = canvas.toDataURL()
  } catch { src.value = '' }
}
watch(() => props.value, gen)
onMounted(gen)
</script>

<template>
  <img v-if="src" :src="src" alt="بارکد" style="max-width: 100%" />
</template>
