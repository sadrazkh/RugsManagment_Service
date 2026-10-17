<script setup lang="ts">
/**
 * فهرست محموله‌ها (`/Shipments`، Floor؛ props I.5 `{ canCreate }` به‌اضافهٔ فیلتر وضعیت صفحه).
 *
 * سرور همین فهرست را بی‌اسکریپت هم می‌سازد (قاب فاز ۴a)؛ جزیره همان کارت‌ها را از `GET /api/shipments?status=` می‌خواند و
 * کارهای زنده را اضافه می‌کند: «محمولهٔ تازه» با یک لمس و بی پنجره (C.1؛ تاریخ امروزِ ایران، بقیه از ترجیحات کارگاه) که
 * مستقیم به زبانهٔ نگله‌ها می‌رود، و حذف محمولهٔ باز و بی‌فرش (فقط Business، با پرسش؛ نگله‌های خالی‌اش هم می‌روند و
 * هزینهٔ وصل به آن «تقسیم‌نشده» می‌شود). دکمه‌ای که بیننده اجازه‌اش را ندارد هرگز نشان داده نمی‌شود (H.3: انباردار
 * «محمولهٔ تازه» نمی‌بیند و متن خودش را می‌خواند).
 *
 * عددها همان شکل سرور (`Messages.FormatNumber`) را دارند تا پیش و پس از سوار شدن جزیره چیزی نپرد. هر «نگله» از
 * `terms` کارگاه است. هیچ مبلغ تومانی این‌جا نیست (دلار گمرکی پول داخلی نیست، A.5).
 */
import { computed, onMounted, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { confirmDialog, toast } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import { faJalali } from '@/lib/jalali'
import { ShipmentStatus, type Shipment, type ShipmentListItem } from '@/lib/types/shipments'
import { canDeleteShipment, copy, listLine, shipmentName, statusQuery } from './shipmentShell'

const props = withDefaults(
  defineProps<{
    /** بیننده Business است: «محمولهٔ تازه» و حذف محمولهٔ خالی */
    canCreate: boolean
    /** فیلتر وضعیت صفحه: «open»، «finalized» یا null (همه) */
    status?: string | null
  }>(),
  { status: null },
)

const ctx = appContext()
const items = ref<ShipmentListItem[]>([])
const loading = ref(true)
const loadError = ref('')
const creating = ref(false)
const deleting = ref<string | null>(null)

const filtered = computed(() => props.status === 'open' || props.status === 'finalized')

function workspaceHref(id: string): string {
  return `/Shipments/${id}`
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    items.value = await api.get<ShipmentListItem[]>(`/api/shipments${statusQuery(props.status)}`)
  } catch (error) {
    loadError.value = errorMessage(error) || copy('loadFailed')
  } finally {
    loading.value = false
  }
}

/** یک لمس، بی پنجره (C.1): ساختن و رفتن به زبانهٔ نگله‌ها. */
async function create() {
  if (creating.value) return
  creating.value = true
  try {
    const shipment = await api.post<Shipment>('/api/shipments', { documentDate: ctx.today })
    window.location.href = `/Shipments/${shipment.id}?tab=bales`
  } catch (error) {
    toast.error(errorMessage(error))
    creating.value = false
  }
}

async function remove(item: ShipmentListItem) {
  if (deleting.value) return
  const ok = await confirmDialog({
    title: copy('deleteConfirmTitle', { number: item.number }),
    message: copy('deleteConfirm'),
    confirmLabel: copy('deleteYes'),
    cancelLabel: copy('no'),
    danger: true,
  })
  if (!ok) return

  deleting.value = item.id
  try {
    await api.del(`/api/shipments/${item.id}`)
    items.value = items.value.filter((s) => s.id !== item.id)
    toast.success(copy('deleted', { number: item.number }))
  } catch (error) {
    toast.error(errorMessage(error))
    await load()
  } finally {
    deleting.value = null
  }
}

onMounted(load)
</script>

