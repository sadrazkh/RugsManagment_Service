/**
 * متن و سنجش‌های «این عدد از کجا آمد؟» (`RugCostBreakdown.island.vue`) — جدا تا بی DOM آزموده شود.
 *
 * - نرخ سطر خرید همان‌طور که ذخیره شده (numeric(18,4)، تا ۴ رقم اعشار) نشان داده می‌شود، نه گردشده به MoneyScale: نرخ
 *   متری ۴۵٫۵۵ روی دستهٔ ۰٫۴۰ × ۰٫۴۰ × ۵۰ مبلغ ۳۵۰ می‌سازد (D.3)؛ «هر متر ۴۶» توضیحی است که جمع نمی‌زند (۴۶ × ۸ ≠ ۳۵۰).
 *   فقط مبلغ‌ها به MoneyScale گرد می‌شوند.
 * - «با سهم خریدهای یکجا» فقط وقتی جمع خرید کارت با جمع **دقیق** مبلغ سطرها فرق دارد. جمع با `lib/num.ts`، نه جمع
 *   Number: ۳۹٬۳۳۰٬۰۰۰٫۱۰ + ۷۸۶٬۶۰۰٫۲۰ در جاوااسکریپت ۴۰۱۱۶۶۰۰٫۳۰۰۰۰۰۰۰۴ می‌شود و سطر دروغین سهم خرید یکجا می‌آمد.
 */
import { currencyLabel, faDecimal } from '@/lib/format'
import { add, cmp, dec, type DecInput } from '@/lib/num'
import { CostBasis } from '@/lib/types/rugs'

/** بیشترین رقم اعشار نرخ سطر خرید (ستون Rate، numeric(18,4)). */
export const RATE_DECIMALS = 4

const BASIS_LABELS: Record<number, string> = {
  [CostBasis.Total]: 'کل',
  [CostBasis.PerM2]: 'هر متر',
  [CostBasis.PerPiece]: 'هر تخته',
  [CostBasis.Percent]: 'درصد',
}

/** «هر متر ۴۵٫۵۵ تومان»، «کل ۴۰٬۰۰۰٬۰۰۰ تومان» یا «۲٪». */
export function purchaseRateText(basis: CostBasis, rate: DecInput, unit: string = currencyLabel()): string {
  if (basis === CostBasis.Percent) return `${faDecimal(rate, RATE_DECIMALS)}٪`
  return `${BASIS_LABELS[basis]} ${faDecimal(rate, RATE_DECIMALS)} ${unit}`
}

/** جمع دقیق مبلغ سطرها. */
export function linesTotal(amounts: readonly DecInput[]): ReturnType<typeof dec> {
  return amounts.reduce<ReturnType<typeof dec>>((sum, amount) => add(sum, amount), dec(0))
}

/** جمع خرید کارت (سطرها + سهم خریدهای یکجا) با جمع دقیق سطرها فرق دارد؟ */
export function hasLumpPurchaseShare(purchaseCost: DecInput, amounts: readonly DecInput[]): boolean {
  return cmp(purchaseCost, linesTotal(amounts)) !== 0
}
