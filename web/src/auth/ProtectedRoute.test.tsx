import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import { mockFetch, renderApp } from '../test/renderApp'

describe('Protected routes', () => {
  it('sends a logged-out visitor to the login page', async () => {
    renderApp('/admin/classes')

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Classes' })).not.toBeInTheDocument()
  })

  it('blocks a coach from admin pages', async () => {
    renderApp('/admin', 'Coach')

    expect(await screen.findByRole('heading', { name: 'Access denied' })).toBeInTheDocument()
  })

  it('lets an admin in and shows admin navigation', async () => {
    mockFetch(() => ({ status: 200, body: { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }))
    renderApp('/admin/classes', 'Admin')

    expect(await screen.findByRole('heading', { name: 'Classes' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Enrolments' })).toBeInTheDocument()
    expect(await screen.findByText('No classes match these filters.')).toBeInTheDocument()
  })
})
