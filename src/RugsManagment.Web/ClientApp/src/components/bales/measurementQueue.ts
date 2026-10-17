/**
 * blur و Enter ممکن است یک اندازه را هم‌زمان ثبت کنند. درخواست یکسان مشترک است؛
 * تغییر تازه پس از پایان قبلی اجرا می‌شود تا نسخهٔ تازهٔ فرش را بگیرد.
 */
export function measurementQueue<T>() {
  const pending = new Map<string, { signature: string; promise: Promise<T> }>()
  return {
    run(key: string, signature: string, write: () => Promise<T>): Promise<T> {
      const previous = pending.get(key)
      if (previous?.signature === signature) return previous.promise
      const promise = (previous ? previous.promise.catch(() => undefined) : Promise.resolve()).then(write)
      const entry = { signature, promise }
      pending.set(key, entry)
      const clear = () => { if (pending.get(key) === entry) pending.delete(key) }
      void promise.then(clear, clear)
      return promise
    },
  }
}
