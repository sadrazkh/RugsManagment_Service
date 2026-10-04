<script setup lang="ts">
/**
 * مدیریت گرافیکیِ یک «ورودی» (گروه قدیمی، `RugBatch`): فرش‌ها بر اساس «مرحلهٔ جاری» دسته‌بندی می‌شوند
 * (مثلاً ۱۰ فرش در قالیشویی، ۵ در دارکشی). می‌توان زیرمجموعه‌ای را انتخاب کرد و:
 *   • با هم پیش برد یا به مرحلهٔ قبل برگرداند (فقط حالت کامل)
 *   • از انتخاب یک «ورودی تازه» ساخت یا به ورودی دیگری منتقل کرد
 *
 * حالت ساده کارهای کارگاه را نشان نمی‌دهد: همهٔ فرش‌ها در یک ستون، بی‌نام مرحله و بی‌دکمهٔ پیشبرد.
 * مبلغ (سرمایهٔ هر فرش) فقط برای Business دیده می‌شود. نام ورودی تازه با دیالوگ گرفته می‌شود، نه prompt().
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { appContext, isSimpleUi, moneyVisible } from '@/lib/context'
import { faMoney, faNumber } from '@/lib/format'
import { faJalali } from '@/lib/jalali'
import { BATCH_TERMS, rugStatusLabel } from '@/lib/labels'
import { message } from '@/lib/messages'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import Modal from '@/components/ui/Modal.vue'
import type { PagedResult, RugListItem } from '@/lib/types'

const props = defineProps<{ groupId: string; groupName: string }>()

interface Batch { id: string; name: string }

/** سقف تعداد فرشی که این صفحه یکجا نشان می‌دهد (سرور هم روی ۱۰۰ کلمپ می‌کند). */
const PAGE_LIMIT = 100

const simple = isSimpleUi()
const canSeeMoney = moneyVisible()

const inGroup = ref<RugListItem[]>([])
const unassigned = ref<RugListItem[]>([])
/** اگر ورودی از سقف بزرگ‌تر باشد کاربر باید بداند همه را نمی‌بیند */
const inGroupTotal = ref(0)
const unassignedTotal = ref(0)
const batches = ref<Batch[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const info = ref('')
const sel = reactive<Record<string, boolean>>({})
const addPick = reactive<Record<string, boolean>>({})
const moveTarget = ref('')

const otherBatches = computed(() => batches.value.filter((g) => g.id !== props.groupId))
const selectedIds = computed(() => inGroup.value.filter((r) => sel[r.id]).map((r) => r.id))

function currentStage(r: RugListItem): string {
  return r.currentStepNameFa ?? `بدون مرحلهٔ فعال (${rugStatusLabel(r.status, false)})`
}
function progress(r: RugListItem): { done: number; total: number } {
  return { done: r.completedStepCount, total: r.totalStepCount }
}

/** سرمایهٔ یک فرش برای نمایش؛ برای غیر Business همیشه null (سرور هم null می‌فرستد). */
function investment(r: RugListItem): number | null {
  return canSeeMoney && typeof r.totalInvestment === 'number' ? r.totalInvestment : null
}

// دسته‌بندی بر اساس مرحلهٔ جاری (در حالت ساده یک ستون)
const stageGroups = computed(() => {
  if (simple) return inGroup.value.length ? [{ name: `فرش‌های این ${BATCH_TERMS.batch}`, rugs: inGroup.value }] : []
  const map = new Map<string, RugListItem[]>()
  for (const r of inGroup.value) {
    const k = currentStage(r)
    if (!map.has(k)) map.set(k, [])
    map.get(k)!.push(r)
  }
  return [...map.entries()].map(([name, rugs]) => ({ name, rugs }))
})

function toggleStage(rugs: RugListItem[], checked: boolean) {
  for (const r of rugs) sel[r.id] = checked
}
function clearSel() { for (const k of Object.keys(sel)) sel[k] = false }

async function load() {
  loading.value = true
  try {
    // سه کوئری هدفمند به‌جای «همهٔ فرش‌های کارگاه و فیلتر در مرورگر»
    const [mine, free, gs] = await Promise.all([
      api.get<PagedResult<RugListItem>>(`/api/rugs?batchId=${props.groupId}&pageSize=${PAGE_LIMIT}`),
      api.get<PagedResult<RugListItem>>(`/api/rugs?withoutBatch=true&pageSize=${PAGE_LIMIT}`),
      api.get<Batch[]>('/api/batches'),
    ])
    inGroup.value = mine.items
    inGroupTotal.value = mine.totalCount
    unassigned.value = free.items
    unassignedTotal.value = free.totalCount
    batches.value = gs
  } catch (e) { error.value = errorMessage(e) } finally { loading.value = false }
}

async function run(fn: () => Promise<void>) {
  busy.value = true; error.value = ''; info.value = ''
  try { await fn(); clearSel(); await load() }
  catch (e) { error.value = errorMessage(e) }
  finally { busy.value = false }
}

async function advanceSel() {
  if (selectedIds.value.length === 0) return
  await run(async () => {
    const r = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/advance', {
      rugIds: selectedIds.value,
      step: { serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null, pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true, adjustment: null },
    })
    info.value = `${faNumber(r.successCount)} فرش پیش رفت` + (r.failedCount ? `، ${faNumber(r.failedCount)} ناموفق` : '')
  })
}
async function backSel() {
  if (selectedIds.value.length === 0) return
  await run(async () => {
    const r = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/back', { rugIds: selectedIds.value })
    info.value = `${faNumber(r.successCount)} فرش به مرحلهٔ قبل رفت`
  })
}

