/**
 * فهرست جزیره‌ها: نام (که در data-island استفاده می‌شود) → بارگذارِ lazy کامپوننت.
 * هر جزیرهٔ جدید فقط یک سطر اینجا اضافه می‌کند؛ Vite آن را به‌صورت جدا code-split می‌کند.
 */
export const islands: Record<string, () => Promise<{ default: unknown }>> = {
  'rug-form': () => import('./RugForm.vue'),
  'rug-workflow': () => import('./RugWorkflowPanel.vue'),
  'rug-gallery': () => import('./RugGallery.vue'),
  'rug-sale': () => import('./RugSalePanel.vue'),
  'sales-report': () => import('./SalesReport.vue'),
  'analytics-dashboard': () => import('./AnalyticsDashboard.vue'),
  'aging-alert': () => import('./AgingAlert.vue'),
  'workflow-template-editor': () => import('./WorkflowTemplateEditor.vue'),
  'list-quick-advance': () => import('./ListQuickAdvance.vue'),
  'rug-bulk-toolbar': () => import('./RugBulkToolbar.vue'),
  'group-detail': () => import('./GroupDetail.vue'),
  'label-designer': () => import('./LabelDesigner.vue'),
  'label-print': () => import('./LabelPrint.vue'),
  'pwa-install': () => import('./PwaInstall.vue'),
  'custom-fields-manager': () => import('./CustomFieldsManager.vue'),
  'providers-manager': () => import('./ProvidersManager.vue'),
  'provider-statement': () => import('./ProviderStatement.vue'),
}
