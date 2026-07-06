<script setup lang="ts">
/** تولید QR به‌صورت data URL (برای پیش‌نمایش و چاپ). */
import { onMounted, ref, watch } from 'vue'
import QRCode from 'qrcode'

const props = withDefaults(defineProps<{ value: string; size?: number }>(), { size: 96 })
const src = ref('')

async function gen() {
  try { src.value = await QRCode.toDataURL(props.value || ' ', { margin: 0, width: props.size }) }
  catch { src.value = '' }
}
watch(() => [props.value, props.size], gen)
onMounted(gen)
</script>

<template>
  <img v-if="src" :src="src" :width="size" :height="size" alt="QR" />
</template>
