<script setup lang="ts">
/**
 * نوار عملیات دسته‌ای روی لیست فرش‌ها.
 * چک‌باکس‌های [data-rug-select] در Razor رندر می‌شوند؛ این جزیره انتخاب‌ها را جمع کرده،
 * و امکان «پیشبرد گروهیِ مرحلهٔ بعد»، «افزودن به ورودی»، چاپ برچسب و ویرایش گروهی را می‌دهد.
 *
 * حالت ساده (بازرگان و انباردار، یا پیش‌نمایش «نمای ساده»): «افزودن به ورودی»، پیشبرد مرحله و تغییر وضعیت
 * پنهان‌اند (A.3)؛ کارهای کارگاه در «بیشتر» است. مبلغ («قیمت فروش هدف») فقط برای Business دیده و فرستاده
 * می‌شود (A.5)؛ سرور مبلغ غیر null از دیگران را با ۴۰۳ رد می‌کند.
 *
 * درگاه افزونه `[data-bulk-action]`: دکمه‌های فازهای بعد («افزودن به نگله»، «ثبت هزینه برای این‌ها») از
 * `CA/bulk-actions/*.action.ts` می‌آیند (`lib/bulkActions.ts`)؛ این فایل از فاز سکو یخ‌زده است.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { visibleBulkActions, type BulkAction, type BulkActionContext } from '@/lib/bulkActions'
import { isSimpleUi, moneyVisible } from '@/lib/context'
import { currencyLabel, faNumber } from '@/lib/format'
import { EMPTY_GUID } from '@/lib/ids'
import { BATCH_TERMS, MANUAL_RUG_STATUSES, rugStatusLabel } from '@/lib/labels'
import { confirmDialog, toast, toastAfterReload } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import MoneyInput from '@/components/MoneyInput.vue'

interface Batch { id: string; name: string }

const simple = isSimpleUi()
const canSeeMoney = moneyVisible()
const unit = currencyLabel()

/** وضعیت‌هایی که دستی گذاشتنی‌اند (فروخته و صادرشده را خودِ کارشان می‌گذارد). */
const STATUSES = MANUAL_RUG_STATUSES.map((value) => ({ value, label: rugStatusLabel(value, false) }))

const selected = ref<Set<string>>(new Set())
const count = computed(() => selected.value.size)
const batches = ref<Batch[]>([])
const targetBatch = ref('')
const busy = ref(false)

/** دکمه‌های افزونه‌ای که بیننده اجازه دارد ببیند */
const extraActions = visibleBulkActions()

// ── ویرایش گروهی مشخصات ──
const editorOpen = ref(false)
const fields = reactive({
  origin: '', pattern: '', material: '',
  knotDensity: null as number | null,
  targetSalePrice: null as number | null,
  status: '' as string,
  batchId: '' as string,
})
/** فقط فیلدهایی که کاربر صریحاً تیک زده ارسال می‌شوند. */
const touched = reactive<Record<string, boolean>>({})

const touchedCount = computed(() => Object.values(touched).filter(Boolean).length)

function openEditor() {
  editorOpen.value = true
  Object.keys(touched).forEach((k) => (touched[k] = false))
}

async function applyFields() {
  if (touchedCount.value === 0) {
    toast.warning('هیچ فیلدی برای تغییر انتخاب نشده است.')
    return
  }

  const ok = await confirmDialog({
    title: `${faNumber(count.value)} فرش ویرایش شود؟`,
    message: 'فقط فیلدهایی که تیک زده‌اید تغییر می‌کنند؛ بقیه دست‌نخورده می‌مانند.',
    confirmLabel: 'اعمال تغییرات',
  })
  if (!ok) return

  await run(async () => {
    const res = await api.put<{ successCount: number; failedCount: number }>('/api/rugs/bulk/fields', {
      rugIds: [...selected.value],
      // null یعنی «دست نزن» — فیلد بدون تیک اصلاً فرستاده نمی‌شود
      origin: touched.origin ? fields.origin : null,
      pattern: touched.pattern ? fields.pattern : null,
      material: touched.material ? fields.material : null,
      knotDensity: touched.knotDensity ? fields.knotDensity : null,
      // مبلغ فقط از Business؛ برای بقیه همیشه null (سرور null را «دست نزن» می‌خواند)
      targetSalePrice: canSeeMoney && touched.targetSalePrice ? fields.targetSalePrice : null,
      status: !simple && touched.status && fields.status !== '' ? Number(fields.status) : null,
      // رشتهٔ خالی در انتخابگر ورودی = بیرون بردن از ورودی
      batchId: !simple && touched.batchId ? (fields.batchId || EMPTY_GUID) : null,
    })

    if (res.failedCount) {
      toastAfterReload(`${faNumber(res.successCount)} فرش ویرایش شد، ${faNumber(res.failedCount)} ناموفق بود.`, 'warning')
    } else {
      toastAfterReload(`${faNumber(res.successCount)} فرش ویرایش شد.`)
    }
  })
}

