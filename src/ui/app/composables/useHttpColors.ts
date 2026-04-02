const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'] as const
export type HttpMethod = (typeof HTTP_METHODS)[number]

export const METHOD_COLORS = {
  GET: 'success',
  POST: 'info',
  PUT: 'warning',
  PATCH: 'warning',
  DELETE: 'error',
  HEAD: 'secondary',
  OPTIONS: 'neutral'
} as const satisfies Record<HttpMethod, string>

export type MethodColorValue = (typeof METHOD_COLORS)[HttpMethod]

export function methodColor(method: string): string {
  return METHOD_COLORS[method as HttpMethod] ?? 'neutral'
}

export function statusSeverity(code: number): string {
  if (code >= 200 && code < 300) return 'success'
  if (code >= 300 && code < 400) return 'info'
  if (code >= 400 && code < 500) return 'warning'
  return 'error'
}

/**
 * Maps HTTP method to a CSS class suffix for color coding.
 * Used by sidebar/tree method badges.
 */
export const METHOD_CSS_COLORS = {
  GET: 'green',
  POST: 'blue',
  PUT: 'orange',
  PATCH: 'yellow',
  DELETE: 'red',
  HEAD: 'purple',
  OPTIONS: 'gray'
} as const satisfies Record<HttpMethod, string>

export function methodCssColor(method: string): string {
  return METHOD_CSS_COLORS[method as HttpMethod] ?? 'gray'
}
