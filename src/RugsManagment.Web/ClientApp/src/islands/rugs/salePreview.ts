/**
 * پیش‌نمایش زندهٔ فرم فروش (`RugSalePanel.vue`) — جدا تا بی DOM آزموده شود.
 *
 * سود با همان سرمایه‌ای حساب می‌شود که سرور حساب می‌کند (D.6): فروش تازه عکس «بهای امروز» را می‌گیرد، پس سرمایهٔ
 * پیش‌نمایش همان بهای امروز فرش است؛ ولی ویرایش فروش موجود عکس بهای لحظهٔ فروش (CostAtSale) را دست نمی‌زند و سرور سود را
 * با همان می‌سازد. پیش‌تر ویرایش هم با بهای امروز حساب می‌شد و بازرگان پیش از تأیید سود دیگری می‌دید. حساب با `lib/num.ts`.
 */
import { cmp, dec, sub, toNumber } from '@/lib/num'

/** سرمایهٔ پیش‌نمایش: فروش موجود → بهای لحظهٔ فروش (`sale.totalInvestment`)؛ فروش تازه → بهای امروز فرش. */
export function previewInvestment(existingSale: { totalInvestment: number } | null, todaysCost: number | null): number | null {
  return existingSale ? existingSale.totalInvestment : todaysCost
}

/** خالص (فروش − تخفیف، کف صفر) و سود (خالص − سرمایه). */
export function salePreview(
  salePrice: number | null | undefined,
  discount: number | null | undefined,
  investment: number | null,
): { net: number; profit: number } {
  const gross = sub(dec(salePrice ?? 0), dec(discount ?? 0))
  const net = cmp(gross, 0) < 0 ? dec(0) : gross
  return { net: toNumber(net), profit: toNumber(sub(net, dec(investment ?? 0))) }
}