function refreshSelection() {
  const set = new Set<string>()
  document.querySelectorAll<HTMLInputElement>('[data-rug-select]:checked').forEach((c) => set.add(c.dataset.rugSelect!))
  selected.value = set
}

function clearAll() {
  document.querySelectorAll<HTMLInputElement>('[data-rug-select]').forEach((c) => (c.checked = false))
  refreshSelection()
}

async function bulkAdvance() {
  if (count.value === 0) return

  const ok = await confirmDialog({
    title: `${faNumber(count.value)} فرش به مرحلهٔ بعد بروند؟`,
    message: 'فرش‌هایی که مرحلهٔ فعالی ندارند نادیده گرفته می‌شوند.',
    confirmLabel: 'پیشبرد همه',
  })
  if (!ok) return

  await run(async () => {
    const res = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/advance', {
      rugIds: [...selected.value],
      step: {
        serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null,
        pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true, adjustment: null,
      },
    })

    if (res.failedCount) {
      toastAfterReload(`${faNumber(res.successCount)} فرش پیش رفت، ${faNumber(res.failedCount)} ناموفق بود.`, 'warning')
    } else {
      toastAfterReload(`${faNumber(res.successCount)} فرش پیش رفت.`)
    }
  })
}

async function addToBatch() {
  if (count.value === 0 || !targetBatch.value) return
  const moved = count.value
  await run(async () => {
    await api.post(`/api/batches/${targetBatch.value}/rugs`, { rugIds: [...selected.value] })
    toastAfterReload(`${faNumber(moved)} فرش به ورودی اضافه شد.`)
  })
}

/** رفتن به صفحهٔ چاپ با فرش‌های انتخاب‌شده — بدون تغییر داده، پس تأیید لازم نیست. */
function printLabels() {
  if (count.value === 0) return
  const params = new URLSearchParams()
  for (const id of selected.value) params.append('rugIds', id)
  window.location.href = `/Labels/Print?${params}`
}

async function run(fn: () => Promise<void>) {
  busy.value = true
  try {
    await fn()
    // پیام موفقیت در sessionStorage است و بعد از بارگذاری روی دادهٔ تازه دیده می‌شود
    window.location.reload()
  } catch (e) {
    toast.error(errorMessage(e))
    busy.value = false
  }
}

/** اجرای یک دکمهٔ افزونه با همان قرارداد (`BulkActionContext`). */
async function runExtra(action: BulkAction) {
  if (count.value === 0 || busy.value) return
  const ctx: BulkActionContext = {
    rugIds: [...selected.value],
    clearSelection: clearAll,
    reload: () => window.location.reload(),
    setBusy: (value) => (busy.value = value),
  }
  try {
    await action.run(ctx)
  } catch (e) {
    toast.error(errorMessage(e))
    busy.value = false
  }
}

onMounted(async () => {
  document
    .querySelectorAll<HTMLInputElement>('[data-rug-select]')
    .forEach((c) => c.addEventListener('change', refreshSelection))
  if (simple) return
  try {
    batches.value = await api.get<Batch[]>('/api/batches')
  } catch {
    /* اگر ورودی‌ای نبود، انتخابگر خالی می‌ماند */
  }
})
</script>

