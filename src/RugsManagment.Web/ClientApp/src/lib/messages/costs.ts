/**
 * پیام‌های دفتر هزینه، تقسیم و حساب محموله از فاز ۵ — آینهٔ `App/Common/Messages.fa.Costs.cs` (هشدارها و خطاهای تقسیم
 * D.8 و H.3b، جمله‌ها و تراشه‌های H.2) به‌اضافهٔ پیام‌هایی که فقط خودِ پنل و جزیره‌های هزینه می‌سازند (پیش‌نویس، ذخیره
 * نشد، حالت خالی، تنظیمات نوع‌ها).
 *
 * قاعدهٔ فایل‌های پیام فاز: فقط `messages` را export می‌کند و `./index` را import نمی‌کند (حلقه نسازد). هر کدی که در
 * سرور هست باید این‌جا دقیقاً همان متن را داشته باشد (`costs.spec.ts` بررسی می‌کند)؛ بیرون از نشانه‌های `{…}` هیچ حرف
 * لاتینی نیست. «نگله» همیشه با `{bale}` / `{baleOf}` تا کارگاهی که «عدل» می‌گوید «عدل» ببیند (E10)؛ واحد پول نشانهٔ
 * `{currency}` است (تومان یا ریال)، نه متن ثابت.
 */
