import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { BrowserRouter, Link, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ArrowUpRight, AudioLines, CircleHelp, Clock3, Copy, DoorOpen, Eye, EyeOff, Flag, LockKeyhole, MessageCircle, Play, Plus, RotateCcw, Shield, Sparkles, UserPlus, Users, Video } from 'lucide-react'
import { BoardState, type Board, type Kind, type Square } from '../features/board/BoardState'
import { ChessBoard } from '../features/board/ChessBoard'
import { AuthInitializer, ProtectedRoute, PublicAuthRoute } from '../components/AuthRoutes'
import { apiErrorMessage, authApi } from '../services/authApi'
import { roomAndGameApi, type GameMoveItem, type GameSnapshot, type LegalTarget, type PlayerStats, type Room, type RoomInvitation } from '../services/roomAndGameApi'
import { safeReturnPath } from '../services/safePath'
import { useAuthStore } from '../stores/authStore'
import ProfilePage from './ProfilePage'
import AiGamePage from './AiGamePage'
import { Shell } from './Shell'
import '../styles.css'

const fenKinds: Record<string, Kind> = { k: 'general', a: 'advisor', b: 'elephant', n: 'horse', r: 'chariot', c: 'cannon', p: 'soldier' }
function boardFromFen(fen: string): Board {
  const [placement, turn] = fen.split(' ')
  const squares: Board['squares'] = Array.from({ length: 10 }, () => Array.from({ length: 9 }, () => null))
  placement.split('/').forEach((line, rank) => {
    let file = 0
    for (const symbol of line) {
      if (/\d/.test(symbol)) { file += Number(symbol); continue }
      const kind = fenKinds[symbol.toLowerCase()]
      if (kind && rank < 10 && file < 9) squares[rank][file++] = { kind, side: symbol === symbol.toUpperCase() ? 'red' : 'black' }
    }
  })
  return { squares, turn: turn === 'b' ? 'black' : 'red' }
}

function HomePage() {
  return <Shell>
    <section className="hero">
      <div className="hero-copy">
        <p className="kicker"><i/>KỲ ĐÀI HOÀNG GIA · HAI BÊN TRÍ TUỆ</p>
        <h1>Giữ vững<br/><em>thế cờ.</em></h1>
        <p className="hero-description">Cờ Tướng trực tuyến được kiến tạo trên nền tảng bàn cờ 3D khắc chìm chuẩn luật máy chủ, phòng đấu thời gian thực, đàm thoại Video 1–1 và Kỳ Sư AI Alpha-Beta.</p>
        <div className="hero-actions">
          <Link className="button button-gold" to="/lobby"><Play size={16} fill="currentColor"/>Khám phá sảnh chơi</Link>
          <Link className="button button-outline" to="/ai"><Sparkles size={15}/>Đấu với Máy</Link>
          <Link className="text-link" to="/register">Tạo tài khoản <ArrowUpRight size={15}/></Link>
        </div>
        <div className="hero-note"><span className="status-dot"/>Hệ thống Kỳ Đài trực tuyến · Chuẩn luật 9×10 <b/>Sẵn sàng đối kháng</div>
      </div>
      <div className="hero-board-wrap">
        <div className="board-caption"><span>THẾ CỜ BAN ĐẦU · 32 QUÂN KHẮC CHÌM</span><span>局 · 01</span></div>
        <ChessBoard board={BoardState.initial()} label="Bàn cờ Tướng khai cuộc"/>
        <p className="board-note"><span>楚河</span>　Sông chia đôi chiến tuyến　<span>漢界</span></p>
      </div>
      <span className="vertical-mark">THINK BEFORE YOU MOVE</span>
    </section>
    <section className="principles" id="cach-choi">
      <div className="section-heading"><span className="kicker">TRÊN BÀN CỜ</span><h2>Truyền thống, với một<br/>nơi chốn mới.</h2></div>
      <div className="principle-list">
        <article><span className="principle-icon"><Shield size={19}/></span><div><h3>Luật cờ được giữ ở máy chủ</h3><p>Mọi nước đi, bắt quân, chiếu tướng và đồng hồ thi đấu đều được kiểm định chặt chẽ tại máy chủ.</p></div><span className="principle-state">HOẠT ĐỘNG</span></article>
        <article><span className="principle-icon"><Users size={19}/></span><div><h3>Gặp nhau tại Kỳ Đài</h3><p>Mở bàn đấu công khai hoặc riêng tư, gửi lời mời bằng hữu và trò chuyện Video 1–1 trực tiếp.</p></div><span className="principle-state">TRỰC TUYẾN</span></article>
        <article><span className="principle-icon"><Clock3 size={19}/></span><div><h3>Luyện cờ cùng Đại Sư AI</h3><p>Đối đầu thuật toán Minimax cắt tỉa Alpha-Beta kết hợp Quiescence Search ở 3 cấp độ.</p></div><span className="principle-state">ALPHA-BETA</span></article>
      </div>
    </section>
  </Shell>
}

