import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Brain, Flag, RotateCcw, Sparkles } from 'lucide-react'
import { BoardState, type Board, type Kind, type Square } from '../features/board/BoardState'
import { ChessBoard } from '../features/board/ChessBoard'
import { apiErrorMessage } from '../services/authApi'
import { aiGameApi, type AiAnalysis, type AiDifficulty, type AiGame, type AiMove, type AiSide } from '../services/aiGameApi'
import { Shell } from './Shell'
import '../styles.css'
import './ai-game.css'

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
function sideName(side: AiSide) { return side === 'Red' ? 'Đỏ' : 'Đen' }
function parseServerSquare(square: string): Square { return { file: square.charCodeAt(0) - 97, rank: Number(square.slice(1)) } }
function displaySquare(square: string) { return `${square[0]}${10 - Number(square.slice(1))}` }

function NewAiGame() {
  const [difficulty, setDifficulty] = useState<AiDifficulty>('Medium')
  const [humanSide, setHumanSide] = useState<AiSide>('Red')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const navigate = useNavigate()
  async function start() {
    setBusy(true); setError('')
    try { const game = await aiGameApi.create(difficulty, humanSide); navigate(`/ai/${game.gameId}`) }
    catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }
  return <Shell><section className="page-wrap ai-create"><Link className="back-link" to="/lobby">← Trở về sảnh</Link><p className="kicker">KỲ ĐÀI · ĐỐI ĐẦU VỚI MÁY</p><h1>Chọn thế trận.</h1><p className="ai-intro">Minimax và Alpha-Beta tìm nước đi hợp lệ trên máy chủ. Bạn có thể tải lại trang để tiếp tục ván đã lưu.</p>
    <div className="ai-settings"><fieldset><legend>Độ khó</legend>{([['Easy', 'Dễ', 'Tìm kiếm nhanh, có biến thiên nhẹ'], ['Medium', 'Vừa', 'Cân bằng độ sâu và thời gian'], ['Hard', 'Khó', 'Tìm sâu hơn trong giới hạn 5 giây']] as const).map(([key, label, note]) => <label key={key} className={`ai-option ${difficulty === key ? 'selected' : ''}`}><input type="radio" name="difficulty" value={key} checked={difficulty === key} onChange={() => setDifficulty(key)}/><span><strong>{label}</strong><small>{note}</small></span></label>)}</fieldset>
      <fieldset><legend>Chọn bên</legend><div className="ai-side-options">{(['Red', 'Black'] as AiSide[]).map(side => <button key={side} type="button" className={`ai-side ${humanSide === side ? 'selected' : ''}`} onClick={() => setHumanSide(side)}><span className={`side-stamp ${side === 'Red' ? 'red-stamp' : ''}`}>{side === 'Red' ? '帥' : '將'}</span><strong>{sideName(side)}</strong></button>)}</div>{humanSide === 'Black' && <p className="ai-hint">Máy sẽ thực hiện nước khai cuộc Đỏ sau khi tạo ván.</p>}</fieldset>
    </div><button className="button button-gold ai-start" disabled={busy} onClick={() => void start()}><Sparkles size={16}/>{busy ? 'Đang tạo ván…' : 'Bắt đầu ván đấu'}</button>{error && <p role="alert" className="form-error">{error}</p>}
  </section></Shell>
}

