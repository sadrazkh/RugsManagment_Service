/**
 * پیام‌های محموله، نگله و ارزش گمرکی از فاز ۴a — آینهٔ `App/Common/Messages.fa.Shipments.cs` (آمادگی، قفل «قطعی»، عضویت
 * در نگله، نگله‌ها، محموله، ارزش گمرکی، تراشهٔ منبع نرخ) به‌اضافهٔ چند پیام که فقط خودِ جزیره‌ها می‌سازند (ویرایشگر جدول
 * ارزش گمرکی). فاز ۴b پیام‌های صفحهٔ نگله‌بندی را به همین فایل اضافه می‌کند.
 *
 * قاعدهٔ فایل‌های پیام فاز: فقط `messages` را export می‌کند و `./index` را import نمی‌کند (حلقه نسازد). هر کدی که در
 * سرور هم هست باید دقیقاً همان متن را داشته باشد (`shipments.spec.ts` بررسی می‌کند)؛ بیرون از نشانه‌های `{…}` هیچ حرف
 * لاتینی نیست. هر «نگله» با `{bale}`/`{baleOf}` نوشته می‌شود تا کارگاهی که «عدل» می‌گوید همه‌جا «عدل» ببیند؛ شمارهٔ
 * نگله `{n}` است، شمارهٔ محموله `{number}` یا `{shipment}`.
 */
