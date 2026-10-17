<script setup lang="ts">
/**
 * «فرش‌های دیگر…» در پنل ثبت هزینه (H.2، D.2): برای کدام فرش‌ها؟
 * - شماره‌ها: «3778، 3780-3790» (ارقام فارسی هم) → `POST /api/costs/resolve-targets` → «۱۲ فرش · ۱۴ تخته · ۳۲٫۵ متر» و
 *   شماره‌های پیدانشده؛ «همین‌ها» فهرست صریح را می‌دهد (فیلتر زنده نیست؛ همان لحظهٔ ذخیره ثابت می‌شود).
 * - «همهٔ فرش‌های انبار»: همان راه با `inStock`؛ یادداشت «۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵» با ثبت می‌ماند.
 * - یک محموله (هزینهٔ دیر پس از «قطعی» هم مجاز است، D.7 #12)، و در نمای کامل نگله‌های همان محموله.
 *
 * هر دکمه ۴۴ پیکسل؛ نتیجه با `pick` به پنل می‌رود و پنل تراشهٔ «برای» را با آن می‌سازد.
 */
import { computed, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import type { ResolvedTargets } from '@/lib/types/costs'
import { ShipmentStatus, type Shipment, type ShipmentListItem } from '@/lib/types/shipments'
import { balesChipLabel, n, PICKER_TEXT, piecesText, shipmentName as shipmentNameOf, type CostTarget } from './costText'

const props = withDefaults(defineProps<{ allowBales?: boolean }>(), { allowBales: false })

const emit = defineEmits<{ pick: [target: CostTarget]; cancel: [] }>()

const ctx = appContext()
const serials = ref('')
const resolving = ref(false)
const error = ref('')
const found = ref<(ResolvedTargets & { label: string }) | null>(null)

const shipments = ref<ShipmentListItem[]>([])
const shipmentsLoaded = ref(false)
const openShipment = ref<Shipment | null>(null)
const pickedBales = ref<string[]>([])

const textarea = ref<HTMLTextAreaElement | null>(null)

async function resolve(body: { serialsText?: string; inStock?: boolean }, label: (r: ResolvedTargets) => string) {
  resolving.value = true
  error.value = ''
  found.value = null
  try {
    const result = await api.post<ResolvedTargets>('/api/costs/resolve-targets', body)
    found.value = { ...result, label: label(result) }
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    resolving.value = false
  }
}

function findSerials() {
  const text = serials.value.trim()
  if (!text) {
    error.value = message('E_SCOPE_EMPTY_SELECTION')
    textarea.value?.focus()
    return
  }
  void resolve({ serialsText: text }, (r) => `این فرش‌ها (${piecesText(r.pieces, r.lines)})`)
}

function allStock() {
  void resolve({ inStock: true }, (r) => `همهٔ انبار (${piecesText(r.pieces)})`)
}

function useFound() {
  const r = found.value
  if (!r) return
  if (r.rugIds.length === 0) {
    error.value = message('E_SCOPE_EMPTY_SELECTION')
    return
  }
  emit('pick', { kind: 'rugs', rugIds: [...r.rugIds], label: r.label, scopeNote: r.scopeNote || null })
}

const foundSummary = computed(() => {
  const r = found.value
  if (!r) return ''
  return `${n(r.lines)} فرش · ${piecesText(r.pieces)} · ${n(r.areaM2)} متر`
})

async function loadShipments() {
  try {
    const list = await api.get<ShipmentListItem[]>('/api/shipments')
    // باز‌ها اول، بعد تازه‌ترین‌ها؛ فهرست کوتاه بماند
    shipments.value = [...list]
      .sort((a, b) => a.status - b.status || b.number - a.number)
      .slice(0, 12)
  } catch {
    shipments.value = []
  } finally {
    shipmentsLoaded.value = true
  }
}

function shipmentName(s: { number: number }): string {
  return shipmentNameOf(s.number)
}

async function chooseShipment(s: ShipmentListItem) {
  if (!props.allowBales) {
    emit('pick', { kind: 'shipment', shipmentId: s.id, label: `${shipmentName(s)} (${piecesText(s.pieces)})` })
    return
  }
  try {
    openShipment.value = await api.get<Shipment>(`/api/shipments/${s.id}`)
    pickedBales.value = []
  } catch (e) {
    error.value = errorMessage(e)
  }
}

function wholeShipment() {
  const s = openShipment.value
  if (!s) return
  emit('pick', { kind: 'shipment', shipmentId: s.id, label: `${shipmentName(s)} (${piecesText(s.totals.pieces)})` })
}

function toggleBale(id: string) {
  pickedBales.value = pickedBales.value.includes(id)
    ? pickedBales.value.filter((b) => b !== id)
    : [...pickedBales.value, id]
}

function useBales() {
  const s = openShipment.value
  if (!s || pickedBales.value.length === 0) return
  const bales = s.bales.filter((b) => pickedBales.value.includes(b.id))
  const label =
    bales.length === 1
      ? `${ctx.terms.baleOf} ${n(bales[0].number)} · ${shipmentName(s)}`
      : `${balesChipLabel(bales.length, ctx.terms.bale)} · ${shipmentName(s)}`
  emit('pick', { kind: 'bales', shipmentId: s.id, baleIds: bales.map((b) => b.id), label })
}

onMounted(() => {
  textarea.value?.focus()
  void loadShipments()
})
</script>

<template>
  <section class="space-y-4 rounded-xl border border-outline-variant bg-surface-container-low p-4" aria-label="فرش‌های دیگر">
    <div class="space-y-2">
      <label for="cost-target-serials" class="block text-sm font-semibold">شماره‌ها</label>
      <textarea
        id="cost-target-serials"
        ref="textarea"
        v-model="serials"
        rows="2"
        dir="auto"
        placeholder="۳۷۷۸، ۳۷۸۰-۳۷۹۰"
        class="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary"
        data-numeric
        @keydown.enter.exact.prevent="findSerials"
      />
      <div class="flex flex-wrap gap-2">
        <Button variant="primary" icon="search" :loading="resolving" @click="findSerials">پیدا کن</Button>
        <Button icon="warehouse" :disabled="resolving" @click="allStock">همهٔ فرش‌های انبار</Button>
        <Button variant="ghost" @click="emit('cancel')">بستن</Button>
      </div>
    </div>

    <p v-if="error" class="flex items-start gap-2 rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">
      <AppIcon name="error" class="mt-0.5 h-4 w-4" />
      {{ error }}
    </p>

    <div v-if="found" class="space-y-2 rounded-lg border border-outline-variant bg-surface-container-lowest p-3" aria-live="polite">
      <p class="font-medium" data-numeric>{{ foundSummary }}</p>
      <p v-if="found.unresolved.length" class="flex items-start gap-2 text-sm text-warning">
        <AppIcon name="warning" class="mt-0.5 h-4 w-4" />
        <span>{{ message('I_COST_TARGETS_NOT_FOUND', { serials: found.unresolved.join('، ') }) }}</span>
      </p>
      <Button variant="primary" icon="check" :disabled="found.rugIds.length === 0" @click="useFound">همین‌ها</Button>
    </div>

    <div v-if="shipmentsLoaded && shipments.length" class="space-y-2">
      <p class="text-sm font-semibold">{{ PICKER_TEXT.orShipment }}</p>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="s in shipments"
          :key="s.id"
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-sm hover:bg-surface-container"
          :class="openShipment?.id === s.id ? 'border-primary' : ''"
          data-numeric
          @click="chooseShipment(s)"
        >
          <AppIcon :name="s.status === ShipmentStatus.Finalized ? 'lock' : 'truck'" class="h-4 w-4" />
          {{ shipmentName(s) }} ({{ piecesText(s.pieces) }})
        </button>
      </div>
    </div>

    <div v-if="openShipment" class="space-y-2 rounded-lg border border-outline-variant bg-surface-container-lowest p-3">
      <Button variant="primary" icon="truck" @click="wholeShipment">
        کل {{ shipmentName(openShipment) }} ({{ piecesText(openShipment.totals.pieces) }})
      </Button>
      <template v-if="openShipment.bales.length">
        <p class="text-sm text-on-surface-variant">یا فقط این {{ ctx.terms.bales }}:</p>
        <div class="flex flex-wrap gap-2" role="group" :aria-label="ctx.terms.bales">
          <button
            v-for="b in openShipment.bales"
            :key="b.id"
            type="button"
            :aria-pressed="pickedBales.includes(b.id)"
            class="inline-flex min-h-11 min-w-11 items-center justify-center gap-1 rounded-lg border px-3 text-sm"
            :class="pickedBales.includes(b.id) ? 'border-primary bg-primary/10 font-semibold' : 'border-outline-variant hover:bg-surface-container'"
            data-numeric
            @click="toggleBale(b.id)"
          >
            <AppIcon v-if="pickedBales.includes(b.id)" name="check" class="h-4 w-4" />
            {{ n(b.number) }}
          </button>
        </div>
        <Button icon="bale" :disabled="pickedBales.length === 0" @click="useBales">همین {{ ctx.terms.bales }}</Button>
      </template>
    </div>
  </section>
</template>
