import { describe, expect, it } from 'vitest'
import { keyAction, navArrow, navEdge, navEnter, navTab, type NavShape, type NavState } from './useGridKeyboard'

/** شماره · فروشنده · تعداد · متراژ (فقط‌خواندنی) · طول · عرض */
const shape: NavShape = {
  rows: 4,
  cols: [{ tabStop: true }, { tabStop: true }, { tabStop: true }, { tabStop: false }, { tabStop: true }, { tabStop: true }],
}

const at = (row: number, col: number, runStart: number | null = null): NavState => ({ row, col, runStart })

describe('پیکان‌ها در راست‌به‌چپ', () => {
  it('← ستون بعد، → ستون قبل', () => {
    expect(navArrow(at(0, 1), shape, 'ArrowLeft')).toEqual(at(0, 2))
    expect(navArrow(at(0, 1), shape, 'ArrowRight')).toEqual(at(0, 0))
  })

  it('↑ و ↓ ردیف؛ لبه‌ها نگه داشته می‌شوند', () => {
    expect(navArrow(at(0, 0), shape, 'ArrowDown')).toEqual(at(1, 0))
    expect(navArrow(at(0, 0), shape, 'ArrowUp')).toEqual(at(0, 0))
    expect(navArrow(at(0, 5), shape, 'ArrowLeft')).toEqual(at(0, 5))
    expect(navArrow(at(3, 0), shape, 'ArrowDown')).toEqual(at(3, 0))
  })

  it('پیکان روی خانهٔ فقط‌خواندنی هم می‌ایستد ولی رشتهٔ Tab را تمام می‌کند', () => {
    expect(navArrow(at(0, 2, 0), shape, 'ArrowLeft')).toEqual(at(0, 3, null))
  })
})

describe('Tab و رشتهٔ Tab', () => {
  it('خانهٔ فقط‌خواندنی ایستگاه Tab نیست', () => {
    expect(navTab(at(0, 2), shape, false)).toEqual(at(0, 4, 2))
    expect(navTab(at(0, 4), shape, true)).toEqual(at(0, 2))
  })

  it('رشته از خانهٔ اول Tab شروع می‌شود', () => {
    let s = navTab(at(0, 0), shape, false)!
    expect(s).toEqual(at(0, 1, 0))
    s = navTab(s, shape, false)!
    expect(s).toEqual(at(0, 2, 0))
  })

  it('ته ردیف به اولین ایستگاه ردیف بعد؛ ته جدول null', () => {
    expect(navTab(at(0, 5, 0), shape, false)).toEqual(at(1, 0))
    expect(navTab(at(3, 5), shape, false)).toBeNull()
    expect(navTab(at(1, 0), shape, true)).toEqual(at(0, 5))
    expect(navTab(at(0, 0), shape, true)).toBeNull()
  })
})

describe('Enter', () => {
  it('پایین در همان ستون', () => {
    expect(navEnter(at(0, 4), shape, 'down', false)).toEqual(at(1, 4))
  })

  it('بازگشت پس از رشتهٔ Tab: به ستون شروع رشته در ردیف بعد (مثل اکسل)', () => {
    let s = at(0, 0)
    s = navTab(s, shape, false)! // فروشنده
    s = navTab(s, shape, false)! // تعداد
    s = navTab(s, shape, false)! // طول (متراژ پریده شد)
    s = navTab(s, shape, false)! // عرض
    expect(s).toEqual(at(0, 5, 0))
    expect(navEnter(s, shape, 'down', false)).toEqual(at(1, 0, null))
  })

  it('رشته‌ای که از وسط ردیف شروع شد', () => {
    const s = navTab(navTab(at(2, 1), shape, false)!, shape, false)!
    expect(s).toEqual(at(2, 4, 1))
    expect(navEnter(s, shape, 'down', false)).toEqual(at(3, 1))
  })

  it('ترجیح «راست»: مثل Tab، و ته ردیف به ستون شروع در ردیف بعد', () => {
    let s = navEnter(at(0, 0), shape, 'right', false)!
    expect(s).toEqual(at(0, 1, 0))
    s = navEnter(at(0, 5, 1), shape, 'right', false)!
    expect(s).toEqual(at(1, 1))
    expect(navEnter(at(0, 5), shape, 'right', false)).toEqual(at(1, 0))
  })

  it('Shift+Enter بالا؛ ته جدول null', () => {
    expect(navEnter(at(2, 4), shape, 'down', true)).toEqual(at(1, 4))
    expect(navEnter(at(3, 4), shape, 'down', false)).toBeNull()
  })
})

