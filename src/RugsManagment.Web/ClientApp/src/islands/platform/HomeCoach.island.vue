<script setup lang="ts">
/**
 * راهنمای شروع خانه (`home-coach`) — گام‌های کار روزانه برای کاربری که تازه آمده (H.5).
 *
 * سرور فقط وقتی این جزیره را می‌گذارد که کاربر آن را نبسته باشد (CoachDismissedJson). «فهمیدم» برای همیشه
 * می‌بندد (PUT /api/me/preferences با dismissCoach)؛ «بعداً» فقط تا پایان همین نشست مرورگر. اگر ذخیره نشد،
 * باز هم بسته می‌شود و پیام می‌گوید دفعهٔ بعد دوباره دیده می‌شود — راهنما نباید جلوی کار را بگیرد.
 *
 * هر گام یک رشته است؛ اگر «عنوان — توضیح» باشد، عنوان پررنگ نوشته می‌شود. `tip` (اختیاری) یک جملهٔ
 * بی‌شماره زیر گام‌هاست، مثل «هر جا گیر کردید، دکمهٔ «؟ راهنما» … را بزنید».
 */
import { computed, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { api } from '@/lib/api'
import { toast } from '@/lib/ui'

const props = withDefaults(
  defineProps<{
    steps: string[]
    /** جملهٔ پایانی بی‌شماره */
    tip?: string
    /** کلید این راهنما در CoachDismissed کاربر */
    dismissKey?: string
  }>(),
  { tip: '', dismissKey: 'home' },
)

const SESSION_KEY = `rugs:coach-later:${props.dismissKey}`

function hiddenForSession(): boolean {
  try {
    return sessionStorage.getItem(SESSION_KEY) === '1'
  } catch {
    return false
  }
}

const visible = ref(!hiddenForSession())
const saving = ref(false)

const SEPARATOR = ' — '

const items = computed(() =>
  props.steps.map((step) => {
    const at = step.indexOf(SEPARATOR)
    return at > 0
      ? { title: step.slice(0, at), text: step.slice(at + SEPARATOR.length) }
      : { title: '', text: step }
  }),
)

function later() {
  try {
    sessionStorage.setItem(SESSION_KEY, '1')
  } catch {
    /* حالت خصوصی — فقط همین صفحه */
  }
  visible.value = false
}

async function dismiss() {
  saving.value = true
  try {
    await api.put('/api/me/preferences', { dismissCoach: props.dismissKey })
  } catch {
    toast.warning('ذخیره نشد؛ این راهنما دفعهٔ بعد دوباره دیده می‌شود.')
  } finally {
    saving.value = false
    visible.value = false
  }
}
</script>

<template>
  <section
    v-if="visible && items.length > 0"
    class="rounded-xl border border-secondary/40 bg-secondary-container/40 p-5"
    aria-labelledby="home-coach-title"
  >
    <div class="flex items-start gap-3">
      <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-secondary-container text-on-secondary-container">
        <AppIcon name="help" />
      </span>
      <div class="min-w-0 flex-1">
        <h2 id="home-coach-title" class="text-lg font-bold">شروع کار</h2>
        <p class="text-on-surface-variant">کار روزانه همین چند قدم است:</p>
      </div>
    </div>

    <ol class="mt-4 space-y-3">
      <li v-for="(item, index) in items" :key="index" class="flex items-start gap-3">
        <span
          class="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-primary text-sm font-bold text-on-primary"
          aria-hidden="true"
          data-numeric
        >{{ (index + 1).toLocaleString('fa-IR') }}</span>
        <p class="min-w-0 flex-1 pt-0.5">
          <strong v-if="item.title" class="font-semibold">{{ item.title }}:</strong>
          {{ item.text }}
        </p>
      </li>
    </ol>

    <p v-if="tip" class="mt-4 flex items-start gap-2 font-medium">
      <AppIcon name="info" class="mt-1 h-5 w-5 shrink-0 text-on-surface-variant" />
      <span>{{ tip }}</span>
    </p>

    <div class="mt-5 flex flex-wrap gap-3">
      <button
        type="button"
        class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
        :disabled="saving"
        @click="dismiss"
      >
        <AppIcon name="check" class="h-5 w-5" />
        فهمیدم، دیگر نشان نده
      </button>
      <button
        type="button"
        class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-4 hover:bg-surface-container"
        @click="later"
      >
        بعداً
      </button>
    </div>
  </section>
</template>
