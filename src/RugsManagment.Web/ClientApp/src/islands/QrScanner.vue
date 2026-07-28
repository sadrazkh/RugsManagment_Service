<script setup lang="ts">
/**
 * اسکن برچسب فرش با دوربین موبایل.
 *
 * QR روی برچسب آدرس کامل صفحهٔ فرش را دارد (از فاز ۳)، پس اسکن یعنی «برو به آن فرش».
 *
 * از BarcodeDetector مرورگر استفاده می‌کند — بدون کتابخانهٔ اضافه. این API روی
 * کروم/اج اندروید و دسکتاپ هست ولی روی سافاری و فایرفاکس نه؛ در آن حالت
 * ورودی دستی کد فرش جایگزین می‌شود تا قابلیت برای همه کار کند.
 */
import { onUnmounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { api } from '@/lib/api'
import { toast } from '@/lib/ui'

interface RugHit { id: string; sku: string; title?: string }

const open = ref(false)
const video = ref<HTMLVideoElement | null>(null)
const manualCode = ref('')
const scanning = ref(false)
const cameraError = ref('')
const looking = ref(false)

let stream: MediaStream | null = null
let rafId = 0
let detector: unknown = null

const supportsCamera = 'BarcodeDetector' in window && !!navigator.mediaDevices?.getUserMedia

async function start() {
  open.value = true
  cameraError.value = ''
  manualCode.value = ''

  if (!supportsCamera) {
    // مرورگر پشتیبانی نمی‌کند — مستقیم سراغ ورودی دستی
    cameraError.value = 'مرورگر شما اسکن با دوربین را پشتیبانی نمی‌کند؛ کد فرش را دستی وارد کنید.'
    return
  }

  try {
    // دوربین پشت برای اسکن برچسب روی فرش
    stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: { ideal: 'environment' } },
    })

    if (video.value) {
      video.value.srcObject = stream
      await video.value.play()
    }

    const Detector = (window as unknown as Record<string, new (o: unknown) => unknown>).BarcodeDetector
    detector = new Detector({ formats: ['qr_code'] })

    scanning.value = true
    tick()
  } catch (e) {
    cameraError.value =
      (e as Error).name === 'NotAllowedError'
        ? 'دسترسی به دوربین رد شد. می‌توانید کد فرش را دستی وارد کنید.'
        : 'دوربین در دسترس نیست؛ کد فرش را دستی وارد کنید.'
  }
}

function tick() {
  if (!scanning.value || !video.value || !detector) return

  const detect = (detector as { detect: (v: unknown) => Promise<{ rawValue: string }[]> }).detect
  detect
    .call(detector, video.value)
    .then((codes) => {
      if (codes.length > 0) handleScan(codes[0].rawValue)
      else rafId = requestAnimationFrame(tick)
    })
    .catch(() => {
      rafId = requestAnimationFrame(tick)
    })
}

/**
 * مقدار اسکن‌شده ممکن است آدرس کامل باشد (برچسب‌های ما) یا فقط کد فرش
 * (برچسب‌های قدیمی یا بارکد). هر دو را می‌پذیریم.
 */
function handleScan(raw: string) {
  scanning.value = false
  stop()

  const value = raw.trim()

  // آدرس صفحهٔ فرش — فقط اگر از همین سایت باشد دنبالش می‌رویم
  try {
    const url = new URL(value)
    if (url.origin === window.location.origin && /\/Rugs\/Details\//i.test(url.pathname)) {
      window.location.href = url.pathname
      return
    }
  } catch {
    /* آدرس نبود — احتمالاً کد فرش است */
  }

  void findBySku(value)
}