function AuthPage({ register = false }: { register?: boolean }) {
  const [visible, setVisible] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [rememberMe, setRememberMe] = useState(true)
  const login = useAuthStore(state => state.login)
  const status = useAuthStore(state => state.status)
  const navigate = useNavigate()
  const location = useLocation()
  const notice = (location.state as { notice?: string } | null)?.notice

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(''); setLoading(true)
    const form = new FormData(event.currentTarget)
    try {
      if (register) {
        const username = String(form.get('username') ?? '')
        const email = String(form.get('email') ?? '')
        const password = String(form.get('password') ?? '')
        const confirmPassword = String(form.get('confirmPassword') ?? '')
        if (password !== confirmPassword) { setError('Mật khẩu nhập lại chưa khớp.'); return }
        await authApi.register({ username, email, password, confirmPassword })
        navigate('/login', { replace: true, state: { notice: 'Tạo tài khoản thành công. Bạn có thể đăng nhập.' } })
      } else {
        const identifier = String(form.get('identifier') ?? '')
        const password = String(form.get('password') ?? '')
        await login(identifier, password, rememberMe)
        const target = safeReturnPath((location.state as { from?: unknown } | null)?.from) ?? '/lobby'
        navigate(target, { replace: true })
      }
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setLoading(false) }
  }

  return <Shell><section className="auth-page"><div className="auth-aside"><span className="brand-mark large">象</span><h1>{register ? <>Mời bạn<br/>ngồi vào bàn.</> : <>Trở lại<br/>Kỳ Đài.</>}</h1><p>{register ? 'Tạo tài khoản để sẵn sàng cho những ván đấu kịch tính tại Kỳ Đài.' : 'Đăng nhập để tiếp tục hành trình của bạn tại Kỳ Đài Hoàng Gia.'}</p><span className="auth-aside-line"/></div><div className="auth-panel"><p className="kicker">{register ? 'TÀI KHOẢN MỚI' : 'CHÀO MỪNG TRỞ LẠI'}</p><h2>{register ? 'Tạo tài khoản' : 'Đăng nhập'}</h2><p className="form-intro">{register ? 'Thông tin tài khoản được lưu an toàn trên máy chủ.' : 'Dùng tên đăng nhập hoặc email của bạn.'}</p>
    {notice && <p role="status" className="form-message">{notice}</p>}
    <form onSubmit={submit}>
      {register ? <><label>Tên đăng nhập<input name="username" autoComplete="username" placeholder="Tên đăng nhập" minLength={3} maxLength={64} required/></label><label>Email<input name="email" type="email" autoComplete="email" placeholder="email@example.com" maxLength={254} required/></label></> : <label>Tên đăng nhập hoặc email<input name="identifier" autoComplete="username" placeholder="tenban hoặc email@example.com" required/></label>}
      <label>{register ? 'Mật khẩu' : 'Mật khẩu'}<span className="password-field"><input name="password" type={visible ? 'text' : 'password'} autoComplete={register ? 'new-password' : 'current-password'} placeholder={register ? 'Ít nhất 10 ký tự' : 'Mật khẩu'} minLength={register ? 10 : 1} maxLength={256} required/><button type="button" className="icon-button" aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} onClick={() => setVisible(!visible)}>{visible ? <EyeOff size={17}/> : <Eye size={17}/>}</button></span></label>
      {register ? <><p className="password-hint">Ít nhất 10 ký tự; không dùng mật khẩu phổ biến.</p><label>Nhập lại mật khẩu<input name="confirmPassword" type={visible ? 'text' : 'password'} autoComplete="new-password" minLength={10} maxLength={256} required/></label></> : <label className="remember-row"><input type="checkbox" checked={rememberMe} onChange={event => setRememberMe(event.currentTarget.checked)}/>Duy trì phiên đăng nhập trong 7 ngày</label>}
      <button className="button button-gold full-width" type="submit" disabled={loading || status === 'initializing'}><LockKeyhole size={16}/>{loading ? 'Đang xử lý…' : register ? 'Đăng ký' : 'Đăng nhập'}</button>
      {error && <p role="alert" className="form-error">{error}</p>}
    </form><p className="switch-auth">{register ? 'Đã có tài khoản?' : 'Chưa có tài khoản?'} <Link to={register ? '/login' : '/register'}>{register ? 'Đăng nhập' : 'Đăng ký'}</Link></p></div></section></Shell>
}

