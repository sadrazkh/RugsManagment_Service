/**
 * قاعده‌های کوچک فرم فرش (`RugForm.vue`) — جدا تا بی DOM آزموده شوند.
 *
 * - شماره (G.4): در «پیشنهاد بعدی» Enter یا Tab در خانهٔ خالی پیشنهاد کم‌رنگ را می‌پذیرد؛ در «خودکار» خانه فقط نمایش است،
 *   چون سرور شمارهٔ فرش تازه را خودش زیر قفل کارگاه می‌گذارد و متن کاربر را بی‌صدا کنار می‌گذاشت.
 * - Enter در خانه‌های ویرایشگر سطرهای خرید که داخل همین فرم است نباید فرم فرش را بفرستد (ارسال ضمنی مرورگر): آن خانه‌ها
 *   دکمهٔ ذخیرهٔ خودشان را دارند و ارسال فرم صفحه را با سطرهای ذخیره‌نشده ترک می‌کرد.
 * - نسخهٔ فرش بعد از ذخیرهٔ ویرایشگر خرید فقط وقتی جلو می‌رود که ذخیره روی همان نسخهٔ فرم بوده؛ وگرنه ویرایشگر تغییر کس
 *   دیگری را خوانده و فرمِ ساخته‌شده از دادهٔ کهنه باید ۴۰۹ بگیرد، نه این‌که تغییر او را بی‌صدا رونویسی کند.
 */
import { SerialMode } from '@/lib/types/platform'

/**
 * کلیدی که در خانهٔ شماره زده شد پیشنهاد را می‌پذیرد؟ شمارهٔ پذیرفته‌شده، یا null. Shift+Tab (برگشت) نمی‌پذیرد؛ متنی که
 * کاربر نوشته هرگز جایگزین نمی‌شود.
 */
export function acceptedSerialSuggestion(
  key: string,
  shiftKey: boolean,
  typed: string,
  suggestion: string | null,
  mode: SerialMode | null | undefined,
): string | null {
  if (key !== 'Enter' && !(key === 'Tab' && !shiftKey)) return null
  if (mode === SerialMode.Auto || mode === SerialMode.Manual) return null
  if (!suggestion || typed.trim() !== '') return null
  return suggestion
}

/** خانهٔ شمارهٔ فرش تازه فقط نمایش است (حالت «خودکار»). */
export function isAutoSerial(mode: SerialMode | null | undefined, isEdit: boolean): boolean {
  return !isEdit && mode === SerialMode.Auto
}

/**
 * نسخهٔ فرم بعد از ذخیرهٔ ویرایشگر خرید: نسخهٔ تازه فقط اگر ذخیره روی همان نسخهٔ فرم فرستاده شده بود؛ وگرنه همان نسخهٔ
 * فرم می‌ماند تا ذخیرهٔ فرم ۴۰۹ بگیرد («صفحه را تازه کن»).
 */
export function adoptSavedVersion(formVersion: number, baseVersion: number, savedVersion: number): number {
  return baseVersion === formVersion ? savedVersion : formVersion
}

/** کمینهٔ رویداد کلید برای {@link blocksImplicitSubmit}. */
export interface KeyLike {
  key: string
  target: unknown
  isComposing?: boolean
}

/**
 * Enter در یک input (نه دکمه، نه textarea) فرم بیرونی را به‌طور ضمنی می‌فرستد؛ این‌جا باید جلویش گرفته شود. حین
 * ترکیب حروف (IME) Enter مال ترکیب است.
 */
export function blocksImplicitSubmit(event: KeyLike): boolean {
  if (event.key !== 'Enter' || event.isComposing) return false
  const tag = (event.target as { tagName?: unknown } | null)?.tagName
  return typeof tag === 'string' && tag.toUpperCase() === 'INPUT'
}
