/**
 * «ثبت هزینه برای این‌ها» در نوار عملیات گروهی فهرست فرش‌ها (H.1، K.2/P5): پنل ثبت هزینه با «برای» = همین فرش‌ها
 * (دامنهٔ «انتخاب»؛ شناسه‌ها هنگام ذخیره ثابت می‌شوند، D.2). فقط Business، و در نمای ساده هم (بازرگان همین را می‌خواهد).
 *
 * خودِ نوار (`RugBulkToolbar.vue`) ویرایش نمی‌شود؛ این فایل از درگاه `bulk-actions/*.action.ts` خودکار پیدا می‌شود.
 * پنل سنگین با `import()` در لحظهٔ لمس بار می‌شود تا نوار سبک بماند. پس از ثبت صفحه تازه می‌شود تا ستون بها عوض شود.
 */
import { defineBulkAction } from '@/lib/bulkActions'

export default defineBulkAction({
  key: 'cost-entry',
  label: 'ثبت هزینه برای این‌ها',
  icon: 'coins',
  order: 120,
  policy: 'business',
  simple: true,
  async run(ctx) {
    if (ctx.rugIds.length === 0) return
    const { openPanel } = await import('@/lib/ui')
    const { CostScope } = await import('@/lib/types/costs')
    const result = await openPanel('cost-entry', {
      scope: CostScope.Selection,
      rugIds: [...ctx.rugIds],
      reloadOnSave: true,
    })
    if (result) ctx.clearSelection()
  },
})
