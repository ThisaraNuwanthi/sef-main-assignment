import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockFetch, renderApp } from '../test/renderApp'
import { validateLogin } from '../utils/validation'

describe('Login form validation', () => {
  it('shows required-field errors and does not call the API when the form is empty', async () => {
    const fetchMock = mockFetch(() => ({ status: 500 }))
    renderApp('/login')

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(screen.getByText('Email is required.')).toBeInTheDocument()
    expect(screen.getByText('Password is required.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects a badly formed email', async () => {
    renderApp('/login')

    await userEvent.type(screen.getByLabelText('Email'), 'not-an-email')
    await userEvent.type(screen.getByLabelText('Password'), 'whatever')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
  })

  it('shows the API message when the password is wrong', async () => {
    mockFetch(() => ({ status: 401, body: { title: 'Unauthorized', detail: 'Invalid email or password.' } }))
    renderApp('/login')

    await userEvent.type(screen.getByLabelText('Email'), 'admin@skca.lk')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
  })

  it('validateLogin accepts a valid pair', () => {
    expect(validateLogin('admin@skca.lk', 'x')).toEqual({})
  })
})
