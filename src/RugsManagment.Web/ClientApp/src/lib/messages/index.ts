/**
 * پیام‌های فارسی با کد ثابت — آینهٔ `App/Common/Messages.fa.cs`.
 *
 * چرا کد: پیامِ یک خطا در جزیره و در toast صفحه باید یکی باشد و بازرگان هرگز متن انگلیسی نبیند. سرور
 * پیام آماده (`message`) می‌فرستد؛ این فایل برای پیام‌هایی است که خودِ کلاینت می‌سازد (اعتبارسنجی خانه،
 * قطعی اینترنت، پرسش «۱۰٬۰۰۰؟») و باید همان متن سرور را داشته باشد.
 *
 * این فایل کدهای پایه و سکو را دارد. هر فاز یک فایل کنار همین می‌گذارد (`lib/messages/kit.ts`،
 * `lib/messages/shipments.ts`، …) که فقط این را export می‌کند و این فایل را import نمی‌کند (حلقه نسازد):
 *
 * ```ts
 * export const messages: Record<string, string> = { W_BALE_OVER_TARGET: '…' }
 * ```
 *
 * همه با `import.meta.glob` خودکار جمع می‌شوند؛ پس هیچ فازی این فایل را ویرایش نمی‌کند. کد تکراری خطای
 * آزمون است (`lib/messages/*.spec.ts`) و در اجرا اولی می‌ماند.
 *
 * نشانه‌ها: `{n}`، `{label}`، `{amount}`، `{serial}`، … با مقدار داده‌شده پر می‌شوند؛ عدد با ارقام فارسی و
 * «٬»/«٫». `{bale}`، `{bales}`، `{baleOf}`، `{baleFooter}` اگر داده نشوند از `terms` در app-context می‌آیند.
 */
import { appContext } from '@/lib/context'

/** کدهای پایه — دقیقاً همان متن‌های Messages.fa.cs. */
export const coreMessages = {
  // خطاهای عمومی
  E_FORBIDDEN: 'این کار برای شما مجاز نیست.',
  E_NOT_FOUND: 'پیدا نشد؛ شاید حذف شده باشد.',
  E_CONFLICT: 'این ردیف را همین الان کس دیگری هم عوض کرد. صفحه را تازه کنید و دوباره بنویسید.',
  E_VALIDATION: 'چند خانه درست پر نشده؛ پیام زیر هر خانه را ببینید.',
  E_SERVER: 'کار انجام نشد؛ دوباره امتحان کنید.',
  E_NETWORK: 'ذخیره نشد؛ اینترنت را نگاه کنید.',

  // دسترسی و پول
  E_MONEY_FORBIDDEN: 'مبلغ‌ها را فقط بازرگان یا مدیر می‌تواند ثبت یا عوض کند.',
  E_RUG_DELETE_FORBIDDEN: 'این فرش را فقط بازرگان می‌تواند حذف کند.',
  E_MONEY_LOCKED: 'واحد پول و تعداد رقم اعشار مبلغ‌ها بعد از ثبت اولین مبلغ عوض نمی‌شود.',

  // خانه‌های فرم
  E_REQUIRED: '«{label}» را بنویسید.',
  E_INVALID_VALUE: '«{label}» درست نیست.',
  E_OUT_OF_RANGE: '«{label}» باید از {min} تا {max} باشد.',
  E_TOO_LONG: '«{label}» حداکثر {n} حرف است.',

  // کاربران و نقش‌ها
  E_INVALID_ROLE: 'این نقش برای کاربر کارگاه نیست.',
  E_LAST_ADMIN: 'کارگاه باید دست‌کم یک تنظیم‌کنندهٔ فعال داشته باشد.',
  E_SELF_DEMOTE: 'نقش خودتان را نمی‌توانید پایین بیاورید و حساب خودتان را نمی‌توانید غیرفعال کنید.',

  // ترجیحات کارگاه
  E_CURRENCY_NOT_ALLOWED: 'واحد پول کارگاه فقط تومان یا ریال است.',
  E_SKU_PREFIX: 'پیشوند کد باید ۱ تا ۱۲ حرف انگلیسی یا رقم باشد.',

  // عدد، اندازه و تاریخ
  E_DIMENSION: 'اندازه را به متر بنویسید، مثل ۲٫۰۸ (از ۵ سانت تا ۳۰ متر)',
  E_MONEY_DECIMALS: 'مبلغ {currency} اعشار ندارد',
  E_NUMBER: 'عدد درست نیست؛ فقط رقم و یک ممیز بنویسید.',
  E_NEGATIVE: 'عدد منفی این‌جا پذیرفته نیست.',
  E_DATE_IMPOSSIBLE: '{date} وجود ندارد',
  E_DATE_YEAR: 'سال را شمسی بنویسید (مثل ۱۴۰۵)',
  Q_MONEY_THOUSANDS: 'منظورتان {amount} است؟',

  // وضعیت ذخیره
  I_SAVING: 'در حال ذخیره…',
  I_SAVED: 'ذخیره شد',
  I_RETRYING: 'ذخیره نشد؛ دوباره امتحان می‌کنیم',
  I_OFFLINE: 'اینترنت قطع است. نوشته‌هایتان روی همین دستگاه مانده و از بین نمی‌رود.',
  I_DELETED: 'حذف شد',
  I_PREFERENCES_SAVED: 'ترجیحات ذخیره شد.',
} as const satisfies Record<string, string>

