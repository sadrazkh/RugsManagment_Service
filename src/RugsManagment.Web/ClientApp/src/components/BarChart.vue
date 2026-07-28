<script setup lang="ts">
/**
 * نمودار میله‌ای افقی برای مقایسهٔ بزرگی یک سری.
 *
 * افقی است چون برچسب‌های فارسی (نام مرحله) بلندند و در نمودار عمودی
 * روی هم می‌افتند یا باید کج شوند.
 *
 * یک سری = یک رنگ ثابت (اسلات ۱)؛ رنگ‌کردن میله‌ها بر اساس مقدارشان
 * کانال هویت را هدر می‌دهد چون طول میله همان را نشان می‌دهد.
 */
import { computed, ref } from 'vue'

interface Bar {
  label: string
  value: number
  /** متن آماده برای نمایش کنار میله (با ارقام فارسی و واحد) */
  display: string
  /** خط دوم در راهنمای شناور */
  hint?: string
}

const props = withDefaults(defineProps<{ bars: Bar[]; barHeight?: number }>(), { barHeight: 34 })

const hovered = ref<number | null>(null)

const max = computed(() => Math.max(1, ...props.bars.map((b) => b.value)))
const widthOf = (value: number) => `${Math.max(0.5, (value / max.value) * 100)}%`
</script>

<template>
  <div class="space-y-1.5" role="list">
    <div
      v-for="(bar, i) in bars"
      :key="bar.label"
      role="listitem"
      class="group rounded-lg px-2 py-1 transition-colors"
      :class="hovered === i ? 'bg-surface-container' : ''"
      @mouseenter="hovered = i"
      @mouseleave="hovered = null"
      @focusin="hovered = i"
      @focusout="hovered = null"
      tabindex="0"
    >
      <div class="mb-1 flex items-baseline justify-between gap-3 text-sm">
        <span class="min-w-0 truncate">{{ bar.label }}</span>
        <!-- برچسب مستقیم: مقدار همیشه خوانده می‌شود، حتی بدون شناور کردن -->
        <span class="shrink-0 text-xs text-on-surface-variant" data-numeric>{{ bar.display }}</span>
      </div>

      <div
        class="w-full overflow-hidden rounded bg-surface-container"
        :style="{ height: '10px' }"
        role="img"
        :aria-label="`${bar.label}: ${bar.display}`"
      >
        <!-- انتهای گرد فقط سمت آزاد میله؛ سمت پایه صاف می‌ماند -->
        <div
          class="h-full rounded-s transition-[width] duration-300"
          :style="{ width: widthOf(bar.value), background: 'var(--color-chart-1)' }"
        ></div>
      </div>

      <p v-if="bar.hint" class="mt-1 text-xs text-on-surface-variant" data-numeric>{{ bar.hint }}</p>
    </div>
  </div>
</template>