function ActiveAiGame({ id }: { id: string }) {
  const [game, setGame] = useState<AiGame | null>(null)
  const [moves, setMoves] = useState<AiMove[]>([])
  const [analysis, setAnalysis] = useState<AiAnalysis | null>(null)
  const [selected, setSelected] = useState<Square | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const inFlight = useRef(false)
  const navigate = useNavigate()
  const load = useCallback(async () => {
    if (inFlight.current) return
    inFlight.current = true
    try {
      const [next, history, status] = await Promise.all([aiGameApi.get(id), aiGameApi.history(id), aiGameApi.analysis(id)])
      setGame(next); setMoves(history.moves); setAnalysis(status); setError('')
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { inFlight.current = false }
  }, [id])
  useEffect(() => { const timer = window.setTimeout(() => { void load() }, 0); return () => window.clearTimeout(timer) }, [load])
  const shouldPoll = Boolean(game && game.status === 'InProgress' && ['Queued', 'Thinking'].includes(game.aiTurnStatus))
  useEffect(() => {
    if (!shouldPoll) return
    const timer = window.setInterval(() => { void load() }, 1100)
    return () => window.clearInterval(timer)
  }, [shouldPoll, load])

  const board = useMemo(() => game ? boardFromFen(game.fen) : BoardState.initial(), [game])
  const latestMove = moves.length ? moves[moves.length - 1] : null
  const lastMove = latestMove ? { from: parseServerSquare(latestMove.fromSquare), to: parseServerSquare(latestMove.toSquare) } : undefined
  const canMove = Boolean(game && game.status === 'InProgress' && game.currentTurn === game.humanSide && game.aiTurnStatus === 'WaitingForHuman' && !busy)
  async function clickSquare(file: number, rank: number) {
    if (!canMove || !game) return
    const piece = board.squares[rank][file]
    if (!selected) { if (piece?.side === (game.humanSide === 'Red' ? 'red' : 'black')) setSelected({ file, rank }); return }
    if (piece?.side === (game.humanSide === 'Red' ? 'red' : 'black')) { setSelected({ file, rank }); return }
    setBusy(true); setError('')
    try { await aiGameApi.move(id, selected.file, selected.rank, file, rank, game.version); setSelected(null); await load() }
    catch (cause) { setError(apiErrorMessage(cause)); await load() }
    finally { setBusy(false) }
  }
  async function resign() {
    if (!game || game.status !== 'InProgress') return
    setBusy(true); setError('')
    try { await aiGameApi.resign(id, game.version); await load() }
    catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setBusy(false) }
  }
  async function restart() {
    if (!game) return
    setBusy(true); setError('')
    try { const next = await aiGameApi.restart(id, game.version); navigate(`/ai/${next.gameId}`, { replace: true }) }
    catch (cause) { setError(apiErrorMessage(cause)); await load() }
    finally { setBusy(false) }
  }
  if (!game && !error) return <Shell><section className="page-wrap ai-loading">Đang tải ván đấu đã lưu…</section></Shell>
  if (!game) return <Shell><section className="page-wrap"><p role="alert" className="form-error">{error}</p><Link className="button button-outline" to="/ai">Tạo ván mới</Link></section></Shell>
  const thinking = game.status === 'InProgress' && ['Queued', 'Thinking'].includes(game.aiTurnStatus)
  const finished = game.status !== 'InProgress'
  const turnText = finished ? game.winnerSide ? `${sideName(game.winnerSide)} thắng` : 'Ván đấu kết thúc' : thinking ? 'Máy đang suy nghĩ…' : `Đến lượt ${sideName(game.currentTurn)}`
  const checkText = !finished && game.isCurrentTurnInCheck ? `Chiếu Tướng! ${sideName(game.currentTurn)} phải thoát chiếu.` : !finished && game.aiTurnStatus === 'WaitingForHuman' ? 'Đến lượt bạn. Máy chủ sẽ xác thực nước đi theo luật.' : null
  return <Shell><section className="page-wrap ai-play"><Link className="back-link" to="/ai">← Tạo ván mới</Link><div className="game-top"><div><p className="kicker">ĐỐI ĐẦU VỚI MÁY · {game.difficulty === 'Easy' ? 'DỄ' : game.difficulty === 'Hard' ? 'KHÓ' : 'VỪA'}</p><h1>Đối diện thế cờ.</h1></div><span className={`turn-indicator ${thinking ? 'ai-thinking' : ''}`} aria-live="polite">{thinking && <Brain size={14}/>}<span className="waiting-dot"/>{turnText}</span></div>
    <div className="ai-game-layout"><div className="ai-board-panel"><div className={`game-player black-player ${game.aiSide === 'Black' ? 'ai-player' : 'human-player'}`}><span className="side-stamp">將</span><div><strong>Đen {game.aiSide === 'Black' ? '· Máy' : game.humanSide === 'Black' ? '· Bạn' : ''}</strong><small>{game.currentTurn === 'Black' && !finished ? 'Đang đi' : 'Sẵn sàng'}</small></div></div><ChessBoard board={board} label="Bàn cờ đấu với AI" interactive={canMove} onSquareClick={clickSquare} lastMove={lastMove}/><div className={`game-player red-player ${game.aiSide === 'Red' ? 'ai-player' : 'human-player'}`}><span className="side-stamp red-stamp">帥</span><div><strong>Đỏ {game.aiSide === 'Red' ? '· Máy' : game.humanSide === 'Red' ? '· Bạn' : ''}</strong><small>{game.currentTurn === 'Red' && !finished ? 'Đang đi' : 'Sẵn sàng'}</small></div></div><p className={`ai-turn-note ${game.isCurrentTurnInCheck ? 'in-check' : ''}`}>{thinking ? 'Đang phân tích nước đi. Bạn có thể tải lại trang; tiến trình được lưu trên máy chủ.' : checkText}</p></div>
      <aside className="ai-sidebar"><div className="ai-status-card"><span className="kicker">TRẠNG THÁI VÁN</span><strong>{turnText}</strong><small>{analysis?.completedDepth ? `Độ sâu hoàn tất ${analysis.completedDepth} · ${analysis.nodesSearched.toLocaleString('vi-VN')} nút · ${analysis.elapsedMs} ms` : `Độ khó ${game.difficulty === 'Easy' ? 'Dễ' : game.difficulty === 'Hard' ? 'Khó' : 'Vừa'}`}</small>{game.error && <p className="ai-error">{game.error}</p>}</div><div className="history-heading"><h2>Lịch sử nước đi</h2><span>{moves.length} nước</span></div><ol className="ai-history">{moves.map(move => <li key={`${move.ply}-${move.fromSquare}`} className={move.isAiMove ? 'ai-move' : ''}><span>{move.ply}</span><strong>{move.isAiMove ? 'Máy' : 'Bạn'}</strong><code>{displaySquare(move.fromSquare)} → {displaySquare(move.toSquare)}</code></li>)}{moves.length === 0 && <li className="ai-history-empty">Chưa có nước đi. {thinking ? 'Máy đang khai cuộc.' : 'Hãy chọn một quân để bắt đầu.'}</li>}</ol><div className="ai-game-actions"><button className="button button-outline" disabled={busy || finished} onClick={() => void resign()}><Flag size={15}/>Đầu hàng</button><button className="button button-gold" disabled={busy} onClick={() => void restart()}><RotateCcw size={15}/>Chơi lại</button></div>{error && <p role="alert" className="form-error">{error}</p>}</aside></div>
  </section></Shell>
}

export default function AiGamePage() { const { id } = useParams(); return id ? <ActiveAiGame id={id}/> : <NewAiGame/> }
