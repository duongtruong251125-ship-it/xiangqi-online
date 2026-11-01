import { describe, expect, it } from 'vitest'
import { BoardState } from './BoardState'

describe('Xiangqi initial board', () => {
  it('places 32 pieces with 16 on each side', () => {
    const board = BoardState.initial()
    const pieces = board.squares.flat().filter(Boolean)
    expect(pieces).toHaveLength(32)
    expect(pieces.filter((piece) => piece?.side === 'red')).toHaveLength(16)
    expect(pieces.filter((piece) => piece?.side === 'black')).toHaveLength(16)
  })
  it('places generals, cannons, and soldiers on their starting points', () => {
    const board = BoardState.initial()
    expect(board.squares[0][4]).toMatchObject({ kind: 'general', side: 'black' })
    expect(board.squares[9][4]).toMatchObject({ kind: 'general', side: 'red' })
    expect(board.squares[2][1]).toMatchObject({ kind: 'cannon', side: 'black' })
    expect(board.squares[7][7]).toMatchObject({ kind: 'cannon', side: 'red' })
    expect(board.squares[3][0]).toMatchObject({ kind: 'soldier', side: 'black' })
    expect(board.squares[6][8]).toMatchObject({ kind: 'soldier', side: 'red' })
  })
})
