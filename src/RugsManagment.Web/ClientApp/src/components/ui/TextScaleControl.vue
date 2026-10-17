<script setup lang="ts">
/**
 * «اندازهٔ نوشته: [آ] [آ+] [آ++]» (A.3) — در HelpDrawer و هر جای دیگری که لازم شد.
 *
 * یک radiogroup سه‌دکمه‌ای: انتخاب همان لحظه روی کل صفحه اعمال و برای همین کاربر ذخیره می‌شود. کار واقعی
 * با `setTextScale` پوسته است (`lib/ui`؛ `data-text-scale` روی `<html>` و `PUT /api/me/preferences`، با
 * برگشت به اندازهٔ قبلی و پیام اگر ذخیره نشد) تا منوی کاربر و این کنترل همیشه هم‌گام باشند. در راست‌به‌چپ
 * ← گزینهٔ بعد است.
 */
import { computed, ref, useId } from 'vue'
import { appContext } from '@/lib/context'
import { TEXT_SCALE_OPTIONS } from '@/lib/labels'
import { message } from '@/lib/messages'
import { setTextScale } from '@/lib/ui'
import { textScaleName, textScaleValue, type TextScale } from '@/lib/types/platform'

withDefaults(defineProps<{ /** فشرده (بی‌پیام وضعیت) */ compact?: boolean }>(), { compact: false })

const ctx = appContext()
const saving = ref(false)
const status = ref('')
const group = ref<HTMLElement | null>(null)
// منوی کاربر و راهنما هر دو این کنترل را دارند؛ شناسه باید یکتا باشد
const labelId = `${useId()}-text-scale`

const current = computed<TextScale>(() => textScaleValue(ctx.textScale))

async function choose(value: TextScale) {
  if (value === current.value || saving.value) return
  saving.value = true
  status.value = ''
  try {
    const saved = await setTextScale(textScaleName(value))
    // پیام خطا را خودِ پوسته به‌صورت toast می‌دهد
    if (saved) status.value = message('I_TEXT_SCALE_SAVED')
  } finally {
    saving.value = false
  }
}

/** پیمایش radiogroup با پیکان‌ها (راست‌به‌چپ: چپ = بعدی). */
function onKeydown(event: KeyboardEvent) {
  const index = TEXT_SCALE_OPTIONS.findIndex((o) => o.value === current.value)
  let next = index
  if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') next = Math.min(TEXT_SCALE_OPTIONS.length - 1, index + 1)
  else if (event.key === 'ArrowRight' || event.key === 'ArrowUp') next = Math.max(0, index - 1)
  else if (event.key === 'Home') next = 0
  else if (event.key === 'End') next = TEXT_SCALE_OPTIONS.length - 1
  else return
  event.preventDefault()
  if (next === index) return
  void choose(TEXT_SCALE_OPTIONS[next].value)
  requestAnimationFrame(() => group.value?.querySelector<HTMLElement>('[aria-checked="true"]')?.focus())
}
</script>

<template>
  <div :class="compact ? 'flex items-center gap-2' : 'space-y-2'">
    <span :id="labelId" class="block text-sm font-medium text-on-surface">اندازهٔ نوشته</span>
    <div
      ref="group"
      role="radiogroup"
      :aria-labelledby="labelId"
      class="inline-flex rounded-lg border border-outline-variant p-0.5"
      @keydown="onKeydown"
    >
      <button
        v-for="option in TEXT_SCALE_OPTIONS"
        :key="option.value"
        type="button"
        role="radio"
        :aria-checked="option.value === current"
        :aria-label="option.label"
        :tabindex="option.value === current ? 0 : -1"
        :aria-disabled="saving || undefined"
        class="min-h-11 min-w-12 rounded-md px-3 font-semibold"
        :class="[
          option.value === current ? 'bg-primary text-on-primary' : 'text-on-surface-variant hover:bg-surface-container',
          option.value === 0 ? 'text-sm' : option.value === 1 ? 'text-base' : 'text-lg',
        ]"
        @click="choose(option.value)"
      >{{ option.short }}</button>
    </div>
    <p v-if="status && !compact" class="text-xs text-on-surface-variant" role="status">{{ status }}</p>
  </div>
</template>
