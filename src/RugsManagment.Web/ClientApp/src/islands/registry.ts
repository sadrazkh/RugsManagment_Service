/**
 * فهرست جزیره‌ها: نام (که در data-island استفاده می‌شود) → بارگذارِ lazy کامپوننت.
 * هر جزیرهٔ جدید فقط یک سطر اینجا اضافه می‌کند؛ Vite آن را به‌صورت جدا code-split می‌کند.
 */
export const islands: Record<string, () => Promise<{ default: unknown }>> = {
  'rug-form': () => import('./RugForm.vue'),
  'rug-workflow': () => import('./RugWorkflowPanel.vue'),
  'workflow-template-editor': () => import('./WorkflowTemplateEditor.vue'),
  'list-quick-advance': () => import('./ListQuickAdvance.vue'),
  'rug-bulk-toolbar': () => import('./RugBulkToolbar.vue'),
  'group-detail': () => import('./GroupDetail.vue'),
  'label-designer': () => import('./LabelDesigner.vue'),
  'custom-fields-manager': () => import('./CustomFieldsManager.vue'),
}