// ── ورودی تازه از انتخاب‌شده‌ها (دیالوگ به‌جای prompt) ──
const newBatch = reactive({ open: false, name: '', error: '' })

function openNewBatch() {
  if (selectedIds.value.length === 0) return
  Object.assign(newBatch, { open: true, name: '', error: '' })
}

async function createBatch() {
  const name = newBatch.name.trim()
  if (!name) {
    newBatch.error = message('E_BATCH_NAME_REQUIRED')
    return
  }
  const ids = selectedIds.value
  newBatch.open = false
  await run(async () => {
    const g = await api.post<Batch>('/api/batches', { name, description: `جدا شده از «${props.groupName}»`, receivedAt: null })
    await api.post(`/api/batches/${g.id}/rugs`, { rugIds: ids })
    info.value = `${faNumber(ids.length)} فرش به ${BATCH_TERMS.batch} «${name}» منتقل شد`
  })
}

const newBatchPlaceholder = computed(() => `${BATCH_TERMS.batch} ${faJalali(appContext().today)} · حاج رضا`)

async function moveToBatch() {
  if (selectedIds.value.length === 0 || !moveTarget.value) return
  const ids = selectedIds.value
  await run(async () => {
    await api.post(`/api/batches/${moveTarget.value}/rugs`, { rugIds: ids })
    info.value = `${faNumber(ids.length)} فرش منتقل شد`
  })
}
async function addSelected() {
  const ids = Object.keys(addPick).filter((k) => addPick[k])
  if (ids.length === 0) return
  await run(async () => {
    await api.post(`/api/batches/${props.groupId}/rugs`, { rugIds: ids })
    for (const k of Object.keys(addPick)) addPick[k] = false
    info.value = `${faNumber(ids.length)} فرش اضافه شد`
  })
}
async function removeOne(id: string) {
  await run(async () => {
    await api.del(`/api/batches/${props.groupId}/rugs`, { rugIds: [id] })
    info.value = `از ${BATCH_TERMS.batch} بیرون رفت`
  })
}

onMounted(load)
</script>

