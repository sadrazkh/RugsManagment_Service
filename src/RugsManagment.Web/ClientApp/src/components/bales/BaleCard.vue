<script setup lang="ts">
/**
 * کارت یک فرش در نمای کارتی تخته (H.1، H.6 تبلت): شماره، مشخصات، اندازه‌ها، متراژ و دلار، و سه کار با دکمهٔ ۴۴ پیکسلی:
 * - «انتقال» (نشانه + نوشته) که انتخاب‌گر نگله را باز می‌کند؛ با کارت فوکوس‌دار کلید M همان کار است (H.7: هیچ کاری فقط
 *   با کشیدن نیست)؛
 * - تراشهٔ «اندازه» که برگهٔ اندازهٔ نهایی را باز می‌کند (Enter روی کارت هم)؛
 * - «همان» (کلید =) برای فرشی که هنوز اندازهٔ نهایی ندارد.
 * منوی «⋮»: «خارج کردن از نگله»، «جدا کردن از دسته» (دسته)، «صفحهٔ فرش».
 *
 * حالت ساده (A.3): جفت «اندازهٔ اول / نهایی» فقط وقتی با هم فرق دارند؛ وگرنه یک اندازه. محمولهٔ قطعی: فقط‌خواندنی.
 */
import { computed, ref, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { appContext } from '@/lib/context'
import { DeclaredRateSource, type BaleRow } from '@/lib/types/shipments'
import {
  areaCellText,
  faDigitsOf,
  faFixed,
  faLoose,
  rateSourceText,
  serialText,
  sizeChangeBadge,
  sizeDiffers,
  sizeText,
} from './baleModel'

const props = withDefaults(
  defineProps<{
    row: BaleRow
    simple?: boolean
    locked?: boolean
    /** «⋮» → «جدا کردن از دسته» */
    canSplit?: boolean
  }>(),
  { simple: true, locked: false, canSplit: true },
)

const emit = defineEmits<{
  move: [row: BaleRow]
  size: [row: BaleRow]
  same: [row: BaleRow]
  remove: [row: BaleRow]
  split: [row: BaleRow]
}>()

const terms = appContext().terms
const uid = useId()
const menuOpen = ref(false)

const hasFinal = computed(() => props.row.finalLength !== null && props.row.finalWidth !== null)
const badge = computed(() => sizeChangeBadge(props.row.sizeChangeCm))
const showPair = computed(() => !props.simple || sizeDiffers(props.row))
const details = computed(() =>
  [props.row.pattern, props.row.origin, props.row.fieldColor, props.row.borderColor, props.row.material]
    .filter((v): v is string => !!v && !!v.trim())
    .filter((v, i, all) => all.indexOf(v) === i)
    .join(' · '),
)

function onKeydown(event: KeyboardEvent) {
  if (event.target !== event.currentTarget || props.locked) return
  if (event.altKey || event.ctrlKey || event.metaKey) return
  if (event.code === 'KeyM' || event.key === 'm' || event.key === 'M') {
    event.preventDefault()
    emit('move', props.row)
  } else if (event.key === '=' && !hasFinal.value) {
    event.preventDefault()
    emit('same', props.row)
  } else if (event.key === 'Enter') {
    event.preventDefault()
    emit('size', props.row)
  }
}

function menu(action: 'remove' | 'split') {
  menuOpen.value = false
  if (action === 'remove') emit('remove', props.row)
  else emit('split', props.row)
}
</script>

<template>
  <article
    tabindex="0"
    :aria-labelledby="`${uid}-serial`"
    :aria-describedby="`${uid}-sizes`"
    :aria-keyshortcuts="locked ? undefined : 'M'"
    data-bale-card
    :data-line-id="row.lineId"
    class="flex flex-col gap-2 rounded-xl border border-outline-variant bg-surface-container-lowest p-3 shadow-sm focus-visible:border-primary"
    @keydown="onKeydown"
  >
    <header class="flex items-start gap-2">
      <div class="min-w-0 flex-1">
        <h3 :id="`${uid}-serial`" class="text-lg font-bold" data-numeric>
          <bdi dir="ltr">{{ serialText(row.serial) }}</bdi>
          <span v-if="row.qty > 1" class="ms-1 text-sm font-medium text-on-surface-variant">({{ faDigitsOf(row.qty) }} تخته)</span>
        </h3>
        <p v-if="details" class="truncate text-sm text-on-surface-variant">{{ details }}</p>
      </div>
      <button
        v-if="!locked"
        type="button"
        class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
        :aria-expanded="menuOpen"
        :aria-controls="`${uid}-menu`"
        :aria-label="`کارهای ${serialText(row.serial)}`"
        @click="menuOpen = !menuOpen"
      >
        <AppIcon name="dots" class="h-5 w-5" />
      </button>
    </header>

    <dl :id="`${uid}-sizes`" class="grid grid-cols-2 gap-x-3 gap-y-1 text-sm" data-numeric>
      <template v-if="showPair">
        <dt class="text-on-surface-variant">اندازهٔ اول</dt>
        <dd><bdi dir="ltr">{{ sizeText(row.intakeLength, row.intakeWidth) }}</bdi></dd>
        <dt class="text-on-surface-variant">اندازهٔ نهایی</dt>
        <dd class="flex flex-wrap items-center gap-1">
          <template v-if="hasFinal">
            <bdi dir="ltr" class="font-semibold">{{ sizeText(row.finalLength, row.finalWidth) }}</bdi>
            <span v-if="badge" class="rounded-full bg-surface-container-high px-2 text-xs font-semibold" :aria-label="badge.label">{{ badge.text }}</span>
          </template>
          <span v-else class="text-warning">ثبت نشده</span>
        </dd>
      </template>
      <template v-else>
        <dt class="text-on-surface-variant">اندازه</dt>
        <dd class="flex flex-wrap items-center gap-1">
          <bdi dir="ltr" class="font-semibold">{{ sizeText(row.finalLength ?? row.intakeLength, row.finalWidth ?? row.intakeWidth) }}</bdi>
          <span v-if="!hasFinal" class="text-xs text-warning">(نهایی ثبت نشده)</span>
        </dd>
      </template>
      <dt class="text-on-surface-variant">مساحت</dt>
      <dd>{{ areaCellText(row) }} متر</dd>
      <dt class="text-on-surface-variant">دلار</dt>
      <dd class="flex flex-wrap items-center gap-1">
        <span>{{ row.lineUsd === null ? '—' : faFixed(row.lineUsd) }}</span>
        <span
          class="rounded-full px-2 text-xs"
          :class="row.rateSource === DeclaredRateSource.Missing ? 'bg-error-container text-on-error-container' : 'bg-surface-container-high text-on-surface-variant'"
        >
          <template v-if="row.usdPerM2 !== null">{{ faLoose(row.usdPerM2) }} · </template>{{ rateSourceText(row.rateSource) }}
        </span>
      </dd>
    </dl>

    <div v-if="!locked" class="flex flex-wrap gap-2">
      <button
        type="button"
        class="inline-flex min-h-11 items-center gap-1.5 rounded-full border px-4 text-sm font-semibold"
        :class="hasFinal ? 'border-outline-variant text-on-surface hover:bg-surface-container' : 'border-primary bg-primary/10 text-primary'"
        @click="emit('size', row)"
      >
        <AppIcon name="ruler" class="h-5 w-5" />
        اندازه
      </button>
      <button
        type="button"
        class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant px-4 text-sm text-on-surface hover:bg-surface-container"
        @click="emit('move', row)"
      >
        <AppIcon name="move" class="h-5 w-5" />
        انتقال
      </button>
      <button
        v-if="!hasFinal"
        type="button"
        class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant px-4 text-sm text-on-surface hover:bg-surface-container"
        :aria-label="`همان اندازهٔ اول برای ${serialText(row.serial)}`"
        @click="emit('same', row)"
      >
        <AppIcon name="check" class="h-5 w-5" />
        همان
      </button>
    </div>

    <ul v-if="menuOpen && !locked" :id="`${uid}-menu`" class="space-y-1 border-t border-outline-variant pt-2">
      <li>
        <button type="button" class="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 text-sm hover:bg-surface-container" @click="menu('remove')">
          <AppIcon name="warehouse" class="h-5 w-5" />
          خارج کردن از {{ terms.bale }}
        </button>
      </li>
      <li v-if="canSplit && row.qty > 1">
        <button type="button" class="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 text-sm hover:bg-surface-container" @click="menu('split')">
          <AppIcon name="split" class="h-5 w-5" />
          جدا کردن از دسته
        </button>
      </li>
      <li>
        <a :href="`/Rugs/Details/${row.rugId}`" class="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 text-sm hover:bg-surface-container">
          <AppIcon name="rug" class="h-5 w-5" />
          صفحهٔ فرش
        </a>
      </li>
    </ul>
  </article>
</template>
