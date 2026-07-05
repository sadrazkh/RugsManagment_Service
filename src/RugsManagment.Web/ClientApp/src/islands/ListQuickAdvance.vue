<script setup lang="ts">
/**
 * پیشبرد سریع مرحله از داخل لیست فرش‌ها.
 * دکمه‌های [data-advance-rug]/[data-advance-step] در Razor رندر می‌شوند؛ این جزیره کلیک را می‌گیرد،
 * تأیید گرفته و مرحلهٔ جاری را (بدون هزینه) تکمیل می‌کند. ثبت هزینهٔ دقیق در صفحهٔ جزئیات است.
 */
import { onMounted, ref } from 'vue'
import { api } from '@/lib/api'

const busy = ref(false)
const error = ref('')

async function advance(rugId: string, stepId: string) {
  if (busy.value) return
  if (!confirm('مرحلهٔ جاری تکمیل و به مرحلهٔ بعد بروید؟ (هزینه را می‌توانید در جزئیات ثبت کنید)')) return
  busy.value = true
  try {
    await api.post(`/api/rugs/${rugId}/steps/${stepId}/advance`, {
      serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null,
      pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true,
    })
    window.location.reload()
  } catch (e) {
    error.value = (e as Error).message
    busy.value = false
    alert(error.value)
  }
}

onMounted(() => {
  document.querySelectorAll<HTMLElement>('[data-advance-rug]').forEach((btn) => {
    btn.addEventListener('click', (e) => {
      e.preventDefault()
      advance(btn.dataset.advanceRug!, btn.dataset.advanceStep!)
    })
  })
})
</script>

<template>
  <span v-if="error" class="hidden">{{ error }}</span>
</template>
