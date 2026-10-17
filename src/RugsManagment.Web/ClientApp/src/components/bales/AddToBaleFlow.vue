<script setup lang="ts">
/**
 * «افزودن به نگله» از نوار گروهی فهرست فرش‌ها (H.1، A.4): فرش‌های انتخاب‌شده را به یک نگلهٔ محمولهٔ هدف می‌برد.
 *
 * ۱. محمولهٔ هدف: هیچ محمولهٔ بازی نیست → بازرگان «محمولهٔ تازه» (یک لمس، C.1) و انباردار «از بازرگان بخواهید یکی
 *    بسازد»؛ یکی → همان؛ چندتا → دکمه‌های ۵۶ پیکسلی «محمولهٔ ۸ (۱۴۰۵/۰۶/۱۵) — ۱۲ نگله»، تازه‌ترین با فوکوس.
 * ۲. نگله: «نگلهٔ جاری» همان محموله اول (همان یادآوری تختهٔ نگله‌بندی)، بقیه به ترتیب، و «نگلهٔ تازه».
 * ۳. یک درخواست `lines/batch`؛ فرش‌هایی که در نگلهٔ دیگری‌اند یک پرسش می‌گیرند: «۳ فرش در نگلهٔ دیگری است؛ منتقل شوند؟».
 *
 * پایان: پیام پس از تازه‌سازی و تازه‌سازی صفحه (`done`)؛ نگلهٔ انتخاب‌شده «جاری» آن محموله می‌شود تا زبانهٔ نگله‌ها از
 * همان‌جا ادامه دهد.
 */
