import { useEffect, type ReactNode } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuthStore } from '../stores/authStore'
import { safeReturnPath } from '../services/safePath'

export function AuthInitializer({ children }: { children: ReactNode }) {
  const initialize = useAuthStore(state => state.initialize)
  useEffect(() => { void initialize() }, [initialize])
  return children
}

export function ProtectedRoute() {
  const { status } = useAuthStore()
  const location = useLocation()
  if (status === 'initializing') return <AuthLoading />
  if (status !== 'authenticated') {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}${location.hash}` }} />
  }
  return <Outlet />
}

export function PublicAuthRoute() {
  const status = useAuthStore(state => state.status)
  const location = useLocation()
  if (status === 'initializing') return <AuthLoading />
  if (status === 'authenticated') return <Navigate to={safeReturnPath(location.state?.from) ?? '/lobby'} replace />
  return <Outlet />
}

function AuthLoading() {
  return <main className="auth-loading" aria-busy="true"><span className="brand-mark">象</span><p>Đang khôi phục phiên đăng nhập…</p></main>
}
