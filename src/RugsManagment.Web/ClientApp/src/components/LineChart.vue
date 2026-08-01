<script setup lang="ts">
/**
 * نمودار خطی روند ماهانه با راهنمای شناور و خط راهنمای عمودی.
 *
 * تک‌محوره است: همهٔ سری‌ها باید یک واحد داشته باشند (مثلاً هر دو تومان).
 * دو محور عمودی با مقیاس‌های متفاوت عمداً پیاده نشده — خواننده را گمراه می‌کند؛
 * برای واحد متفاوت باید نمودار جدا ساخت.
 *
 * جهت: چون صفحه RTL است، محور زمان از راست به چپ پیش می‌رود.
 */
import { computed, ref } from 'vue'

interface Series {
  name: string
  values: number[]
  /** یکی از توکن‌های --color-chart-* */
  color: string
}

const props = withDefaults(
  defineProps<{
    labels: string[]
    series: Series[]
    /** تبدیل عدد به متن فارسی برای راهنما و محور */
    format: (value: number) => string
    height?: number
  }>(),
  { height: 220 },
)

// مختصات داخلی SVG؛ با viewBox مقیاس می‌خورد
const W = 720
const PAD = { top: 12, right: 12, bottom: 26, left: 12 }

const hoverIndex = ref<number | null>(null)

const H = computed(() => props.height)
const innerW = computed(() => W - PAD.left - PAD.right)
const innerH = computed(() => H.value - PAD.top - PAD.bottom)

const max = computed(() => {
  const all = props.series.flatMap((s) => s.values)
  const peak = Math.max(0, ...all)
  // سقف کمی بالاتر از بیشینه تا خط به لبه نچسبد
  return peak === 0 ? 1 : peak * 1.12
})

const count = computed(() => Math.max(1, props.labels.length))

/** در RTL نقطهٔ اول باید سمت راست بنشیند. */
function x(i: number): number {
  const step = count.value === 1 ? 0 : innerW.value / (count.value - 1)
  return PAD.left + innerW.value - i * step
}

function y(value: number): number {
  return PAD.top + innerH.value - (value / max.value) * innerH.value
}

const paths = computed(() =>
  props.series.map((s) => ({
    ...s,
    d: s.values.map((v, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' '),
  })),
)

/** خطوط افقی شبکه — عامدانه کم‌تعداد و کم‌رنگ */
const gridLines = computed(() => [0, 0.5, 1].map((f) => ({ v: max.value * f, y: y(max.value * f) })))

/** فقط چند برچسب محور تا شلوغ نشود */
const axisLabels = computed(() => {
  const step = Math.max(1, Math.ceil(count.value / 6))
  return props.labels.map((l, i) => ({ label: l, i, x: x(i), show: i % step === 0 || i === count.value - 1 }))
})

function onMove(event: MouseEvent) {
  const rect = (event.currentTarget as SVGElement).getBoundingClientRect()
  // موقعیت نسبی، سپس معکوس چون محور RTL است
  const ratio = (event.clientX - rect.left) / rect.width
  const fromRight = 1 - ratio
  hoverIndex.value = Math.max(0, Math.min(count.value - 1, Math.round(fromRight * (count.value - 1))))
}
</script>

<template>
  <div class="relative">
    <!-- راهنمای سری‌ها: هویت هرگز فقط با رنگ منتقل نمی‌شود -->
    <div v-if="series.length > 1" class="mb-2 flex flex-wrap gap-4 text-xs">
      <span v-for="s in series" :key="s.name" class="inline-flex items-center gap-1.5">
        <span class="h-2.5 w-2.5 rounded-full" :style="{ background: s.color }" aria-hidden="true"></span>
        <span class="text-on-surface-variant">{{ s.name }}</span>
      </span>
    </div>

    <svg
      :viewBox="`0 0 ${W} ${H}`"
      class="w-full"
      :style="{ height: H + 'px' }"
      role="img"
      :aria-label="`نمودار روند: ${series.map((s) => s.name).join('، ')}`"
      @mousemove="onMove"
      @mouseleave="hoverIndex = null"
    >
      <!-- شبکه -->
      <line
        v-for="g in gridLines"
        :key="g.y"
        :x1="PAD.left" :x2="W - PAD.right" :y1="g.y" :y2="g.y"
        stroke="var(--color-chart-grid)" stroke-width="1"
      />

      <!-- خط راهنمای عمودی زیر نشانگر -->
      <line
        v-if="hoverIndex !== null"
        :x1="x(hoverIndex)" :x2="x(hoverIndex)" :y1="PAD.top" :y2="PAD.top + innerH"
        stroke="var(--color-chart-grid)" stroke-width="1" stroke-dasharray="3 3"
      />

      <!-- خطوط سری‌ها -->
      <path
        v-for="p in paths"
        :key="p.name"
        :d="p.d"
        fill="none"
        :stroke="p.color"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      />

      <!-- نشانگر نقطهٔ زیر ماوس؛ حلقهٔ هم‌رنگ سطح تا روی خط گم نشود -->
      <template v-if="hoverIndex !== null">
        <circle
          v-for="p in paths"
          :key="p.name + '-dot'"
          :cx="x(hoverIndex)" :cy="y(p.values[hoverIndex] ?? 0)" r="4.5"
          :fill="p.color" stroke="var(--color-surface-container-lowest)" stroke-width="2"
        />
      </template>

      <!-- برچسب محور زمان -->
      <text
        v-for="a in axisLabels.filter((a) => a.show)"
        :key="a.i"
        :x="a.x" :y="H - 8"
        text-anchor="middle"
        font-size="11"
        fill="var(--color-on-surface-variant)"
      >{{ a.label }}</text>
    </svg>

    <!-- راهنمای شناور -->
    <div
      v-if="hoverIndex !== null"
      class="pointer-events-none absolute top-0 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 text-xs shadow-lg"
      :style="{ right: `${(hoverIndex / Math.max(1, count - 1)) * 100}%`, transform: 'translateX(50%)' }"
    >
      <div class="mb-1 font-semibold">{{ labels[hoverIndex] }}</div>
      <div v-for="s in series" :key="s.name" class="flex items-center gap-2 whitespace-nowrap">
        <span class="h-2 w-2 rounded-full" :style="{ background: s.color }" aria-hidden="true"></span>
        <span class="text-on-surface-variant">{{ s.name }}:</span>
        <span data-numeric>{{ format(s.values[hoverIndex] ?? 0) }}</span>
      </div>
    </div>
  </div>
</template>
