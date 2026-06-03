<script setup lang="ts">
/** فقط SystemAdmin — ساخت کارگاه جدید + مدیر اول */
import { onMounted, ref } from 'vue'
import api from '@/api/client'
import type { Tenant } from '@/types'

const tenants = ref<Tenant[]>([])
const form = ref({
  name: '',
  slug: '',
  adminEmail: '',
  adminPassword: '',
  adminFullName: '',
  contactPhone: '',
})

onMounted(async () => {
  const { data } = await api.get<Tenant[]>('/tenants')
  tenants.value = data
})

async function create() {
  await api.post('/tenants', form.value)
  form.value = { name: '', slug: '', adminEmail: '', adminPassword: '', adminFullName: '', contactPhone: '' }
  const { data } = await api.get<Tenant[]>('/tenants')
  tenants.value = data
}
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <header>
      <h2 class="text-2xl font-bold">مدیریت مشتریان (فرش‌بافان)</h2>
      <p class="text-sm text-on-surface-variant">ایجاد اکانت اختصاصی برای هر کارگاه</p>
    </header>

    <form class="bg-surface-container-lowest border border-outline-variant rounded-xl p-5 space-y-3" @submit.prevent="create">
      <h3 class="font-semibold">کارگاه جدید</h3>
      <div class="grid sm:grid-cols-2 gap-3">
        <input v-model="form.name" placeholder="نام کارگاه" required class="field" />
        <input v-model="form.slug" placeholder="شناسه (slug)" required class="field" />
        <input v-model="form.adminFullName" placeholder="نام مدیر" required class="field" />
        <input v-model="form.adminEmail" type="email" placeholder="ایمیل مدیر" required class="field" />
        <input v-model="form.adminPassword" type="password" placeholder="رمز عبور" required class="field" />
        <input v-model="form.contactPhone" placeholder="تلفن" class="field" />
      </div>
      <button type="submit" class="w-full py-2.5 bg-primary text-on-primary rounded-full font-medium">ایجاد کارگاه</button>
    </form>

    <ul class="space-y-2">
      <li
        v-for="t in tenants"
        :key="t.id"
        class="flex items-center justify-between p-4 rounded-xl border border-outline-variant bg-surface-container-lowest"
      >
        <div>
          <p class="font-medium">{{ t.name }}</p>
          <p class="text-xs font-mono text-on-surface-variant">{{ t.slug }}</p>
        </div>
        <span
          class="text-xs px-2 py-1 rounded-full"
          :class="t.isActive ? 'bg-secondary-container text-on-secondary-container' : 'bg-error-container text-error'"
        >
          {{ t.isActive ? 'فعال' : 'غیرفعال' }}
        </span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.field {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
}
</style>
