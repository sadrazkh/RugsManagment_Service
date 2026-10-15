/**
 * پیش‌نویس ردیف‌های ذخیره‌نشدهٔ جدول روی همین دستگاه (G.7).
 *
 * چرا: انبار اینترنت پایدار ندارد. اگر فرشی که نوشته شده با قطع شبکه یا بستن صفحه گم شود، بازرگان دیگر به جدول اعتماد
 * نمی‌کند. پس هر ردیف تغییرکرده (با تأخیر ۳۰۰ میلی‌ثانیه) در localStorage نوشته می‌شود و دفعهٔ بعد پیشنهاد برگشت دارد.
 *
 * - کلید: `rugs:draft:{tenantId}:{userId}:{gridId}:{contextId}` (contextId برای جدول ورود همان روز است). بی کاربر
 *   (صفحهٔ بی‌layout) هیچ چیز نوشته نمی‌شود.
 * - «پاک کردن» هرگز یک‌ضربه‌ای نیست و حتی بعدش ۲۴ ساعت راه برگشت هست: محموله پیش از حذف به
 *   `rugs:draft-discarded:{…همان…}` می‌رود تا toast «پاک شد [برگردان]» برش گرداند.
 * - هر دسترسی به storage در try/catch است: حالت خصوصی، سهمیهٔ پر یا storage بسته فقط پیش‌نویس را بی‌اثر می‌کند،
 *   هرگز جدول را نمی‌شکند.
 */
import type { JsonValue } from '@/lib/types/grid'

/** یک ردیف پیش‌نویس. */
export interface DraftRow {
  clientRowId: string
  rugId: string | null
  version: number | null
  /** مقدار کلیدهای تغییرکرده (ردیف تازه: همهٔ کلیدهای پر) */
  values: Record<string, JsonValue>
  /** مقدار پیشین همان کلیدها در سرور (پایهٔ ادغام سه‌طرفه) */
  base: Record<string, JsonValue>
  dirty: string[]
  /** شماره از پیشنهاد آمده (قاعدهٔ دسته با تعداد) */
  serialSuggested?: boolean
}

export interface DraftPayload {
  v: 1
  savedAt: number
  rows: DraftRow[]
}

export interface DiscardedPayload extends DraftPayload {
  expiresAt: number
}

export interface DraftScope {
  tenantId: string | null
  userId: string | null
  gridId: string
  /** زمینه: روز جدول ورود، «list» برای فهرست فرش‌ها، … */
  contextId: string
}

/** نسخهٔ کمینهٔ Storage (برای آزمون). */
export interface StorageLike {
  getItem(key: string): string | null
  setItem(key: string, value: string): void
  removeItem(key: string): void
}

export const DRAFT_PREFIX = 'rugs:draft'
export const DISCARDED_PREFIX = 'rugs:draft-discarded'
/** محمولهٔ پاک‌شده تا ۲۴ ساعت برمی‌گردد. */
export const DISCARD_TTL_MS = 24 * 60 * 60 * 1000

function suffix(scope: DraftScope): string | null {
  if (!scope.tenantId || !scope.userId) return null
  return `${scope.tenantId}:${scope.userId}:${scope.gridId}:${scope.contextId}`
}

export function draftKey(scope: DraftScope): string | null {
  const s = suffix(scope)
  return s ? `${DRAFT_PREFIX}:${s}` : null
}

export function discardedKey(scope: DraftScope): string | null {
  const s = suffix(scope)
  return s ? `${DISCARDED_PREFIX}:${s}` : null
}

/** localStorage اگر در دسترس است؛ خودِ دسترسی هم شاید خطا بدهد (کوکی بسته). */
export function safeStorage(): StorageLike | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    return null
  }
}

function isRow(value: unknown): value is DraftRow {
  if (typeof value !== 'object' || value === null) return false
  const r = value as Record<string, unknown>
  return (
    typeof r.clientRowId === 'string' &&
    (r.rugId === null || typeof r.rugId === 'string') &&
    typeof r.values === 'object' &&
    r.values !== null &&
    Array.isArray(r.dirty)
  )
}

function readJson(storage: StorageLike | null, key: string | null): unknown {
  if (!storage || !key) return null
  try {
    const raw = storage.getItem(key)
    return raw ? (JSON.parse(raw) as unknown) : null
  } catch {
    return null
  }
}

function writeJson(storage: StorageLike | null, key: string | null, value: unknown): boolean {
  if (!storage || !key) return false
  try {
    storage.setItem(key, JSON.stringify(value))
    return true
  } catch {
    return false
  }
}

