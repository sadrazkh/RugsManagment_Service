/**
 * فهرست جزیره‌ها: نام (که در data-island استفاده می‌شود) → بارگذارِ lazy کامپوننت.
 * هر جزیرهٔ جدید فقط یک سطر اینجا اضافه می‌کند؛ Vite آن را به‌صورت جدا code-split می‌کند.
 */
export const islands: Record<string, () => Promise<{ default: unknown }>> = {
  hello: () => import('./HelloIsland.vue'),
  'rug-form': () => import('./RugForm.vue'),
  'rug-cost-panel': () => import('./RugCostPanel.vue'),
  'custom-fields-manager': () => import('./CustomFieldsManager.vue'),
}
