<script setup lang="ts">
/**
 * گالری عکس یک فرش: آپلود (انتخاب فایل یا کشیدن‌ورها)، حذف، انتخاب عکس شاخص،
 * تغییر ترتیب و نمایش بزرگ.
 *
 * تصاویر قبل از ارسال در مرورگر کوچک و به WebP تبدیل می‌شوند (lib/imageResize).
 */
import { computed, onMounted, ref } from 'vue'
import { VueDraggable } from 'vue-draggable-plus'
import AppIcon from '@/components/AppIcon.vue'
import { api } from '@/lib/api'
import { formatBytes, resizeForUpload } from '@/lib/imageResize'
import { faNumber } from '@/lib/format'
import { confirmDialog, toast } from '@/lib/ui'

const props = defineProps<{ rugId: string; readOnly?: boolean }>()

interface RugImage {
  id: string
  url: string
  thumbnailUrl: string
  width: number
  height: number
  sizeBytes: number
  sortOrder: number
  isPrimary: boolean
}

const MAX_IMAGES = 12

const items = ref<RugImage[]>([])
const loading = ref(true)
const uploading = ref(0)
const dragOver = ref(false)
const lightboxIndex = ref<number | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

const canAddMore = computed(() => !props.readOnly && items.value.length < MAX_IMAGES)
const lightboxImage = computed(() =>
  lightboxIndex.value === null ? null : items.value[lightboxIndex.value] ?? null,
)

async function load() {
  loading.value = true
  try {
    items.value = await api.get<RugImage[]>(`/api/rugs/${props.rugId}/images`)
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    loading.value = false
  }
}

async function uploadFiles(files: FileList | File[]) {
  const picked = Array.from(files).filter((f) => f.type.startsWith('image/'))
  if (picked.length === 0) {
    toast.warning('فقط فایل تصویری قابل افزودن است.')
    return
  }

  const room = MAX_IMAGES - items.value.length
  if (picked.length > room) {
    toast.warning(`فقط ${faNumber(room)} عکس دیگر جا هست؛ بقیه نادیده گرفته شد.`)
  }

  for (const file of picked.slice(0, room)) {
    uploading.value++
    try {
      const resized = await resizeForUpload(file)

      const form = new FormData()
      form.append('file', resized.full, 'image.webp')
      form.append('thumbnail', resized.thumbnail, 'thumb.webp')
      form.append('width', String(resized.width))
      form.append('height', String(resized.height))

      // FormData را نمی‌توان با api.post فرستاد چون آن Content-Type: application/json می‌گذارد
      const res = await fetch(`/api/rugs/${props.rugId}/images`, {
        method: 'POST',
        headers: { 'X-CSRF-TOKEN': csrfToken() },
        credentials: 'same-origin',
        body: form,
      })

      if (!res.ok) {
        let message = 'آپلود ناموفق بود.'
        try { message = (await res.json()).message ?? message } catch { /* پاسخ بدون بدنه */ }
        throw new Error(message)
      }

      items.value.push(await res.json())
    } catch (e) {
      toast.error(`${file.name}: ${(e as Error).message}`)
    } finally {
      uploading.value--
    }
  }
}

function csrfToken(): string {
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
}

function onPick(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files?.length) void uploadFiles(input.files)
  // پاک کردن مقدار تا انتخاب دوبارهٔ همان فایل هم رویداد بدهد
  input.value = ''
}

function onDrop(event: DragEvent) {
  dragOver.value = false
  if (props.readOnly) return
  if (event.dataTransfer?.files.length) void uploadFiles(event.dataTransfer.files)
}

async function remove(image: RugImage) {
  const ok = await confirmDialog({
    title: 'این عکس حذف شود؟',
    message: 'فایل عکس برای همیشه پاک می‌شود.',
    confirmLabel: 'حذف عکس',
    danger: true,
  })
  if (!ok) return

  try {
    await api.del(`/api/rugs/${props.rugId}/images/${image.id}`)
    toast.success('عکس حذف شد.')
    await load()
  } catch (e) {
    toast.error((e as Error).message)
  }
}

