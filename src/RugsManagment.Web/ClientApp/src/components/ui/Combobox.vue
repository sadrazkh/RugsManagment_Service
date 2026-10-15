<script setup lang="ts">
/**
 * خانهٔ انتخابی با جست‌وجو (الگوی combobox در ARIA 1.2) و گزینهٔ «افزودن «…»» برای مقدار تازه.
 *
 * برای فهرست‌هایی که بلندند یا هر روز چیزی به آن‌ها اضافه می‌شود (فروشنده، نقشه، محل بافت): تایپ کنید تا
 * فهرست کوتاه شود؛ ↓/↑ و Enter انتخاب می‌کند؛ Esc می‌بندد. با `allowCreate`، اگر نوشته با هیچ گزینه‌ای
 * یکی نبود، آخرِ فهرست «افزودن «حاج رضا»» می‌آید که `create` را می‌فرستد، و رها کردن خانه هم همان نوشته را
 * نگه می‌دارد. بی `allowCreate` نوشتهٔ ناجور هنگام خروج به مقدار قبلی برمی‌گردد.
 *
 * مقایسه «ی/ي» و «ک/ك» و ارقام فارسی/لاتین را یکی می‌گیرد تا «کاشان» و «كاشان» هر دو پیدا شوند.
 *
 * - گزینه‌هایی که بعد از سوار شدن می‌رسند (`api.get` در onMounted) برچسب مقدار فعلی را جای شناسهٔ خام می‌نشانند.
 * - فوکوس بی‌تایپ هیچ گزینه‌ای را برجسته نمی‌کند (مگر مقدار فعلی)؛ پس Enter در خانهٔ خالیِ دست‌نخورده فرم را
 *   می‌فرستد و خانهٔ اختیاری بی‌صدا با گزینهٔ اول پر نمی‌شود. ↓/↑ یا تایپ برجسته‌سازی را شروع می‌کند.
 */
