<script setup lang="ts">
/**
 * سهم هر فرش از یک هزینه (پیش‌نمایش یا نتیجهٔ ذخیره‌شده؛ D.3): شماره، وزن به زبان بازرگان («۴٫۳۷ متر»، «۵۰ تخته»)،
 * سهم، سهم هر تخته برای دسته، و «مبلغ ثابت» (pin) هر فرش.
 *
 * عددها همه از سرورند (بیشترین باقی‌مانده، قاعدهٔ دسته)؛ این‌جا فقط نشان داده و جمع زده می‌شوند (`lib/num.ts`). با
 * `pinEditable` هر ردیف خانهٔ «مبلغ ثابت» دارد (به ارز کارگاه، D.3 «Pins»)؛ بقیهٔ مبلغ بین فرش‌های بی‌مبلغ ثابت تقسیم
 * می‌شود. فهرست بلند (محمولهٔ ۲۴۲ فرشی) اول ۵۰ ردیف نشان می‌دهد و «همه» بقیه را باز می‌کند.
 */
import { computed, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { toPersianDigits } from '@/lib/format'
import { add, toNumber } from '@/lib/num'
import type { AllocationPreview, Pin } from '@/lib/types/costs'
import { baseCurrencyWord, n, weightText } from './costText'

const props = withDefaults(
  defineProps<{
    preview: AllocationPreview
    pins?: Pin[]
    pinEditable?: boolean
    maxRows?: number
    caption?: string
  }>(),
  { pins: () => [], pinEditable: false, maxRows: 50, caption: 'سهم هر فرش' },
)

const emit = defineEmits<{ 'update:pins': [value: Pin[]] }>()

const showAll = ref(false)
const currency = computed(() => baseCurrencyWord())

const rows = computed(() => (showAll.value ? props.preview.shares : props.preview.shares.slice(0, props.maxRows)))
const hidden = computed(() => Math.max(0, props.preview.shares.length - rows.value.length))

const allocated = computed(() => toNumber(props.preview.shares.reduce((sum, s) => add(sum, s.amount), add(0, 0))))

const pinByRug = computed(() => new Map(props.pins.map((p) => [p.rugId, p.amount])))

function pinOf(rugId: string): number | null {
  return pinByRug.value.get(rugId) ?? null
}

function setPin(rugId: string, value: number | null) {
  const rest = props.pins.filter((p) => p.rugId !== rugId)
  emit('update:pins', value === null ? rest : [...rest, { rugId, amount: value }])
}

function serialText(serial: string | null): string {
  return serial ? toPersianDigits(serial) : '—'
}
</script>

<template>
  <div class="space-y-2">
    <div class="overflow-x-auto rounded-lg border border-outline-variant">
      <table class="w-full min-w-[22rem] text-sm" data-numeric>
        <caption class="sr-only">{{ caption }}</caption>
        <thead class="bg-surface-container text-on-surface-variant">
          <tr>
            <th scope="col" class="px-3 py-2 text-start font-medium">شماره</th>
            <th scope="col" class="px-3 py-2 text-start font-medium">وزن</th>
            <th scope="col" class="px-3 py-2 text-end font-medium">سهم ({{ currency }})</th>
            <th v-if="pinEditable" scope="col" class="px-3 py-2 text-start font-medium">مبلغ ثابت</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-outline-variant">
          <tr v-for="s in rows" :key="s.rugId" :data-rug="s.rugId">
            <td class="px-3 py-2">
              <a :href="`/Rugs/Details/${s.rugId}`" class="hover:text-primary">
                <bdi dir="ltr">{{ serialText(s.serial) }}</bdi>
              </a>
            </td>
            <td class="px-3 py-2 text-on-surface-variant">{{ weightText(s.weightUnits, s.weightUnit) }}</td>
            <td class="px-3 py-2 text-end font-medium">
              <span class="inline-flex items-center gap-1">
                <AppIcon v-if="s.isPinned" name="lock" class="h-3.5 w-3.5 text-on-surface-variant" label="مبلغ ثابت" />
                {{ n(s.amount) }}
              </span>
              <span v-if="s.perPiece !== null" class="block text-xs font-normal text-on-surface-variant">
                هر تخته {{ n(s.perPiece) }}
              </span>
            </td>
            <td v-if="pinEditable" class="w-40 px-3 py-1">
              <NumberField
                :model-value="pinOf(s.rugId)"
                kind="money"
                :words="false"
                suffix=""
                allow-negative
                :aria-label="`مبلغ ثابت فرش ${serialText(s.serial)}`"
                @update:model-value="setPin(s.rugId, $event)"
              />
            </td>
          </tr>
        </tbody>
        <tfoot class="border-t border-outline-variant bg-surface-container-low font-semibold">
          <tr>
            <th scope="row" class="px-3 py-2 text-start" colspan="2">جمع</th>
            <td class="px-3 py-2 text-end">{{ n(allocated) }}</td>
            <td v-if="pinEditable" />
          </tr>
          <tr v-if="preview.unallocated !== 0" class="text-warning">
            <th scope="row" class="px-3 py-2 text-start font-medium" colspan="2">تقسیم‌نشده</th>
            <td class="px-3 py-2 text-end">{{ n(preview.unallocated) }}</td>
            <td v-if="pinEditable" />
          </tr>
        </tfoot>
      </table>
    </div>
    <button
      v-if="hidden > 0"
      type="button"
      class="inline-flex min-h-11 items-center gap-1 rounded-lg px-3 text-sm text-primary hover:bg-surface-container"
      @click="showAll = true"
    >
      <AppIcon name="chevron-down" class="h-4 w-4" />
      {{ n(hidden) }} فرش دیگر
    </button>
  </div>
</template>
