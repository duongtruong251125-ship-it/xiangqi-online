import { RotateCcw } from 'lucide-react'

type Props = { onFlip: () => void }
export function BoardToolbar({ onFlip }: Props) {
  return <div className="board-toolbar"><span>ĐỎ ĐI TRƯỚC</span><button type="button" onClick={onFlip} aria-label="Đảo chiều bàn cờ"><RotateCcw size={13}/>Đảo chiều</button></div>
}
