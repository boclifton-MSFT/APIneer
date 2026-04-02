import { describe, it, expect } from 'vitest'
import {
  methodColor,
  statusSeverity,
  methodCssColor,
  METHOD_COLORS,
  METHOD_CSS_COLORS
} from '~/composables/useHttpColors'

// ---------------------------------------------------------------------------
// methodColor — UI component color tokens
// ---------------------------------------------------------------------------
describe('methodColor', () => {
  it('maps GET → success', () => expect(methodColor('GET')).toBe('success'))
  it('maps POST → info', () => expect(methodColor('POST')).toBe('info'))
  it('maps PUT → warning', () => expect(methodColor('PUT')).toBe('warning'))
  it('maps PATCH → warning', () => expect(methodColor('PATCH')).toBe('warning'))
  it('maps DELETE → error', () => expect(methodColor('DELETE')).toBe('error'))
  it('maps HEAD → secondary', () => expect(methodColor('HEAD')).toBe('secondary'))
  it('maps OPTIONS → neutral', () => expect(methodColor('OPTIONS')).toBe('neutral'))

  it('returns neutral for unknown method', () => {
    expect(methodColor('TRACE')).toBe('neutral')
    expect(methodColor('UNKNOWN')).toBe('neutral')
  })

  it('returns neutral for empty string', () => {
    expect(methodColor('')).toBe('neutral')
  })

  it('is case-sensitive — lowercase returns neutral', () => {
    expect(methodColor('get')).toBe('neutral')
    expect(methodColor('post')).toBe('neutral')
  })
})

// ---------------------------------------------------------------------------
// statusSeverity — HTTP response status classification
// ---------------------------------------------------------------------------
describe('statusSeverity', () => {
  it('classifies 2xx as success', () => {
    expect(statusSeverity(200)).toBe('success')
    expect(statusSeverity(201)).toBe('success')
    expect(statusSeverity(204)).toBe('success')
    expect(statusSeverity(299)).toBe('success')
  })

  it('classifies 3xx as info', () => {
    expect(statusSeverity(301)).toBe('info')
    expect(statusSeverity(302)).toBe('info')
    expect(statusSeverity(304)).toBe('info')
    expect(statusSeverity(399)).toBe('info')
  })

  it('classifies 4xx as warning', () => {
    expect(statusSeverity(400)).toBe('warning')
    expect(statusSeverity(401)).toBe('warning')
    expect(statusSeverity(404)).toBe('warning')
    expect(statusSeverity(422)).toBe('warning')
    expect(statusSeverity(499)).toBe('warning')
  })

  it('classifies 5xx as error', () => {
    expect(statusSeverity(500)).toBe('error')
    expect(statusSeverity(502)).toBe('error')
    expect(statusSeverity(503)).toBe('error')
    expect(statusSeverity(599)).toBe('error')
  })

  it('classifies 0 (no response / proxy error) as error', () => {
    expect(statusSeverity(0)).toBe('error')
  })

  it('classifies 1xx as error (fallthrough)', () => {
    // 1xx is not a 2xx/3xx/4xx boundary, so falls to the error default
    expect(statusSeverity(100)).toBe('error')
  })

  it('boundary: 200 is success, 199 is error', () => {
    expect(statusSeverity(200)).toBe('success')
    expect(statusSeverity(199)).toBe('error')
  })

  it('boundary: 300 is info, 299 is success', () => {
    expect(statusSeverity(300)).toBe('info')
    expect(statusSeverity(299)).toBe('success')
  })

  it('boundary: 400 is warning, 399 is info', () => {
    expect(statusSeverity(400)).toBe('warning')
    expect(statusSeverity(399)).toBe('info')
  })

  it('boundary: 500 is error, 499 is warning', () => {
    expect(statusSeverity(500)).toBe('error')
    expect(statusSeverity(499)).toBe('warning')
  })
})

// ---------------------------------------------------------------------------
// methodCssColor — sidebar badge CSS class suffixes
// ---------------------------------------------------------------------------
describe('methodCssColor', () => {
  it('maps GET → green', () => expect(methodCssColor('GET')).toBe('green'))
  it('maps POST → blue', () => expect(methodCssColor('POST')).toBe('blue'))
  it('maps PUT → orange', () => expect(methodCssColor('PUT')).toBe('orange'))
  it('maps PATCH → yellow', () => expect(methodCssColor('PATCH')).toBe('yellow'))
  it('maps DELETE → red', () => expect(methodCssColor('DELETE')).toBe('red'))
  it('maps HEAD → purple', () => expect(methodCssColor('HEAD')).toBe('purple'))
  it('maps OPTIONS → gray', () => expect(methodCssColor('OPTIONS')).toBe('gray'))

  it('returns gray for unknown method', () => {
    expect(methodCssColor('TRACE')).toBe('gray')
    expect(methodCssColor('FOO')).toBe('gray')
    expect(methodCssColor('')).toBe('gray')
  })
})

// ---------------------------------------------------------------------------
// TYPE CONTRACT: METHOD_COLORS and METHOD_CSS_COLORS cover all 7 standard methods
// ---------------------------------------------------------------------------
describe('METHOD_COLORS constant', () => {
  const EXPECTED_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS']

  it('has an entry for every standard HTTP method', () => {
    for (const m of EXPECTED_METHODS) {
      expect(METHOD_COLORS).toHaveProperty(m)
    }
  })

  it('covers exactly the 7 standard methods', () => {
    expect(Object.keys(METHOD_COLORS)).toHaveLength(7)
  })
})

describe('METHOD_CSS_COLORS constant', () => {
  const EXPECTED_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS']

  it('has an entry for every standard HTTP method', () => {
    for (const m of EXPECTED_METHODS) {
      expect(METHOD_CSS_COLORS).toHaveProperty(m)
    }
  })

  it('covers exactly the 7 standard methods', () => {
    expect(Object.keys(METHOD_CSS_COLORS)).toHaveLength(7)
  })
})