async function findBySku(code: string) {
  looking.value = true
  try {
    const res = await api.get<{ items: RugHit[] }>(
      `/api/rugs?search=${encodeURIComponent(code)}&pageSize=5`,
    )

    const exact = res.items.find((r) => r.sku.toLowerCase() === code.toLowerCase()) ?? res.items[0]
    if (!exact) {
      toast.error(`فرشی با کد «${code}» پیدا نشد.`)
      looking.value = false
      // دوباره اسکن را روشن کن تا کاربر مجبور نشود از اول شروع کند
      void start()
      return
    }

    window.location.href = `/Rugs/Details/${exact.id}`
  } catch (e) {
    toast.error((e as Error).message)
    looking.value = false
  }
}

function submitManual() {
  const code = manualCode.value.trim()
  if (!code) return
  void findBySku(code)
}

function stop() {
  scanning.value = false
  cancelAnimationFrame(rafId)
  stream?.getTracks().forEach((t) => t.stop())
  stream = null
}

function close() {
  stop()
  open.value = false
}

onUnmounted(stop)

// دکمهٔ اسکن در هدر و پارامتر ?scan=1 (از پالت فرمان) هر دو اسکنر را باز می‌کنند
document.addEventListener('DOMContentLoaded', () => {
  if (new URLSearchParams(window.location.search).get('scan') === '1') void start()
})
if (new URLSearchParams(window.location.search).get('scan') === '1') void start()

defineExpose({ start })
</script>

<template>
  <div>
    <button
      type="button"
      class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container"
      title="اسکن برچسب فرش"
      @click="start"
    >
      <AppIcon name="qr" label="اسکن برچسب فرش" />
    </button>

    <Teleport to="body">
      <div
        v-if="open"
        class="fixed inset-0 z-[70] grid place-items-center bg-black/80 p-4"
        role="dialog"
        aria-modal="true"
        aria-label="اسکن برچسب"
        data-no-print
        @click.self="close"
      >
        <div class="w-full max-w-md overflow-hidden rounded-xl border border-outline-variant bg-surface-container-lowest">
          <div class="flex items-center justify-between border-b border-outline-variant px-4 py-3">
            <h2 class="flex items-center gap-2 text-sm font-semibold text-primary">
              <AppIcon name="qr" class="h-4 w-4" />
              اسکن برچسب فرش
            </h2>
            <button type="button" class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container"
                    @click="close">
              <AppIcon name="close" label="بستن" />
            </button>
          </div>

          <div class="p-4">
            <!-- تصویر دوربین -->
            <div v-if="scanning" class="relative mb-4 overflow-hidden rounded-lg bg-black">
              <video ref="video" class="aspect-video w-full object-cover" muted playsinline></video>
              <!-- کادر راهنما: کاربر بداند برچسب را کجا بگیرد -->
              <div class="pointer-events-none absolute inset-0 grid place-items-center">
                <div class="h-40 w-40 rounded-lg border-2 border-white/80"></div>
              </div>
            </div>

            <p v-if="looking" class="mb-3 flex items-center gap-2 text-sm text-on-surface-variant">
              <span class="h-4 w-4 animate-spin rounded-full border-2 border-outline-variant border-t-primary"></span>
              در حال یافتن فرش…
            </p>

            <p v-if="cameraError" class="mb-3 flex items-start gap-2 rounded-lg bg-warning/10 px-3 py-2 text-xs text-warning">
              <AppIcon name="warning" class="mt-0.5 h-4 w-4 shrink-0" />
              <span>{{ cameraError }}</span>
            </p>

            <!-- ورودی دستی: همیشه در دسترس، نه فقط هنگام خطا -->
            <label class="block">
              <span class="mb-1 block text-sm">یا کد فرش را وارد کنید</span>
              <div class="flex gap-2">
                <input
                  v-model="manualCode"
                  dir="ltr"
                  placeholder="RUG-202607-0001"
                  class="min-h-11 flex-1 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 font-mono text-sm outline-none focus:border-primary"
                  @keydown.enter.prevent="submitManual"
                />
                <button
                  type="button"
                  :disabled="!manualCode.trim() || looking"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
                  @click="submitManual"
                >
                  <AppIcon name="arrow-left" class="h-4 w-4" />
                  برو
                </button>
              </div>
            </label>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
