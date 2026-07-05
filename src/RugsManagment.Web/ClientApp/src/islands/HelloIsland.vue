<script setup lang="ts">
/** جزیرهٔ آزمایشی Phase 0 — فقط برای اطمینان از کارکرد خط لولهٔ Vue↔Razor. */
import { ref } from 'vue'

const props = withDefaults(defineProps<{ name?: string }>(), { name: 'دنیا' })
const count = ref(0)
</script>

<template>
  <div class="rounded-lg border border-outline-variant bg-white p-4 shadow-sm">
    <p class="text-on-surface">
      سلام <strong class="text-primary">{{ props.name }}</strong> — جزیرهٔ Vue فعال است ✅
    </p>
    <button
      class="mt-3 rounded-md bg-primary px-4 py-2 text-on-primary transition hover:opacity-90"
      type="button"
      @click="count++"
    >
      شمارنده: {{ count }}
    </button>
  </div>
</template>
