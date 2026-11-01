export type Side = 'red' | 'black'
export type Kind = 'general' | 'advisor' | 'elephant' | 'horse' | 'chariot' | 'cannon' | 'soldier'
export type Piece = { kind: Kind; side: Side }
export type Square = { file: number; rank: number }
export type Board = { squares: (Piece | null)[][]; turn: Side }

export const pieceNames: Record<Kind, { red: string; black: string; character: { red: string; black: string } }> = {
  general: { red: 'Tướng đỏ', black: 'Tướng đen', character: { red: '帥', black: '將' } },
  advisor: { red: 'Sĩ đỏ', black: 'Sĩ đen', character: { red: '仕', black: '士' } },
  elephant: { red: 'Tượng đỏ', black: 'Tượng đen', character: { red: '相', black: '象' } },
  horse: { red: 'Mã đỏ', black: 'Mã đen', character: { red: '傌', black: '馬' } },
  chariot: { red: 'Xe đỏ', black: 'Xe đen', character: { red: '俥', black: '車' } },
  cannon: { red: 'Pháo đỏ', black: 'Pháo đen', character: { red: '炮', black: '砲' } },
  soldier: { red: 'Tốt đỏ', black: 'Tốt đen', character: { red: '兵', black: '卒' } },
}

export class BoardState {
  static initial(): Board {
    const squares: (Piece | null)[][] = Array.from({ length: 10 }, () => Array.from({ length: 9 }, () => null))
    const back: Kind[] = ['chariot', 'horse', 'elephant', 'advisor', 'general', 'advisor', 'elephant', 'horse', 'chariot']
    back.forEach((kind, file) => { squares[0][file] = { kind, side: 'black' }; squares[9][file] = { kind, side: 'red' } })
    for (const file of [1, 7]) { squares[2][file] = { kind: 'cannon', side: 'black' }; squares[7][file] = { kind: 'cannon', side: 'red' } }
    for (const file of [0, 2, 4, 6, 8]) { squares[3][file] = { kind: 'soldier', side: 'black' }; squares[6][file] = { kind: 'soldier', side: 'red' } }
    return { squares, turn: 'red' }
  }
}
