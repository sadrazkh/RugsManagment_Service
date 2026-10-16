<script setup lang="ts">
/**
 * جزیرهٔ `pack-mode` (`/Shipments/{id}/Pack`، گوشی؛ G.5): نگله‌بندی سرپا در انبار.
 *
 * - سرِ «نگلهٔ جاری» با جمع زنده و نوار هدف؛ جمع‌ها در `aria-live` خوانده می‌شوند.
 * - خانهٔ بزرگ شماره (`inputmode="numeric"`) و دکمهٔ اسکن (`BarcodeScan`): هر شماره یا برچسب خوانده‌شده به نگلهٔ جاری
 *   می‌رود و صفحه عوض نمی‌شود.
 * - «نگلهٔ بعد» فقط شمارهٔ جاری را بالا می‌برد؛ نگله را اولین فرشش می‌سازد (C.2).
 * - آخرین ۵ فرش این نوبت، هر کدام با «برگردان» (خروج از نگله، که خودش «برگردان» دارد).
 * - «اندازهٔ نهایی را هم بپرس» (اختیاری): پس از هر افزودن برگهٔ اندازه باز می‌شود.
 * - قطع اینترنت: افزودن روی همین دستگاه می‌ماند و فقط با «الان دوباره بفرست» می‌رود (G.7؛ بی فرستادن خودکار).
 *
 * همان مغز زبانهٔ نگله‌ها (`useBalingBoard`)، پس قاعده‌ها یکی است.
 */
import { computed, nextTick, onMounted, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import CurrentBaleHeader from '@/components/bales/CurrentBaleHeader.vue'
import FinalSizeSheet from '@/components/bales/FinalSizeSheet.vue'
import LotSplitDialog from '@/components/bales/LotSplitDialog.vue'
import { faDigitsOf, serialText } from '@/components/bales/baleModel'
import { useBalingBoard, type SplitRequest } from '@/components/bales/useBalingBoard'
import BarcodeScan from '@/components/scan/BarcodeScan.vue'
import Button from '@/components/ui/Button.vue'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import type { FinalSizeInput, RecentAdd } from '@/lib/types/bales'
import type { SplitRugResult } from '@/lib/types/rugs'

const props = withDefaults(
  defineProps<{
    shipmentId: string
    baleId?: string | null
    locked?: boolean
    targetAreaM2?: number | null
    tolerancePercent?: number
    shipmentNumber?: number | null
    backUrl?: string | null
  }>(),
  { baleId: null, locked: false, targetAreaM2: null, tolerancePercent: 10, shipmentNumber: null, backUrl: null },
)

const ctx = appContext()
const terms = ctx.terms
const uid = useId()

// ── «اندازهٔ نهایی را هم بپرس» (برای هر کاربر) ─────────────────────────

const ASK_KEY = `rugs:pack-ask-size:${ctx.tenantId ?? ''}:${ctx.userId ?? ''}`
function readAsk(): boolean {
  try {
    return typeof localStorage !== 'undefined' && localStorage.getItem(ASK_KEY) === '1'
  } catch {
    return false
  }
}
const askSize = ref(readAsk())
function setAsk(value: boolean) {
  askSize.value = value
  try {
    localStorage.setItem(ASK_KEY, value ? '1' : '0')
  } catch {
    /* فقط یادآوری از دست می‌رود */
  }
}

// ── مغز ─────────────────────────────────────────────────────────────

const sheetOpen = ref(false)
const sheetLine = ref<string | null>(null)

const board = useBalingBoard({
  shipmentId: () => props.shipmentId,
  locked: () => props.locked,
  targetAreaM2: () => props.targetAreaM2,
  shipmentNumber: () => props.shipmentNumber,
  initialBaleId: () => props.baleId,
  source: 'pack-mode',
  onAdded: (row, via) => {
    if (via === 'undo') return
    if (askSize.value) {
      sheetLine.value = row.lineId
      sheetOpen.value = true
    }
  },
  onSplitRequested: (request) => {
    split.value = request
    splitOpen.value = true
  },
})

const locked = computed(() => board.locked.value)
const lockText = computed(() =>
  ctx.policies.business && props.shipmentNumber !== null
    ? message('E_SHIPMENT_LOCKED_HEADER', { number: props.shipmentNumber })
    : message('E_SHIPMENT_LOCKED_OPERATOR'),
)
const backHref = computed(() => props.backUrl || `/Shipments/${props.shipmentId}?tab=bales`)
const title = computed(() =>
  props.shipmentNumber !== null ? `محمولهٔ ${faDigitsOf(props.shipmentNumber)} · حالت اسکن` : 'حالت اسکن',
)

// ── شماره و اسکن ─────────────────────────────────────────────────────

const serialInput = ref<HTMLInputElement | null>(null)
const serial = ref('')
const adding = ref(false)

async function submit() {
  const text = serial.value.trim()
  if (!text || adding.value) return
  adding.value = true
  try {
    const row = await board.addBySerial(text)
    if (row || board.question.value || board.pending.value.some((p) => p.serial === text)) serial.value = ''
  } finally {
    adding.value = false
    if (!sheetOpen.value && !board.question.value) void nextTick(() => serialInput.value?.focus())
  }
}

function onScanned(code: string) {
  void board.addBySerial(code)
}

async function answer(index: number) {
  await board.answer(index)
  if (!board.question.value && !sheetOpen.value) void nextTick(() => serialInput.value?.focus())
}

const questionBox = ref<HTMLElement | null>(null)
watch(
  () => board.question.value?.id,
  (id) => {
    if (id) void nextTick(() => questionBox.value?.querySelector<HTMLElement>('button')?.focus())
  },
)

function onQuestionKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    void answer(-1)
  }
}

