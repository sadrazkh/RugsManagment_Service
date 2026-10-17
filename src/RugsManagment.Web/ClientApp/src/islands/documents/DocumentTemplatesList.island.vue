<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { toPersianDigits } from '@/lib/format'
import type { DocumentTemplate } from '@/lib/types/documents'
const templates = ref<DocumentTemplate[]>([]), error = ref('')
async function load() { try { templates.value = await api.get('/api/document-templates') } catch (e) { error.value = errorMessage(e) } }
async function duplicate(t: DocumentTemplate) { try { await api.post(`/api/document-templates/${t.id}/duplicate`); await load() } catch (e) { error.value = errorMessage(e) } }
async function setDefault(t: DocumentTemplate) { try { await api.post(`/api/document-templates/${t.id}/set-default`,{ version: t.version }); await load() } catch (e) { error.value = errorMessage(e) } }
onMounted(load)
</script>
<template><section class="space-y-3 py-4"><p v-if="error" role="alert" class="text-error">{{ error }}</p><article v-for="t in templates" :key="t.id" class="flex flex-wrap items-center gap-4 rounded-xl border border-outline-variant bg-surface-container-lowest p-4"><a class="inline-flex min-h-11 items-center font-bold text-primary" :href="`/settings/documents/${t.id}`">{{ t.name }}</a><span>نسخهٔ {{ toPersianDigits(String(t.revision)) }}</span><span v-if="t.usesMoneyFields">دارای مبلغ</span><span v-if="t.isDefault">پیش‌فرض</span><button class="min-h-11 rounded-lg border border-outline-variant px-4" @click="duplicate(t)">رونوشت</button><button v-if="!t.isDefault" class="min-h-11 rounded-lg border border-outline-variant px-4" @click="setDefault(t)">انتخاب به‌عنوان پیش‌فرض</button></article><a href="/settings/setup" class="inline-flex min-h-11 items-center text-primary">بستهٔ صادرات فرش</a></section></template>