<template>
  <Transition
    enter-active-class="transition duration-200 ease-out"
    enter-from-class="translate-y-full"
    leave-active-class="transition duration-150 ease-in"
    leave-to-class="translate-y-full"
  >
    <div
      v-if="count > 0"
      class="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-outline-variant bg-surface-container-lowest/95 px-4 pt-3 shadow-lg backdrop-blur"
      role="region"
      aria-label="عملیات دسته‌ای"
      data-no-print
    >
      <!-- ویرایش گروهی مشخصات — بالای نوار باز می‌شود -->
      <div v-if="editorOpen" class="mx-auto mb-3 max-w-[1440px] rounded-lg border border-outline-variant bg-surface-container p-3">
        <p class="mb-2 text-xs text-on-surface-variant">
          فقط فیلدهایی که تیک بزنید روی
          <span data-numeric>{{ faNumber(count) }}</span>
          فرش اعمال می‌شوند. بقیه دست‌نخورده می‌مانند.
        </p>

        <div class="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          <label class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.origin" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              اصالت
            </span>
            <input v-model="fields.origin" :disabled="!touched.origin" class="fld" placeholder="خالی = پاک کردن" />
          </label>

          <label class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.pattern" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              طرح
            </span>
            <input v-model="fields.pattern" :disabled="!touched.pattern" class="fld" placeholder="خالی = پاک کردن" />
          </label>

          <label class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.material" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              جنس
            </span>
            <input v-model="fields.material" :disabled="!touched.material" class="fld" placeholder="خالی = پاک کردن" />
          </label>

          <label class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.knotDensity" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              رجشمار
            </span>
            <input v-model.number="fields.knotDensity" :disabled="!touched.knotDensity" type="number" dir="ltr" class="fld" />
          </label>

          <label v-if="canSeeMoney" class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.targetSalePrice" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              قیمت فروش هدف
            </span>
            <MoneyInput v-if="touched.targetSalePrice" v-model="fields.targetSalePrice" :suffix="unit" />
            <input v-else disabled class="fld" />
          </label>

          <label v-if="!simple" class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.status" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              وضعیت
            </span>
            <select v-model="fields.status" :disabled="!touched.status" class="fld">
              <option value="">— انتخاب —</option>
              <option v-for="s in STATUSES" :key="s.value" :value="String(s.value)">{{ s.label }}</option>
            </select>
          </label>

          <label v-if="!simple" class="block">
            <span class="mb-1 flex items-center gap-1.5 text-xs">
              <input v-model="touched.batchId" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
              {{ BATCH_TERMS.batch }}
            </span>
            <select v-model="fields.batchId" :disabled="!touched.batchId" class="fld">
              <option value="">— {{ BATCH_TERMS.removeFrom }} —</option>
              <option v-for="b in batches" :key="b.id" :value="b.id">{{ b.name }}</option>
            </select>
          </label>

          <div class="flex items-end gap-2">
            <button type="button" :disabled="busy || touchedCount === 0"
                    class="inline-flex min-h-11 flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
                    @click="applyFields">
              <AppIcon name="check" class="h-4 w-4" />
              اعمال
            </button>
            <button type="button"
                    class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container-high"
                    @click="editorOpen = false">بستن</button>
          </div>
        </div>
      </div>

      <div class="mx-auto flex max-w-[1440px] flex-wrap items-center gap-3">
        <span class="font-bold text-primary" aria-live="polite"><span data-numeric>{{ faNumber(count) }}</span> انتخاب‌شده</span>
        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-1 rounded-lg px-2 text-sm text-on-surface-variant hover:bg-surface-container"
          @click="clearAll"
        >
          <AppIcon name="close" class="h-4 w-4" />
          لغو انتخاب
        </button>

        <div class="flex-1"></div>

        <!-- درگاه افزونه: دکمه‌های فازهای بعد -->
        <div v-if="extraActions.length" class="contents" data-bulk-action-slot>
          <button
            v-for="action in extraActions"
            :key="action.key"
            type="button"
            :data-bulk-action="action.key"
            :disabled="busy"
            class="inline-flex min-h-11 items-center gap-2 rounded-lg px-4 text-sm"
            :class="action.primary
              ? 'bg-primary font-semibold text-on-primary hover:bg-primary-hover'
              : 'border border-outline-variant hover:bg-surface-container'"
            @click="runExtra(action)"
          >
            <AppIcon :name="action.icon" class="h-4 w-4" />
            {{ action.label }}
          </button>
        </div>

        <template v-if="!simple">
          <label class="sr-only" for="bulk-batch">{{ BATCH_TERMS.batch }} مقصد</label>
          <select
            id="bulk-batch"
            v-model="targetBatch"
            class="min-h-11 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-sm outline-none focus:border-primary"
          >
            <option value="">— {{ BATCH_TERMS.batch }} —</option>
            <option v-for="b in batches" :key="b.id" :value="b.id">{{ b.name }}</option>
          </select>

          <button
            type="button"
            :disabled="busy || !targetBatch"
            class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
            @click="addToBatch"
          >
            <AppIcon name="package" class="h-4 w-4" />
            {{ BATCH_TERMS.addTo }}
          </button>
        </template>

        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
          @click="printLabels"
        >
          <AppIcon name="printer" class="h-4 w-4" />
          چاپ برچسب
        </button>

        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
          :aria-expanded="editorOpen"
          @click="editorOpen ? (editorOpen = false) : openEditor()"
        >
          <AppIcon name="edit" class="h-4 w-4" />
          ویرایش گروهی
        </button>

        <button
          v-if="!simple"
          type="button"
          :disabled="busy"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
          @click="bulkAdvance"
        >
          <AppIcon name="check" class="h-4 w-4" />
          پیشبرد مرحلهٔ بعد
        </button>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.fld {
  min-height: 2.75rem;
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  padding: 0 0.75rem;
  outline: none;
  font-size: 0.875rem;
}
.fld:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
.fld:disabled {
  opacity: 0.45;
}
</style>