// ── آخرین فرش‌ها ─────────────────────────────────────────────────────

async function undoRecent(item: RecentAdd) {
  const row = board.findRow(item.lineId)
  if (row) await board.removeRow(row)
}

// ── برگهٔ اندازه ─────────────────────────────────────────────────────

const sheetRow = computed(() => (sheetLine.value ? board.findRow(sheetLine.value) : null))

async function sheetSave(size: FinalSizeInput): Promise<boolean> {
  const row = sheetRow.value
  if (!row) return false
  return (await board.measure(row, size)) !== null
}

function onSheetOpen(value: boolean) {
  sheetOpen.value = value
  if (!value) void nextTick(() => serialInput.value?.focus())
}

// ── جدا کردن دسته ────────────────────────────────────────────────────

const splitOpen = ref(false)
const split = ref<SplitRequest | null>(null)

async function onSplitDone(result: SplitRugResult) {
  board.notify(message('I_SPLIT_DONE', { taken: result.created.quantity }), 'success')
  await board.reload()
  const retry = split.value?.retrySerial
  split.value = null
  if (retry) await board.addBySerial(retry)
}

onMounted(async () => {
  await board.load()
  if (!locked.value) serialInput.value?.focus()
})
</script>

<template>
  <div class="space-y-4 pb-24" data-pack-mode>
    <!--
      سربرگ صفحه (بازگشت به نگله‌ها، شمارهٔ محموله) و پیام قفلِ بار اول بیرون از جزیره‌اند (Pack.cshtml)؛ این‌جا فقط وقتی
      محموله همین حالا قطعی شد. بی آن صفحه (props.backUrl نیامده) جزیره سربرگ خودش را دارد.
    -->
    <header v-if="!props.backUrl" class="flex items-center gap-2">
      <a :href="backHref" class="grid h-11 w-11 shrink-0 place-items-center rounded-lg border border-outline-variant hover:bg-surface-container">
        <AppIcon name="arrow-right" :label="`بازگشت به ${terms.bales}`" />
      </a>
      <h1 class="min-w-0 flex-1 truncate text-xl font-bold" data-numeric>{{ title }}</h1>
    </header>

    <p v-if="locked && !props.locked" class="flex items-start gap-2 rounded-lg bg-surface-container-high px-4 py-3 text-sm" role="alert" data-numeric>
      <AppIcon name="lock" class="mt-0.5 h-4 w-4 shrink-0" />
      <span>{{ lockText }}</span>
    </p>

    <p v-if="board.loadError.value" class="rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">{{ board.loadError.value }}</p>

    <template v-if="!locked && board.loaded.value">
      <CurrentBaleHeader
        large
        :bales="board.bales.value"
        :current="board.current.value"
        :target="targetAreaM2"
        :tolerance-percent="tolerancePercent"
        @update:current="board.setCurrent"
        @next="
          () => {
            board.nextBale()
            serialInput?.focus()
          }
        "
      />

      <form class="space-y-2" @submit.prevent="submit">
        <label :for="`${uid}-serial`" class="block text-base font-medium">{{ message('I_BOARD_SERIAL_HINT') }}</label>
        <input
          :id="`${uid}-serial`"
          ref="serialInput"
          v-model="serial"
          dir="ltr"
          inputmode="numeric"
          enterkeyhint="go"
          autocomplete="off"
          autocapitalize="off"
          spellcheck="false"
          data-hotkeys-off
          class="min-h-14 w-full rounded-xl border border-outline-variant bg-surface-container-lowest px-4 text-end text-2xl font-bold outline-none focus:border-primary focus:ring-2 focus:ring-primary"
          :aria-invalid="board.lastError.value ? 'true' : undefined"
          :aria-describedby="board.lastError.value ? `${uid}-error` : undefined"
          data-numeric
        />
        <div class="grid grid-cols-2 gap-2">
          <Button variant="primary" size="lg" type="submit" icon="plus" :loading="adding" block>
            {{ message('I_BOARD_ADD_TO', { n: board.current.value }) }}
          </Button>
          <BarcodeScan large :label="message('I_BOARD_SCAN_TITLE')" @scanned="onScanned" />
        </div>
        <p v-if="board.lastError.value" :id="`${uid}-error`" class="flex items-start gap-1 text-sm text-error" role="alert">
          <AppIcon name="error" class="mt-0.5 h-4 w-4 shrink-0" />
          {{ board.lastError.value }}
        </p>
      </form>

      <label class="flex min-h-11 cursor-pointer items-center gap-3 rounded-lg px-1 text-sm">
        <input type="checkbox" class="h-5 w-5 accent-primary" :checked="askSize" @change="setAsk(($event.target as HTMLInputElement).checked)" />
        {{ message('I_BOARD_ASK_SIZE') }}
      </label>
    </template>

    <!-- پرسش (منتقل شود؟ / داخل دسته) -->
    <div
      v-if="board.question.value"
      ref="questionBox"
      class="space-y-3 rounded-xl border border-secondary/50 bg-secondary-container/50 p-4"
      role="group"
      :aria-label="board.question.value.message"
      @keydown="onQuestionKeydown"
    >
      <p class="font-medium" aria-live="assertive" data-numeric>{{ board.question.value.message }}</p>
      <div class="grid gap-2">
        <button
          v-for="(choice, index) in board.question.value.choices"
          :key="index"
          type="button"
          class="inline-flex min-h-14 items-center justify-center gap-2 rounded-lg px-4 text-base"
          :class="choice.primary ? 'bg-primary font-semibold text-on-primary' : 'border border-outline-variant bg-surface-container-lowest text-on-surface'"
          @click="answer(index)"
        >
          <AppIcon v-if="choice.icon" :name="choice.icon" class="h-5 w-5" />
          {{ choice.label }}
        </button>
      </div>
    </div>

    <!-- قطع اینترنت (G.7) -->
    <div v-if="board.pending.value.length > 0" class="space-y-2 rounded-xl border border-warning/40 bg-surface-container-low p-4" role="alert">
      <p class="flex items-start gap-2 text-sm">
        <AppIcon name="warning" class="mt-0.5 h-5 w-5 shrink-0 text-warning" />
        {{ message('I_BOARD_PENDING', { n: board.pending.value.length }) }}
      </p>
      <ul class="flex flex-wrap gap-2 text-sm" data-numeric>
        <li v-for="p in board.pending.value" :key="p.clientOpId" class="inline-flex items-center gap-1 rounded-full bg-surface-container px-3 py-1">
          <bdi dir="ltr">{{ serialText(p.serial) }}</bdi>
          <button
            type="button"
            class="-me-2 grid h-9 w-9 place-items-center rounded-full hover:bg-surface-container-high"
            :aria-label="`کنار گذاشتن ${serialText(p.serial)}`"
            @click="board.discardPending(p.clientOpId)"
          >
            <AppIcon name="close" class="h-4 w-4" />
          </button>
        </li>
      </ul>
      <Button variant="primary" size="lg" icon="upload" block :loading="board.busy.value > 0" @click="board.retryPending()">الان دوباره بفرست</Button>
    </div>

    <!-- آخرین فرش‌های این نوبت -->
    <section v-if="board.recent.value.length > 0" :aria-labelledby="`${uid}-recent`" class="space-y-2">
      <h2 :id="`${uid}-recent`" class="text-base font-semibold">{{ message('I_BOARD_RECENT') }}</h2>
      <ul class="divide-y divide-outline-variant/60 rounded-xl border border-outline-variant bg-surface-container-lowest">
        <li v-for="item in board.recent.value" :key="item.lineId" class="flex items-center gap-3 px-4 py-2" data-numeric>
          <div class="min-w-0 flex-1">
            <p class="text-lg font-bold"><bdi dir="ltr">{{ serialText(item.serial) }}</bdi></p>
            <p class="text-sm text-on-surface-variant">{{ terms.baleOf }} {{ faDigitsOf(item.baleNumber) }}</p>
          </div>
          <button
            v-if="!locked && board.findRow(item.lineId)"
            type="button"
            class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container"
            :aria-label="`برگردان ${serialText(item.serial)}`"
            @click="undoRecent(item)"
          >
            <AppIcon name="undo" class="h-5 w-5" />
            برگردان
          </button>
        </li>
      </ul>
    </section>

    <!-- پیام‌ها با «برگردان» -->
    <div class="fixed inset-x-3 bottom-3 z-[55] flex flex-col gap-2 pb-safe" role="status" aria-live="polite">
      <div
        v-for="notice in board.notices.value"
        :key="notice.id"
        class="flex items-center gap-3 rounded-xl border bg-surface-container-lowest px-4 py-2 text-sm text-on-surface shadow-lg"
        :class="notice.kind === 'error' ? 'border-error/50' : notice.kind === 'success' ? 'border-success/50' : 'border-outline-variant'"
      >
        <AppIcon
          :name="notice.kind === 'error' ? 'error' : notice.kind === 'success' ? 'success' : notice.kind === 'warning' ? 'warning' : 'info'"
          class="h-5 w-5 shrink-0"
          :class="notice.kind === 'error' ? 'text-error' : notice.kind === 'success' ? 'text-success' : notice.kind === 'warning' ? 'text-warning' : 'text-on-surface-variant'"
        />
        <p class="min-w-0 flex-1" data-numeric>{{ notice.text }}</p>
        <button
          v-if="notice.action"
          type="button"
          class="min-h-11 shrink-0 rounded-lg px-3 font-semibold text-primary"
          @click="
            () => {
              board.dismissNotice(notice.id)
              void notice.action?.run()
            }
          "
        >{{ notice.action.label }}</button>
        <button type="button" class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-on-surface-variant" aria-label="بستن" @click="board.dismissNotice(notice.id)">
          <AppIcon name="close" class="h-4 w-4" />
        </button>
      </div>
    </div>

    <FinalSizeSheet
      :open="sheetOpen"
      :row="sheetRow"
      :save="sheetSave"
      :can-undo="board.undoCount.value > 0"
      :locked="locked"
      @update:open="onSheetOpen"
      @undo="board.undo()"
    />
    <LotSplitDialog
      v-model:open="splitOpen"
      :rug-id="split?.rugId ?? null"
      :serial="split?.serial ?? null"
      :quantity="split?.quantity ?? 0"
      @done="onSplitDone"
    />
  </div>
</template>
