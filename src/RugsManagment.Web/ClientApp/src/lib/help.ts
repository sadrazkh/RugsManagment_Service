/**
 * متن «؟ راهنما» هر صفحه (H.5) — چند گام شماره‌دار، با آیکون و کلید صفحه‌کلید.
 *
 * چرا جدا از کامپوننت: متن راهنما با هر فاز بیشتر می‌شود (ورود فرش، محموله، هزینه) و در «کارت راهنما»ی
 * چاپی هم همین گام‌ها می‌آیند؛ پس فقط این فایل ویرایش می‌شود، نه HelpDrawer. متن به زبان ساده و بازاری
 * است و از واژه‌نامهٔ A.2 بیرون نمی‌رود. واژهٔ نگله/عدل با نشانهٔ `{bale}` و `{baleOf}` نوشته می‌شود و از
 * `terms` کارگاه پر می‌شود.
 *
 * متن از سیاست بیننده پیروی می‌کند (H.3): گامی که دکمه یا خانه‌اش فقط برای Business است (ثبت هزینه، مبلغ‌ها،
 * «قطعی کن») `business: true` دارد و برای انباردار نشان داده نمی‌شود — همان قاعدهٔ `MerchantSteps.For` سرور که
 * راهنمای شروع خانه و «کارت راهنما» را می‌سازد، تا دو راهنما دو حرف نزنند.
 */

export interface HelpStep {
  /** متن گام (ممکن است نشانهٔ `{bale}` / `{baleOf}` داشته باشد) */
  text: string
  /** آیکون از /icons.svg */
  icon?: string
  /** کلیدهای صفحه‌کلید، مثل ['Ctrl', 'K'] */
  keys?: string[]
  /** فقط برای کسی که سیاست Business دارد (دکمه یا خانه‌اش برای انباردار نیست) */
  business?: boolean
}

export interface HelpTopic {
  title: string
  steps: HelpStep[]
}

/** کلید صفحه‌ها (همان که `data-help-page` یا مسیر نشانی می‌دهد). */
export type HelpPage =
  | 'home'
  | 'rugs'
  | 'rug-details'
  | 'rug-form'
  | 'batches'
  | 'intake'
  | 'shipments'
  | 'pack'
  | 'output'
  | 'import'
  | 'setup'
  | 'costs'
  | 'settings'
  | 'default'