function remove(storage: StorageLike | null, key: string | null): void {
  if (!storage || !key) return
  try {
    storage.removeItem(key)
  } catch {
    /* storage بسته؛ کاری نمی‌شود کرد */
  }
}

function payloadOf(raw: unknown): DraftPayload | null {
  if (typeof raw !== 'object' || raw === null) return null
  const p = raw as Record<string, unknown>
  if (!Array.isArray(p.rows)) return null
  const rows = p.rows.filter(isRow).map((r) => ({ ...r, base: r.base ?? {}, version: r.version ?? null }))
  return { v: 1, savedAt: typeof p.savedAt === 'number' ? p.savedAt : 0, rows }
}

/** پیش‌نویس این زمینه، یا null. */
export function readDraft(scope: DraftScope, storage: StorageLike | null = safeStorage()): DraftPayload | null {
  const payload = payloadOf(readJson(storage, draftKey(scope)))
  return payload && payload.rows.length > 0 ? payload : null
}

/** ردیف‌ها را می‌نویسد؛ فهرست خالی کلید را پاک می‌کند. true اگر نوشته (یا پاک) شد. */
export function writeDraft(
  scope: DraftScope,
  rows: readonly DraftRow[],
  storage: StorageLike | null = safeStorage(),
  now: number = Date.now(),
): boolean {
  const key = draftKey(scope)
  if (!storage || !key) return false
  if (rows.length === 0) {
    remove(storage, key)
    return true
  }
  const payload: DraftPayload = { v: 1, savedAt: now, rows: [...rows] }
  return writeJson(storage, key, payload)
}

export function clearDraft(scope: DraftScope, storage: StorageLike | null = safeStorage()): void {
  remove(storage, draftKey(scope))
}

/**
 * «پاک کن» پس از تأیید: محموله پیش از حذف به کلید «پاک‌شده» با انقضای ۲۴ ساعت می‌رود. `rows` (اگر داده شود) همان
 * ردیف‌هایی است که پاک می‌شود؛ وگرنه کل پیش‌نویس. خروجی: محمولهٔ منتقل‌شده یا null.
 */
export function discardDraft(
  scope: DraftScope,
  rows?: readonly DraftRow[],
  storage: StorageLike | null = safeStorage(),
  now: number = Date.now(),
): DiscardedPayload | null {
  const current = readDraft(scope, storage)
  const list = rows ? [...rows] : current?.rows ?? []
  if (list.length === 0) return null
  const payload: DiscardedPayload = { v: 1, savedAt: now, rows: list, expiresAt: now + DISCARD_TTL_MS }
  writeJson(storage, discardedKey(scope), payload)
  if (rows && current) {
    // فقط همین ردیف‌ها پاک شوند؛ بقیهٔ پیش‌نویس (نوشته‌های همین نشست) بماند
    const gone = new Set(rows.map((r) => r.clientRowId))
    writeDraft(scope, current.rows.filter((r) => !gone.has(r.clientRowId)), storage, now)
  } else {
    clearDraft(scope, storage)
  }
  return payload
}

/** محمولهٔ پاک‌شدهٔ هنوز معتبر (و پاک کردن کلید منقضی). */
export function readDiscarded(
  scope: DraftScope,
  storage: StorageLike | null = safeStorage(),
  now: number = Date.now(),
): DiscardedPayload | null {
  const key = discardedKey(scope)
  const raw = readJson(storage, key) as { expiresAt?: unknown } | null
  const payload = payloadOf(raw)
  if (!payload) return null
  const expiresAt = typeof raw?.expiresAt === 'number' ? raw.expiresAt : 0
  if (expiresAt <= now) {
    remove(storage, key)
    return null
  }
  return { ...payload, expiresAt }
}

/**
 * «برگردان» روی toast «پاک شد»: ردیف‌های پاک‌شده دوباره پیش‌نویس می‌شوند (کنار نوشته‌های فعلی) و کلید «پاک‌شده» حذف
 * می‌شود. خروجی: ردیف‌های برگشته یا null اگر چیزی (یا وقتی) نمانده.
 */
export function restoreDiscarded(
  scope: DraftScope,
  storage: StorageLike | null = safeStorage(),
  now: number = Date.now(),
): DraftRow[] | null {
  const discarded = readDiscarded(scope, storage, now)
  if (!discarded) return null
  const current = readDraft(scope, storage)?.rows ?? []
  const ids = new Set(current.map((r) => r.clientRowId))
  writeDraft(scope, [...current, ...discarded.rows.filter((r) => !ids.has(r.clientRowId))], storage, now)
  remove(storage, discardedKey(scope))
  return discarded.rows
}
