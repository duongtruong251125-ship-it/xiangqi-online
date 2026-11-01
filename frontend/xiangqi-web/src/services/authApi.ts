import { apiClient } from './apiClient'

export type User = {
  id: string
  username: string
  email: string
  displayName: string
  avatarUrl: string | null
  createdAt: string
}

export type ApiEnvelope<T> = { success: boolean; data: T; requestId: string }
export type RegisterInput = { username: string; email: string; password: string; confirmPassword: string }
export type LoginInput = { identifier: string; password: string; rememberMe: boolean }
export type Session = { accessToken: string; expiresIn: number; user: User }
export type RegisteredUser = { id: string; username: string; displayName: string }

async function data<T>(request: Promise<{ data: ApiEnvelope<T> }>) {
  return (await request).data.data
}

export const authApi = {
  register: (input: RegisterInput) => data(apiClient.post<ApiEnvelope<RegisteredUser>>('/auth/register', input)),
  login: (input: LoginInput) => data(apiClient.post<ApiEnvelope<Session>>('/auth/login', input)),
  refresh: () => data(apiClient.post<ApiEnvelope<Session>>('/auth/refresh')),
  logout: () => data(apiClient.post<ApiEnvelope<{ loggedOut: boolean }>>('/auth/logout')),
  logoutAll: () => data(apiClient.post<ApiEnvelope<{ loggedOut: boolean }>>('/auth/logout-all')),
  me: () => data(apiClient.get<ApiEnvelope<User>>('/users/me')),
  updateProfile: (displayName: string) => data(apiClient.patch<ApiEnvelope<User>>('/users/me', { displayName })),
  changePassword: (currentPassword: string, newPassword: string, confirmNewPassword: string) =>
    data(apiClient.put<ApiEnvelope<{ changed: boolean }>>('/users/me/password', { currentPassword, newPassword, confirmNewPassword })),
}

export function apiErrorMessage(error: unknown): string {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const body = (error as { response?: { data?: { error?: { message?: string } } } }).response?.data
    if (body?.error?.message) return body.error.message
  }
  return 'Không thể kết nối máy chủ. Vui lòng thử lại.'
}
