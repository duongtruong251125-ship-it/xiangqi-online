import type { ReactNode } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { ArrowUpRight, ChevronDown, LogOut } from 'lucide-react'
import { useAuthStore } from '../stores/authStore'

function Header() {
  const { user, status, logout } = useAuthStore()
  const navigate = useNavigate()
  async function signOut() {
    try { await logout(); navigate('/', { replace: true }) } catch { /* Keep the signed-in state if the server could not revoke the session. */ }
  }
  return <header className="site-header"><Link className="brand" to="/"><span className="brand-mark">象</span><span>Kỳ Đài<small>XIANGQI ONLINE</small></span></Link><nav aria-label="Điều hướng chính"><NavLink to="/lobby">Phòng chơi</NavLink>{user && <NavLink to="/ai">Đấu với máy</NavLink>}<a href="/#cach-choi">Cách chơi</a>{user && <NavLink className="profile-link" to="/profile"><span className="avatar">{user.displayName.slice(0, 1).toUpperCase()}</span>{user.displayName}<ChevronDown size={15}/></NavLink>}</nav>{user ? <button className="header-cta" onClick={() => void signOut()}><LogOut size={15}/>Đăng xuất</button> : <Link className="header-cta" to="/login">{status === 'initializing' ? 'Tài khoản' : 'Đăng nhập'} <ArrowUpRight size={16}/></Link>}</header>
}

function Footer() { return <footer className="site-footer"><span>象 Kỳ Đài <small>Gìn giữ một cuộc đấu trí lâu đời.</small></span><span>Phase 2 · Xác thực và hồ sơ người dùng</span></footer> }

export function Shell({ children }: { children: ReactNode }) { return <><Header/><main>{children}</main><Footer/></> }
