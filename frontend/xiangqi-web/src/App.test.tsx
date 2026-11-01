import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './app/App'
import { useAuthStore } from './stores/authStore'
import type { User } from './services/authApi'

const api = vi.hoisted(() => ({
  register: vi.fn(), login: vi.fn(), refresh: vi.fn(), logout: vi.fn(), logoutAll: vi.fn(),
  me: vi.fn(), updateProfile: vi.fn(), changePassword: vi.fn(),
}))

vi.mock('./services/authApi', () => ({
  authApi: api,
  apiErrorMessage: (error: unknown) => error instanceof Error ? error.message : 'Không thể kết nối máy chủ.',
}))

const user: User = {
  id: 'u-1', username: 'kythu', email: 'kythu@example.com', displayName: 'Kỳ Thủ',
  avatarUrl: null, createdAt: '2026-10-31T00:00:00Z',
}

function useUnauthenticatedApi() {
  api.refresh.mockRejectedValue(new Error('no session'))
  api.login.mockReset(); api.register.mockReset(); api.me.mockReset()
  api.logout.mockResolvedValue({ loggedOut: true }); api.logoutAll.mockResolvedValue({ loggedOut: true })
  api.updateProfile.mockReset(); api.changePassword.mockReset()
}

describe('application routes and authentication UI', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.history.replaceState({}, '', '/')
    useUnauthenticatedApi()
    useAuthStore.setState({ user: null, accessToken: null, status: 'initializing' })
  })
  afterEach(() => { cleanup(); useAuthStore.getState().clearSession() })

  it('renders the home page and its 32-piece board', () => {
    render(<App />)
    expect(screen.getByRole('heading', { name: /giữ vững/i })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Bàn cờ Tướng khai cuộc' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /đỏ|đen/i })).toHaveLength(32)
  })

  it('redirects anonymous visitors from protected routes and preserves a safe return path', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('link', { name: 'Phòng chơi' }))
    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeInTheDocument()
    expect(screen.getByLabelText('Tên đăng nhập hoặc email')).toBeRequired()
  })

  it('validates login and registration fields in the browser', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('link', { name: 'Đăng nhập' }))
    expect(screen.getByLabelText('Tên đăng nhập hoặc email')).toBeRequired()
    expect(screen.getByLabelText('Mật khẩu')).toHaveAttribute('minLength', '1')
    fireEvent.click(screen.getByRole('link', { name: 'Đăng ký' }))
    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email')
    expect(screen.getByLabelText('Nhập lại mật khẩu')).toBeRequired()
    expect(screen.getByLabelText('Mật khẩu')).toHaveAttribute('minLength', '10')
    expect(screen.getByRole('button', { name: 'Đăng ký' })).toBeEnabled()
  })

  it('registers without signing in and routes the user to login', async () => {
    api.register.mockResolvedValue({ id: 'u-2', username: 'newplayer', displayName: 'newplayer' })
    render(<App />)
    fireEvent.click(await screen.findByRole('link', { name: 'Tạo tài khoản' }))
    fireEvent.change(screen.getByLabelText('Tên đăng nhập'), { target: { value: 'newplayer' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'new@example.com' } })
    fireEvent.change(screen.getByLabelText('Mật khẩu'), { target: { value: 'Strong horse 456!' } })
    fireEvent.change(screen.getByLabelText('Nhập lại mật khẩu'), { target: { value: 'Strong horse 456!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))
    expect(await screen.findByText('Tạo tài khoản thành công. Bạn có thể đăng nhập.')).toBeInTheDocument()
    expect(api.register).toHaveBeenCalledWith({ username: 'newplayer', email: 'new@example.com', password: 'Strong horse 456!', confirmPassword: 'Strong horse 456!' })
    expect(api.login).not.toHaveBeenCalled()
  })

  it('logs in, returns to the requested protected page, and keeps auth state in memory', async () => {
    api.login.mockResolvedValue({ accessToken: 'memory-token', expiresIn: 900, user })
    render(<App />)
    fireEvent.click(await screen.findByRole('link', { name: 'Phòng chơi' }))
    await screen.findByRole('heading', { name: 'Đăng nhập' })
    fireEvent.change(screen.getByLabelText('Tên đăng nhập hoặc email'), { target: { value: 'kythu' } })
    fireEvent.change(screen.getByLabelText('Mật khẩu'), { target: { value: 'Strong horse 456!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))
    expect(await screen.findByRole('heading', { name: 'Chọn một bàn đấu.' })).toBeInTheDocument()
    expect(api.login).toHaveBeenCalledWith({ identifier: 'kythu', password: 'Strong horse 456!', rememberMe: true })
    expect(useAuthStore.getState().accessToken).toBe('memory-token')
    expect(useAuthStore.getState().user?.id).toBe('u-1')
  })

  it('loads real profile state, updates display name, and logs out', async () => {
    api.refresh.mockResolvedValue({ accessToken: 'memory-token', expiresIn: 900, user })
    api.me.mockResolvedValue(user)
    api.updateProfile.mockResolvedValue({ ...user, displayName: 'Kỳ Thủ Mới' })
    render(<App />)
    fireEvent.click(await screen.findByRole('link', { name: /Kỳ Thủ/ }))
    expect(await screen.findByRole('heading', { name: 'Hồ sơ kỳ thủ.' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Chỉnh sửa tên hiển thị' }))
    fireEvent.change(screen.getByLabelText('Tên hiển thị'), { target: { value: 'Kỳ Thủ Mới' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))
    await waitFor(() => expect(api.updateProfile).toHaveBeenCalledWith('Kỳ Thủ Mới'))
    await waitFor(() => expect(useAuthStore.getState().user?.displayName).toBe('Kỳ Thủ Mới'))
    expect(screen.getByRole('heading', { name: 'Kỳ Thủ Mới' })).toBeInTheDocument()
    fireEvent.click(screen.getAllByRole('button', { name: 'Đăng xuất' })[1])
    await waitFor(() => expect(useAuthStore.getState().status).toBe('unauthenticated'))
    expect(api.logout).toHaveBeenCalledOnce()
  })
})