function LobbyPage() {
  const navigate = useNavigate()
  const [rooms, setRooms] = useState<Room[]>([])
  const [invitations, setInvitations] = useState<RoomInvitation[]>([])
  const [stats, setStats] = useState<PlayerStats | null>(null)
  const [roomName, setRoomName] = useState('Kỳ đài cuối tuần')
  const [isPrivate, setIsPrivate] = useState(false)
  const [showCreate, setShowCreate] = useState(false)
  const [joinCode, setJoinCode] = useState('')
  const [statusFilter, setStatusFilter] = useState('Waiting')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const loadLobby = useCallback(async (nextStatus = statusFilter) => {
    try {
      const [roomPage, inviteList, userStats] = await Promise.all([
        roomAndGameApi.listRooms('', nextStatus, 1),
        roomAndGameApi.listInvitations(),
        roomAndGameApi.stats(),
      ])
      setRooms(roomPage.items)
      setInvitations(inviteList)
      setStats(userStats)
    } catch {
      /* Safe fallback in test or offline mode */
    }
  }, [statusFilter])

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadLobby() }, 0)
    return () => window.clearTimeout(timer)
  }, [loadLobby])

  async function handleCreateRoom(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true); setError('')
    try {
      const created = await roomAndGameApi.createRoom(roomName, isPrivate)
      navigate(`/rooms/${created.id}`)
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }

  async function handleJoinByCode() {
    if (joinCode.trim().length !== 6) return
    setBusy(true); setError('')
    try {
      const joined = await roomAndGameApi.joinByCode(joinCode.trim().toUpperCase())
      navigate(`/rooms/${joined.id}`)
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }

  async function handleLocalGame() {
    setBusy(true); setError('')
    try {
      const game = await roomAndGameApi.createLocalGame()
      navigate(`/games/${game.id}`)
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }

  return <Shell>
    <section className="page-wrap">
      <div className="page-title-row">
        <div>
          <p className="kicker">KỲ ĐÀI · SẢNH CHƠI</p>
          <h1>Chọn một bàn đấu.</h1>
          <p>Khởi tạo bàn đấu trực tuyến, vào nhanh bằng mã phòng 6 ký tự hoặc so tài cùng Kỳ Sư AI.</p>
        </div>
        <div className="lobby-quick-actions">
          <button className="button button-gold" onClick={() => setShowCreate(!showCreate)}><Plus size={17}/>Tạo phòng</button>
          <button className="button button-outline" disabled={busy} onClick={() => void handleLocalGame()}><Play size={16}/>Đấu 2 người tại máy</button>
          <Link className="button button-outline" to="/ai"><Sparkles size={16}/>Đấu với Máy</Link>
        </div>
      </div>

      {showCreate && (
        <form className="imperial-create-drawer" onSubmit={handleCreateRoom}>
          <label>Tên bàn đấu<input value={roomName} onChange={e => setRoomName(e.currentTarget.value)} minLength={3} maxLength={80} required/></label>
          <label className="remember-row"><input type="checkbox" checked={isPrivate} onChange={e => setIsPrivate(e.currentTarget.checked)}/>Phòng riêng tư (chỉ vào bằng mã 6 ký tự / lời mời)</label>
          <button className="button button-gold" type="submit" disabled={busy}>{busy ? 'Đang tạo…' : 'Mở bàn ngay'}</button>
        </form>
      )}

      {stats && (
        <div className="imperial-stat-strip">
          <div><strong>{stats.wins}</strong><span>Ván thắng</span></div>
          <div><strong>{stats.losses}</strong><span>Ván thua</span></div>
          <div><strong>{stats.played}</strong><span>Tổng số trận</span></div>
          <div><strong>{invitations.length}</strong><span>Lời mời đang chờ</span></div>
        </div>
      )}

      <div className="lobby-controls">
        <div className="segmented">
          <button className={statusFilter === 'Waiting' ? 'active' : ''} onClick={() => { setStatusFilter('Waiting'); void loadLobby('Waiting') }}>Tất cả phòng chờ</button>
          <button className={statusFilter === 'InGame' ? 'active' : ''} onClick={() => { setStatusFilter('InGame'); void loadLobby('InGame') }}>Đang thi đấu</button>
        </div>
        <div className="join-code">
          <input aria-label="Mã phòng" value={joinCode} onChange={e => setJoinCode(e.currentTarget.value)} maxLength={6} placeholder="Nhập mã phòng (6 ký tự)"/>
          <button className="button button-outline" disabled={busy || joinCode.trim().length !== 6} onClick={() => void handleJoinByCode()}>Vào phòng</button>
        </div>
      </div>

      {error && <p role="alert" className="form-error">{error}</p>}

      {invitations.length > 0 && (
        <div className="imperial-invite-strip">
          <p className="kicker">LỜI MỜI VÀO BÀN ({invitations.length})</p>
          <div className="imperial-room-grid">
            {invitations.map(inv => (
              <article key={inv.id} className="imperial-room-card">
                <div>
                  <strong>{inv.inviterUsername} mời bạn</strong>
                  <p>{inv.roomName} · Mã #{inv.roomCode}</p>
                </div>
                <div className="profile-form-actions">
                  <button className="button button-gold" disabled={busy} onClick={async () => {
                    const res = await roomAndGameApi.respondInvitation(inv.id, true)
                    if (res.roomId) navigate(`/rooms/${res.roomId}`)
                  }}>Chấp nhận</button>
                  <button className="button button-outline" disabled={busy} onClick={async () => {
                    await roomAndGameApi.respondInvitation(inv.id, false)
                    void loadLobby()
                  }}>Từ chối</button>
                </div>
              </article>
            ))}
          </div>
        </div>
      )}

      {rooms.length > 0 ? (
        <div className="imperial-room-grid">
          {rooms.map(room => {
            const red = room.members.find(m => m.side === 'Red')
            const black = room.members.find(m => m.side === 'Black')
            return (
              <article key={room.id} className="imperial-room-card">
                <div className="imperial-room-head">
                  <strong>{room.name}</strong>
                  <span className="phase-tag">#{room.code}</span>
                </div>
                <div className="imperial-seat-row">
                  <span className="imperial-seat red">帥 Đỏ: {red?.displayName ?? 'Ghế trống'}</span>
                  <span className="imperial-seat black">將 Đen: {black?.displayName ?? 'Ghế trống'}</span>
                </div>
                <button className="button button-gold full-width" disabled={busy || room.status !== 'Waiting'} onClick={async () => {
                  try {
                    setBusy(true)
                    const joined = await roomAndGameApi.joinPublic(room.id)
                    navigate(`/rooms/${joined.id}`)
                  } catch (cause) { setError(apiErrorMessage(cause)) }
                  finally { setBusy(false) }
                }}>{room.status === 'Waiting' ? 'Ngồi vào bàn' : 'Đang đấu'}</button>
              </article>
            )
          })}
        </div>
      ) : (
        <div className="empty-state">
          <div className="empty-board"><span>象</span></div>
          <p className="kicker">CHƯA CÓ PHÒNG ĐẤU</p>
          <h2>Sảnh đang chờ nước đi đầu tiên.</h2>
          <p>Hãy nhấn <strong>Tạo phòng</strong> để mở bàn đấu mới hoặc chọn <strong>Đấu với Máy</strong> để luyện cờ ngay.</p>
          <Link className="button button-outline" to="/ai"><CircleHelp size={16}/>Luyện cờ với AI</Link>
        </div>
      )}
    </section>
  </Shell>
}

function RoomPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const user = useAuthStore(state => state.user)
  const [room, setRoom] = useState<Room | null>(null)
  const [inviteUsername, setInviteUsername] = useState('')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const loadRoom = useCallback(async () => {
    if (!id) return
    try {
      const data = await roomAndGameApi.getRoom(id)
      setRoom(data)
      if (data.gameId) navigate(`/games/${data.gameId}`)
    } catch (cause) { setError(apiErrorMessage(cause)) }
  }, [id, navigate])

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadRoom() }, 0)
    const poll = window.setInterval(() => { void loadRoom() }, 2500)
    return () => { window.clearTimeout(timer); window.clearInterval(poll) }
  }, [loadRoom])

  const redMember = room?.members.find(m => m.side === 'Red')
  const blackMember = room?.members.find(m => m.side === 'Black')
  const myMember = room?.members.find(m => m.userId === user?.id)
  const isOwner = room?.ownerId === user?.id
  const canStart = Boolean(room && room.members.length === 2 && room.members.every(m => m.side && m.isReady))

  return <Shell>
    <section className="page-wrap">
      <Link className="back-link" to="/lobby">← Trở về sảnh</Link>
      <div className="page-title-row">
        <div>
          <p className="kicker">PHÒNG CHỜ · ĐÀI ĐỐI KHÁNG</p>
          <h1>{room?.name ?? 'Chỗ ngồi bên bàn.'}</h1>
          <p>Chọn bên cầm quân Đỏ hoặc Đen và nhấn Sẵn sàng để chủ phòng khai cuộc.</p>
        </div>
        <span className="phase-tag">{room ? `MÃ PHÒNG: ${room.code}` : 'ĐANG TẢI'}</span>
      </div>

      <div className="room-layout">
        <div className="room-main">
          <div className="player-slots">
            <div className="player-slot red-slot">
              <span className="side-stamp red-stamp">帥</span>
              <div>
                <strong>Đỏ · {redMember?.displayName ?? 'Đang chờ kỳ thủ'}</strong>
                <p>{redMember ? (redMember.isReady ? '✓ Đã sẵn sàng' : 'Đang chuẩn bị') : 'Ghế trống'}</p>
                {room && !redMember && <button className="button button-outline" style={{ marginTop: 8, minHeight: 32 }} disabled={busy} onClick={async () => {
                  setRoom(await roomAndGameApi.chooseSide(room.id, 'Red', room.version))
                }}>Chọn Đỏ</button>}
              </div>
              <span className="waiting-dot"/>
            </div>
            <span className="versus">VS</span>
            <div className="player-slot">
              <span className="side-stamp">將</span>
              <div>
                <strong>Đen · {blackMember?.displayName ?? 'Chỗ trống'}</strong>
                <p>{blackMember ? (blackMember.isReady ? '✓ Đã sẵn sàng' : 'Đang chuẩn bị') : 'Ghế trống'}</p>
                {room && !blackMember && <button className="button button-outline" style={{ marginTop: 8, minHeight: 32 }} disabled={busy} onClick={async () => {
                  setRoom(await roomAndGameApi.chooseSide(room.id, 'Black', room.version))
                }}>Chọn Đen</button>}
              </div>
              <span className="waiting-dot"/>
            </div>
          </div>
          <div className="board-placeholder">
            <ChessBoard board={BoardState.initial()} label="Bàn cờ xem trước"/>
            <p>Bàn cờ chuẩn 9×10 · Khai cuộc ngay khi 2 kỳ thủ sẵn sàng</p>
          </div>
        </div>

        <aside className="room-tools">
          <div className="tool-heading"><h2>Thông tin bàn đấu</h2><span className="phase-tag">{room?.status ?? 'WAITING'}</span></div>
          <div className="room-detail"><span>Mã phòng</span><strong>{room?.code ?? '---'}</strong></div>
          <div className="room-detail"><span>Chế độ</span><strong>Cờ Tướng tiêu chuẩn</strong></div>
          {room && (
            <div style={{ display: 'grid', gap: 10, marginTop: 16 }}>
              <button className="button button-gold full-width" disabled={busy || !myMember?.side} onClick={async () => {
                setBusy(true)
                try { setRoom(await roomAndGameApi.setReady(room.id, !myMember?.isReady, room.version)) }
                catch (cause) { setError(apiErrorMessage(cause)) }
                finally { setBusy(false) }
              }}><Users size={16}/>{myMember?.isReady ? 'Hủy sẵn sàng' : 'Sẵn sàng thi đấu'}</button>
              {isOwner && (
                <button className="button button-gold full-width" disabled={busy || !canStart} onClick={async () => {
                  setBusy(true)
                  try {
                    const started = await roomAndGameApi.startRoom(room.id, room.version)
                    if (started.gameId) navigate(`/games/${started.gameId}`)
                  } catch (cause) { setError(apiErrorMessage(cause)) }
                  finally { setBusy(false) }
                }}><Play size={16}/>Bắt đầu ván đấu</button>
              )}
              <button className="button button-outline full-width" onClick={() => {
                void navigator.clipboard?.writeText(room.code)
                setNotice(`Đã sao chép mã phòng ${room.code}`)
              }}><Copy size={15}/>Sao chép mã</button>
              <button className="button button-outline full-width" disabled={busy} onClick={async () => {
                await roomAndGameApi.leaveRoom(room.id)
                navigate('/lobby')
              }}><DoorOpen size={15}/>Rời phòng</button>
            </div>
          )}

          {room && (
            <form style={{ marginTop: 18, display: 'grid', gap: 8 }} onSubmit={async e => {
              e.preventDefault()
              if (!inviteUsername.trim()) return
              try {
                await roomAndGameApi.invitePlayer(room.id, inviteUsername.trim())
                setNotice(`Đã gửi lời mời tới ${inviteUsername}`)
                setInviteUsername('')
              } catch (cause) { setError(apiErrorMessage(cause)) }
            }}>
              <label style={{ fontSize: 12, color: 'var(--text-sand)' }}>Mời bằng hữu theo username</label>
              <div className="join-code" style={{ width: '100%' }}>
                <input style={{ flex: 1 }} value={inviteUsername} onChange={e => setInviteUsername(e.currentTarget.value)} placeholder="Nhập username..."/>
                <button className="button button-outline" type="submit"><UserPlus size={15}/></button>
              </div>
            </form>
          )}
          {notice && <p className="form-message">{notice}</p>}
          {error && <p className="form-error">{error}</p>}
          <div className="tool-tabs"><span className="selected"><MessageCircle size={15}/>Trò chuyện</span><span><Video size={15}/>Video 1–1</span></div>
          <div className="tool-empty">Khung đàm đạo và gọi Video 1–1 trực tiếp sẽ mở ngay khi bước vào ván đấu.</div>
        </aside>
      </div>
    </section>
  </Shell>
}