async function setPrimary(image: RugImage) {
  if (image.isPrimary) return
  try {
    items.value = await api.post<RugImage[]>(`/api/rugs/${props.rugId}/images/${image.id}/primary`)
    toast.success('عکس شاخص تغییر کرد.')
  } catch (e) {
    toast.error((e as Error).message)
  }
}

/** بعد از جابه‌جایی با drag، ترتیب جدید ذخیره می‌شود. */
async function persistOrder() {
  try {
    await api.put(`/api/rugs/${props.rugId}/images/order`, { imageIds: items.value.map((i) => i.id) })
  } catch (e) {
    toast.error((e as Error).message)
    await load()
  }
}

function openLightbox(index: number) {
  lightboxIndex.value = index
}

function step(delta: number) {
  if (lightboxIndex.value === null || items.value.length === 0) return
  lightboxIndex.value = (lightboxIndex.value + delta + items.value.length) % items.value.length
}

function onLightboxKey(event: KeyboardEvent) {
  if (event.key === 'Escape') lightboxIndex.value = null
  // در RTL جهت فلش‌ها معکوس است
  else if (event.key === 'ArrowLeft') step(1)
  else if (event.key === 'ArrowRight') step(-1)
}

onMounted(load)
</script>

<template>
  <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
    <div class="mb-4 flex items-center justify-between gap-3">
      <h2 class="flex items-center gap-2 text-sm font-semibold text-primary">
        <AppIcon name="image" class="h-4 w-4" />
        عکس‌های فرش
        <span v-if="items.length" class="text-xs font-normal text-on-surface-variant" data-numeric>
          ({{ faNumber(items.length) }} از {{ faNumber(MAX_IMAGES) }})
        </span>
      </h2>

      <button
        v-if="canAddMore"
        type="button"
        class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container"
        @click="fileInput?.click()"
      >
        <AppIcon name="plus" class="h-4 w-4" />
        افزودن عکس
      </button>
    </div>

    <input
      ref="fileInput"
      type="file"
      accept="image/*"
      multiple
      class="hidden"
      @change="onPick"
    />

    <div v-if="loading" class="skeleton h-40 w-full" aria-hidden="true"></div>

    <template v-else>
      <!-- ناحیهٔ کشیدن‌ورها / حالت خالی -->
      <div
        v-if="items.length === 0"
        class="rounded-xl border-2 border-dashed p-10 text-center transition-colors"
        :class="dragOver ? 'border-primary bg-primary/5' : 'border-outline-variant'"
        @dragover.prevent="dragOver = true"
        @dragleave="dragOver = false"
        @drop.prevent="onDrop"
      >
        <span class="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-full bg-surface-container text-on-surface-variant">
          <AppIcon name="image" class="h-7 w-7" />
        </span>
        <p class="font-medium text-on-surface">هنوز عکسی برای این فرش ثبت نشده</p>
        <p class="mx-auto mt-1 max-w-sm text-sm text-on-surface-variant">
          عکس‌ها را اینجا رها کنید یا از دکمهٔ بالا انتخاب کنید. تصاویر قبل از ارسال خودکار کوچک می‌شوند.
        </p>
        <button
          v-if="canAddMore"
          type="button"
          class="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
          @click="fileInput?.click()"
        >
          <AppIcon name="plus" class="h-5 w-5" />
          انتخاب عکس
        </button>
      </div>

      <!-- شبکهٔ عکس‌ها -->
      <div
        v-else
        @dragover.prevent="dragOver = true"
        @dragleave="dragOver = false"
        @drop.prevent="onDrop"
      >
        <VueDraggable
          v-model="items"
          :animation="150"
          :disabled="readOnly"
          handle=".drag-handle"
          class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
          @end="persistOrder"
        >
          <figure
            v-for="(image, index) in items"
            :key="image.id"
            class="group relative overflow-hidden rounded-lg border border-outline-variant bg-surface-container"
          >
            <button
              type="button"
              class="block w-full"
              :aria-label="`نمایش بزرگ عکس ${faNumber(index + 1)}`"
              @click="openLightbox(index)"
            >
              <img
                :src="image.thumbnailUrl"
                :width="image.width || undefined"
                :height="image.height || undefined"
                alt=""
                loading="lazy"
                decoding="async"
                class="aspect-square w-full object-cover"
              />
            </button>

            <span
              v-if="image.isPrimary"
              class="absolute right-2 top-2 inline-flex items-center gap-1 rounded-full bg-primary px-2 py-0.5 text-xs text-on-primary"
            >
              <AppIcon name="check" class="h-3.5 w-3.5" />
              شاخص
            </span>

            <figcaption v-if="!readOnly" class="flex items-center justify-between gap-1 border-t border-outline-variant px-1 py-1">
              <span class="drag-handle grid h-9 w-9 cursor-grab place-items-center text-on-surface-variant" title="جابه‌جایی">
                <AppIcon name="menu" class="h-4 w-4" />
              </span>

              <span class="truncate text-[0.7rem] text-on-surface-variant" data-numeric>
                {{ formatBytes(image.sizeBytes) }}
              </span>

              <span class="flex">
                <button
                  v-if="!image.isPrimary"
                  type="button"
                  class="grid h-9 w-9 place-items-center rounded text-on-surface-variant hover:bg-surface-container-high hover:text-primary"
                  @click="setPrimary(image)"
                >
                  <AppIcon name="check" class="h-4 w-4" label="انتخاب به‌عنوان عکس شاخص" />
                </button>
                <button
                  type="button"
                  class="grid h-9 w-9 place-items-center rounded text-error hover:bg-error-container"
                  @click="remove(image)"
                >
                  <AppIcon name="trash" class="h-4 w-4" :label="`حذف عکس ${faNumber(index + 1)}`" />
                </button>
              </span>
            </figcaption>
          </figure>
        </VueDraggable>

        <p v-if="!readOnly" class="mt-3 text-xs text-on-surface-variant">
          برای تغییر ترتیب، عکس‌ها را از دستگیره جابه‌جا کنید. عکس شاخص در فهرست و برچسب نمایش داده می‌شود.
        </p>
      </div>

      <!-- نوار پیشرفت آپلود -->
      <p v-if="uploading > 0" class="mt-3 flex items-center gap-2 text-sm text-on-surface-variant" aria-live="polite">
        <span class="h-4 w-4 animate-spin rounded-full border-2 border-outline-variant border-t-primary"></span>
        در حال آپلود {{ faNumber(uploading) }} عکس…
      </p>
    </template>

    <!-- نمایش بزرگ -->
    <Teleport to="body">
      <div
        v-if="lightboxImage"
        class="fixed inset-0 z-[65] grid place-items-center bg-black/85 p-4"
        role="dialog"
        aria-modal="true"
        aria-label="نمایش بزرگ عکس"
        tabindex="-1"
        data-no-print
        @click.self="lightboxIndex = null"
        @keydown="onLightboxKey"
        ref="lightboxRoot"
      >
        <img :src="lightboxImage.url" alt="" class="max-h-[85vh] max-w-full rounded-lg object-contain" />

        <button
          type="button"
          class="absolute left-4 top-4 grid h-11 w-11 place-items-center rounded-lg bg-white/10 text-white hover:bg-white/20"
          @click="lightboxIndex = null"
        >
          <AppIcon name="close" label="بستن" />
        </button>

        <template v-if="items.length > 1">
          <button
            type="button"
            class="absolute right-4 top-1/2 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-lg bg-white/10 text-white hover:bg-white/20"
            @click="step(-1)"
          >
            <AppIcon name="arrow-right" label="عکس قبلی" />
          </button>
          <button
            type="button"
            class="absolute left-4 top-1/2 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-lg bg-white/10 text-white hover:bg-white/20"
            @click="step(1)"
          >
            <AppIcon name="arrow-left" label="عکس بعدی" />
          </button>
        </template>
      </div>
    </Teleport>
  </section>
</template>