import { computed, nextTick, onMounted, ref, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import Modal from '@/components/ui/Modal.vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { newId } from '@/lib/ids'
import { faJalali } from '@/lib/jalali'
import { message } from '@/lib/messages'
import { ShipmentStatus, type Bale, type LineOp, type LinesBatchResult, type Shipment, type ShipmentListItem } from '@/lib/types/shipments'
import { currentBaleKey, faDigitsOf, maxBaleNumber, readCurrentBale, resolveCurrent, writeCurrentBale } from './baleModel'

const props = defineProps<{
  rugIds: readonly string[]
  /** پایان: `added` تعداد افزوده‌ها؛ `message` پیام پس از تازه‌سازی (اگر چیزی رفت) */
  done: (outcome: { added: number; message: string | null }) => void
}>()

type Step = 'loading' | 'shipment' | 'none' | 'bale' | 'saving' | 'move' | 'error'

const ctx = appContext()
const terms = ctx.terms
const uid = useId()
const open = ref(true)
const step = ref<Step>('loading')
const error = ref('')
const shipments = ref<ShipmentListItem[]>([])
const shipment = ref<{ id: string; number: number } | null>(null)
const bales = ref<Bale[]>([])
const current = ref(1)
const chosen = ref<number | null>(null)
const moveIds = ref<string[]>([])
const added = ref(0)
const failures = ref<string[]>([])
const creating = ref(false)
const body = ref<HTMLElement | null>(null)

const count = computed(() => props.rugIds.length)
const title = computed(() => {
  switch (step.value) {
    case 'shipment':
      return message('I_BOARD_CHOOSE_SHIPMENT')
    default:
      return message('I_BOARD_ADD_MANY_TITLE', { n: count.value })
  }
})

const orderedBales = computed(() => {
  const list = [...bales.value].sort((a, b) => a.number - b.number)
  const cur = list.find((b) => b.number === current.value)
  return cur ? [cur, ...list.filter((b) => b !== cur)] : list
})
const fresh = computed(() => Math.max(maxBaleNumber(bales.value) + 1, 1))

function focusFirst() {
  void nextTick(() => body.value?.querySelector<HTMLElement>('[data-first], button')?.focus())
}

async function start() {
  try {
    const list = await api.get<ShipmentListItem[]>(`/api/shipments?status=${ShipmentStatus.Open}`)
    shipments.value = list.filter((s) => s.status === ShipmentStatus.Open)
    if (shipments.value.length === 0) step.value = 'none'
    else if (shipments.value.length === 1) await chooseShipment(shipments.value[0])
    else step.value = 'shipment'
    focusFirst()
  } catch (e) {
    error.value = errorMessage(e)
    step.value = 'error'
  }
}

async function chooseShipment(s: { id: string; number: number }) {
  shipment.value = { id: s.id, number: s.number }
  step.value = 'loading'
  try {
    bales.value = await api.get<Bale[]>(`/api/shipments/${s.id}/bales`)
    current.value = resolveCurrent(readCurrentBale(currentBaleKey(ctx.tenantId, ctx.userId, s.id)), bales.value)
    step.value = 'bale'
    focusFirst()
  } catch (e) {
    error.value = errorMessage(e)
    step.value = 'error'
  }
}

/** «محمولهٔ تازه» با یک لمس (Business؛ C.1). */
async function createShipment() {
  creating.value = true
  try {
    const created = await api.post<Shipment>('/api/shipments', { documentDate: ctx.today })
    await chooseShipment(created)
  } catch (e) {
    error.value = errorMessage(e)
    step.value = 'error'
  } finally {
    creating.value = false
  }
}

async function send(ops: LineOp[]): Promise<LinesBatchResult | null> {
  if (!shipment.value) return null
  try {
    return await api.post<LinesBatchResult>(`/api/shipments/${shipment.value.id}/lines/batch`, { ops })
  } catch (e) {
    error.value = errorMessage(e)
    step.value = 'error'
    return null
  }
}

async function chooseBale(number: number) {
  chosen.value = number
  step.value = 'saving'
  const ops: LineOp[] = props.rugIds.map((rugId) => ({ op: 'add', clientOpId: newId(), rugId, baleNumber: number }))
  const result = await send(ops)
  if (!result) return
  collect(result, props.rugIds)
  if (moveIds.value.length > 0) {
    step.value = 'move'
    focusFirst()
    return
  }
  finish()
}

function collect(result: LinesBatchResult, rugIds: readonly string[]) {
  const moves: string[] = []
  result.results.forEach((r, i) => {
    if (r.ok) added.value++
    else if (r.prompt && (r.prompt.code === 'Q_LINE_MOVE' || r.prompt.code === 'Q_LINE_MOVE_SAME')) moves.push(rugIds[i])
    else if (r.errors.length > 0) failures.value.push(r.errors[0].message)
  })
  moveIds.value = moves
}

async function confirmMove(yes: boolean) {
  const ids = moveIds.value
  moveIds.value = []
  if (!yes || chosen.value === null) return finish()
  step.value = 'saving'
  const number = chosen.value
  const result = await send(ids.map((rugId) => ({ op: 'add', clientOpId: newId(), rugId, baleNumber: number, move: true })))
  if (!result) return
  collect(result, ids)
  finish()
}

function finish() {
  const s = shipment.value
  if (s && chosen.value !== null && added.value > 0) writeCurrentBale(currentBaleKey(ctx.tenantId, ctx.userId, s.id), chosen.value)
  const parts: string[] = []
  if (s && added.value > 0) parts.push(message('I_BOARD_ADDED_MANY', { n: added.value, number: chosen.value ?? 0, shipment: s.number }))
  if (failures.value.length > 0) {
    parts.push(message('E_BOARD_ADD_SOME_FAILED', { n: failures.value.length, reason: [...new Set(failures.value)].join(' ') }))
  }
  open.value = false
  props.done({ added: added.value, message: parts.length > 0 ? parts.join(' ') : null })
}

function cancel() {
  open.value = false
  props.done({ added: added.value, message: null })
}

function choiceText(s: ShipmentListItem) {
  return message('I_BOARD_SHIPMENT_CHOICE', { number: s.number, date: faJalali(s.documentDate), count: s.bales })
}

onMounted(start)
</script>

<template>
  <Modal :open="open" :title="title" size="md" :close-on-backdrop="false" @update:open="(v) => (v ? null : cancel())">
    <div ref="body" class="space-y-4" data-numeric>
      <p v-if="step === 'loading' || step === 'saving'" class="flex items-center gap-2 text-sm text-on-surface-variant" role="status">
        <span class="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden="true"></span>
        {{ step === 'saving' ? message('I_SAVING') : 'در حال خواندن…' }}
      </p>

      <!-- کدام محموله؟ (A.4) -->
      <div v-else-if="step === 'shipment'" class="grid gap-2" role="group" :aria-label="message('I_BOARD_CHOOSE_SHIPMENT')">
        <button
          v-for="(s, i) in shipments"
          :key="s.id"
          type="button"
          :data-first="i === 0 ? '' : undefined"
          class="inline-flex min-h-14 items-center gap-2 rounded-lg border px-4 text-start text-base"
          :class="i === 0 ? 'border-primary bg-primary/10 font-semibold text-primary' : 'border-outline-variant hover:bg-surface-container'"
          @click="chooseShipment(s)"
        >
          <AppIcon name="truck" class="h-5 w-5 shrink-0" />
          {{ choiceText(s) }}
        </button>
      </div>

      <!-- محمولهٔ بازی نیست -->
      <div v-else-if="step === 'none'" class="space-y-3">
        <p class="text-sm">{{ ctx.policies.business ? message('I_BOARD_NO_OPEN_SHIPMENT_BUSINESS') : message('I_BOARD_NO_OPEN_SHIPMENT') }}</p>
        <Button v-if="ctx.policies.business" variant="primary" size="lg" icon="plus" :loading="creating" data-first @click="createShipment">
          محمولهٔ تازه
        </Button>
      </div>

      <!-- کدام نگله؟ -->
      <div v-else-if="step === 'bale'" class="space-y-2">
        <p class="text-sm text-on-surface-variant">محمولهٔ {{ faDigitsOf(shipment?.number ?? 0) }}</p>
        <div :id="`${uid}-bales`" role="group" :aria-label="terms.bales" class="grid grid-cols-[repeat(auto-fill,minmax(3.5rem,1fr))] gap-2">
          <button
            v-for="(b, i) in orderedBales"
            :key="b.id"
            type="button"
            :data-first="i === 0 ? '' : undefined"
            :aria-label="`${terms.baleOf} ${faDigitsOf(b.number)}${b.number === current ? ` (${message('I_BOARD_CURRENT')})` : ''}`"
            class="flex min-h-12 flex-col items-center justify-center rounded-lg border text-lg font-bold"
            :class="b.number === current ? 'border-primary bg-primary/10 text-primary' : 'border-outline-variant hover:bg-surface-container'"
            @click="chooseBale(b.number)"
          >
            {{ faDigitsOf(b.number) }}
            <span v-if="b.number === current" class="text-[0.65rem] font-medium leading-none" aria-hidden="true">جاری</span>
          </button>
          <button
            type="button"
            :data-first="orderedBales.length === 0 ? '' : undefined"
            class="col-span-2 flex min-h-12 items-center justify-center gap-1.5 rounded-lg border border-dashed border-outline px-2 text-sm font-semibold hover:bg-surface-container"
            @click="chooseBale(fresh)"
          >
            <AppIcon name="plus" class="h-5 w-5" />
            {{ message('I_BOARD_NEW_BALE') }} ({{ faDigitsOf(fresh) }})
          </button>
        </div>
      </div>

      <!-- منتقل شوند؟ -->
      <div v-else-if="step === 'move'" class="space-y-3">
        <p class="font-medium">{{ message('Q_BOARD_MOVE_MANY', { n: moveIds.length }) }}</p>
        <div class="flex flex-wrap gap-2">
          <Button variant="primary" icon="move" data-first @click="confirmMove(true)">منتقل کن</Button>
          <Button @click="confirmMove(false)">نه</Button>
        </div>
      </div>

      <p v-else-if="step === 'error'" class="rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">{{ error }}</p>
    </div>

    <template #footer>
      <Button variant="ghost" :disabled="step === 'saving'" @click="cancel">{{ step === 'error' || step === 'none' ? 'بستن' : 'انصراف' }}</Button>
    </template>
  </Modal>
</template>
