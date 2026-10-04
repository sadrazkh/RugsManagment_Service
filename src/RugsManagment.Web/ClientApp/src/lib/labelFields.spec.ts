import { describe, expect, it } from 'vitest'
import { fillPlaceholders, resolveField, STATIC_FIELDS, type LabelRug } from './labelFields'

const legacy: LabelRug = {
  id: '00000000-0000-0000-0000-000000000001',
  sku: 'RUG-202610-0001',
  widthMeters: 2.1,
  lengthMeters: 2.08,
  areaSquareMeters: 4.37,
  status: 0,
  workflowSteps: [],
  costs: null,
}

describe('فیلدهای برچسب (فاز ۲)', () => {
  it('شماره با ارقام لاتین می‌ماند تا QR و بارکد همان را بخوانند که سرور دارد', () => {
    const rug: LabelRug = { ...legacy, serialNo: '3911 - 3960', quantity: 50 }
    expect(resolveField(rug, 'serial')).toBe('3911 - 3960')
    expect(resolveField(rug, 'quantity')).toBe('۵۰')
    expect(fillPlaceholders(rug, '{{serial}}')).toBe('3911 - 3960')
  })

  it('اندازهٔ اول و نهایی «طول × عرض» با ارقام فارسی', () => {
    const rug: LabelRug = { ...legacy, intakeLength: 0.4, intakeWidth: 0.4, finalLength: 0.39, finalWidth: 0.4, totalAreaM2: 8 }
    expect(resolveField(rug, 'intakeSize')).toBe('۰.۴۰ × ۰.۴۰ م')
    expect(resolveField(rug, 'finalSize')).toBe('۰.۳۹ × ۰.۴۰ م')
    expect(resolveField(rug, 'totalArea')).toBe('۸.۰۰ م²')
  })

  it('فرش شکل قدیمی (بی عضوهای فاز ۲) خطا نمی‌دهد و «—» چاپ می‌کند', () => {
    for (const key of ['serial', 'quantity', 'intakeSize', 'finalSize', 'totalArea', 'batch', 'seller'])
      expect(resolveField(legacy, key), key).toBe('—')
  })

  it('فهرست فیلدها شماره را اول دارد و کلید تکراری ندارد', () => {
    expect(STATIC_FIELDS[0].key).toBe('serial')
    expect(new Set(STATIC_FIELDS.map((f) => f.key)).size).toBe(STATIC_FIELDS.length)
  })
})
