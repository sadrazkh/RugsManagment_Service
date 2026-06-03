export function formatMoneyInput(value: number | undefined | null): string {
  if (value == null || Number.isNaN(value)) return ''
  return new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 0 }).format(value)
}

export function parseMoneyInput(raw: string): number | undefined {
  const digits = raw.replace(/[^\d]/g, '')
  if (!digits) return undefined
  const n = Number(digits)
  return Number.isFinite(n) ? n : undefined
}
