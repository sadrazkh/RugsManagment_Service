<script setup lang="ts">
import { ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { toPersianDigits } from '@/lib/format'
import type { Revision, DocumentTemplate } from '@/lib/types/documents'
const props = defineProps<{ templateId: string; version: number }>(), emit = defineEmits<{ restored: [template: DocumentTemplate] }>()
const revisions = ref<Revision[]>([]), error = ref('')
async function load() { try { revisions.value = await api.get(`/api/document-templates/${props.templateId}/revisions`) } catch (e) { error.value = errorMessage(e) } }
async function restore(rev: number) { try { emit('restored', await api.post(`/api/document-templates/${props.templateId}/revisions/${rev}/restore`, { version: props.version })); await load() } catch (e) { error.value = errorMessage(e) } }
</script>
<template><details class="rounded-lg border border-outline-variant p-3" @toggle="($event.target as HTMLDetailsElement).open && load()"><summary class="min-h-11 cursor-pointer font-bold">نسخه‌های پیشین</summary><p v-if="error" role="alert">{{ error }}</p><ul><li v-for="r in revisions" :key="r.revision" class="flex items-center justify-between border-b border-outline-variant py-2"><span>نسخهٔ {{ toPersianDigits(String(r.revision)) }} — {{ new Date(r.savedAt).toLocaleString('fa-IR') }}</span><button class="min-h-11 px-4 text-primary" @click="restore(r.revision)">بازگردانی</button></li></ul></details></template>
