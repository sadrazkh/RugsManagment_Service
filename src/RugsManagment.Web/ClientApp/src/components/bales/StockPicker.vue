<script setup lang="ts">
/**
 * «افزودن از انبار» (C.3): فرش‌های «در انبار» (نه در نگله، نه فروخته، نه سطل زباله؛ C.10) با جست‌وجوی شماره یا نقشه،
 * هر کدام با دکمهٔ ۴۴ پیکسلی «افزودن به نگلهٔ ۳». برگه باز می‌ماند تا چند فرش پشت سر هم برود؛ فرشی که رفت از فهرست
 * برداشته می‌شود.
 *
 * خواندن از `GET /api/rugs?location=InStock` (همان فیلتر صفحهٔ انبار)؛ نوشتن را والد انجام می‌دهد (`add`).
 */
import { computed, nextTick, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Drawer from '@/components/ui/Drawer.vue'
import { api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import { RugLocation, type PagedResult, type RugListItem } from '@/lib/types/rugs'
import { faDigitsOf, serialText, sizeText } from './baleModel'

const props = defineProps<{
  open: boolean
  /** نگلهٔ مقصد (جاری) */
  baleNumber: number
  /** والد اضافه می‌کند؛ true یعنی رفت */
  add: (rug: RugListItem) => Promise<boolean>
}>()

const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const terms = appContext().terms
const uid = useId()
const query = ref('')
const items = ref<RugListItem[]>([])
const loading = ref(false)
const error = ref('')
const adding = ref<string | null>(null)
const searchInput = ref<HTMLInputElement | null>(null)

const title = computed(() => message('I_BOARD_STOCK_TITLE', { n: props.baleNumber }))

let timer: ReturnType<typeof setTimeout> | null = null
let request = 0

async function search() {
  const id = ++request
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ location: String(RugLocation.InStock), pageSize: '40' })
    const q = query.value.trim()
    if (q) params.set('search', q)
    const page = await api.get<PagedResult<RugListItem>>(`/api/rugs?${params.toString()}`)
    if (id === request) items.value = page.items
  } catch (e) {
    if (id === request) error.value = errorMessage(e)
  } finally {
    if (id === request) loading.value = false
  }
}

watch(query, () => {
  if (timer) clearTimeout(timer)
  timer = setTimeout(() => void search(), 300)
})

watch(
  () => props.open,
  (open) => {
    if (!open) return
    void search()
    void nextTick(() => searchInput.value?.focus())
  },
  { immediate: true },
)

async function pick(rug: RugListItem) {
  if (adding.value) return
  adding.value = rug.id
  try {
    if (await props.add(rug)) items.value = items.value.filter((r) => r.id !== rug.id)
  } finally {
    adding.value = null
  }
}

const emptyText = computed(() => (query.value.trim() ? message('I_BOARD_STOCK_NOT_FOUND') : message('I_BOARD_STOCK_EMPTY')))
</script>

<template>
  <Drawer :open="open" :title="title" side="end" size="md" @update:open="(v) => emit('update:open', v)">
    <div class="space-y-3">
      <div>
        <label :for="`${uid}-q`" class="mb-1 block text-sm font-medium">شماره یا نقشه</label>
        <div class="relative">
          <input
            :id="`${uid}-q`"
            ref="searchInput"
            v-model="query"
            type="search"
            inputmode="search"
            autocomplete="off"
            enterkeyhint="search"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest pe-10 ps-3 outline-none focus:border-primary"
          />
          <AppIcon name="search" class="pointer-events-none absolute inset-y-0 end-3 my-auto h-5 w-5 text-on-surface-variant" />
        </div>
      </div>

      <p v-if="error" class="rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">{{ error }}</p>

      <p v-if="loading && items.length === 0" class="text-sm text-on-surface-variant" role="status">در حال خواندن…</p>
      <p v-else-if="!loading && items.length === 0 && !error" class="py-6 text-center text-sm text-on-surface-variant" role="status">
        {{ emptyText }}
      </p>

      <ul class="divide-y divide-outline-variant/60" :aria-busy="loading || undefined">
        <li v-for="rug in items" :key="rug.id" class="flex items-center gap-3 py-2">
          <div class="min-w-0 flex-1" data-numeric>
            <p class="font-semibold">
              <bdi dir="ltr">{{ serialText(rug.serialNo) }}</bdi>
              <span v-if="rug.quantity > 1" class="ms-1 text-sm font-normal text-on-surface-variant">({{ faDigitsOf(rug.quantity) }} تخته)</span>
            </p>
            <p class="truncate text-sm text-on-surface-variant">
              {{ sizeText(rug.lengthMeters, rug.widthMeters) }}
              <template v-if="rug.pattern"> · {{ rug.pattern }}</template>
              <template v-if="rug.origin"> · {{ rug.origin }}</template>
            </p>
          </div>
          <button
            type="button"
            class="inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-lg bg-primary px-3 text-sm font-semibold text-on-primary hover:bg-primary-hover"
            :disabled="adding !== null"
            :aria-busy="adding === rug.id || undefined"
            :aria-label="`${message('I_BOARD_ADD_TO', { n: baleNumber })}: ${serialText(rug.serialNo)}`"
            @click="pick(rug)"
          >
            <AppIcon name="plus" class="h-5 w-5" />
            {{ terms.baleOf }} {{ faDigitsOf(baleNumber) }}
          </button>
        </li>
      </ul>
    </div>
  </Drawer>
</template>