function GamePage() {
  const { id } = useParams()
  const [game, setGame] = useState<GameSnapshot | null>(null)
  const [moves, setMoves] = useState<GameMoveItem[]>([])
  const [selected, setSelected] = useState<Square | null>(null)
  const [legalTargets, setLegalTargets] = useState<LegalTarget[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const loadGame = useCallback(async () => {
    if (!id) return
    try {
      const [snap, history] = await Promise.all([roomAndGameApi.getGame(id), roomAndGameApi.getMoves(id)])
      setGame(snap)
      setMoves(history)
    } catch {
      /* Preview fallback if demo ID */
    }
  }, [id])

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadGame() }, 0)
    return () => window.clearTimeout(timer)
  }, [loadGame])

  const board = useMemo(() => game?.currentFen ? boardFromFen(game.currentFen) : BoardState.initial(), [game])
  const latest = moves.length ? moves[moves.length - 1] : null
  const lastMove = latest ? { from: { file: latest.fromFile, rank: latest.fromRank }, to: { file: latest.toFile, rank: latest.toRank } } : undefined

  async function onSquareClick(file: number, rank: number) {
    if (!game || !id || game.status !== 'InProgress' || busy) return
    const target = legalTargets.some(t => t.file === file && t.rank === rank)
    if (selected && target) {
      setBusy(true); setError('')
      try {
        const updated = await roomAndGameApi.moveLocal(id, selected.file, selected.rank, file, rank, game.version)
        setGame(updated)
        setMoves(await roomAndGameApi.getMoves(id))
        setSelected(null); setLegalTargets([])
      } catch (cause) { setError(apiErrorMessage(cause)) }
      finally { setBusy(false) }
      return
    }
    const piece = board.squares[rank][file]
    const activeSide = game.currentTurn === 'Red' ? 'red' : 'black'
    if (piece && piece.side === activeSide) {
      setSelected({ file, rank })
      try { setLegalTargets(await roomAndGameApi.getLegalMoves(id, file, rank)) }
      catch { setLegalTargets([]) }
    } else {
      setSelected(null); setLegalTargets([])
    }
  }

  return <Shell>
    <section className="page-wrap">
      <Link className="back-link" to="/lobby">← Trở về sảnh</Link>
      <div className="game-top">
        <div>
          <p className="kicker">KỲ ĐÀI · {game?.mode === 'Local' ? 'ĐẤU HAI NGƯỜI TẠI MÁY' : 'ĐÀI THI ĐẤU TRỰC TUYẾN'}</p>
          <h1>Đối diện thế cờ.</h1>
        </div>
        <span className="turn-indicator"><span className="waiting-dot"/>{game ? `Lượt ${game.currentTurn === 'Red' ? 'Đỏ (帥)' : 'Đen (將)'} · Nước ${game.ply}` : 'Bàn cờ sẵn sàng'}</span>
      </div>
      {game?.isInCheck && <p className="form-error" style={{ textAlign: 'center', fontWeight: 700 }}>將軍 · CHIẾU TƯỚNG! Phe {game.currentTurn === 'Red' ? 'Đỏ' : 'Đen'} phải thoát chiếu.</p>}
      <div className="game-layout">
        <div className="game-board-area">
          <div className="game-player black-player">
            <span className="side-stamp">將</span>
            <div><strong>Phe Đen (Đi hậu)</strong><small>{game?.currentTurn === 'Black' ? 'Đang đến lượt đi' : 'Chờ lượt'}</small></div>
            <span className="game-clock">10:00</span>
          </div>
          <ChessBoard
            board={board}
            label="Bàn cờ xem trước ván đấu"
            interactive={Boolean(game && game.status === 'InProgress')}
            onSquareClick={(f, r) => void onSquareClick(f, r)}
            lastMove={lastMove}
            legalTargets={legalTargets}
          />
          <div className="game-player red-player">
            <span className="side-stamp red-stamp">帥</span>
            <div><strong>Phe Đỏ (Đi tiên)</strong><small>{game?.currentTurn === 'Red' ? 'Đang đến lượt đi' : 'Chờ lượt'}</small></div>
            <span className="game-clock">10:00</span>
          </div>
        </div>
        <aside className="game-sidebar">
          <div className="history-heading"><h2>Biên bản nước đi</h2><span>{moves.length} nước</span></div>
          {moves.length === 0 ? (
            <div className="history-empty">
              <RotateCcw size={21}/>
              <p>Chưa có nước đi.</p>
              <small>Chọn một quân cờ trên bàn để hiển thị các điểm đến hợp lệ.</small>
            </div>
          ) : (
            <ol className="ai-history" style={{ maxHeight: 260, overflow: 'auto', margin: '12px 0' }}>
              {moves.map(m => <li key={m.ply}><span>#{m.ply}</span><strong>{m.piece}</strong><code>{m.fromSquare} → {m.toSquare}</code></li>)}
            </ol>
          )}
          <div className="game-actions">
            <button disabled={!game}><Flag size={16}/>Xin hòa</button>
            <button disabled={!game}><DoorOpen size={16}/>Đầu hàng</button>
            <button disabled={!game || busy} onClick={async () => {
              if (!game || !id) return
              const restarted = await roomAndGameApi.restartGame(id, game.version)
              setGame(restarted); setMoves([]); setLegalTargets([])
            }}><RotateCcw size={16}/>Chơi lại</button>
          </div>
          {error && <p role="alert" className="form-error">{error}</p>}
          <div className="video-note"><AudioLines size={16}/><span>Kết nối đồng bộ máy chủ &amp; bảo toàn biên bản ván đấu.</span></div>
        </aside>
      </div>
    </section>
  </Shell>
}

function NotFoundPage() {
  return <Shell><section className="not-found"><span className="brand-mark large">象</span><p className="kicker">THẾ CỜ KHÔNG TỒN TẠI</p><h1>404</h1><p>Đường dẫn này chưa có trên Kỳ Đài.</p><Link className="button button-gold" to="/">Về trang chủ <ArrowUpRight size={15}/></Link></section></Shell>
}

export default function App() {
  return <BrowserRouter><AuthInitializer><Routes><Route path="/" element={<HomePage/>}/><Route element={<PublicAuthRoute/>}><Route path="/login" element={<AuthPage/>}/><Route path="/register" element={<AuthPage register/>}/></Route><Route element={<ProtectedRoute/>}><Route path="/profile" element={<ProfilePage/>}/><Route path="/lobby" element={<LobbyPage/>}/><Route path="/rooms/:id" element={<RoomPage/>}/><Route path="/games/:id" element={<GamePage/>}/><Route path="/ai" element={<AiGamePage/>}/><Route path="/ai/:id" element={<AiGamePage/>}/></Route><Route path="*" element={<NotFoundPage/>}/></Routes></AuthInitializer></BrowserRouter>
}
