<script setup lang="ts">
/**
 * اسکن برچسب شماره برای نگله‌بندی (H.6): `BarcodeDetector` مرورگر با `qr_code` و `code_128`، که فقط `scanned(code)`
 * می‌فرستد و **هرگز به صفحهٔ دیگری نمی‌رود** — زبانهٔ نگله‌ها و حالت اسکن همان کد را به نگلهٔ جاری اضافه می‌کنند.
 * (جزیرهٔ `qr-scanner` بالای صفحه کار دیگری دارد: «برو به آن فرش».)
 *
 * - پیوسته: دوربین پس از هر خواندن باز می‌ماند تا یک نگله پشت سر هم اسکن شود؛ همان کد تا ۲٫۵ ثانیه دوباره فرستاده
 *   نمی‌شود (برچسبی که جلوی دوربین مانده دو بار اضافه نشود). `continuous=false` پس از اولین خواندن می‌بندد.
 * - بی هیچ کتابخانه: جایی که `BarcodeDetector` نیست (سافاری، فایرفاکس) یا دوربین رد شد، همان پنجره خانهٔ نوشتن شماره
 *   دارد، پس کار برای همه پیش می‌رود.
 * - پنجره `role="dialog"` با حبس فوکوس و Esc؛ دوربین با بسته شدن یا رفتن از صفحه خاموش می‌شود.
 */
import { nextTick, onBeforeUnmount, ref, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { useFocusTrap } from '@/components/ui/useFocusTrap'
import { message } from '@/lib/messages'

const props = withDefaults(
  defineProps<{
    /** نوشتهٔ دکمه */
    label?: string
    /** پس از هر خواندن باز بماند */
    continuous?: boolean
    disabled?: boolean
    /** دکمهٔ بزرگ ۵۶ پیکسلی (حالت اسکن گوشی) */
    large?: boolean
  }>(),
  { label: 'اسکن', continuous: true, disabled: false, large: false },
)

const emit = defineEmits<{ scanned: [code: string] }>()

interface DetectedCode {
  rawValue: string
}
interface Detector {
  detect(source: HTMLVideoElement): Promise<DetectedCode[]>
}
type DetectorConstructor = new (options: { formats: string[] }) => Detector

/** فاصلهٔ کمینهٔ دو خواندن یکسان. */
const SAME_CODE_MS = 2500

const uid = useId()
const open = ref(false)
const panel = ref<HTMLElement | null>(null)
const video = ref<HTMLVideoElement | null>(null)
const manual = ref('')
const problem = ref('')
const lastRead = ref('')
const scanning = ref(false)

let stream: MediaStream | null = null
let detector: Detector | null = null
let frame = 0
let lastCode = ''
let lastAt = 0

function detectorClass(): DetectorConstructor | null {
  if (typeof window === 'undefined') return null
  return ((window as unknown as { BarcodeDetector?: DetectorConstructor }).BarcodeDetector ?? null)
}

async function start() {
  if (props.disabled) return
  open.value = true
  problem.value = ''
  manual.value = ''
  lastRead.value = ''

  const Detector = detectorClass()
  if (!Detector || !navigator.mediaDevices?.getUserMedia) {
    problem.value = message('E_BOARD_SCAN_UNSUPPORTED')
    return
  }
  try {
    stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false })
    if (!open.value) return stopCamera()
    await nextTick()
    if (video.value) {
      video.value.srcObject = stream
      await video.value.play()
    }
    detector = new Detector({ formats: ['qr_code', 'code_128'] })
    scanning.value = true
    tick()
  } catch (e) {
    stopCamera()
    problem.value = (e as Error)?.name === 'NotAllowedError' ? message('E_BOARD_SCAN_DENIED') : message('E_BOARD_SCAN_CAMERA')
  }
}