export const messages: Record<string, string> = {
  // ── هشدارهای تقسیم (D.8) — همان سرور ──
  W_STEP_DOUBLE: 'برای {n} فرش «{category}» در کارهای کارگاه هم حساب شده؛ دوبار حساب نشود؟',
  W_PURCHASE_DOUBLE: 'برای {n} فرش هم «قیمت خرید» ثبت شده و هم «خرید یکجا»؛ مطمئنید دوبار حساب نشده؟',
  W_PIN_DROPPED: 'مبلغ ثابتِ فرش {serial} حساب نشد، چون دیگر جزو این فرش‌ها نیست.',
  W_ZERO_WEIGHTS: 'چیزی برای نسبت گرفتن نبود؛ مساوی تقسیم شد.',
  W_NO_DECLARED_VALUE: '{n} فرش ارزش گمرکی ندارند و سهمی نگرفتند.',
  W_EMPTY_TARGET: 'فرشی برای تقسیم نیست؛ هزینه «تقسیم‌نشده» ماند.',
  W_TARGET_DELETED: 'جایی که این هزینه به آن وصل بود ({bale}، محموله یا ورودی) حذف شد؛ دوباره مشخص کنید برای کدام فرش‌هاست.',
  W_LATE_COST: 'این محموله قطعی شده؛ هزینهٔ تازه فقط سود را تغییر می‌دهد، نه اظهار را.',
  W_RATE_DRIFT: 'مبلغ این رسید با اندازه‌های فعلی {amount} {currency} فرق دارد.',
  W_PINS_RESIDUAL: '{amount} {currency} از این هزینه به هیچ فرشی نرسید، چون همهٔ فرش‌ها مبلغ ثابت دارند.',
  W_NEGATIVE_WEIGHT: 'قیمت خرید {n} فرش منفی بود و سهمی نگرفتند.',

  // ── خطاهای ثبت (D.8) — همان سرور ──
  E_PINS_EXCEED: 'جمع مبلغ‌های ثابت ({pins}) از کل هزینه ({total}) بیشتر است.',
  E_PINS_BASIS: 'مبلغ ثابت فقط برای هزینهٔ «کل» است، یا هزینه‌ای که شمارهٔ رسید یا طرف حساب دارد.',
  E_CIRCULAR: 'خرید را نمی‌شود به نسبت قیمت خرید تقسیم کرد.',
  E_FX_REQUIRED: 'برای مبلغ دلاری نرخ دلار را بنویسید.',
  E_SCOPE_EMPTY_SELECTION: 'هیچ فرشی انتخاب نشده؛ شماره‌ها را بنویسید یا از لیست بزنید.',
  E_COST_PERCENT_BASIS: 'هزینه «درصد» ندارد؛ «هر متر»، «هر تخته» یا «کل» را بزنید.',
  E_COST_SHIPMENT_REQUIRED: 'محموله را مشخص کنید.',
  E_COST_BATCH_REQUIRED: 'ورودی را مشخص کنید.',
  E_COST_BALE_REQUIRED: '{bale} را مشخص کنید.',
  E_COST_CATEGORY_INACTIVE: 'این نوع هزینه غیرفعال است؛ نوع دیگری بزنید یا آن را در تنظیمات فعال کنید.',
  E_COST_DELETED: 'این هزینه حذف شده؛ اول آن را برگردانید.',
  E_COST_DECIMALS: 'مبلغ {currency} بیش از {n} رقم اعشار ندارد.',
  E_COST_TOO_LARGE: 'این مبلغ از حد بیشتر است.',
  E_COST_CATEGORY_GROUP_LOCKED: 'گروه «{category}» عوض نمی‌شود، چون با آن هزینه یا قیمت خرید ثبت شده.',
  E_COST_CATEGORY_IN_USE: '«{category}» به کار رفته و حذف نمی‌شود؛ غیرفعالش کنید.',
  E_COST_CATEGORY_NAME_TAKEN: 'نوع «{category}» از قبل هست.',
  E_REVENUE_NEGATIVE: 'فروش محموله منفی نمی‌شود.',
  E_COST_CURRENCY: 'ارز این مبلغ پذیرفته نیست؛ تومان، ریال یا دلار را بزنید.',

  // ── تراشه‌ها و جمله‌ها (H.2، H.3b) — همان سرور ──
  I_COST_UNALLOCATED: 'تقسیم‌نشده',
  I_COST_PARTIAL: '{amount} {currency} هنوز تقسیم نشده.',
  I_COST_SENTENCE_TOTAL: '{amount} {currency} {category} بین {pieces} تخته {method} تقسیم می‌شود',
  I_COST_SENTENCE_PER_M2: '{rate} {currency} برای هر متر × {area} متر = {amount} {currency}',
  I_COST_SENTENCE_PER_PIECE: '{rate} {currency} برای هر تخته × {pieces} تخته = {amount} {currency}',
  I_COST_SENTENCE_RECEIPT: 'رسید {amount} {currency} {category} بین {pieces} تخته {method} تقسیم می‌شود',
  I_COST_SAVED: 'ثبت شد · {amount} {currency} بین {pieces} تخته تقسیم شد',
  I_COST_DELETED: 'هزینهٔ شمارهٔ {number} حذف شد',
  I_COST_RESTORED: 'هزینهٔ شمارهٔ {number} برگشت',
  I_COST_METHOD_BY_AREA: 'به نسبت متراژ',
  I_COST_METHOD_BY_COUNT: 'به تعداد تخته',
  I_COST_METHOD_EQUAL: 'مساوی برای هر ردیف',
  I_COST_METHOD_BY_DECLARED: 'به نسبت ارزش گمرکی',
  I_COST_METHOD_BY_PURCHASE: 'به نسبت قیمت خرید',
  I_COST_METHOD_MANUAL: 'با سهم‌هایی که خودتان نوشتید',
  I_COST_AREA_INTAKE: '(اندازهٔ اول)',
  I_COST_TARGET_SHIPMENT: 'محمولهٔ {number} ({pieces} تخته)',
  I_COST_TARGET_BALES: '{baleOf} {numbers} ({pieces} تخته)',
  I_COST_TARGET_BATCH: 'ورودی {number} ({pieces} تخته)',
  I_COST_TARGET_RUGS: '{n} فرش ({pieces} تخته)',
  I_COST_TARGET_ONE_RUG: 'فرش {serial}',
  I_COST_SCOPE_BATCH: '{n} فرش ورودی {number}',
  I_COST_SCOPE_STOCK: '{n} فرش انبار در {date}',
  I_COST_SCOPE_SERIALS: '{n} فرش از شماره‌های نوشته‌شده',
  I_COST_SCOPE_FILTER: '{n} فرش از فهرست فرش‌ها در {date}',
  I_COST_SERIAL_NOT_FOUND: '{serial} پیدا نشد',

  // ── پنل ثبت هزینه (H.2) — فقط کلاینت ──
  I_COST_ALLOCATED: 'تقسیم شد',
  I_COST_SENTENCE_MANUAL: '{amount} {currency} {category}؛ سهم هر فرش را خودتان می‌نویسید',
  I_COST_SENTENCE_NO_TARGET: 'بنویسید این هزینه برای کدام فرش‌هاست تا تقسیمش معلوم شود.',
  I_COST_SENTENCE_NO_AMOUNT: 'مبلغ را بنویسید تا سهم هر فرش معلوم شود.',
  I_COST_SUMMARY: '{basis} {amount} · {method}',
  I_COST_SAVED_UNALLOCATED: 'ثبت شد · {amount} {currency}؛ هنوز بین فرشی تقسیم نشده',
  I_COST_UPDATED: 'هزینهٔ شمارهٔ {number} ذخیره شد.',
  I_COST_SAVE_FAILED: 'ذخیره نشد؛ اینترنت را نگاه کنید.',
  I_COST_DRAFT_FOUND: 'هزینه‌ای که دفعهٔ قبل نوشتید ذخیره نشده بود.',
  I_COST_EMPTY: 'هزینه‌ای ثبت نشده. رسیدها را اینجا بزنید تا قیمت تمام‌شدهٔ هر متر معلوم شود.',
  I_COST_EMPTY_FILTERED: 'با این فیلترها هزینه‌ای پیدا نشد.',
  I_COST_UNALLOCATED_COUNT: '{n} هزینه هنوز بین فرش‌ها تقسیم نشده.',
  I_COST_FREEZES: 'با شمارهٔ رسید یا کارگاه، مبلغ همین حالا ثابت می‌شود و با تغییر اندازه یا حذف فرش کم و زیاد نمی‌شود.',
  I_COST_TARGETS_NOT_FOUND: 'این شماره‌ها پیدا نشد: {serials}',
  I_COST_NEW_CATEGORY_SAVED: 'نوع «{name}» اضافه شد.',
  I_COST_PINS_LEFT: '{amount} {currency} می‌ماند برای بقیهٔ فرش‌ها.',
  E_COST_CATEGORY_REQUIRED: 'نوع هزینه را انتخاب کنید.',
  E_COST_AMOUNT_REQUIRED: 'مبلغ را بنویسید.',
  E_COST_TARGET_REQUIRED: 'بنویسید این هزینه برای کدام فرش‌هاست.',

  // ── حساب محموله (D.6) — فقط کلاینت ──
  I_REVENUE_SAVED: 'فروش محموله ذخیره شد.',
  E_REVENUE_FX_REQUIRED: 'برای فروش دلاری نرخ دلار را بنویسید.',

  // ── نوع‌های هزینه (تنظیمات) — فقط کلاینت ──
  I_COST_CATEGORY_SAVED: 'نوع هزینه ذخیره شد.',
  I_COST_CATEGORY_DEACTIVATED: '«{name}» به کار رفته بود، پس حذف نشد و غیرفعال شد.',
  I_COST_CATEGORY_DELETED: '«{name}» حذف شد.',
}