export type CoreMessageCode = keyof typeof coreMessages

/** مقدار یک نشانه: عدد با ارقام فارسی نوشته می‌شود؛ null/undefined یعنی «نشانه را دست نزن». */
export type MessageTokens = Record<string, string | number | null | undefined>

interface MessageModule {
  messages?: Record<string, string>
}

// فایل‌های پیام فازها (همهٔ ‎./*.ts جز همین فایل و آزمون‌ها)، همگام در build جمع می‌شوند
const featureModules = import.meta.glob<MessageModule>(['./*.ts', '!./index.ts', '!./*.spec.ts'], {
  eager: true,
})

function collect(): { all: Readonly<Record<string, string>>; duplicates: string[] } {
  const all: Record<string, string> = { ...coreMessages }
  const duplicates: string[] = []
  for (const path of Object.keys(featureModules).sort()) {
    const extra = featureModules[path]?.messages
    if (!extra) continue
    for (const [code, text] of Object.entries(extra)) {
      if (code in all) duplicates.push(code)
      else all[code] = text
    }
  }
  return { all: Object.freeze(all), duplicates }
}

const collected = collect()

/** همهٔ پیام‌ها (پایه + همهٔ فایل‌های فاز)، به کد. */
export const messages: Readonly<Record<string, string>> = collected.all

/** کدهایی که در دو فایل آمده‌اند — باید خالی باشد (آزمون بررسی می‌کند). */
export const duplicateMessageCodes: readonly string[] = collected.duplicates

const TOKEN = /\{([A-Za-z][A-Za-z0-9]*)\}/g

/** عدد برای متن فارسی: ۱۰۰۰۰۰۰۰ → «۱۰٬۰۰۰٬۰۰۰»، ۲٫۵ → «۲٫۵» (حداکثر دو رقم اعشار، مثل سرور). */
export function formatMessageNumber(value: number): string {
  if (!Number.isFinite(value)) return ''
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 })
    .format(value)
    .replace(/[0-9,.]/g, (c) => (c === ',' ? '٬' : c === '.' ? '٫' : '۰۱۲۳۴۵۶۷۸۹'[Number(c)]))
}

/**
 * نشانه‌های `{key}` یک الگو را پر می‌کند. نشانهٔ بی‌مقدار دست‌نخورده می‌ماند تا جاافتادنش دیده شود؛
 * واژه‌های نگله/عدل اگر داده نشوند از app-context می‌آیند.
 */
export function fillMessage(template: string, tokens: MessageTokens = {}): string {
  const terms = appContext().terms
  const values: MessageTokens = {
    bale: terms.bale,
    bales: terms.bales,
    baleOf: terms.baleOf,
    baleFooter: terms.baleFooter,
    ...tokens,
  }
  return template.replace(TOKEN, (match, key: string) => {
    const value = values[key]
    if (value === null || value === undefined) return match
    return typeof value === 'number' ? formatMessageNumber(value) : value
  })
}

/** متن پرشدهٔ یک کد؛ کدِ ناشناخته پیام عمومی «کار انجام نشد» می‌شود، نه خودِ کد انگلیسی. */
export function message(code: string, tokens?: MessageTokens): string {
  return fillMessage(messages[code] ?? coreMessages.E_SERVER, tokens)
}
