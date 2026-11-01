import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { KeyRound, LogOut, Shield, UserRound, Users } from 'lucide-react'
import { Shell } from './Shell'
import { apiErrorMessage } from '../services/authApi'
import { useAuthStore } from '../stores/authStore'

export default function ProfilePage() {
  const { user, updateUser, changePassword, logout, logoutAll } = useAuthStore()
  const navigate = useNavigate()
  const [editing, setEditing] = useState(false)
  const [changingPassword, setChangingPassword] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  async function update(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const displayName = new FormData(event.currentTarget).get('displayName')?.toString() ?? ''
    setBusy(true); setError(''); setMessage('')
    try { await updateUser(displayName); setEditing(false); setMessage('Đã cập nhật tên hiển thị.') }
    catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }

  async function password(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const current = form.get('currentPassword')?.toString() ?? ''
    const next = form.get('newPassword')?.toString() ?? ''
    const confirm = form.get('confirmNewPassword')?.toString() ?? ''
    if (next !== confirm) { setError('Mật khẩu mới nhập lại chưa khớp.'); return }
    setBusy(true); setError(''); setMessage('')
    try {
      await changePassword(current, next, confirm)
      navigate('/login', { replace: true, state: { notice: 'Đổi mật khẩu thành công. Hãy đăng nhập lại.' } })
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }

  async function signOut(all: boolean) {
    setBusy(true); setError('')
    try { if (all) await logoutAll(); else await logout(); navigate('/', { replace: true }) }
    catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }

  if (!user) return null
  return <Shell><section className="page-wrap profile-page">
    <p className="kicker">TÀI KHOẢN · HỒ SƠ</p><h1 className="profile-title">Hồ sơ kỳ thủ.</h1>
    <div className="profile-layout"><section className="profile-card">
      <div className="profile-identity"><span className="profile-avatar">{user.displayName.slice(0, 1).toUpperCase() || <UserRound size={24}/>}</span><div><h2>{user.displayName}</h2><p>@{user.username}</p></div></div>
      <dl className="profile-details"><div><dt>Tên hiển thị</dt><dd>{user.displayName}</dd></div><div><dt>Tên đăng nhập</dt><dd>{user.username}</dd></div><div><dt>Email</dt><dd>{user.email}</dd></div><div><dt>Ngày tham gia</dt><dd>{new Intl.DateTimeFormat('vi-VN', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(user.createdAt))}</dd></div></dl>
      {!editing ? <button className="button button-outline" onClick={() => { setEditing(true); setMessage(''); setError('') }}>Chỉnh sửa tên hiển thị</button> : <form className="profile-form" onSubmit={update}><label>Tên hiển thị<input name="displayName" defaultValue={user.displayName} minLength={2} maxLength={80} required/></label><div className="profile-form-actions"><button className="button button-gold" type="submit" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu thay đổi'}</button><button className="button button-outline" type="button" onClick={() => setEditing(false)}>Hủy</button></div></form>}
    </section><aside className="profile-card profile-security"><p className="kicker"><Shield size={14}/> BẢO MẬT</p><h2>Phiên đăng nhập</h2><p>Refresh token được giữ trong cookie HttpOnly và tự xoay vòng khi phiên được làm mới.</p>
      {!changingPassword ? <button className="profile-action" onClick={() => { setChangingPassword(true); setError(''); setMessage('') }}><KeyRound size={16}/>Đổi mật khẩu</button> : <form className="profile-form" onSubmit={password}><label>Mật khẩu hiện tại<input name="currentPassword" type="password" autoComplete="current-password" required/></label><label>Mật khẩu mới<input name="newPassword" type="password" autoComplete="new-password" minLength={10} maxLength={256} required/></label><label>Nhập lại mật khẩu mới<input name="confirmNewPassword" type="password" autoComplete="new-password" minLength={10} maxLength={256} required/></label><p className="password-hint">Ít nhất 10 ký tự; tránh mật khẩu phổ biến.</p><div className="profile-form-actions"><button className="button button-gold" type="submit" disabled={busy}>{busy ? 'Đang cập nhật…' : 'Đổi mật khẩu'}</button><button className="button button-outline" type="button" onClick={() => setChangingPassword(false)}>Hủy</button></div></form>}
      <button className="profile-action" disabled={busy} onClick={() => void signOut(false)}><LogOut size={16}/>Đăng xuất</button><button className="profile-action danger-action" disabled={busy} onClick={() => void signOut(true)}><Users size={16}/>Đăng xuất tất cả thiết bị</button><div className="profile-empty-stat"><Users size={18}/><span>Thống kê thắng/thua chưa khả dụng.</span></div>
    </aside></div>
    {message && <p className="form-message" role="status">{message}</p>}{error && <p className="form-error" role="alert">{error}</p>}
  </section></Shell>
}
