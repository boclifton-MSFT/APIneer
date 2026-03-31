import { setupServer } from 'msw/node'
import { handlers } from './mocks/handlers'
import { afterAll, afterEach, beforeAll } from 'vitest'

// Make navigator.clipboard writable so tests can mock it with Object.assign
Object.defineProperty(navigator, 'clipboard', {
  value: { writeText: () => Promise.resolve(), readText: () => Promise.resolve('') },
  writable: true,
  configurable: true
})

const server = setupServer(...handlers)

beforeAll(() => server.listen({ onUnhandledRequest: 'bypass' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
