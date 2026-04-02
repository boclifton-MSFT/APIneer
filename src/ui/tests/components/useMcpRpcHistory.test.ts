import { describe, it, expect, beforeEach } from 'vitest'
import { useMcpRpcHistory } from '~/composables/useMcpRpcHistory'

// useMcpRpcHistory uses module-level singleton state.
// Each test calls clearRpcHistory() in beforeEach to reset.

describe('useMcpRpcHistory', () => {
  let addRpcEntry: ReturnType<typeof useMcpRpcHistory>['addRpcEntry']
  let clearRpcHistory: ReturnType<typeof useMcpRpcHistory>['clearRpcHistory']
  let rpcHistory: ReturnType<typeof useMcpRpcHistory>['rpcHistory']

  beforeEach(() => {
    const composable = useMcpRpcHistory()
    addRpcEntry = composable.addRpcEntry
    clearRpcHistory = composable.clearRpcHistory
    rpcHistory = composable.rpcHistory
    clearRpcHistory()
  })

  // ── Initial state ──────────────────────────────────────────

  it('starts with empty history after clear', () => {
    expect(rpcHistory.value).toHaveLength(0)
  })

  // ── addRpcEntry ────────────────────────────────────────────

  it('adds an entry to the history', () => {
    addRpcEntry('tools/list', { id: 1 }, { tools: [] }, false)
    expect(rpcHistory.value).toHaveLength(1)
  })

  it('records the method name', () => {
    addRpcEntry('resources/list', {}, {}, false)
    expect(rpcHistory.value[0].method).toBe('resources/list')
  })

  it('records status as success when isError is false', () => {
    addRpcEntry('tools/list', {}, {}, false)
    expect(rpcHistory.value[0].status).toBe('success')
  })

  it('records status as error when isError is true', () => {
    addRpcEntry('tools/call', {}, { error: { code: -32000 } }, true)
    expect(rpcHistory.value[0].status).toBe('error')
  })

  it('stores the request payload', () => {
    const req = { method: 'tools/call', name: 'my-tool', arguments: { x: 1 } }
    addRpcEntry('tools/call', req, {}, false)
    expect(rpcHistory.value[0].request).toEqual(req)
  })

  it('stores the response payload', () => {
    const res = { tools: [{ name: 'search', description: 'Web search' }] }
    addRpcEntry('tools/list', {}, res, false)
    expect(rpcHistory.value[0].response).toEqual(res)
  })

  it('sets a timestamp close to now', () => {
    const before = new Date()
    addRpcEntry('ping', {}, {}, false)
    const after = new Date()
    const ts = rpcHistory.value[0].timestamp
    expect(ts.getTime()).toBeGreaterThanOrEqual(before.getTime())
    expect(ts.getTime()).toBeLessThanOrEqual(after.getTime())
  })

  it('assigns a numeric id to each entry', () => {
    addRpcEntry('a', {}, {}, false)
    expect(typeof rpcHistory.value[0].id).toBe('number')
  })

  // ── Ordering ───────────────────────────────────────────────

  it('inserts entries in LIFO order — newest at index 0', () => {
    addRpcEntry('first', {}, {}, false)
    addRpcEntry('second', {}, {}, false)
    addRpcEntry('third', {}, {}, false)

    expect(rpcHistory.value[0].method).toBe('third')
    expect(rpcHistory.value[1].method).toBe('second')
    expect(rpcHistory.value[2].method).toBe('first')
  })

  it('assigns monotonically increasing IDs', () => {
    addRpcEntry('a', {}, {}, false)
    addRpcEntry('b', {}, {}, false)
    addRpcEntry('c', {}, {}, false)

    const ids = rpcHistory.value.map(e => e.id)
    // Newest is at [0], so IDs should be descending (largest first)
    expect(ids[0]).toBeGreaterThan(ids[1])
    expect(ids[1]).toBeGreaterThan(ids[2])
  })

  // ── MAX_HISTORY cap (50) ───────────────────────────────────

  it('caps history at 50 entries', () => {
    for (let i = 0; i < 60; i++) {
      addRpcEntry(`method-${i}`, {}, {}, false)
    }
    expect(rpcHistory.value).toHaveLength(50)
  })

  it('retains the most recent 50 entries when capped', () => {
    for (let i = 0; i < 55; i++) {
      addRpcEntry(`method-${i}`, {}, {}, false)
    }
    // The most recent entry (method-54) should be at index 0
    expect(rpcHistory.value[0].method).toBe('method-54')
    // The oldest retained entry should be method-5 (54 - 49)
    expect(rpcHistory.value[49].method).toBe('method-5')
  })

  it('discards the oldest entries when cap is exceeded', () => {
    for (let i = 0; i < 55; i++) {
      addRpcEntry(`method-${i}`, {}, {}, false)
    }
    const methods = rpcHistory.value.map(e => e.method)
    // method-0 through method-4 should be gone
    for (let i = 0; i < 5; i++) {
      expect(methods).not.toContain(`method-${i}`)
    }
  })

  // ── clearRpcHistory ────────────────────────────────────────

  it('clearRpcHistory empties the history', () => {
    addRpcEntry('a', {}, {}, false)
    addRpcEntry('b', {}, {}, false)
    clearRpcHistory()
    expect(rpcHistory.value).toHaveLength(0)
  })

  it('clearRpcHistory resets the ID counter', () => {
    addRpcEntry('before-clear', {}, {}, false)
    const idBefore = rpcHistory.value[0].id

    clearRpcHistory()
    addRpcEntry('after-clear', {}, {}, false)

    // After clearing, the counter restarts — the new ID should be ≤ idBefore
    expect(rpcHistory.value[0].id).toBeLessThanOrEqual(idBefore)
    expect(rpcHistory.value[0].id).toBe(1)
  })

  it('can add new entries normally after clearing', () => {
    addRpcEntry('first-run', {}, {}, false)
    clearRpcHistory()
    addRpcEntry('second-run', {}, {}, false)

    expect(rpcHistory.value).toHaveLength(1)
    expect(rpcHistory.value[0].method).toBe('second-run')
  })

  // ── Composable returns readonly ref ───────────────────────

  it('rpcHistory is a readonly ref — has .value', () => {
    // The composable exposes rpcHistory as readonly(ref), so .value is accessible
    expect(rpcHistory.value).toBeDefined()
    expect(Array.isArray(rpcHistory.value)).toBe(true)
  })
})
