/**
 * درگاه افزونهٔ نوار عملیات گروهی فهرست فرش‌ها (`[data-bulk-action]`، J.2).
 *
 * چرا: نوار گروهی (`RugBulkToolbar.vue`) از فاز سکو یخ‌زده است؛ فازهای بعد دکمهٔ خودشان را («افزودن به
 * نگله»، «ثبت هزینه برای این‌ها») با یک فایل تازه در `CA/bulk-actions/*.action.ts` اضافه می‌کنند و نوار
 * را ویرایش نمی‌کنند. هر فایل یک `BulkAction` را default export می‌کند:
 *
 * ```ts
 * import { defineBulkAction } from '@/lib/bulkActions'
 * export default defineBulkAction({
 *   key: 'add-to-bale', label: 'افزودن به نگله', icon: 'package', order: 100, policy: 'floor',
 *   async run(ctx) { const { openPanel } = await import('@/lib/ui'); … },
 * })
 * ```
 *
 * کار سنگین را داخل `run` با `import()` بار کنید تا نوار سبک بماند. دسترسی واقعی را سرور می‌سنجد؛
 * `policy` و `simple` فقط دکمه‌ای را که بیننده نمی‌تواند به کار ببرد پنهان می‌کنند.
 */
import { appContext, isSimpleUi, type AppContext } from '@/lib/context'

export type BulkActionPolicy = 'floor' | 'business' | 'configure'

export interface BulkActionContext {
  /** شناسهٔ فرش‌های انتخاب‌شده (به ترتیب صفحه) */
  readonly rugIds: readonly string[]
  /** خالی کردن انتخاب */
  clearSelection(): void
  /** تازه کردن صفحه (پیام موفقیت را پیش از آن با `toastAfterReload` بگذارید) */
  reload(): void
  /** قفل نوار در حین کار طولانی */
  setBusy(busy: boolean): void
}

export interface BulkAction {
  /** کلید یکتا؛ روی دکمه `data-bulk-action="{key}"` می‌نشیند */
  key: string
  /** برچسب دکمه (فارسی، فعل روشن) */
  label: string
  /** آیکون از /icons.svg */
  icon: string
  /** ترتیب در نوار؛ کوچک‌تر اول (دکمه‌های خودِ نوار ۱۰ تا ۵۰ هستند) */
  order: number
  /** کمترین سیاست لازم؛ پیش‌فرض floor */
  policy?: BulkActionPolicy
  /** در حالت ساده هم دیده شود؟ پیش‌فرض true */
  simple?: boolean
  /** دکمهٔ اصلی (رنگ primary) */
  primary?: boolean
  run(ctx: BulkActionContext): void | Promise<void>
}

/** فقط برای نوع‌دهی فایل‌های action. */
export function defineBulkAction(action: BulkAction): BulkAction {
  return action
}

type ActionModule = { default?: BulkAction }

// همهٔ فایل‌های افزونه؛ پوشه هنوز خالی است و glob خالی برمی‌گرداند
const modules = import.meta.glob<ActionModule>('../bulk-actions/*.action.ts', { eager: true })

function isAction(value: unknown): value is BulkAction {
  const a = value as BulkAction | undefined
  return !!a && typeof a.key === 'string' && typeof a.label === 'string' && typeof a.run === 'function'
}

/** همهٔ عملیات ثبت‌شده، مرتب؛ کلید تکراری هشدار می‌دهد و اولی می‌ماند. */
export function registeredBulkActions(source: Record<string, ActionModule> = modules): BulkAction[] {
  const byKey = new Map<string, BulkAction>()
  for (const path of Object.keys(source).sort()) {
    const action = source[path]?.default
    if (!isAction(action)) {
      console.warn(`[bulk-actions] ${path} یک BulkAction را default export نمی‌کند`)
      continue
    }
    if (byKey.has(action.key)) {
      console.warn(`[bulk-actions] کلید تکراری «${action.key}» در ${path}`)
      continue
    }
    byKey.set(action.key, action)
  }
  return [...byKey.values()].sort((a, b) => a.order - b.order || a.key.localeCompare(b.key))
}

function allowed(policy: BulkActionPolicy | undefined, ctx: Readonly<AppContext>): boolean {
  switch (policy ?? 'floor') {
    case 'configure':
      return ctx.policies.configure
    case 'business':
      return ctx.policies.business
    default:
      return ctx.policies.floor
  }
}

/** عملیاتی که بینندهٔ جاری می‌بیند (سیاست و حالت ساده). */
export function visibleBulkActions(
  ctx: Readonly<AppContext> = appContext(),
  source: Record<string, ActionModule> = modules,
): BulkAction[] {
  const simple = isSimpleUi(ctx)
  return registeredBulkActions(source).filter((a) => allowed(a.policy, ctx) && (!simple || a.simple !== false))
}
