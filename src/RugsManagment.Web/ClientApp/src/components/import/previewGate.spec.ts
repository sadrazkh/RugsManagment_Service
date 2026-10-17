import { computed, reactive } from 'vue'
import { describe, expect, it } from 'vitest'
import { createPreviewGate } from './previewGate'
describe('import preview commit guard', () => {
  it('blocks old preview after corrected dimensions or changed import policy until re-preview succeeds', () => {
    const draft = reactive({ sheet: 'Sheet1', mode: 0, policy: 0, overrides: { 3: { length: '2.08' } } })
    const gate = createPreviewGate(computed(() => draft))
    expect(gate.current.value).toBe(false)
    gate.accept(gate.capture()); expect(gate.current.value).toBe(true)
    draft.overrides[3].length = '2.20'; expect(gate.current.value).toBe(false)
    gate.accept(gate.capture()); expect(gate.current.value).toBe(true)
    draft.policy = 1; expect(gate.current.value).toBe(false)
  })
  it('does not treat a late preview response as approval of edits made while it was in flight', () => {
    const draft = reactive({ serial: '100' })
    const gate = createPreviewGate(computed(() => draft))
    const sent = gate.capture()
    draft.serial = '101'
    gate.accept(sent)
    expect(gate.current.value).toBe(false)
    gate.accept(gate.capture())
    expect(gate.current.value).toBe(true)
  })
})
