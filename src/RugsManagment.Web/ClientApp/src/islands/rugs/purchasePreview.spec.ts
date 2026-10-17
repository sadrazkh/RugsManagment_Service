import { describe, expect, it } from 'vitest'
import { toPlainString } from '@/lib/num'
import { CostBasis } from '@/lib/types/rugs'
import { pieceAmount, purchaseAmounts } from './purchasePreview'

const plain = (values: ReturnType<typeof purchaseAmounts>) => values.map((v) => toPlainString(v))

describe('پیش‌نمایش سطرهای خرید (قاعدهٔ دسته، D.3)', () => {
  it('D.7 #2–#3، فرش A: هر متر ۹٬۰۰۰٬۰۰۰ روی ۴٫۳۷ و دلالی ۲٪', () => {
    const amounts = purchaseAmounts(
      [
        { basis: CostBasis.PerM2, rate: 9_000_000 },
        { basis: CostBasis.Percent, rate: 2 },
      ],
      '4.37',
      1,
      0,
    )
    expect(plain(amounts)).toEqual(['39330000', '786600'])
  })

  it('D.7 #2–#3، دستهٔ B: هر تخته ۳۰۰٬۰۰۰ × ۵۰ و دلالی ۲٪', () => {
    const amounts = purchaseAmounts(
      [
        { basis: CostBasis.PerPiece, rate: 300_000 },
        { basis: CostBasis.Percent, rate: 2 },
      ],
      '0.16',
      50,
      0,
    )
    expect(plain(amounts)).toEqual(['15000000', '300000'])
  })

  it('هر متر ۴۵٫۵۵ روی ۰٫۱۶ × ۵۰: ۳۵۰ با رقم اعشار ۰ و ۳۶۴٫۵ با ۲ (اول تخته، بعد تعداد)', () => {
    expect(plain(purchaseAmounts([{ basis: CostBasis.PerM2, rate: '45.55' }], '0.16', 50, 0))).toEqual(['350'])
    expect(plain(purchaseAmounts([{ basis: CostBasis.PerM2, rate: '45.55' }], '0.16', 50, 2))).toEqual(['364.5'])
  })

  it('هر تخته با نرخ اعشاری: ROUND(۱۲٬۳۴۵٫۵) = ۱۲٬۳۴۶ × ۳', () => {
    expect(plain(purchaseAmounts([{ basis: CostBasis.PerPiece, rate: '12345.5' }], '1', 3, 0))).toEqual(['37038'])
  })

  it('«کل» یک بار و بی تعداد؛ منفی هم مجاز است و درصد را کم می‌کند', () => {
    const amounts = purchaseAmounts(
      [
        { basis: CostBasis.Total, rate: 1_000_000 },
        { basis: CostBasis.Total, rate: -200_000 },
        { basis: CostBasis.Percent, rate: '2.5' },
      ],
      '4',
      50,
      0,
    )
    expect(plain(amounts)).toEqual(['1000000', '-200000', '20000'])
  })

  it('سطر بی‌نرخ صفر است و درصدها روی هم اثر ندارند', () => {
    const amounts = purchaseAmounts(
      [
        { basis: CostBasis.Total, rate: 1000 },
        { basis: CostBasis.Percent, rate: 10 },
        { basis: CostBasis.Percent, rate: 10 },
        { basis: CostBasis.PerM2, rate: null },
      ],
      '2',
      1,
      0,
    )
    expect(plain(amounts)).toEqual(['1000', '100', '100', '0'])
  })

  it('مبلغ یک تخته فقط برای هر متر و هر تخته', () => {
    expect(toPlainString(pieceAmount(CostBasis.PerM2, 9_000_000, '0.16', 0)!)).toBe('1440000')
    expect(toPlainString(pieceAmount(CostBasis.PerPiece, 300_000, '0.16', 0)!)).toBe('300000')
    expect(pieceAmount(CostBasis.Total, 1, '1', 0)).toBeNull()
  })
})
