<script setup lang="ts">
/**
 * جزیرهٔ `purchase-lines-editor` (I.5): سطرهای قیمت خرید در صفحهٔ جزئیات فرش — فقط Business (view برای بقیه رندرش
 * نمی‌کند و API هم ۴۰۳ می‌دهد).
 *
 * props: `{ rugId, version, readOnly }` و اختیاری `intakeAreaM2`، `quantity` برای پیش‌نمایش مبلغ «هر متر» هنگام تایپ.
 * بعد از ذخیره صفحه تازه می‌شود تا کارت «بهای تمام‌شده» و هر بخش دیگری که از همین عدد خوانده عدد تازه را نشان دهد
 * (اگر جزیرهٔ دیگری کار ذخیره‌نشده دارد، تازه‌سازی صبر می‌کند — `blockReload`).
 */
import PurchaseLinesEditor from '@/components/PurchaseLinesEditor.vue'
import { message } from '@/lib/messages'
import { requestReload, toastAfterReload } from '@/lib/ui'

withDefaults(
  defineProps<{
    rugId: string
    version: number
    readOnly?: boolean
    intakeAreaM2?: number | null
    quantity?: number
  }>(),
  { readOnly: false, intakeAreaM2: null, quantity: 1 },
)

function onSaved() {
  toastAfterReload(message('I_PURCHASE_SAVED'))
  requestReload()
}
</script>

<template>
  <PurchaseLinesEditor
    :rug-id="rugId"
    :version="version"
    :read-only="readOnly"
    :intake-area-m2="intakeAreaM2"
    :quantity="quantity"
    @saved="onSaved"
  />
</template>