<template>
  <div class="space-y-5 pb-24">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">{{ error }}</div>
    <div v-if="info" class="rounded-lg bg-success/10 px-4 py-3 text-sm text-success" role="status">{{ info }}</div>
    <div v-if="loading" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center text-on-surface-variant">در حال بارگذاری…</div>

    <template v-else>
      <div class="text-sm text-on-surface-variant">
        <span class="font-bold text-on-surface" data-numeric>{{ faNumber(inGroupTotal) }}</span>
        فرش در این {{ BATCH_TERMS.batch }}<template v-if="!simple">، در {{ faNumber(stageGroups.length) }} مرحله</template>
      </div>

      <!-- اگر ورودی از سقف نمایش بزرگ‌تر است صریح بگو؛ سکوت یعنی «همه را دیدی» که درست نیست -->
      <div v-if="inGroupTotal > inGroup.length"
           class="flex items-start gap-2 rounded-lg bg-warning/10 px-4 py-3 text-sm text-warning">
        <AppIcon name="warning" class="mt-0.5 h-4 w-4" />
        <span>
          فقط {{ faNumber(inGroup.length) }} فرش اول از {{ faNumber(inGroupTotal) }} فرش این {{ BATCH_TERMS.batch }} نمایش داده شده است.
          برای دیدن همه از صفحهٔ فرش‌ها با فیلتر همین {{ BATCH_TERMS.batch }} استفاده کنید.
        </span>
      </div>

      <div v-if="inGroup.length === 0" class="rounded-xl border border-dashed border-outline-variant bg-surface-container-lowest p-8 text-center text-on-surface-variant">
        هنوز فرشی در این {{ BATCH_TERMS.batch }} نیست. از پایین اضافه کنید.
      </div>

      <!-- ستون‌های مرحله‌محور -->
      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <section v-for="sg in stageGroups" :key="sg.name" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-4 shadow-sm">
          <div class="mb-3 flex items-center justify-between gap-2">
            <h2 class="flex items-center gap-2 font-semibold text-primary">
              {{ sg.name }}
              <span class="rounded-full bg-primary/10 px-2 py-0.5 text-xs" data-numeric>{{ faNumber(sg.rugs.length) }}</span>
            </h2>
            <label class="flex min-h-11 items-center gap-1 text-xs text-on-surface-variant">
              <input type="checkbox" class="h-5 w-5 rounded border-outline-variant text-primary"
                     @change="toggleStage(sg.rugs, ($event.target as HTMLInputElement).checked)" />
              همه
            </label>
          </div>
          <ul class="space-y-2">
            <li v-for="r in sg.rugs" :key="r.id" class="flex items-center gap-2 rounded-lg border px-3 py-2"
                :class="sel[r.id] ? 'border-primary bg-primary/5' : 'border-outline-variant'">
              <input type="checkbox" v-model="sel[r.id]" class="h-5 w-5 shrink-0 rounded border-outline-variant text-primary"
                     :aria-label="`انتخاب ${r.title || r.sku}`" />
              <a :href="`/Rugs/Details/${r.id}`" class="min-w-0 flex-1">
                <div class="truncate text-sm font-medium hover:text-primary">{{ r.title || 'بدون عنوان' }}</div>
                <div class="text-xs text-on-surface-variant" dir="ltr">{{ r.sku }}</div>
              </a>
              <div v-if="!simple" class="shrink-0 text-left">
                <div class="text-xs text-on-surface-variant" data-numeric>
                  {{ faNumber(progress(r).done) }}/{{ faNumber(progress(r).total) }}
                </div>
                <div class="mt-0.5 h-1.5 w-12 overflow-hidden rounded-full bg-surface-container"
                     role="img" :aria-label="`${faNumber(progress(r).done)} از ${faNumber(progress(r).total)} مرحله تکمیل شده`">
                  <div class="h-full rounded-full bg-success" :style="{ width: (progress(r).total ? progress(r).done / progress(r).total * 100 : 0) + '%' }"></div>
                </div>
              </div>
              <button type="button" :disabled="busy" @click="removeOne(r.id)"
                      class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-error hover:bg-error-container">
                <AppIcon name="close" class="h-4 w-4" :label="`بیرون بردن ${r.sku} از ${BATCH_TERMS.batch}`" />
              </button>
            </li>
          </ul>
        </section>
      </div>

      <!-- افزودن فرش بدون ورودی -->
      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="text-sm font-semibold text-primary">افزودن فرش به این {{ BATCH_TERMS.batch }}</h2>
          <button :disabled="busy" @click="addSelected" class="min-h-11 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover disabled:opacity-50">افزودن</button>
        </div>
        <div v-if="unassigned.length === 0" class="py-4 text-center text-sm text-on-surface-variant">فرش {{ BATCH_TERMS.none }} پیدا نشد.</div>
        <template v-else>
          <p v-if="unassignedTotal > unassigned.length" class="mb-2 text-xs text-on-surface-variant">
            {{ faNumber(unassigned.length) }} فرش از {{ faNumber(unassignedTotal) }} فرش {{ BATCH_TERMS.none }} نمایش داده شده است.
          </p>
          <ul class="max-h-60 divide-y divide-outline-variant overflow-y-auto">
            <li v-for="r in unassigned" :key="r.id" class="flex items-center gap-3">
              <label class="flex min-h-11 flex-1 items-center gap-3">
                <input type="checkbox" v-model="addPick[r.id]" class="h-5 w-5 shrink-0 rounded border-outline-variant text-primary" />
                <span class="min-w-0">
                  <span class="block truncate text-sm font-medium">{{ r.title || 'بدون عنوان' }}</span>
                  <span class="block font-mono text-xs text-on-surface-variant" dir="ltr">{{ r.sku }}</span>
                </span>
              </label>
              <span v-if="investment(r) !== null" class="shrink-0 text-xs text-on-surface-variant" data-numeric>{{ faMoney(investment(r)) }}</span>
            </li>
          </ul>
        </template>
      </section>
    </template>

    <!-- نوار عملیات زیرمجموعه (چسبان پایین) -->
    <transition name="fade">
      <div v-if="selectedIds.length > 0" class="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-outline-variant bg-surface-container-lowest/95 px-4 pt-3 shadow-lg backdrop-blur" data-no-print>
        <div class="mx-auto flex max-w-[1440px] flex-wrap items-center gap-2">
          <span class="font-bold text-primary" aria-live="polite"><span data-numeric>{{ faNumber(selectedIds.length) }}</span> انتخاب</span>
          <button type="button" @click="clearSel"
                  class="inline-flex min-h-11 items-center rounded-lg px-2 text-sm text-on-surface-variant hover:bg-surface-container">لغو</button>
          <div class="flex-1"></div>
          <template v-if="!simple">
            <button type="button" :disabled="busy" @click="backSel"
                    class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container">
              <AppIcon name="arrow-right" class="h-4 w-4" />
              مرحلهٔ قبل
            </button>
            <button type="button" :disabled="busy" @click="advanceSel"
                    class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-3 text-sm font-semibold text-on-primary hover:bg-primary-hover">
              مرحلهٔ بعد
              <AppIcon name="arrow-left" class="h-4 w-4" />
            </button>
            <span class="mx-1 h-6 w-px bg-outline-variant" aria-hidden="true"></span>
          </template>
          <button type="button" :disabled="busy" @click="openNewBatch"
                  class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-primary px-3 text-sm text-primary hover:bg-primary/10 disabled:opacity-50">
            <AppIcon name="plus" class="h-4 w-4" />
            {{ BATCH_TERMS.newFromSelection }}
          </button>
          <template v-if="otherBatches.length">
            <label class="sr-only" for="group-move-target">{{ BATCH_TERMS.moveTo }}</label>
            <select id="group-move-target" v-model="moveTarget"
                    class="min-h-11 rounded-lg border border-outline-variant bg-surface-container-lowest px-2 text-sm">
              <option value="">انتقال به…</option>
              <option v-for="g in otherBatches" :key="g.id" :value="g.id">{{ g.name }}</option>
            </select>
            <button :disabled="busy || !moveTarget" @click="moveToBatch"
                    class="min-h-11 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container disabled:opacity-50">انتقال</button>
          </template>
        </div>
      </div>
    </transition>

    <!-- نام ورودی تازه -->
    <Modal
      v-model:open="newBatch.open"
      :title="BATCH_TERMS.newFromSelection"
      :description="`${faNumber(selectedIds.length)} فرش انتخاب‌شده به این ${BATCH_TERMS.batch} تازه منتقل می‌شوند.`"
      :close-on-backdrop="false"
    >
      <form @submit.prevent="createBatch">
        <Field :label="`نام ${BATCH_TERMS.batch}`" :error="newBatch.error" required v-slot="f">
          <input
            :id="f.id"
            v-model="newBatch.name"
            :aria-describedby="f.describedBy"
            :aria-invalid="f.invalid || undefined"
            :placeholder="newBatchPlaceholder"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary"
            @input="newBatch.error = ''"
          />
        </Field>
        <div class="mt-6 flex flex-wrap justify-end gap-3">
          <Button @click="newBatch.open = false">انصراف</Button>
          <Button type="submit" variant="primary" icon="check" :loading="busy">ساختن و انتقال</Button>
        </div>
      </form>
    </Modal>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.15s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