const TOPICS: Record<HelpPage, HelpTopic> = {
  home: {
    title: 'خانه',
    steps: [
      { text: 'برای ثبت فرش‌هایی که رسیده، «ثبت فرش تازه» را بزنید.', icon: 'plus' },
      { text: 'برای بستن فرش‌ها در {bale}، «{bale}‌بندی» را بزنید.', icon: 'bale' },
      { text: 'هر رسید یا هزینه را با «ثبت هزینه» بنویسید.', icon: 'receipt', business: true },
      { text: 'برای گرفتن اکسل یا چاپ اظهارنامه، «اکسل و چاپ» را بزنید.', icon: 'printer' },
      { text: 'هر جا گیر کردید، دکمهٔ «؟ راهنما» همین صفحه را باز کنید.', icon: 'help' },
    ],
  },
  rugs: {
    title: 'فرش‌ها',
    steps: [
      { text: 'شمارهٔ فرش را در جست‌وجو بنویسید تا پیدا شود.', icon: 'search', keys: ['/'] },
      { text: 'با تیک زدن چند فرش، کارهای گروهی پایین صفحه پیدا می‌شوند.', icon: 'check' },
      { text: 'برای دیدن جزئیات هر فرش روی نامش بزنید.', icon: 'rug' },
      { text: 'برای ثبت فرش تازه «ثبت فرش» را بزنید.', icon: 'plus', keys: ['N'] },
    ],
  },
  'rug-details': {
    title: 'جزئیات فرش',
    steps: [
      { text: 'اندازه‌ها و مشخصات فرش بالای صفحه است.', icon: 'rug' },
      { text: 'برای عوض کردن مشخصات «ویرایش» را بزنید.', icon: 'edit' },
      { text: 'عکس‌ها را با «افزودن عکس» اضافه کنید.', icon: 'image' },
      { text: 'برای چاپ برچسب این فرش «چاپ برچسب» را بزنید.', icon: 'printer' },
    ],
  },
  'rug-form': {
    title: 'ثبت فرش',
    steps: [
      { text: 'طول و عرض را به متر بنویسید، مثل ۲٫۰۸. اگر به سانت بنویسید (۲۰۸) خودش می‌پرسد.', icon: 'ruler' },
      { text: 'می‌توانید «۲۰۸×۲۱۰» را یک‌جا بنویسید تا هر دو خانه پر شود.', icon: 'ruler' },
      { text: 'مبلغ را با «م» برای میلیون و «ه» برای هزار هم می‌شود نوشت: ۱۰م یعنی ده میلیون.', icon: 'coins', business: true },
      { text: 'زیر هر مبلغ، همان عدد به حروف نوشته می‌شود؛ یک نگاه بیندازید که صفرها درست باشد.', icon: 'check', business: true },
      { text: 'در آخر «ثبت فرش» را بزنید.', icon: 'check', keys: ['Enter'] },
    ],
  },
  batches: {
    title: 'ورودی‌ها',
    steps: [
      { text: 'هر ورودی یعنی فرش‌هایی که یک روز از یک فروشنده رسیده.', icon: 'package' },
      { text: 'روی یک ورودی بزنید تا فرش‌هایش را ببینید.', icon: 'search' },
      { text: 'برای جدا کردن چند فرش، تیکشان بزنید و «ورودی تازه از انتخاب‌شده‌ها» را بزنید.', icon: 'check' },
    ],
  },
  intake: {
    title: 'ورود فرش',
    steps: [
      { text: 'تاریخ رسیدن را بالای صفحه ببینید؛ پیش‌فرض امروز است.', icon: 'calendar' },
      { text: 'شماره، فروشنده، طول و عرض هر فرش را در یک ردیف بنویسید.', icon: 'edit' },
      { text: 'با Enter به خانهٔ پایین می‌روید، مثل اکسل.', icon: 'keyboard', keys: ['Enter'] },
      { text: 'نوشته‌ها خودکار ذخیره می‌شوند؛ اگر اینترنت قطع شد هم روی همین دستگاه می‌مانند.', icon: 'check' },
    ],
  },
  shipments: {
    title: 'محموله‌ها',
    steps: [
      { text: 'هر محموله یعنی یک اظهارنامهٔ صادرات.', icon: 'truck' },
      { text: 'شمارهٔ فرش را بزنید تا در {baleOf} جاری برود.', icon: 'scan' },
      { text: 'اندازهٔ نهایی هر فرش را بنویسید، یا «همان اندازه» را بزنید.', icon: 'ruler', keys: ['='] },
      { text: 'وقتی همه آماده شد، «قطعی کن» را بزنید.', icon: 'lock', business: true },
    ],
  },
  costs: {
    title: 'هزینه‌ها',
    steps: [
      { text: 'نوع هزینه را انتخاب کنید، مثل کرایه یا شستشو.', icon: 'receipt', business: true },
      { text: 'مبلغ را «هر متر»، «هر تخته» یا «کل» بنویسید.', icon: 'coins', business: true },
      { text: 'بگویید برای کدام فرش‌هاست؛ خودش بین آن‌ها تقسیم می‌کند.', icon: 'check', business: true },
    ],
  },
  pack: {
    title: 'حالت اسکن',
    steps: [
      { text: 'شماره را تایپ یا اسکن کنید؛ فرش به {baleOf} جاری می‌رود.', icon: 'scan' },
      { text: 'اندازهٔ نهایی را بنویسید یا «همان اندازهٔ اول» را بزنید.', icon: 'ruler' },
      { text: 'جمع متراژ را ببینید؛ برای ادامه «{baleOf} بعد» را بزنید.', icon: 'bale' },
      { text: 'برای انتقال یا بیرون آوردن فرش، منوی همان فرش را باز کنید.', icon: 'menu' },
    ],
  },
  output: {
    title: 'اکسل و چاپ',
    steps: [
      { text: 'قالب خروجی را انتخاب کنید و پیش‌نمایش را ببینید.', icon: 'spreadsheet' },
      { text: 'اگر اندازه یا نرخ ناقص بود، از پیام همان فرش برگردید و درستش کنید.', icon: 'ruler' },
      { text: 'اکسل را بگیرید یا چاپ را باز کنید؛ خروجی پیش از قطعی شدن «پیش‌نویس» است.', icon: 'printer' },
    ],
  },
  import: {
    title: 'ورود از اکسل',
    steps: [
      { text: 'فایل اکسل را انتخاب کنید و ستون‌ها را به شماره، اندازه و مشخصات وصل کنید.', icon: 'upload', business: true },
      { text: 'پیش‌نمایش و خطاهای هر ردیف را بررسی کنید؛ تا تأیید شما چیزی ثبت نمی‌شود.', icon: 'check', business: true },
      { text: 'پس از ثبت، نتیجه را ببینید؛ اگر لازم شد از همان کار «برگردان» استفاده کنید.', icon: 'undo', business: true },
    ],
  },
  setup: {
    title: 'آماده‌سازی کارگاه',
    steps: [
      { text: 'بستهٔ صادرات فرش را اعمال کنید و کارهای انجام‌شده را در فهرست ببینید.', icon: 'check', business: true },
      { text: 'جدول ارزش گمرکی، نوع هزینه‌ها و قالب خروجی را یک بار تنظیم کنید.', icon: 'settings', business: true },
      { text: 'برای کار روزانه از خانه به «ثبت فرش تازه» بروید.', icon: 'home' },
    ],
  },
  settings: {
    title: 'تنظیمات',
    steps: [
      { text: 'هر بخش تنظیمات یک کارت است؛ روی کارت بزنید تا باز شود.', icon: 'settings' },
      { text: 'تغییرها فقط با دکمهٔ «ذخیره» ثبت می‌شوند.', icon: 'check' },
      { text: 'کاربران و نقش‌ها را از «کاربران» بسازید.', icon: 'users' },
    ],
  },
  default: {
    title: 'راهنما',
    steps: [
      { text: 'از منوی کنار صفحه (یا پایین، روی گوشی) به هر بخش بروید.', icon: 'menu' },
      { text: 'برای پیدا کردن سریع هر صفحه یا فرش Ctrl و K را با هم بزنید.', icon: 'keyboard', keys: ['Ctrl', 'K'] },
      { text: 'اندازهٔ نوشته را از همین‌جا بزرگ‌تر کنید.', icon: 'type' },
    ],
  },
}

