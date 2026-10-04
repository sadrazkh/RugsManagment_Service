<script setup lang="ts">
/**
 * دکمهٔ یکدست جزیره‌ها: هدف لمسی دست‌کم ۴۴ پیکسل (اندازهٔ `lg` ۵۶ پیکسل برای دکمه‌های اصلی بازرگان)،
 * آیکون + نوشته، و حالت «در حال انجام» که دکمه را قفل و برای صفحه‌خوان `aria-busy` می‌کند.
 * با `href` به‌صورت پیوند رندر می‌شود (همان ظاهر).
 */
import { computed } from 'vue'
import AppIcon from '@/components/AppIcon.vue'

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger'
    size?: 'md' | 'lg'
    type?: 'button' | 'submit' | 'reset'
    /** آیکون پیش از نوشته */
    icon?: string
    /** آیکون پس از نوشته (مثلاً پیکان «بعدی») */
    iconEnd?: string
    loading?: boolean
    disabled?: boolean
    /** تمام‌عرض */
    block?: boolean
    href?: string
    /** برچسب صفحه‌خوان برای دکمهٔ فقط‌آیکون */
    label?: string
  }>(),
  {
    variant: 'outline',
    size: 'md',
    type: 'button',
    icon: undefined,
    iconEnd: undefined,
    loading: false,
    disabled: false,
    block: false,
    href: undefined,
    label: undefined,
  },
)

const variants: Record<string, string> = {
  primary: 'bg-primary text-on-primary font-semibold hover:bg-primary-hover',
  secondary: 'bg-secondary-container text-on-secondary-container font-semibold hover:opacity-90',
  outline: 'border border-outline-variant text-on-surface hover:bg-surface-container',
  ghost: 'text-on-surface-variant hover:bg-surface-container',
  danger: 'bg-error text-on-error font-semibold hover:opacity-90',
}

const classes = computed(() => [
  'inline-flex items-center justify-center gap-2 rounded-lg select-none',
  props.size === 'lg' ? 'min-h-14 px-5 text-base' : 'min-h-11 px-4 text-sm',
  props.block ? 'w-full' : '',
  variants[props.variant],
])

const inactive = computed(() => props.disabled || props.loading)
</script>

<template>
  <a
    v-if="href && !inactive"
    :href="href"
    :class="classes"
    :aria-label="label"
  >
    <AppIcon v-if="icon" :name="icon" class="h-5 w-5" />
    <slot />
    <AppIcon v-if="iconEnd" :name="iconEnd" class="h-5 w-5" />
  </a>
  <button
    v-else
    :type="type"
    :class="classes"
    :disabled="inactive"
    :aria-busy="loading || undefined"
    :aria-label="label"
  >
    <span
      v-if="loading"
      class="h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-current border-t-transparent"
      aria-hidden="true"
    ></span>
    <AppIcon v-else-if="icon" :name="icon" class="h-5 w-5" />
    <slot />
    <AppIcon v-if="iconEnd && !loading" :name="iconEnd" class="h-5 w-5" />
  </button>
</template>
