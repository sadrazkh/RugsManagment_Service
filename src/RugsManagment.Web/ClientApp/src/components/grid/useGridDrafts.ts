/**
 * پیش‌نویس جدول به‌صورت composable (G.7): هر ردیف ذخیره‌نشده با ۳۰۰ میلی‌ثانیه تأخیر نوشته می‌شود؛ هنگام باز شدن،
 * پیش‌نویس جاماندهٔ دفعهٔ قبل «در انتظار تصمیم» می‌ماند تا کاربر «برگرداندن ردیف‌ها» یا «پاک کردن» را بزند.
 *
 * تا کاربر تصمیم نگرفته، ردیف‌های قدیمی کنار نوشته‌های تازه در همان کلید می‌مانند؛ پس اگر بی‌تصمیم صفحه را ببندد
 * هیچ‌کدام گم نمی‌شود. کار با storage خودش در `lib/gridDrafts.ts` است (try/catch، کلیدها، ۲۴ ساعت برگشت).
 */
import { ref } from 'vue'
import {
  discardDraft,
  readDraft,
  restoreDiscarded,
  safeStorage,
  writeDraft,
  type DraftRow,
  type DraftScope,
  type StorageLike,
} from '@/lib/gridDrafts'

export interface UseGridDraftsOptions {
  scope: () => DraftScope
  /** ردیف‌های ذخیره‌نشدهٔ همین حالا */
  collect: () => DraftRow[]
  delayMs?: number
  storage?: () => StorageLike | null
  now?: () => number
}

export function useGridDrafts(options: UseGridDraftsOptions) {
  const delay = options.delayMs ?? 300
  const storage = options.storage ?? safeStorage
  const now = options.now ?? Date.now

  /** پیش‌نویس جامانده از دفعهٔ قبل که هنوز تصمیمی برایش گرفته نشده */
  const pending = ref<DraftRow[] | null>(null)
  let timer: ReturnType<typeof setTimeout> | null = null

  function cancelTimer() {
    if (timer !== null) {
      clearTimeout(timer)
      timer = null
    }
  }

  /** پیش‌نویس این زمینه را بخوان؛ ردیف‌هایی که همین حالا در جدول‌اند (`present`) پیشنهاد نمی‌شوند. */
  function check(present: ReadonlySet<string> = new Set()): number {
    cancelTimer()
    const found = readDraft(options.scope(), storage())?.rows.filter((r) => !present.has(r.clientRowId)) ?? []
    pending.value = found.length > 0 ? found : null
    return found.length
  }

  /** همین حالا بنویس. */
  function flush(): void {
    cancelTimer()
    const rows = options.collect()
    const ids = new Set(rows.map((r) => r.clientRowId))
    const waiting = (pending.value ?? []).filter((r) => !ids.has(r.clientRowId))
    writeDraft(options.scope(), [...rows, ...waiting], storage(), now())
  }

  /** پس از هر تغییر صدا بزنید (۳۰۰ میلی‌ثانیه بعد نوشته می‌شود). */
  function schedule(): void {
    cancelTimer()
    timer = setTimeout(() => {
      timer = null
      flush()
    }, delay)
  }

  /** «برگرداندن ردیف‌ها»: ردیف‌های جامانده را بده تا جدول برشان گرداند. */
  function takePending(): DraftRow[] {
    const rows = pending.value ?? []
    pending.value = null
    schedule()
    return rows
  }

  /** «پاک کن» (پس از تأیید): ردیف‌های جامانده به کلید «پاک‌شده» می‌روند. خروجی: تعداد. */
  function discardPending(): number {
    const rows = pending.value ?? []
    if (rows.length > 0) discardDraft(options.scope(), rows, storage(), now())
    pending.value = null
    flush()
    return rows.length
  }

  /** «برگردان» روی toast «پاک شد». */
  function undoDiscard(): DraftRow[] | null {
    return restoreDiscarded(options.scope(), storage(), now())
  }

  function dispose(): void {
    cancelTimer()
  }

  return { pending, check, flush, schedule, takePending, discardPending, undoDiscard, dispose }
}
