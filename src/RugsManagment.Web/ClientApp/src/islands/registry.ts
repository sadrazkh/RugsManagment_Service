/**
 * فهرست جزیره‌ها: نام (که در data-island استفاده می‌شود) → بارگذارِ lazy کامپوننت.
 *
 * دو منبع:
 *   • خودکار: هر فایل `islands/**\/*.island.vue` با نامِ kebab-case بخش اول نام فایل
 *     (`islands/platform/HomeCoach.island.vue` → `home-coach`). فازهای بعد فقط فایل اضافه می‌کنند و این
 *     فایل را ویرایش نمی‌کنند.
 *   • ۱۹ جزیرهٔ قدیمی با نام صریح پایین همین فایل (نامشان با نام فایل یکی نیست و viewها به همین نام‌ها
 *     تکیه دارند).
 *
 * Vite هر جزیره را جدا code-split می‌کند. نام تکراری (دو فایل با یک نام، یا هم‌نام با یک جزیرهٔ قدیمی) در
 * کنسول خطا می‌دهد و `islandConflicts` را پر می‌کند؛ نسخهٔ صریح قدیمی (یا اولین فایل به ترتیب مسیر) می‌ماند.
 */
import { toKebab } from '@/lib/ui'

export type IslandLoader = () => Promise<{ default: unknown }>

/** جزیره‌های پیش از بازطراحی — نام‌ها ثابت‌اند؛ چیزی به این فهرست اضافه نکنید (فایل *.island.vue بسازید). */
const legacy: Record<string, IslandLoader> = {
  'rug-form': () => import('./RugForm.vue'),
  'rug-workflow': () => import('./RugWorkflowPanel.vue'),
  'rug-gallery': () => import('./RugGallery.vue'),
  'rug-sale': () => import('./RugSalePanel.vue'),
  'sales-report': () => import('./SalesReport.vue'),
  'analytics-dashboard': () => import('./AnalyticsDashboard.vue'),
  'aging-alert': () => import('./AgingAlert.vue'),
  'step-type-manager': () => import('./StepTypeManager.vue'),
  'qr-scanner': () => import('./QrScanner.vue'),
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

/** نام جزیره از مسیر فایل: «./settings/PreferencesForm.island.vue» → «preferences-form». */
export function islandNameFromPath(path: string): string {
  const file = path.split('/').pop() ?? path
  return toKebab(file.replace(/\.island\.vue$/, ''))
}

function build(): { islands: Record<string, IslandLoader>; conflicts: string[] } {
  const islands: Record<string, IslandLoader> = { ...legacy }
  const conflicts: string[] = []
  const discovered = import.meta.glob<{ default: unknown }>('./**/*.island.vue')

  for (const path of Object.keys(discovered).sort()) {
    const name = islandNameFromPath(path)
    if (name in islands) {
      conflicts.push(`${name} (${path})`)
      continue
    }
    islands[name] = discovered[path]
  }

  if (conflicts.length > 0) console.error('[island] نام تکراری؛ فقط اولی بار می‌شود:', conflicts)
  return { islands, conflicts }
}

const registry = build()

/** همهٔ جزیره‌ها، به نام. */
export const islands: Readonly<Record<string, IslandLoader>> = Object.freeze(registry.islands)

/** نام‌های تکراری (باید خالی باشد). */
export const islandConflicts: readonly string[] = registry.conflicts
