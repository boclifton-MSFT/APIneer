import { describe, it, expect } from 'vitest'
import {
  buildEnvObject,
  buildHeadersObject,
  parseKeyValueJson
} from '~/composables/useMcpHelpers'

// ---------------------------------------------------------------------------
// buildEnvObject
// ---------------------------------------------------------------------------
describe('buildEnvObject', () => {
  it('returns undefined for an empty array', () => {
    expect(buildEnvObject([])).toBeUndefined()
  })

  it('returns undefined when all keys are empty', () => {
    expect(buildEnvObject([{ key: '', value: 'val' }])).toBeUndefined()
  })

  it('returns undefined when all keys are whitespace-only', () => {
    expect(buildEnvObject([{ key: '   ', value: 'val' }])).toBeUndefined()
  })

  it('builds an object from valid key-value entries', () => {
    const result = buildEnvObject([
      { key: 'KEY1', value: 'value1' },
      { key: 'KEY2', value: 'value2' }
    ])
    expect(result).toEqual({ KEY1: 'value1', KEY2: 'value2' })
  })

  it('trims whitespace from keys', () => {
    const result = buildEnvObject([{ key: '  MY_KEY  ', value: 'val' }])
    expect(result).toEqual({ MY_KEY: 'val' })
  })

  it('skips entries with blank keys, includes entries with non-blank keys', () => {
    const result = buildEnvObject([
      { key: 'VALID', value: 'yes' },
      { key: '   ', value: 'skipped' },
      { key: '', value: 'also-skipped' }
    ])
    expect(result).toEqual({ VALID: 'yes' })
  })

  it('preserves empty string values for non-empty keys', () => {
    const result = buildEnvObject([{ key: 'EMPTY_VAL', value: '' }])
    expect(result).toEqual({ EMPTY_VAL: '' })
  })

  it('handles values with special characters', () => {
    const result = buildEnvObject([{ key: 'TOKEN', value: 'Bearer abc.def.ghi' }])
    expect(result).toEqual({ TOKEN: 'Bearer abc.def.ghi' })
  })

  it('last duplicate key wins', () => {
    const result = buildEnvObject([
      { key: 'KEY', value: 'first' },
      { key: 'KEY', value: 'second' }
    ])
    expect(result).toEqual({ KEY: 'second' })
  })
})

// ---------------------------------------------------------------------------
// buildHeadersObject — same logic as buildEnvObject, different semantics
// ---------------------------------------------------------------------------
describe('buildHeadersObject', () => {
  it('returns undefined for empty array', () => {
    expect(buildHeadersObject([])).toBeUndefined()
  })

  it('returns undefined when all keys are empty', () => {
    expect(buildHeadersObject([{ key: '', value: 'Bearer token' }])).toBeUndefined()
  })

  it('builds headers object from entries', () => {
    const result = buildHeadersObject([
      { key: 'Authorization', value: 'Bearer token123' },
      { key: 'Content-Type', value: 'application/json' }
    ])
    expect(result).toEqual({
      Authorization: 'Bearer token123',
      'Content-Type': 'application/json'
    })
  })

  it('trims whitespace from header names', () => {
    const result = buildHeadersObject([{ key: '  Authorization  ', value: 'Bearer x' }])
    expect(result).toEqual({ Authorization: 'Bearer x' })
  })

  it('skips entries with empty header names', () => {
    const result = buildHeadersObject([
      { key: 'X-Custom', value: 'abc' },
      { key: '', value: 'should-be-skipped' }
    ])
    expect(result).toEqual({ 'X-Custom': 'abc' })
  })
})

// ---------------------------------------------------------------------------
// parseKeyValueJson
// ---------------------------------------------------------------------------
describe('parseKeyValueJson', () => {
  it('parses a valid JSON object into key-value entries', () => {
    const result = parseKeyValueJson('{"Authorization":"Bearer tok","X-Api":"abc"}')
    expect(result).toEqual([
      { key: 'Authorization', value: 'Bearer tok' },
      { key: 'X-Api', value: 'abc' }
    ])
  })

  it('returns default empty row for undefined input', () => {
    expect(parseKeyValueJson(undefined)).toEqual([{ key: '', value: '' }])
  })

  it('returns default empty row for invalid JSON', () => {
    expect(parseKeyValueJson('not { valid json')).toEqual([{ key: '', value: '' }])
  })

  it('returns default empty row for empty JSON object', () => {
    expect(parseKeyValueJson('{}')).toEqual([{ key: '', value: '' }])
  })

  it('returns default empty row for empty string', () => {
    expect(parseKeyValueJson('')).toEqual([{ key: '', value: '' }])
  })

  it('coerces numeric values to strings', () => {
    const result = parseKeyValueJson('{"count":42}')
    expect(result).toContainEqual({ key: 'count', value: '42' })
  })

  it('coerces boolean values to strings', () => {
    const result = parseKeyValueJson('{"active":true,"disabled":false}')
    expect(result).toContainEqual({ key: 'active', value: 'true' })
    expect(result).toContainEqual({ key: 'disabled', value: 'false' })
  })

  it('coerces null values to "null" string', () => {
    const result = parseKeyValueJson('{"key":null}')
    expect(result).toContainEqual({ key: 'key', value: 'null' })
  })

  it('returns default empty row for JSON array (not an object)', () => {
    // JSON.parse('[]') is not an object with entries — Object.entries([]) is empty
    expect(parseKeyValueJson('[]')).toEqual([{ key: '', value: '' }])
  })

  it('handles single entry correctly', () => {
    const result = parseKeyValueJson('{"X-Tenant":"acme"}')
    expect(result).toEqual([{ key: 'X-Tenant', value: 'acme' }])
  })
})
