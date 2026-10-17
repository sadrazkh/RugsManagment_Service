/**
 * «افزودن به نگله» در نوار گروهی فهرست فرش‌ها (H.1؛ درگاه `[data-bulk-action]` فاز سکو، J.2).
 *
 * فرش‌های انتخاب‌شده را به یک نگلهٔ محمولهٔ هدف می‌برد: انتخاب محموله (وقتی چند محمولهٔ باز هست، A.4)، بعد انتخاب‌گر
 * نگله با «نگلهٔ جاری» اول و «نگلهٔ تازه». کار سنگین (کامپوننت و پنجره) فقط هنگام اجرا بار می‌شود تا نوار سبک بماند.
 * انباردار هم می‌تواند (Floor)؛ دسترسی واقعی را سرور می‌سنجد. واژهٔ نگله از `terms` است («افزودن به عدل»).
 */
import { defineBulkAction } from '@/lib/bulkActions'
import { message } from '@/lib/messages'

export default defineBulkAction({
  key: 'add-to-bale',
  // برچسب هنگام خواندن ساخته می‌شود تا واژهٔ کارگاه (نگله/عدل) از app-context بیاید
  get label() {
    return message('I_BOARD_ADD_TO_BALE')
  },
  icon: 'bale',
  order: 100,
  policy: 'floor',
  simple: true,
  async run(ctx) {
    if (ctx.rugIds.length === 0) return
    const [{ createApp, h }, { default: AddToBaleFlow }, ui] = await Promise.all([
      import('vue'),
      import('@/components/bales/AddToBaleFlow.vue'),
      import('@/lib/ui'),
    ])

    ctx.setBusy(true)
    const host = document.createElement('div')
    host.dataset.addToBale = ''
    document.body.appendChild(host)

    await new Promise<void>((resolve) => {
      const app = createApp({
        render: () =>
          h(AddToBaleFlow, {
            rugIds: [...ctx.rugIds],
            done: (outcome: { added: number; message: string | null }) => {
              app.unmount()
              host.remove()
              ctx.setBusy(false)
              if (outcome.added > 0) {
                if (outcome.message) ui.toastAfterReload(outcome.message, 'success')
                ctx.clearSelection()
                ctx.reload()
              } else if (outcome.message) {
                ui.toast.error(outcome.message)
              }
              resolve()
            },
          }),
      })
      app.mount(host)
    })
  },
})