export const messages: Record<string, string> = {
  // ── آمادگی (C.6) ──
  E_EMPTY_SHIPMENT: 'هنوز فرشی در این محموله نیست.',
  E_EMPTY_BALE: '{baleOf} {n} خالی است؛ یا فرش در آن بگذارید یا حذفش کنید.',
  E_NO_FINAL_SIZE: 'اندازهٔ نهایی {n} فرش ثبت نشده.',
  E_NO_DECLARED_VALUE: 'ارزش گمرکی {n} فرش «{key}» معلوم نیست.',
  E_NO_DECLARED_KEY: 'ارزش گمرکی {n} فرش معلوم نیست، چون «{label}» آن‌ها خالی است.',
  W_BALE_OVER_TARGET: '{baleOf} {n} شد {area} متر؛ از {target} متر هدف بیشتر است.',
  W_NO_SERIAL: '{n} فرش شماره ندارد.',

  // ── قفل «قطعی» (C.8) ──
  E_SHIPMENT_LOCKED: 'این فرش در محمولهٔ قطعی‌شدهٔ {number} است. برای تغییر، اول «باز کردن دوباره» را بزنید.',
  E_SHIPMENT_LOCKED_HEADER: 'محمولهٔ {number} قطعی شده است. برای تغییر، اول «باز کردن دوباره» را بزنید.',
  E_SHIPMENT_LOCKED_OPERATOR: 'این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.',

  // ── عضویت در نگله (C.3، C.10) ──
  E_TRASH_BALED: 'اول این فرش را از {baleOf} {n} خارج کنید.',
  E_SALE_BALED: 'این فرش در {baleOf} {n} است؛ اول آن را از {bale} خارج کنید.',
  E_LINE_RUG_IN_OTHER: 'این فرش در محمولهٔ {shipment} / {baleOf} {n} است.',
  E_LINE_RUG_TRASHED: 'این فرش در سطل زباله است؛ اول آن را بازگردانید.',
  E_LINE_RUG_SOLD: 'این فرش فروخته شده و در {bale} نمی‌رود.',
  E_LINE_BALE_MISMATCH: 'این {bale} مال این محموله نیست.',
  E_LINE_NOT_IN_SHIPMENT: 'این ردیف در این محموله نیست؛ صفحه را تازه کنید.',
  E_LINE_OP_UNKNOWN: 'این کار شناخته نشد.',
  E_LINE_RUG_REQUIRED: 'شمارهٔ فرش را بنویسید.',
  E_LINE_BALE_REQUIRED: '{bale} را مشخص کنید.',
  E_LINE_RATE_FORBIDDEN: 'نرخ دلار هر ردیف را فقط بازرگان عوض می‌کند.',
  Q_LINE_MOVE: 'این فرش در محمولهٔ {shipment} / {baleOf} {n} است؛ منتقل شود؟',
  Q_LINE_MOVE_SAME: 'این فرش در {baleOf} {n} همین محموله است؛ منتقل شود؟',
  Q_LINE_SERIAL_IN_RANGE: 'این شماره داخل دستهٔ {from} تا {to} است.',

  // ── نگله‌ها (C.2) ──
  E_BALE_NOT_EMPTY: '{baleOf} {n} خالی نیست؛ اول فرش‌هایش را خارج کنید.',
  E_BALE_NUMBER_TAKEN: '{baleOf} {n} از پیش هست.',
  E_BALE_NUMBER: 'شمارهٔ {bale} باید از ۱ تا {max} باشد.',
  E_BALE_WEIGHT: 'وزن {bale} را به کیلو و بزرگ‌تر از صفر بنویسید.',

  // ── محموله (C.1، C.7، C.9) ──
  I_SHIPMENT_TITLE: 'اظهار {number}',
  E_SHIPMENT_NOT_EMPTY: 'این محموله فرش دارد؛ فقط محمولهٔ بی‌فرش حذف می‌شود.',
  E_SHIPMENT_DELETE_FINALIZED: 'محمولهٔ قطعی‌شده حذف نمی‌شود؛ اول «باز کردن دوباره» را بزنید.',
  E_SHIPMENT_NOT_FINALIZED: 'این محموله قطعی نیست.',
  E_SHIPMENT_ALREADY_FINALIZED: 'این محموله پیش از این قطعی شده.',
  E_SHIPMENT_NOT_READY: 'اول این‌ها را درست کنید: {issues}',
  E_SHIPMENT_NEGATIVE: '«{label}» منفی نمی‌شود.',

  // ── ارزش گمرکی (C.5) ──
  E_DECLARED_KEY_EXISTS: 'ارزش گمرکی «{key}» از پیش در جدول هست؛ برای عوض کردنش به تنظیمات بروید.',
  E_DECLARED_NEGATIVE: 'ارزش گمرکی منفی نمی‌شود.',
  E_DECLARED_KEY_REQUIRED: 'نام ردیف را بنویسید؛ مثلاً همان محل بافت.',
  E_DECLARED_KEY_DUPLICATE: '«{key}» دو بار در جدول آمده.',
  E_DECLARED_KEY_TOO_LONG: 'نام ردیف حداکثر {n} حرف است.',
  E_DECLARED_STALE: 'جدول ارزش گمرکی را همین الان کس دیگری عوض کرد؛ صفحه را تازه کنید و دوباره بنویسید.',
  E_DECLARED_KEY_FIELD_IN_USE: '«{label}» کلید ارزش گمرکی است؛ اول در ترجیحات کلید دیگری انتخاب کنید.',
  I_DECLARED_ADDED: 'ارزش گمرکی «{key}» ثبت شد؛ {n} ردیف دوباره حساب شد.',

  // ── تراشهٔ منبع نرخ (H.3b) ──
  I_RATE_OVERRIDE: 'دستی',
  I_RATE_TABLE: 'از جدول',
  I_RATE_FALLBACK: 'نرخ پیش‌فرض',
  I_RATE_MISSING: 'ندارد',

  // ══ فقط جزیره‌ها (سرور این‌ها را نمی‌سازد) ══

  // ── جدول ارزش گمرکی (/settings/declared-values) ──
  /** راهنمای بالای جدول؛ `{label}` نام فیلد کلید (مثل «محل بافت») */
  I_DECLARED_VALUES_HINT:
    'برای هر {label} دلار هر متر را بنویسید. ردیف‌های محموله‌های باز همان لحظه با نرخ تازه حساب می‌شوند؛ محمولهٔ قطعی‌شده دست نمی‌خورد.',
  /** پس از ذخیره؛ `{n}` تعداد ردیف‌های محموله‌های باز که دوباره ارزش‌گذاری شد */
  I_DECLARED_VALUES_SAVED: 'جدول ارزش گمرکی ذخیره شد؛ ارزش {n} ردیف محمولهٔ باز دوباره حساب شد.',
  I_DECLARED_VALUES_SAVED_NONE: 'جدول ارزش گمرکی ذخیره شد.',
  /** چیپ‌های مقدارهای بی‌نرخ */
  I_DECLARED_VALUES_MISSING: 'این‌ها در فرش‌ها آمده ولی هنوز نرخ ندارد:',
  /** نرخ پیش‌فرض؛ `{usd}` با ارقام فارسی */
  I_DECLARED_VALUES_FALLBACK: 'هر {label} که در جدول نیست: {usd} دلار هر متر (نرخ پیش‌فرض).',
  I_DECLARED_VALUES_NO_FALLBACK: 'هر {label} که در جدول نیست بی‌نرخ می‌ماند و جلوی قطعی شدن محموله را می‌گیرد.',
  I_DECLARED_VALUES_EMPTY: 'هنوز نرخی ننوشته‌اید. برای هر {label} یک ردیف بزنید.',
  Q_DECLARED_VALUES_DISCARD: 'تغییرهای ذخیره‌نشده کنار گذاشته شود؟',

  // ══ فاز ۴b: تختهٔ نگله‌بندی، حالت اسکن، «افزودن به نگله» (فقط جزیره‌ها) ══
  // عددها (`{n}`، `{pieces}`) ارقام فارسی می‌شوند؛ متراژ و دلار پانویس را جزیره با دو رقم اعشار می‌سازد («۱۳٫۵۱»).

  // ── نگلهٔ جاری (C.2) ──
  I_BOARD_CURRENT: '{baleOf} جاری',
  I_BOARD_NEXT: '{baleOf} بعد',
  I_BOARD_NEW_BALE: '{baleOf} تازه',
  /** «نگلهٔ ۴ هنوز فرشی ندارد…» — «نگلهٔ بعد» چیزی نمی‌سازد؛ اولین فرش نگله را می‌سازد */
  I_BOARD_CURRENT_EMPTY: '{baleOf} {n} هنوز فرشی ندارد؛ اولین فرشش آن را می‌سازد.',
  I_BOARD_SERIAL_HINT: 'شماره را بزنید یا اسکن کنید',
  I_BOARD_EMPTY: 'این محموله هنوز {bale} ندارد. شمارهٔ اولین فرش {baleOf} {n} را بزنید.',
  /** پانویس هر نگله، مثل اکسل: «نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار» */
  I_BOARD_FOOTER: '{baleOf} {n} · شامل {pieces} تخته · {area} متر · {usd} دلار',
  I_BOARD_TOTALS: 'شامل {pieces} تخته · {area} متر · {usd} دلار',
  /** خواندن جمع برای صفحه‌خوان (`aria-live`، H.6) */
  I_BOARD_ANNOUNCE: '{baleOf} {n}: {pieces} تخته، {area} متر',
  /** متن نوار هدف (`aria-valuetext`، H.7) */
  I_BOARD_TARGET_VALUE: '{area} از {target} متر',
  I_BOARD_TARGET_BELOW: 'هنوز به {target} متر هدف نرسیده',
  I_BOARD_TARGET_WITHIN: 'به اندازهٔ هدف ({target} متر)',

  // ── کارها و «برگردان» ──
  I_BOARD_ADDED: '{serial} به {baleOf} {n} رفت.',
  I_BOARD_MOVED: '{serial} به {baleOf} {n} منتقل شد.',
  I_BOARD_REMOVED: '{serial} به انبار برگشت',
  I_BOARD_MEASURED: 'اندازهٔ نهایی {serial} ثبت شد.',
  I_BOARD_MEASURE_CLEARED: 'اندازهٔ نهایی {serial} پاک شد.',
  I_BOARD_UNDONE: 'برگشت؛ همان‌طور که بود شد.',
  I_BOARD_UNDO_EMPTY: 'کاری برای برگشتن نمانده.',
  Q_BOARD_SAME_AS_INTAKE: 'اندازهٔ نهایی {n} فرش همان اندازهٔ اول ثبت شود؟',
  I_BOARD_SAME_AS_INTAKE_DONE: 'اندازهٔ نهایی {n} فرش همان اندازهٔ اول ثبت شد.',
  I_BOARD_SHOWING: 'فقط {n} فرش نشان داده شده.',
  I_BOARD_BALE_DELETED: '{baleOf} {n} حذف شد.',
  I_BOARD_RELOADED: 'این {bale} را همین الان کس دیگری هم عوض کرد؛ تازه‌اش را آوردیم. دوباره امتحان کنید.',
  I_BOARD_PENDING: '{n} فرش برای قطع اینترنت ذخیره نشد؛ روی همین دستگاه مانده.',
  E_BOARD_LOAD: '{bales} این محموله بار نشد؛ صفحه را تازه کنید.',
  /** راهنمای صفحه‌کلید (فقط با موس دیده می‌شود)؛ نام کلیدها نشانه‌اند */
  I_BOARD_KEYS_HINT: 'شماره و {enter}؛ طول و {tab}؛ عرض و {enter}. «{same}» یعنی همان اندازه.',

  // ── انتقال (بی کشیدن؛ H.7) ──
  I_BOARD_MOVE_TITLE: 'انتقال {serial}',
  I_BOARD_MOVE_NOW: 'الان در {baleOf} {n} است.',
  I_BOARD_MOVE_OTHER: 'شمارهٔ {bale} دیگر',
  I_BOARD_BACK_TO_STOCK: 'برگشت به انبار',

  // ── اندازهٔ نهایی (C.4، H.6) ──
  I_BOARD_SIZE_TITLE: 'اندازهٔ نهایی {serial}',
  I_BOARD_INTAKE_SIZE: 'اندازهٔ اول: {size}',
  I_BOARD_SIZE_DOWN: '{n} سانت کوچک‌تر شده',
  I_BOARD_SIZE_UP: '{n} سانت بزرگ‌تر شده',

  // ── «داخل دسته» و جدا کردن (D.9) ──
  I_BOARD_SPLIT_ASK: 'این دسته {n} تخته است. چند تخته جدا شود؟',

  // ── انبار ──
  I_BOARD_STOCK_TITLE: 'افزودن از انبار به {baleOf} {n}',
  I_BOARD_STOCK_EMPTY: 'فرشی در انبار نیست.',
  I_BOARD_STOCK_NOT_FOUND: 'فرشی با این نوشته در انبار پیدا نشد.',
  I_BOARD_ADD_TO: 'افزودن به {baleOf} {n}',

  // ── اسکن (H.6) ──
  I_BOARD_SCAN_TITLE: 'اسکن برچسب',
  I_BOARD_SCAN_HINT: 'برچسب شماره را جلوی دوربین بگیرید؛ هر برچسب خوانده‌شده به {baleOf} جاری می‌رود.',
  I_BOARD_SCAN_READ: 'خوانده شد: {code}',
  E_BOARD_SCAN_UNSUPPORTED: 'این مرورگر اسکن با دوربین را پشتیبانی نمی‌کند؛ شماره را بنویسید.',
  E_BOARD_SCAN_DENIED: 'دسترسی به دوربین رد شد؛ شماره را بنویسید.',
  E_BOARD_SCAN_CAMERA: 'دوربین در دسترس نیست؛ شماره را بنویسید.',

  // ── حالت اسکن (G.5) ──
  I_BOARD_PACK_LINK: 'حالت اسکن (گوشی)',
  I_BOARD_PACK_QR: 'این کد را با دوربین گوشی بخوانید تا حالت اسکن همین محموله روی گوشی باز شود.',
  I_BOARD_RECENT: 'آخرین فرش‌های این نوبت',
  I_BOARD_ASK_SIZE: 'اندازهٔ نهایی را هم بپرس',

  // ── «افزودن به نگله» از فهرست فرش‌ها (H.1، A.4) ──
  I_BOARD_ADD_TO_BALE: 'افزودن به {bale}',
  I_BOARD_ADD_MANY_TITLE: 'افزودن {n} فرش به…',
  I_BOARD_ADDED_MANY: '{n} فرش به {baleOf} {number} محمولهٔ {shipment} رفت.',
  Q_BOARD_MOVE_MANY: '{n} فرش در {bale} دیگری است؛ منتقل شوند؟',
  I_BOARD_CHOOSE_SHIPMENT: 'کدام محموله؟',
  /** دکمهٔ انتخاب محموله: «محمولهٔ ۸ (۱۴۰۵/۰۶/۱۵) — ۱۲ نگله» */
  I_BOARD_SHIPMENT_CHOICE: 'محمولهٔ {number} ({date}) — {count} {bale}',
  I_BOARD_NO_OPEN_SHIPMENT: 'هنوز محمولهٔ بازی نیست؛ از بازرگان بخواهید یکی بسازد.',
  I_BOARD_NO_OPEN_SHIPMENT_BUSINESS: 'محمولهٔ بازی نیست. یک محمولهٔ تازه بسازید تا این فرش‌ها در آن بروند.',
  E_BOARD_ADD_SOME_FAILED: '{n} فرش اضافه نشد: {reason}',

  // ── ارزش گمرکی از تراشه (C.5) ──
  I_BOARD_RATE_FORM: 'ارزش گمرکی «{key}» برای هر متر:',
  I_BOARD_RATE_CLEAR: 'نرخ دستی پاک شد؛ از جدول حساب می‌شود.',
}
