<script setup lang="ts">
/**
 * پیشبرد سریع مرحله از داخل لیست فرش‌ها.
 * دکمه‌های [data-advance-rug]/[data-advance-step] در Razor رندر می‌شوند؛ این جزیره کلیک را می‌گیرد،
 * تأیید گرفته و مرحلهٔ جاری را (بدون هزینه) تکمیل می‌کند. ثبت هزینهٔ دقیق در صفحهٔ جزئیات است.
 */
import { onMounted, ref } from 'vue'
import { api } from '@/lib/api'
import { confirmDialog, toast, toastAfterReload } from '@/lib/ui'

const busy = ref(false)

async function advance(rugId: string, stepId: string) {
  if (busy.value) return

  const ok = await confirmDialog({
    title: 'مرحلهٔ جاری تکمیل شود؟',
    message: 'فرش به مرحلهٔ بعد می‌رود. هزینه را می‌توانید بعداً در صفحهٔ جزئیات ثبت کنید.',
    confirmLabel: 'تکمیل و ادامه',
  })
  if (!ok) return

  busy.value = true
  try {
    await api.post(`/api/rugs/${rugId}/steps/${stepId}/advance`, {
      serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null,
      pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true,
    })
    // پیام بعد از بارگذاری مجدد نمایش داده می‌شود تا کاربر آن را روی دادهٔ تازه ببیند
    toastAfterReload('مرحله تکمیل شد.')
    window.location.reload()
  } catch (e) {
    busy.value = false
    toast.error((e as Error).message)
  }
}

onMounted(() => {
  document.querySelectorAll<HTMLElement>('[data-advance-rug]').forEach((btn) => {
    btn.addEventListener('click', (e) => {
      e.preventDefault()
      void advance(btn.dataset.advanceRug!, btn.dataset.advanceStep!)
    })
  })
})
</script>

<template>
  <!-- این جزیره فقط رفتار اضافه می‌کند و چیزی رندر نمی‌کند -->
  <span class="hidden" />
</template>
