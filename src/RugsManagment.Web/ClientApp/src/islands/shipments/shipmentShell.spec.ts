/**
 * منطق خالص قاب محموله (فهرست و سربرگ، فاز ۴b): متن‌ها با واژهٔ کارگاه، دکمه‌های تراشه‌های آمادگی برای هر نقش، «بقیه همان
 * اندازهٔ اول» با نسخهٔ هر فرش، و قاعدهٔ حذف محمولهٔ خالی.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { ReadinessCode, ShipmentStatus, type Bale, type BaleRow, type ReadinessIssue } from '@/lib/types/shipments'
import {
  BALES_COMMAND_EVENT,
  SHIPMENT_CHANGED_EVENT,
  actionLabel,
  balesCommand,
  baleVersion,
  blockingIssues,
  canDeleteShipment,
  copy,
  isReady,
  listLine,
  num,
  orderIssues,
  piecesText,
  readinessActions,
  sameSizeOps,
  shellCopy,
  shipmentName,
  statusQuery,
} from './shipmentShell'

const ADL = { bale: 'عدل', bales: 'عدل‌ها', baleOf: 'عدل', baleFooter: 'عدل شماره' }

function issue(code: string, extra: Partial<ReadinessIssue> = {}): ReadinessIssue {
  return {
    code,
    message: code,
    count: extra.rugIds?.length ?? 1,
    rugIds: [],
    blocking: code.startsWith('E_'),
    key: null,
    baleId: null,
    baleNumber: null,
    ...extra,
  }
}

function row(rugId: string, rugVersion: number, final: [number, number] | null): BaleRow {
  return {
    lineId: `l-${rugId}`,
    lineVersion: 1,
    rugId,
    rugVersion,
    seq: 1,
    serial: rugId,
    qty: 1,
    fieldColor: null,
    borderColor: null,
    pattern: null,
    origin: null,
    material: null,
    intakeLength: 2,
    intakeWidth: 3,
    finalLength: final?.[0] ?? null,
    finalWidth: final?.[1] ?? null,
    sizeChangeCm: null,
    areaM2: 6,
    lineAreaM2: 6,
    usdPerM2: null,
    rateSource: 3,
    pieceUsd: null,
    lineUsd: null,
    baleId: 'b1',
    baleNumber: 1,
    usdPerM2Override: null,
    sku: null,
    declaredKey: null,
  }
}

function bale(rows: BaleRow[]): Bale {
  return { id: 'b1', number: 1, version: 7, weightKg: null, notes: null, pieces: rows.length, areaM2: 0, usdTotal: 0, targetAreaM2: 13, rows }
}

afterEach(() => resetAppContext())

describe('متن‌های قاب محموله', () => {
  it('بیرون از نشانه‌ها هیچ حرف لاتینی ندارند', () => {
    for (const [key, text] of Object.entries(shellCopy)) {
      expect(text.replace(/\{[A-Za-z]+\}/g, ''), key).not.toMatch(/[A-Za-z]/)
    }
  })

  it('هر «نگله» از واژهٔ کارگاه است: «عدل» برای کارگاهی که عدل می‌گوید', () => {
    resetAppContext({ terms: ADL })
    expect(copy('baleDeleted', { n: 3 })).toBe('عدل ۳ حذف شد.')
    expect(copy('finalizeConfirm', { pieces: 242 })).toContain('اندازه‌ها، عدل‌ها و ارزش گمرکی قفل می‌شوند')
    expect(listLine({ bales: 2, pieces: 52, lines: 3, areaM2: 18.25, usdTotal: 859.8 })).toBe(
      '۲ عدل · ۵۲ تخته (۳ ردیف) · ۱۸٫۲۵ متر · ۸۵۹٫۸ دلار',
    )
    expect(Object.values(shellCopy).join(' ')).not.toContain('نگله')
  })

  it('پرسش «قطعی کن» همان جملهٔ H.3 است', () => {
    resetAppContext({})
    expect(copy('finalizeConfirmTitle', { number: 8 })).toBe('محمولهٔ ۸ قطعی شود؟')
    expect(copy('finalizeConfirm', { pieces: 242 })).toBe(
      '۲۴۲ تخته «صادر شده» می‌شوند و اندازه‌ها، نگله‌ها و ارزش گمرکی قفل می‌شوند. هزینهٔ تازه هنوز قابل ثبت است.',
    )
    expect(copy('reopenConfirmTitle', { number: 8 })).toBe('محمولهٔ ۸ دوباره باز شود؟')
    expect(copy('sameSizeConfirmTitle', { n: 12 })).toBe('اندازهٔ نهایی ۱۲ فرش همان اندازهٔ اول ثبت شود؟')
  })

  it('عدد و تخته به شکل سرور', () => {
    expect(num(13.51)).toBe('۱۳٫۵۱')
    expect(num(10000)).toBe('۱۰٬۰۰۰')
    expect(num(null)).toBe('—')
    expect(shipmentName(12)).toBe('محمولهٔ ۱۲')
    expect(piecesText(4, 4)).toBe('۴ تخته')
    expect(piecesText(52, 3)).toBe('۵۲ تخته (۳ ردیف)')
  })
})

describe('فهرست محموله‌ها', () => {
  it('فیلتر صفحه به عدد enum سرور', () => {
    expect(statusQuery('open')).toBe(`?status=${ShipmentStatus.Open}`)
    expect(statusQuery('finalized')).toBe(`?status=${ShipmentStatus.Finalized}`)
    expect(statusQuery(null)).toBe('')
    expect(statusQuery('nope')).toBe('')
  })

  it('فقط محمولهٔ باز و بی‌فرش، و فقط برای Business، حذف می‌شود', () => {
    expect(canDeleteShipment({ status: ShipmentStatus.Open, lines: 0 }, true)).toBe(true)
    expect(canDeleteShipment({ status: ShipmentStatus.Open, lines: 0 }, false)).toBe(false)
    expect(canDeleteShipment({ status: ShipmentStatus.Open, lines: 1 }, true)).toBe(false)
    expect(canDeleteShipment({ status: ShipmentStatus.Finalized, lines: 0 }, true)).toBe(false)
  })
})

describe('آمادگی و «قطعی کن»', () => {
  const list = [
    issue(ReadinessCode.BaleOverTarget),
    issue(ReadinessCode.NoFinalSize, { rugIds: ['a', 'b'] }),
    issue(ReadinessCode.NoSerial, { rugIds: ['c'] }),
    issue(ReadinessCode.EmptyBale, { baleId: 'b9', baleNumber: 5 }),
  ]

  it('بازدارنده‌ها اول و ترتیب سرور در هر گروه', () => {
    expect(orderIssues(list).map((i) => i.code)).toEqual([
      ReadinessCode.NoFinalSize,
      ReadinessCode.EmptyBale,
      ReadinessCode.BaleOverTarget,
      ReadinessCode.NoSerial,
    ])
    expect(blockingIssues(list)).toHaveLength(2)
  })

  it('«آمادهٔ قطعی شدن» یعنی فرش دارد و بازدارنده ندارد؛ هشدار مانع نیست', () => {
    expect(isReady(0, [])).toBe(false)
    expect(isReady(3, [issue(ReadinessCode.BaleOverTarget)])).toBe(true)
    expect(isReady(3, list)).toBe(false)
  })

  it('دکمه‌های هر تراشه برای هر نقش (H.3b)', () => {
    expect(readinessActions(issue(ReadinessCode.EmptyShipment), false, false)).toEqual(['addRug'])
    expect(readinessActions(issue(ReadinessCode.EmptyBale, { baleId: 'b1' }), false, false)).toEqual(['deleteBale'])
    expect(readinessActions(issue(ReadinessCode.NoFinalSize, { rugIds: ['a'] }), false, false)).toEqual(['showMe', 'sameSize'])
    expect(readinessActions(issue(ReadinessCode.NoSerial, { rugIds: ['a'] }), false, false)).toEqual(['showMe'])
    // «نرخ بگذارید» کار بازرگان است (افزودن کلید ارزش گمرکی Business است، C.5)
    const missing = issue(ReadinessCode.NoDeclaredValue, { key: 'کاشان', rugIds: ['a'] })
    expect(readinessActions(missing, true, false)).toEqual(['setRate'])
    expect(readinessActions(missing, false, false)).toEqual([])
    // محمولهٔ قطعی دکمه‌ای ندارد
    expect(readinessActions(issue(ReadinessCode.NoFinalSize, { rugIds: ['a'] }), true, true)).toEqual([])
    expect(actionLabel('sameSize')).toBe('بقیه همان اندازهٔ اول')
    expect(actionLabel('setRate')).toBe('نرخ بگذارید')
  })

  it('«بقیه همان اندازهٔ اول»: یک عمل measure با sameAsIntake و نسخهٔ هر فرش بی‌اندازه', () => {
    const bales = [bale([row('a', 11, null), row('b', 12, [2, 3]), row('c', 13, null), row('d', 14, null)])]
    const ops = sameSizeOps(bales, ['a', 'b', 'c', 'zz'])
    expect(ops).toEqual([
      { op: 'measure', clientOpId: 'same-a', rugId: 'a', rugVersion: 11, sameAsIntake: true },
      { op: 'measure', clientOpId: 'same-c', rugId: 'c', rugVersion: 13, sameAsIntake: true },
    ])
  })

  it('فرمان‌های تخته همان قرارداد صفحهٔ نگله‌بندی است (رویدادهای window)', () => {
    expect(SHIPMENT_CHANGED_EVENT).toBe('rugs:shipment-changed')
    expect(BALES_COMMAND_EVENT).toBe('rugs:bales-command')
    const noSize = issue(ReadinessCode.NoFinalSize, { rugIds: ['a', 'b'] })
    expect(balesCommand(noSize, 'showMe')).toEqual({ command: 'show', rugIds: ['a', 'b'] })
    expect(balesCommand(noSize, 'sameSize')).toEqual({ command: 'same-as-intake', rugIds: ['a', 'b'] })
    expect(balesCommand(issue(ReadinessCode.EmptyShipment), 'addRug')).toEqual({ command: 'focus-serial' })
    expect(balesCommand(issue(ReadinessCode.EmptyBale, { baleId: 'b5' }), 'deleteBale')).toEqual({
      command: 'delete-bale',
      baleId: 'b5',
    })
    expect(balesCommand(issue(ReadinessCode.NoDeclaredValue, { key: 'کاشان' }), 'setRate')).toBeNull()
  })

  it('نسخهٔ نگلهٔ خالی برای حذف', () => {
    expect(baleVersion([{ id: 'b1', version: 7 }], 'b1')).toBe(7)
    expect(baleVersion([{ id: 'b1', version: 7 }], 'b2')).toBeNull()
    expect(baleVersion([{ id: 'b1', version: 7 }], null)).toBeNull()
  })
})