import { computed, nextTick, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { normalizeDigits } from '@/lib/money'

export type ComboboxOption = string | { value: string; label: string; hint?: string }

const props = withDefaults(
  defineProps<{
    modelValue: string | null
    options: readonly ComboboxOption[]
    allowCreate?: boolean
    /** الگوی برچسب گزینهٔ افزودن؛ `{text}` نوشتهٔ کاربر است */
    createLabel?: string
    placeholder?: string
    id?: string
    describedBy?: string
    invalid?: boolean
    disabled?: boolean
    required?: boolean
    /** برچسب صفحه‌خوان وقتی Field دور آن نیست */
    ariaLabel?: string
    /** بیشترین تعداد گزینهٔ نمایشی */
    maxVisible?: number
  }>(),
  {
    allowCreate: false,
    createLabel: 'افزودن «{text}»',
    placeholder: undefined,
    id: undefined,
    describedBy: undefined,
    invalid: false,
    disabled: false,
    required: false,
    ariaLabel: undefined,
    maxVisible: 50,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  create: [text: string]
}>()

interface Item {
  key: string
  value: string
  label: string
  hint?: string
  create?: boolean
}

const uid = useId()
const inputId = computed(() => props.id ?? `${uid}-input`)
const listId = `${uid}-list`
const input = ref<HTMLInputElement | null>(null)
const list = ref<HTMLElement | null>(null)

const normalized = computed(() =>
  props.options.map((o) => (typeof o === 'string' ? { value: o, label: o } : o)),
)

function labelIn(list: readonly { value: string; label: string }[], value: string | null): string {
  if (value === null) return ''
  return list.find((o) => o.value === value)?.label ?? value
}

function labelOf(value: string | null): string {
  return labelIn(normalized.value, value)
}

const text = ref(labelOf(props.modelValue))
const open = ref(false)
const active = ref(-1)

watch(
  () => props.modelValue,
  (v) => {
    if (!open.value) text.value = labelOf(v)
  },
)

// گزینه‌ها دیر رسیدند یا عوض شدند: اگر خانه همان نمایش قبلیِ مقدار را دارد (کاربر چیزی ننوشته)، برچسب تازه
watch(normalized, (now, before) => {
  if (open.value) return
  if (text.value === labelIn(before ?? [], props.modelValue)) text.value = labelIn(now, props.modelValue)
})

/** یکسان‌سازی برای مقایسه. */
function fold(value: string): string {
  return normalizeDigits(value)
    .replace(/ي/g, 'ی')
    .replace(/ك/g, 'ک')
    // نیم‌فاصله و نشانه‌های جهت (نویسه‌های قالب) مثل فاصله شمرده می‌شوند
    .replace(/\p{Cf}/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase()
}

const items = computed<Item[]>(() => {
  const needle = fold(text.value)
  const all = normalized.value
  const starts: Item[] = []
  const contains: Item[] = []
  for (const o of all) {
    const hay = fold(o.label)
    const item: Item = { key: `o:${o.value}`, value: o.value, label: o.label, hint: o.hint }
    if (!needle || hay.startsWith(needle)) starts.push(item)
    else if (hay.includes(needle)) contains.push(item)
  }
  const result = [...starts, ...contains].slice(0, props.maxVisible)
  const exact = all.some((o) => fold(o.label) === needle)
  if (props.allowCreate && needle && !exact) {
    const typed = text.value.trim()
    result.push({ key: 'create', value: typed, label: props.createLabel.replace('{text}', typed), create: true })
  }
  return result
})

const activeId = computed(() => (open.value && active.value >= 0 ? `${uid}-opt-${active.value}` : undefined))

/**
 * باز کردن فهرست. فقط مقدار فعلی برجسته می‌شود؛ بی آن هیچ (Enter فرم را می‌فرستد). `highlightFirst` برای ↓ که
 * کاربر صریحاً به فهرست رفته است.
 */
function openList(highlightFirst = false) {
  if (props.disabled) return
  open.value = true
  const current = items.value.findIndex((i) => !i.create && i.value === props.modelValue)
  active.value = current >= 0 ? current : highlightFirst && items.value.length > 0 ? 0 : -1
}

function closeList() {
  open.value = false
  active.value = -1
}

function choose(item: Item) {
  if (item.create) {
    emit('create', item.value)
    emit('update:modelValue', item.value)
    text.value = item.value
  } else {
    emit('update:modelValue', item.value)
    text.value = item.label
  }
  closeList()
}

function onInput() {
  open.value = true
  // نوشته همه پاک شد: چیزی برجسته نمی‌شود، تا Enter فرم را بفرستد نه این‌که گزینهٔ اول را بی‌صدا بگذارد
  active.value = text.value.trim() && items.value.length > 0 ? 0 : -1
}

async function move(delta: number) {
  if (!open.value) {
    openList(true)
    return
  }
  const count = items.value.length
  if (count === 0) return
  // بی برجسته: ↓ اولی، ↑ آخری
  active.value = active.value < 0 ? (delta > 0 ? 0 : count - 1) : (active.value + delta + count) % count
  await nextTick()
  list.value?.querySelector<HTMLElement>(`#${CSS.escape(`${uid}-opt-${active.value}`)}`)?.scrollIntoView({ block: 'nearest' })
}

function onKeydown(event: KeyboardEvent) {
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      void move(1)
      break
    case 'ArrowUp':
      event.preventDefault()
      void move(-1)
      break
    case 'Enter':
      if (open.value && active.value >= 0 && items.value[active.value]) {
        event.preventDefault()
        choose(items.value[active.value])
      }
      break
    case 'Escape':
      if (open.value) {
        event.preventDefault()
        event.stopPropagation()
        closeList()
      }
      break
    case 'Tab':
      closeList()
      break
  }
}

/** خروج از خانه: نوشتهٔ آزاد با allowCreate می‌ماند، وگرنه به مقدار قبلی برمی‌گردد. */
function onBlur(event: FocusEvent) {
  if (list.value && event.relatedTarget instanceof Node && list.value.contains(event.relatedTarget)) return
  closeList()
  const typed = text.value.trim()
  const match = normalized.value.find((o) => fold(o.label) === fold(typed))
  if (match) {
    if (match.value !== props.modelValue) emit('update:modelValue', match.value)
    text.value = match.label
  } else if (!typed) {
    if (props.modelValue !== null) emit('update:modelValue', null)
  } else if (props.allowCreate) {
    if (typed !== props.modelValue) emit('update:modelValue', typed)
    text.value = typed
  } else {
    text.value = labelOf(props.modelValue)
  }
}
</script>

<template>
  <div class="relative">
    <div class="relative">
      <input
        :id="inputId"
        ref="input"
        v-model="text"
        type="text"
        role="combobox"
        autocomplete="off"
        aria-autocomplete="list"
        :aria-expanded="open"
        :aria-controls="listId"
        :aria-activedescendant="activeId"
        :aria-describedby="describedBy"
        :aria-invalid="invalid || undefined"
        :aria-required="required || undefined"
        :aria-label="ariaLabel"
        :placeholder="placeholder"
        :disabled="disabled"
        class="min-h-11 w-full rounded-lg border bg-surface-container-lowest ps-3 pe-11 text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary"
        :class="invalid ? 'border-error' : 'border-outline-variant'"
        @focus="openList()"
        @click="openList()"
        @input="onInput"
        @keydown="onKeydown"
        @blur="onBlur"
      />
      <button
        type="button"
        tabindex="-1"
        :disabled="disabled"
        class="absolute inset-y-0 end-0 grid w-11 place-items-center text-on-surface-variant"
        @mousedown.prevent="open ? closeList() : (openList(), input?.focus())"
      >
        <AppIcon :name="open ? 'chevron-up' : 'chevron-down'" class="h-4 w-4" label="نمایش گزینه‌ها" />
      </button>
    </div>

    <ul
      v-show="open && items.length > 0"
      :id="listId"
      ref="list"
      role="listbox"
      class="absolute inset-x-0 top-full z-40 mt-1 max-h-64 overflow-y-auto rounded-lg border border-outline-variant bg-surface-container-lowest py-1 shadow-lg"
    >
      <li
        v-for="(item, index) in items"
        :id="`${uid}-opt-${index}`"
        :key="item.key"
        role="option"
        :aria-selected="index === active"
        class="flex min-h-11 cursor-pointer items-center gap-2 px-3 text-sm"
        :class="[
          index === active ? 'bg-primary/10 text-primary' : 'text-on-surface hover:bg-surface-container',
          item.create ? 'border-t border-outline-variant font-medium' : '',
        ]"
        @mousedown.prevent="choose(item)"
        @mousemove="active = index"
      >
        <AppIcon v-if="item.create" name="plus" class="h-4 w-4" />
        <AppIcon v-else-if="!item.create && item.value === modelValue" name="check" class="h-4 w-4" />
        <span class="min-w-0 flex-1 truncate">{{ item.label }}</span>
        <span v-if="item.hint" class="shrink-0 text-xs text-on-surface-variant">{{ item.hint }}</span>
      </li>
    </ul>
  </div>
</template>