function tick() {
  if (!scanning.value || !detector || !video.value) return
  detector
    .detect(video.value)
    .then((codes) => {
      const code = codes.find((c) => c.rawValue && c.rawValue.trim())?.rawValue.trim()
      if (code) found(code)
    })
    .catch(() => {
      /* فریم خوانده نشد؛ فریم بعد */
    })
    .finally(() => {
      if (scanning.value) frame = requestAnimationFrame(tick)
    })
}

function found(code: string) {
  const now = Date.now()
  if (code === lastCode && now - lastAt < SAME_CODE_MS) return
  lastCode = code
  lastAt = now
  lastRead.value = code
  emit('scanned', code)
  if (!props.continuous) close()
}

function submitManual() {
  const code = manual.value.trim()
  if (!code) return
  manual.value = ''
  lastRead.value = code
  emit('scanned', code)
  if (!props.continuous) close()
}

function stopCamera() {
  scanning.value = false
  if (frame) cancelAnimationFrame(frame)
  frame = 0
  stream?.getTracks().forEach((t) => t.stop())
  stream = null
  detector = null
}

function close() {
  stopCamera()
  open.value = false
}

const { onKeydown } = useFocusTrap(panel, open, { onEscape: close })

onBeforeUnmount(stopCamera)

defineExpose({ start, close })
</script>

<template>
  <div class="contents">
    <button
      type="button"
      class="inline-flex shrink-0 items-center justify-center gap-2 rounded-lg border border-outline-variant bg-surface-container-lowest font-semibold text-on-surface hover:bg-surface-container"
      :class="large ? 'min-h-14 px-5 text-base' : 'min-h-11 px-4 text-sm'"
      :disabled="disabled"
      @click="start"
    >
      <AppIcon name="scan" class="h-5 w-5" />
      {{ label }}
    </button>

    <Teleport to="body">
      <div v-if="open" class="fixed inset-0 z-[69] grid place-items-center bg-black/80 p-4" data-no-print @mousedown.self="close">
        <div
          ref="panel"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="`${uid}-title`"
          tabindex="-1"
          class="w-full max-w-md overflow-hidden rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface outline-none"
          @keydown="onKeydown"
        >
          <div class="flex items-center gap-3 border-b border-outline-variant px-4 py-2">
            <h2 :id="`${uid}-title`" class="flex min-w-0 flex-1 items-center gap-2 font-semibold">
              <AppIcon name="scan" class="h-5 w-5" />
              {{ message('I_BOARD_SCAN_TITLE') }}
            </h2>
            <button type="button" class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container" @click="close">
              <AppIcon name="close" class="h-5 w-5" label="بستن" />
            </button>
          </div>

          <div v-show="scanning" class="relative aspect-[4/3] bg-black">
            <video ref="video" class="h-full w-full object-cover" playsinline muted aria-hidden="true"></video>
            <div class="pointer-events-none absolute inset-8 rounded-lg border-2 border-on-primary/80" aria-hidden="true"></div>
          </div>

          <div class="space-y-3 p-4">
            <p v-if="problem" class="rounded-lg bg-surface-container px-3 py-2 text-sm" role="alert">{{ problem }}</p>
            <p v-else class="text-sm text-on-surface-variant">{{ message('I_BOARD_SCAN_HINT') }}</p>
            <p v-if="lastRead" class="flex items-center gap-1.5 text-sm font-semibold text-success" role="status" aria-live="polite" data-numeric>
              <AppIcon name="check" class="h-4 w-4" />
              {{ message('I_BOARD_SCAN_READ', { code: lastRead }) }}
            </p>
            <form class="flex items-end gap-2" @submit.prevent="submitManual">
              <div class="min-w-0 flex-1">
                <label :for="`${uid}-manual`" class="mb-1 block text-sm font-medium">شماره</label>
                <input
                  :id="`${uid}-manual`"
                  v-model="manual"
                  dir="ltr"
                  inputmode="numeric"
                  autocomplete="off"
                  enterkeyhint="send"
                  class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end outline-none focus:border-primary"
                  data-numeric
                />
              </div>
              <button type="submit" class="min-h-11 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover">افزودن</button>
            </form>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
