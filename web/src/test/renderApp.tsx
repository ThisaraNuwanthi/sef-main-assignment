import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import { AppRoutes } from '../App'
import type { Role } from '../api/types'
import { AuthProvider } from '../auth/AuthContext'
import { saveSession } from '../auth/authStorage'

/** Renders the real routes at a given URL, optionally already logged in as a role. */
export function renderApp(route: string, role?: Role) {
  if (role) {
    saveSession({
      token: 'test-token',
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 1, fullName: `Test ${role}`, email: `${role.toLowerCase()}@test.lk`, role },
    })
  }
  // Fresh cache per test, and no automatic retries so error states appear immediately.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <MemoryRouter initialEntries={[route]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

/** Replaces window.fetch; `handler` decides the response for each (method, url). */
export function mockFetch(handler: (method: string, url: string) => { status: number; body?: unknown }) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const { status, body } = handler(init?.method ?? 'GET', url)
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
    })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}