describe('Home/End', () => {
  it('اول و آخر ردیف؛ با Ctrl اول و آخر جدول', () => {
    expect(navEdge(at(2, 3), shape, 'home', false)).toEqual(at(2, 0))
    expect(navEdge(at(2, 3), shape, 'end', true)).toEqual(at(3, 5))
  })
})

describe('کلید → کار', () => {
  it('حالت انتخاب', () => {
    expect(keyAction({ key: 'Tab' }, 'nav')).toEqual({ kind: 'tab', shift: false })
    expect(keyAction({ key: 'Enter', shiftKey: true }, 'nav')).toEqual({ kind: 'enter', shift: true })
    expect(keyAction({ key: 'F2' }, 'nav')).toEqual({ kind: 'edit' })
    expect(keyAction({ key: 'Delete' }, 'nav')).toEqual({ kind: 'clear' })
    expect(keyAction({ key: 'ArrowLeft', shiftKey: true }, 'nav')).toEqual({ kind: 'arrow', key: 'ArrowLeft', extend: true })
    expect(keyAction({ key: 'a' }, 'nav')).toBeNull()
  })

  it('Ctrl+D و Ctrl+Z با چیدمان فارسی (جای کلید)', () => {
    expect(keyAction({ key: 'ی', code: 'KeyD', ctrlKey: true }, 'nav')).toEqual({ kind: 'fillDown' })
    expect(keyAction({ key: 'ظ', code: 'KeyZ', ctrlKey: true }, 'nav')).toEqual({ kind: 'undo' })
    expect(keyAction({ key: 'z', metaKey: true }, 'nav')).toEqual({ kind: 'undo' })
  })

  it('Alt+↓ چیپ واحد را عوض می‌کند', () => {
    expect(keyAction({ key: 'ArrowDown', altKey: true }, 'nav')).toEqual({ kind: 'cycleUnit' })
  })

  it('با تایپ شروع‌شده پیکان ثبت و جابه‌جا می‌کند؛ با F2 چپ/راست مکان‌نماست', () => {
    expect(keyAction({ key: 'ArrowLeft' }, 'enter')).toEqual({ kind: 'arrow', key: 'ArrowLeft', extend: false })
    expect(keyAction({ key: 'ArrowLeft' }, 'edit')).toBeNull()
    expect(keyAction({ key: 'ArrowDown' }, 'edit')).toEqual({ kind: 'arrow', key: 'ArrowDown', extend: false })
    expect(keyAction({ key: 'Delete' }, 'edit')).toBeNull()
    expect(keyAction({ key: 'z', ctrlKey: true }, 'enter')).toBeNull()
  })

  it('پرسش باز: Enter پاسخ اول، Esc پاسخ دوم، تایپ بسته', () => {
    expect(keyAction({ key: 'Enter' }, 'ask')).toEqual({ kind: 'answer', index: 0 })
    expect(keyAction({ key: 'Escape' }, 'ask')).toEqual({ kind: 'answer', index: 1 })
    expect(keyAction({ key: '5' }, 'ask')).toEqual({ kind: 'blocked' })
    expect(keyAction({ key: 'Tab' }, 'ask')).toEqual({ kind: 'blocked' })
  })
})