/** آنچه بیننده می‌تواند ببیند (از `policies` در app-context). */
export interface HelpViewer {
  business: boolean
}

/**
 * متن راهنمای یک صفحه؛ صفحهٔ ناشناخته راهنمای کلی می‌گیرد. با `viewer`، گام‌های Business برای کسی که Business
 * نیست حذف می‌شوند (شماره‌گذاری بعد از حذف است). بی `viewer` همهٔ گام‌ها (کارت راهنمای چاپی بازرگان و آزمون).
 */
export function helpFor(page: string | null | undefined, viewer?: HelpViewer): HelpTopic {
  const topic = isHelpPage(page) ? TOPICS[page] : TOPICS.default
  if (!viewer || viewer.business) return topic
  const steps = topic.steps.filter((s) => !s.business)
  return { ...topic, steps: steps.length >= 3 ? steps : TOPICS.default.steps }
}

/** آیا این کلید یک صفحهٔ راهنماست؟ (کلیدهای پیش‌نمونهٔ شیء مثل «constructor» نه) */
export function isHelpPage(page: string | null | undefined): page is HelpPage {
  return !!page && Object.prototype.hasOwnProperty.call(TOPICS, page)
}

/** همهٔ صفحه‌ها (برای «کارت راهنما»ی چاپی و آزمون). */
export const HELP_PAGES = Object.keys(TOPICS) as HelpPage[]

/**
 * کلید راهنما از مسیر نشانی، وقتی صفحه `data-help-page` نگذاشته. ترتیب مهم است: مسیر دقیق‌تر اول.
 */
export function helpPageFor(pathname: string): HelpPage {
  const path = pathname.toLowerCase().replace(/\/+$/, '') || '/'
  if (path === '/' || path.startsWith('/home')) return 'home'
  if (path.startsWith('/rugs/details')) return 'rug-details'
  if (path.startsWith('/rugs/create') || path.startsWith('/rugs/edit')) return 'rug-form'
  if (path.startsWith('/rugs')) return 'rugs'
  if (path.startsWith('/groups')) return 'batches'
  if (path.startsWith('/intake')) return 'intake'
  if (path.startsWith('/shipments') && path.endsWith('/pack')) return 'pack'
  if (path.startsWith('/documents') || path.includes('/output')) return 'output'
  if (path.startsWith('/import')) return 'import'
  if (path.startsWith('/settings/setup')) return 'setup'
  if (path.startsWith('/shipments')) return 'shipments'
  if (path.startsWith('/costs')) return 'costs'
  if (path.startsWith('/settings') || path.startsWith('/users')) return 'settings'
  return 'default'
}

/** کلید راهنمای صفحهٔ جاری: اول `data-help-page` روی body، بعد مسیر نشانی. */
export function currentHelpPage(): HelpPage {
  if (typeof document === 'undefined') return 'default'
  const declared = document.body?.dataset.helpPage
  if (isHelpPage(declared)) return declared
  return helpPageFor(window.location.pathname)
}
