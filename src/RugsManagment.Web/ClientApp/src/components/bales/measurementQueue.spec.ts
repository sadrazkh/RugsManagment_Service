import { describe, expect, it, vi } from 'vitest'
import { measurementQueue } from './measurementQueue'

describe('final-size blur and Enter race', () => {
  it('coalesces the same request and reads the latest version for a later size', async () => {
    const queue = measurementQueue<number>()
    let finish!: () => void
    let version = 1
    const requests: number[] = []
    const firstWrite = vi.fn(async () => {
      requests.push(version)
      await new Promise<void>(resolve => { finish = resolve })
      return ++version
    })
    const first = queue.run('rug1', '2x3', firstWrite)
    const blur = queue.run('rug1', '2x3', firstWrite)
    const changed = queue.run('rug1', '2x4', async () => { requests.push(version); return ++version })
    await Promise.resolve()
    expect(firstWrite).toHaveBeenCalledTimes(1)
    expect(first).toBe(blur)
    expect(requests).toEqual([1])
    finish()
    expect(await first).toBe(2)
    expect(await changed).toBe(3)
    expect(requests).toEqual([1, 2])
  })

  it('independent rugs remain parallel and a failed write does not poison the queue', async () => {
    const queue = measurementQueue<number>()
    const failed = queue.run('rug1', '2x3', async () => { throw new Error('offline') })
    const retry = queue.run('rug1', '2x4', async () => 4)
    const other = queue.run('rug2', '1x1', async () => 1)
    await expect(failed).rejects.toThrow('offline')
    expect(await other).toBe(1)
    expect(await retry).toBe(4)
  })
})