<template>
  <div aria-live="polite" :aria-busy="loading">
    <div v-if="loading && items.length === 0" class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3" aria-hidden="true">
      <div v-for="i in 3" :key="i" class="skeleton h-40 rounded-xl" />
    </div>

    <div
      v-else-if="loadError"
      class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-error/30 bg-error-container px-4 py-3 text-on-error-container"
      role="alert"
    >
      <span class="flex items-center gap-2">
        <AppIcon name="error" class="h-5 w-5 shrink-0" />
        {{ loadError }}
      </span>
      <Button icon="undo" @click="load">{{ copy('retry') }}</Button>
    </div>

    <template v-else-if="items.length === 0">
      <EmptyState v-if="filtered" icon="truck" :title="copy('emptyFiltered')">
        <template #actions>
          <Button href="/Shipments">{{ copy('allShipments') }}</Button>
        </template>
      </EmptyState>
      <EmptyState v-else-if="canCreate" icon="truck" :title="copy('emptyBusiness')">
        <template #actions>
          <Button variant="primary" size="lg" icon="plus" :loading="creating" @click="create">
            {{ copy('newShipment') }}
          </Button>
        </template>
      </EmptyState>
      <EmptyState v-else icon="truck" :title="copy('emptyOperator')" />
    </template>

    <ul v-else class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <li v-for="s in items" :key="s.id" class="relative">
        <a
          :href="workspaceHref(s.id)"
          :data-shipment-id="s.id"
          class="flex h-full flex-col gap-3 rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm transition hover:border-primary hover:shadow-md"
        >
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0">
              <h2 class="text-lg font-bold" data-numeric>{{ shipmentName(s.number) }}</h2>
              <p class="truncate text-sm text-on-surface-variant">{{ s.title }}</p>
            </div>
            <span
              v-if="s.status === ShipmentStatus.Finalized"
              class="inline-flex shrink-0 items-center gap-1 rounded-full bg-surface-container-highest px-2.5 py-1 text-xs font-medium text-on-surface"
            >
              <AppIcon name="lock" class="h-3.5 w-3.5" />
              {{ copy('final') }}
            </span>
            <span
              v-else
              class="inline-flex shrink-0 items-center gap-1 rounded-full bg-secondary-container px-2.5 py-1 text-xs font-medium text-on-secondary-container"
            >
              <AppIcon name="edit" class="h-3.5 w-3.5" />
              {{ copy('draft') }}
            </span>
          </div>

          <p class="flex items-center gap-1.5 text-sm text-on-surface-variant" data-numeric>
            <AppIcon name="calendar" class="h-4 w-4" />
            {{ faJalali(s.documentDate) }}
          </p>

          <p class="text-sm" data-numeric>{{ listLine(s) }}</p>

          <p
            v-if="s.status === ShipmentStatus.Open && s.readinessBlocking > 0"
            class="mt-auto inline-flex items-center gap-1.5 self-start rounded-full bg-warning/12 px-2.5 py-1 text-xs font-medium text-warning"
            data-numeric
          >
            <AppIcon name="warning" class="h-3.5 w-3.5" />
            {{ copy('blockingLeft', { n: s.readinessBlocking }) }}
          </p>
          <p
            v-else-if="s.status === ShipmentStatus.Open && s.lines > 0"
            class="mt-auto inline-flex items-center gap-1.5 self-start rounded-full bg-success/12 px-2.5 py-1 text-xs font-medium text-success"
          >
            <AppIcon name="check" class="h-3.5 w-3.5" />
            {{ copy('readyToFinalize') }}
          </p>
        </a>

        <!-- حذف بیرون از پیوند کارت (دکمه داخل پیوند معتبر نیست)؛ گوشهٔ پایین کارت -->
        <button
          v-if="canDeleteShipment(s, canCreate)"
          type="button"
          class="absolute bottom-3 end-3 grid h-11 w-11 place-items-center rounded-lg text-on-surface-variant hover:bg-error-container hover:text-on-error-container disabled:opacity-50"
          :disabled="deleting === s.id"
          :aria-label="copy('deleteLabel', { number: s.number })"
          @click="remove(s)"
        >
          <AppIcon name="trash" class="h-5 w-5" />
        </button>
      </li>
    </ul>
  </div>
</template>
