<script setup lang="ts">
/**
 * دکمهٔ نصب PWA. روی مرورگرهای پشتیبان (کروم/اج/اندروید) رویداد beforeinstallprompt را می‌گیرد
 * و دکمهٔ «نصب اپلیکیشن» نشان می‌دهد. روی iOS راهنمای «افزودن به صفحهٔ اصلی» را نمایش می‌دهد.
 * اگر برنامه از قبل نصب/standalone باشد چیزی نشان داده نمی‌شود.
 */
import { onMounted, ref } from 'vue'

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const deferred = ref<BeforeInstallPromptEvent | null>(null)
const canInstall = ref(false)
const showIosHint = ref(false)
const iosGuide = ref(false)

const isStandalone = () =>
  window.matchMedia('(display-mode: standalone)').matches ||
  (window.navigator as unknown as { standalone?: boolean }).standalone === true

onMounted(() => {
  if (isStandalone()) return

  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault()
    deferred.value = e as BeforeInstallPromptEvent
    canInstall.value = true
  })
  window.addEventListener('appinstalled', () => { canInstall.value = false })

  // iOS Safari از beforeinstallprompt پشتیبانی نمی‌کند
  const ua = window.navigator.userAgent
  const isIos = /iphone|ipad|ipod/i.test(ua)
  const isSafari = /^((?!chrome|android|crios|fxios).)*safari/i.test(ua)
  if (isIos && isSafari) showIosHint.value = true
})

async function install() {
  if (!deferred.value) return
  await deferred.value.prompt()
  await deferred.value.userChoice
  deferred.value = null
  canInstall.value = false
}
</script>

<template>
  <button v-if="canInstall" @click="install" type="button"
          class="rounded-lg bg-primary px-3 py-1.5 text-sm font-semibold text-on-primary hover:opacity-90">
    ⬇ نصب اپلیکیشن
  </button>

  <div v-else-if="showIosHint" class="relative">
    <button @click="iosGuide = !iosGuide" type="button"
            class="rounded-lg border border-outline-variant px-3 py-1.5 text-sm text-on-surface-variant hover:bg-surface-container">
      ⬇ نصب
    </button>
    <div v-if="iosGuide" class="absolute left-0 top-full z-50 mt-1 w-56 rounded-lg border border-outline-variant bg-white p-3 text-xs shadow-lg">
      برای نصب روی iPhone/iPad: دکمهٔ <strong>اشتراک‌گذاری</strong> (مربع با فلش) → <strong>Add to Home Screen</strong> (افزودن به صفحهٔ اصلی).
    </div>
  </div>
</template>
