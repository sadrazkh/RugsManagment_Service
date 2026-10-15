<script setup lang="ts">
/**
 * کشوی «؟ راهنما» (H.5): ۳ تا ۵ گام شماره‌دار صفحهٔ جاری با آیکون و کلید صفحه‌کلید، کنترل
 * «اندازهٔ نوشته» و پیوند «کارت راهنما» برای چاپ و گذاشتن کنار رایانه.
 *
 * متن هر صفحه در `lib/help.ts` است؛ کلید صفحه از `page`، وگرنه از `data-help-page` روی body یا مسیر نشانی.
 * واژهٔ نگله/عدل از `terms` کارگاه پر می‌شود. گام‌های فقط-Business (ثبت هزینه، مبلغ‌ها، «قطعی کن») برای انباردار
 * نشان داده نمی‌شوند (H.3: متن از سیاست بیننده پیروی می‌کند).
 *
 *   <HelpDrawer v-model:open="helpOpen" page="rugs" />
 */
import { computed } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { appContext } from '@/lib/context'
import { toPersianDigits } from '@/lib/format'
import { currentHelpPage, helpFor } from '@/lib/help'
import { fillMessage } from '@/lib/messages'
import Drawer from './Drawer.vue'
import TextScaleControl from './TextScaleControl.vue'

const props = withDefaults(defineProps<{ open: boolean; page?: string }>(), { page: undefined })
const emit = defineEmits<{ 'update:open': [value: boolean]; close: [] }>()

const ctx = appContext()
const topic = computed(() => helpFor(props.page ?? currentHelpPage(), { business: ctx.policies.business }))
const steps = computed(() => topic.value.steps.map((s) => ({ ...s, text: fillMessage(s.text) })))

function update(value: boolean) {
  emit('update:open', value)
  if (!value) emit('close')
}
</script>

<template>
  <Drawer :open="open" :title="`راهنما: ${topic.title}`" side="end" size="md" @update:open="update">
    <ol class="space-y-3">
      <li v-for="(step, index) in steps" :key="index" class="flex items-start gap-3">
        <span
          class="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-secondary-container font-semibold text-on-secondary-container"
          data-numeric
          aria-hidden="true"
        >{{ toPersianDigits(index + 1) }}</span>
        <div class="min-w-0 flex-1 pt-1">
          <p class="flex items-start gap-2 leading-7">
            <AppIcon v-if="step.icon" :name="step.icon" class="mt-1 h-5 w-5 text-on-surface-variant" />
            <span>{{ step.text }}</span>
          </p>
          <p v-if="step.keys?.length" class="mt-1 flex flex-wrap items-center gap-1 text-xs text-on-surface-variant" dir="ltr">
            <template v-for="(key, k) in step.keys" :key="k">
              <span v-if="k > 0" aria-hidden="true">+</span>
              <kbd class="rounded border border-outline-variant bg-surface-container px-1.5 py-0.5 font-mono">{{ key }}</kbd>
            </template>
          </p>
        </div>
      </li>
    </ol>

    <hr class="my-5 border-outline-variant" />

    <TextScaleControl />

    <a
      href="/Help/Card"
      target="_blank"
      rel="noopener"
      class="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm text-on-surface hover:bg-surface-container"
    >
      <AppIcon name="printer" class="h-5 w-5" />
      کارت راهنما برای چاپ
    </a>
  </Drawer>
</template>
