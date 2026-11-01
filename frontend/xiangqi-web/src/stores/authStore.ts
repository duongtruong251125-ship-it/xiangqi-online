import { create } from 'zustand'
import { apiErrorMessage, authApi, type User } from '../services/authApi'
import { registerAccessTokenHandler, registerSessionExpiredHandler, setAccessToken } from '../services/apiClient'

export type AuthStatus = 'initializing' | 'authenticated' | 'unauthenticated'

type AuthState = {
  user: User | null
  accessToken: string | null
  status: AuthStatus
  initialize: () => Promise<void>
  login: (identifier: string, password: string, rememberMe: boolean) => Promise<void>
  logout: () => Promise<void>
  logoutAll: () => Promise<void>
  updateUser: (displayName: string) => Promise<void>
  changePassword: (currentPassword: string, newPassword: string, confirmNewPassword: string) => Promise<void>
  clearSession: () => void
}

let initializeFlight: Promise<void> | null = null

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: null,
  status: 'initializing',
  initialize: () => {
    if (!initializeFlight) {
      set({ status: 'initializing' })
      initializeFlight = (async () => {
        try {
          const session = await authApi.refresh()
          setAccessToken(session.accessToken)
          const user = await authApi.me()
          set({ user, accessToken: session.accessToken, status: 'authenticated' })
        } catch {
          get().clearSession()
        } finally {
          initializeFlight = null
        }
      })()
    }
    return initializeFlight
  },
  login: async (identifier, password, rememberMe) => {
    const session = await authApi.login({ identifier, password, rememberMe })
    setAccessToken(session.accessToken)
    set({ user: session.user, accessToken: session.accessToken, status: 'authenticated' })
  },
  logout: async () => {
    await authApi.logout()
    get().clearSession()
  },
  logoutAll: async () => {
    await authApi.logoutAll()
    get().clearSession()
  },
  updateUser: async (displayName) => {
    const user = await authApi.updateProfile(displayName)
    set({ user })
  },
  changePassword: async (currentPassword, newPassword, confirmNewPassword) => {
    await authApi.changePassword(currentPassword, newPassword, confirmNewPassword)
    get().clearSession()
  },
  clearSession: () => {
    setAccessToken(null)
    set({ user: null, accessToken: null, status: 'unauthenticated' })
  },
}))

registerSessionExpiredHandler(() => useAuthStore.getState().clearSession())
registerAccessTokenHandler(value => useAuthStore.setState({ accessToken: value }))

export { apiErrorMessage }
