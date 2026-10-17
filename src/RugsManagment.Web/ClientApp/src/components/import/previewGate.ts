import { computed, ref, type ComputedRef } from 'vue'

/** اجازهٔ ثبت فقط به همان ورودی‌هایی داده می‌شود که پاسخ بازبینی آن‌ها رسیده است. */
export function createPreviewGate(input: ComputedRef<unknown>) {
  const accepted = ref<string | null>(null)
  const signature = computed(() => JSON.stringify(input.value))
  return {
    current: computed(() => accepted.value !== null && accepted.value === signature.value),
    capture: () => signature.value,
    accept: (sentSignature: string) => { accepted.value = sentSignature },
  }
}
