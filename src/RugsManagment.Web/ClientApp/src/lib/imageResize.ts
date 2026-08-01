/**
 * کوچک‌سازی و فشرده‌سازی تصویر در خودِ مرورگر، قبل از آپلود.
 *
 * چرا سمت کلاینت: عکس مستقیم از دوربین موبایل چند مگابایت است. کوچک‌کردن قبل از
 * ارسال یعنی آپلود سریع‌تر روی اینترنت ضعیف، مصرف دیتای کمتر، و بی‌نیازی سرور از
 * کتابخانهٔ پردازش تصویر. سرور همچنان نوع و اندازه را مستقل بررسی می‌کند.
 */
import { toPersianDigits } from './format'

/** بیشترین ضلع تصویر اصلی — برای نمایش تمام‌صفحه کافی است. */
const FULL_MAX_EDGE = 1600
/** بیشترین ضلع بندانگشتی — برای فهرست و گالری. */
const THUMB_MAX_EDGE = 400

export interface ResizedImage {
  full: Blob
  thumbnail: Blob
  width: number
  height: number
}

/** فرمت خروجی: WebP اگر مرورگر پشتیبانی کند، وگرنه JPEG. */
function pickOutputType(): { mime: string; quality: number } {
  const canvas = document.createElement('canvas')
  canvas.width = 1
  canvas.height = 1
  const supportsWebp = canvas.toDataURL('image/webp').startsWith('data:image/webp')
  return supportsWebp ? { mime: 'image/webp', quality: 0.82 } : { mime: 'image/jpeg', quality: 0.85 }
}

function loadBitmap(file: File): Promise<ImageBitmap | HTMLImageElement> {
  // createImageBitmap چرخش EXIF را هم درست اعمال می‌کند
  if ('createImageBitmap' in window) {
    return createImageBitmap(file, { imageOrientation: 'from-image' } as ImageBitmapOptions)
  }

  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve(img)
    img.onerror = () => reject(new Error('خواندن تصویر ممکن نشد.'))
    img.src = URL.createObjectURL(file)
  })
}

function drawScaled(
  source: ImageBitmap | HTMLImageElement,
  maxEdge: number,
  type: { mime: string; quality: number },
): Promise<{ blob: Blob; width: number; height: number }> {
  const sourceWidth = source.width
  const sourceHeight = source.height
  // تصویر کوچکتر از حد را بزرگ نمی‌کنیم
  const scale = Math.min(1, maxEdge / Math.max(sourceWidth, sourceHeight))
  const width = Math.round(sourceWidth * scale)
  const height = Math.round(sourceHeight * scale)

  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height

  const ctx = canvas.getContext('2d')
  if (!ctx) return Promise.reject(new Error('پردازش تصویر در این مرورگر ممکن نیست.'))

  ctx.imageSmoothingQuality = 'high'
  ctx.drawImage(source, 0, 0, width, height)

  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob ? resolve({ blob, width, height }) : reject(new Error('فشرده‌سازی تصویر ناموفق بود.'))),
      type.mime,
      type.quality,
    )
  })
}

/**
 * دو نسخه می‌سازد: اصلی (حداکثر ۱۶۰۰px) و بندانگشتی (حداکثر ۴۰۰px).
 * ابعاد برگشتی مربوط به نسخهٔ اصلی است و برای رزرو فضا در چیدمان استفاده می‌شود.
 */
export async function resizeForUpload(file: File): Promise<ResizedImage> {
  const type = pickOutputType()
  const source = await loadBitmap(file)

  try {
    const full = await drawScaled(source, FULL_MAX_EDGE, type)
    const thumbnail = await drawScaled(source, THUMB_MAX_EDGE, type)
    return { full: full.blob, thumbnail: thumbnail.blob, width: full.width, height: full.height }
  } finally {
    if ('close' in source) source.close()
  }
}

/** «۲.۴ مگابایت» — حجم فایل با ارقام فارسی، هماهنگ با بقیهٔ اعداد رابط کاربری. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${toPersianDigits(bytes)} بایت`
  if (bytes < 1024 * 1024) return `${toPersianDigits((bytes / 1024).toFixed(0))} کیلوبایت`
  return `${toPersianDigits((bytes / (1024 * 1024)).toFixed(1))} مگابایت`
}
