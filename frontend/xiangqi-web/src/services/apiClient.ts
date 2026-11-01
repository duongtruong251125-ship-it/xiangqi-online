import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

let accessToken: string | null = null
let refreshFlight: Promise<string | null> | null = null
let onSessionExpired: (() => void) | null = null
let onAccessTokenUpdated: ((value: string | null) => void) | null = null

export function setAccessToken(value: string | null) { accessToken = value; onAccessTokenUpdated?.(value) }
export function registerSessionExpiredHandler(handler: () => void) { onSessionExpired = handler }
export function registerAccessTokenHandler(handler: (value: string | null) => void) { onAccessTokenUpdated = handler }

type RetriableConfig = InternalAxiosRequestConfig & { _authRetried?: boolean }
type ApiEnvelope<T> = { success: boolean; data: T; requestId: string }

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 10_000,
  withCredentials: true,
  headers: { Accept: 'application/json' },
})

apiClient.interceptors.request.use((config) => {
  config.headers.set('X-Request-Id', crypto.randomUUID())
  if (accessToken) config.headers.set('Authorization', `Bearer ${accessToken}`)
  return config
})

apiClient.interceptors.response.use((response) => response, async (error: AxiosError) => {
  const config = error.config as RetriableConfig | undefined
  if (error.response?.status !== 401 || !config || config._authRetried || isAuthEndpoint(config.url)) return Promise.reject(error)

  const mayRetry = ['get', 'head', 'options'].includes((config.method || 'get').toLowerCase())
    || Boolean(config.headers.get('Idempotency-Key'))
  config._authRetried = true
  try {
    const token = await refreshAccessToken()
    if (token && mayRetry) {
      config.headers.set('Authorization', `Bearer ${token}`)
      return await apiClient.request(config)
    }
    if (!token) onSessionExpired?.()
  } catch {
    setAccessToken(null)
    onSessionExpired?.()
  }
  return Promise.reject(error)
})

function isAuthEndpoint(url?: string) {
  return ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout', '/auth/logout-all'].some(path => url?.includes(path))
}

function refreshAccessToken(): Promise<string | null> {
  if (!refreshFlight) {
    refreshFlight = apiClient.post<ApiEnvelope<{ accessToken: string }>>('/auth/refresh')
      .then(response => {
        const token = response.data.data.accessToken
        setAccessToken(token)
        return token
      })
      .catch(() => {
        setAccessToken(null)
        return null
      })
      .finally(() => { refreshFlight = null })
  }
  return refreshFlight
}
