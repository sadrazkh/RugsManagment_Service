import { defineBulkAction } from '@/lib/bulkActions'
export default defineBulkAction({
  key: 'export-rugs', label: 'خروجی اکسل', icon: 'spreadsheet', order: 200, policy: 'floor', simple: true,
  async run(ctx) {
    if (!ctx.rugIds.length) return
    const form = document.createElement('form'); form.method = 'POST'; form.action = '/documents/rugs.xlsx'
    const values = [...ctx.rugIds.map(id => ['rugIds', id]), ['__RequestVerificationToken', document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? '']]
    for (const [name, value] of values) { const input = document.createElement('input'); input.type = 'hidden'; input.name = name; input.value = value; form.append(input) }
    document.body.append(form); form.submit(); form.remove()
  },
})
