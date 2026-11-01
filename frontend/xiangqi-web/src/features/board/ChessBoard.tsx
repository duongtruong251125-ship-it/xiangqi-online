import { useState } from 'react'
import type { Board, Square } from './BoardState'
import { pieceNames } from './BoardState'
import { useBoardOrientation } from '../../hooks/useBoardOrientation'
import { BoardToolbar } from '../../components/BoardToolbar'
import './ChessBoard.css'

type Props = {
  board: Board
  flipped?: boolean
  label: string
  onSquareClick?: (file: number, rank: number) => void
  interactive?: boolean
  lastMove?: { from: Square; to: Square }
  legalTargets?: Square[]
}

export function ChessBoard({ board, flipped: flippedProp, label, onSquareClick, interactive = false, lastMove, legalTargets = [] }: Props) {
  const [selected, setSelected] = useState<Square | null>(null)
  const { flipped, toggle } = useBoardOrientation(flippedProp)
  const files = Array.from({ length: 9 }, (_, column) => flipped ? 8 - column : column)
  const ranks = Array.from({ length: 10 }, (_, row) => flipped ? 9 - row : row)
  function select(file: number, rank: number) {
    if (board.squares[rank][file]) setSelected({ file, rank })
    if (interactive) onSquareClick?.(file, rank)
  }

  return <div className="board-frame" role="group" aria-label={label}><BoardToolbar onFlip={toggle}/>
    <div className="board-surface">
      <svg className="board-lines" viewBox="0 0 900 1000" aria-hidden="true">
        <g transform="translate(50 50)">
          <rect x="-12" y="-12" width="824" height="924" fill="none" stroke="currentColor" strokeWidth="7"/>
          <rect x="0" y="0" width="800" height="900" fill="none" stroke="currentColor" strokeWidth="3.5"/>
          <line x1="0" y1="0" x2="0" y2="900" stroke="currentColor" strokeWidth="2.5"/>
          <line x1="800" y1="0" x2="800" y2="900" stroke="currentColor" strokeWidth="2.5"/>
          {[1, 2, 3, 4, 5, 6, 7].map((x) => <g key={`v${x}`}><line x1={x * 100} y1="0" x2={x * 100} y2="400" stroke="currentColor" strokeWidth="2.2"/><line x1={x * 100} y1="500" x2={x * 100} y2="900" stroke="currentColor" strokeWidth="2.2"/></g>)}
          {Array.from({ length: 10 }, (_, y) => <line key={`h${y}`} x1="0" y1={y * 100} x2="800" y2={y * 100} stroke="currentColor" strokeWidth="2.2"/>)}
          <line x1="300" y1="0" x2="500" y2="200" stroke="currentColor" strokeWidth="2.2"/><line x1="500" y1="0" x2="300" y2="200" stroke="currentColor" strokeWidth="2.2"/>
          <line x1="300" y1="700" x2="500" y2="900" stroke="currentColor" strokeWidth="2.2"/><line x1="500" y1="700" x2="300" y2="900" stroke="currentColor" strokeWidth="2.2"/>
          {[1, 7].map((x) => <g key={`mark-${x}`} stroke="currentColor" strokeWidth="3" fill="none"><path d={`M${x * 100 - 20} 186h12v-12 M${x * 100 + 20} 186h-12v-12 M${x * 100 - 20} 214h12v12 M${x * 100 + 20} 214h-12v12 M${x * 100 - 20} 686h12v-12 M${x * 100 + 20} 686h-12v-12 M${x * 100 - 20} 714h12v12 M${x * 100 + 20} 714h-12v12`}/></g>)}
          {[2, 4, 6].map((x) => <g key={`pmark-${x}`} stroke="currentColor" strokeWidth="2.8" fill="none"><path d={`M${x * 100 - 18} 286h10v-10 M${x * 100 + 18} 286h-10v-10 M${x * 100 - 18} 314h10v10 M${x * 100 + 18} 314h-10v10 M${x * 100 - 18} 586h10v-10 M${x * 100 + 18} 586h-10v-10 M${x * 100 - 18} 614h10v10 M${x * 100 + 18} 614h-10v10`}/></g>)}
          <g stroke="currentColor" strokeWidth="2.8" fill="none"><path d="M8 286h10v-10 M8 314h10v10 M792 286h-10v-10 M792 314h-10v10 M8 586h10v-10 M8 614h10v10 M792 586h-10v-10 M792 614h-10v10"/></g>
        </g>
      </svg>
      <div className="river-label" aria-hidden="true"><span>楚河</span><span>漢界</span></div>
      <div className="board-pieces">{ranks.flatMap((rank, row) => files.map((file, column) => {
        const piece = board.squares[rank][file]
        const last = lastMove && ((lastMove.from.file === file && lastMove.from.rank === rank) || (lastMove.to.file === file && lastMove.to.rank === rank))
        const isLegal = legalTargets.some(target => target.file === file && target.rank === rank)
        if (!piece && !interactive && !last && !isLegal) return null
        if (!piece) return <button key={`${file}-${rank}`} type="button" disabled={!interactive} className={`board-square-target ${last ? 'last-move' : ''} ${isLegal ? 'legal-target' : ''}`} style={{ left: `${((50 + column * 100) / 900) * 100}%`, top: `${((50 + row * 100) / 1000) * 100}%` }} aria-label={`Ô ${String.fromCharCode(97 + file)}${10 - rank}`} onClick={() => select(file, rank)}/>
        const active = selected?.file === file && selected.rank === rank
        const name = pieceNames[piece.kind]
        const coordinate = `${String.fromCharCode(97 + file)}${10 - rank}`
        return <button key={`${file}-${rank}`} type="button" disabled={interactive && !onSquareClick} className={`piece ${piece.side} ${active ? 'selected' : ''} ${last ? 'last-move' : ''} ${isLegal ? 'legal-capture' : ''}`} style={{ left: `${((50 + column * 100) / 900) * 100}%`, top: `${((50 + row * 100) / 1000) * 100}%` }} aria-label={`${name[piece.side]} ${coordinate}${active ? ', đã chọn' : ''}`} aria-pressed={active} onClick={() => select(file, rank)} title={`${name[piece.side]} · ${coordinate}`}><span>{name.character[piece.side]}</span></button>
      }))}</div>
    </div>
    <p className="board-access-note">{interactive ? 'Chọn quân của bạn rồi chọn điểm đến. Máy chủ sẽ kiểm tra luật trước khi lưu nước đi.' : 'Chọn quân để làm nổi bật.'}</p>
  </div>
}
