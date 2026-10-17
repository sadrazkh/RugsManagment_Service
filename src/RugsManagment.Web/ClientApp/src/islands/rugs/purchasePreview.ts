/**
 * پیش‌نمایش مبلغ سطرهای خرید پیش از ذخیره — آینهٔ `RugCostMath.PurchaseLineAmounts` (C#) و `CostSql` (SQL)، با همان
 * قاعدهٔ دسته (D.3، B.6):
 * - «کل»: ROUND(نرخ، s) — یک بار، بی تعداد؛
 * - «هر متر»: ROUND(نرخ × مساحت یک تخته در **اندازهٔ اول**، s) × تعداد؛
 * - «هر تخته»: ROUND(نرخ، s) × تعداد؛
 * - «درصد»: ROUND(Σ مبلغ سطرهای غیردرصدی × درصد ÷ ۱۰۰، s) — یک بار روی جمع (درصدها روی هم اثر ندارند).
 *
 * فقط برای نشان دادن «۳۰۰٬۰۰۰ × ۵۰ = ۱۵٬۰۰۰٬۰۰۰» هنگام تایپ است؛ عدد ذخیره‌شده را همیشه سرور (پاس سازگاری) حساب
 * می‌کند و ویرایشگر بعد از ذخیره همان را نشان می‌دهد. هیچ ضرب Number این‌جا نیست (`lib/num.ts`).
 */
import { add, dec, lineMoney, mul, pieceMoney, pieceMoneyPerPiece, round, type Dec, type DecInput } from '@/lib/num'
import { CostBasis } from '@/lib/types/rugs'

export interface PreviewLine {
  basis: CostBasis
  /** نرخ (درصد برای درصد)؛ null یعنی هنوز نوشته نشده و مبلغ صفر حساب می‌شود */
  rate: DecInput | null
}

/** مبلغ هر سطر، هم‌ترتیب با ورودی. */
export function purchaseAmounts(
  lines: readonly PreviewLine[],
  intakePieceArea: DecInput,
  quantity: number,
  moneyScale: number,
): Dec[] {
  const amounts: Dec[] = lines.map(() => dec(0))
  let nonPercent = dec(0)
  lines.forEach((line, i) => {
    if (line.basis === CostBasis.Percent || line.rate === null) return
    const amount = nonPercentAmount(line.basis, line.rate, intakePieceArea, quantity, moneyScale)
    amounts[i] = amount
    nonPercent = add(nonPercent, amount)
  })
  lines.forEach((line, i) => {
    if (line.basis !== CostBasis.Percent || line.rate === null) return
    amounts[i] = round(divideBy100(mul(nonPercent, line.rate)), moneyScale)
  })
  return amounts
}

/** مبلغ یک تخته برای نمایش حساب «۳۰۰٬۰۰۰ × ۵۰» (فقط هر متر و هر تخته؛ بقیه null). */
export function pieceAmount(basis: CostBasis, rate: DecInput, intakePieceArea: DecInput, moneyScale: number): Dec | null {
  if (basis === CostBasis.PerM2) return pieceMoney(rate, intakePieceArea, moneyScale)
  if (basis === CostBasis.PerPiece) return pieceMoneyPerPiece(rate, moneyScale)
  return null
}

function nonPercentAmount(basis: CostBasis, rate: DecInput, area: DecInput, quantity: number, moneyScale: number): Dec {
  switch (basis) {
    case CostBasis.PerM2:
      return lineMoney(pieceMoney(rate, area, moneyScale), quantity)
    case CostBasis.PerPiece:
      return lineMoney(pieceMoneyPerPiece(rate, moneyScale), quantity)
    default:
      return round(rate, moneyScale)
  }
}

/** تقسیم دقیق بر ۱۰۰ با جابه‌جا کردن مقیاس (بی تقسیم ممیز شناور). */
function divideBy100(value: Dec): Dec {
  return { units: value.units, scale: value.scale + 2 }
}
