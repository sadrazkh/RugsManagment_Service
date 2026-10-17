<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import type { ImportJob, ImportUndo } from '@/lib/types/import'
import { importStatuses } from '@/lib/messages/import'
import { confirmDialog } from '@/lib/ui'
const jobs = ref<ImportJob[]>([])
const error = ref('')
const busy = ref(false)
const blockers = ref<string[]>([])
const n = (v: number) => new Intl.NumberFormat('fa-IR').format(v)
async function load() { jobs.value = await api.get<ImportJob[]>('/api/imports') }
async function undo(job: ImportJob) {
  if (busy.value || !await confirmDialog({ title: 'برگرداندن ورود', message: 'این ورود برگردانده شود؟', confirmLabel: 'برگردان' })) return
  busy.value = true; error.value = ''; blockers.value = []
  try { const r = await api.post<ImportUndo>(`/api/imports/${job.id}/undo`, {}); blockers.value = r.blockers; if (r.ok) await load() }
  catch (e) { error.value = errorMessage(e) } finally { busy.value = false }
}
onMounted(async () => { busy.value = true; try { await load() } catch (e) { error.value = errorMessage(e) } finally { busy.value = false } })
</script>
<template>
  <section class="space-y-4" :aria-busy="busy">
    <p v-if="error" role="alert" class="text-error">{{ error }}</p>
    <ul v-if="blockers.length" role="alert"><li v-for="blocker in blockers" :key="blocker">{{ blocker }}</li></ul>
    <p v-if="busy" role="status">در حال انجام…</p>
    <p v-else-if="!jobs.length" class="rounded-2xl border border-outline-variant p-6">هنوز فایلی وارد نکرده‌اید.</p>
    <article v-for="job in jobs" :key="job.id" class="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-outline-variant bg-surface-container p-4">
      <div class="min-w-0"><h2 class="font-bold break-all">{{ n(job.number) }} · {{ job.fileName }}</h2><p>{{ importStatuses[job.status] }} · {{ new Date(job.createdAt).toLocaleDateString('fa-IR') }}</p></div>
      <div class="flex gap-3"><a :href="`/Import?id=${job.id}`" class="inline-flex min-h-11 items-center px-3 text-primary">باز کردن</a><button v-if="job.status === 2" type="button" :disabled="busy" class="min-h-11 rounded-xl border border-outline px-4" @click="undo(job)">برگردان</button></div>
    </article>
  </section>
</template>
