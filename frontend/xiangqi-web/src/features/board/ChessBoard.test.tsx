import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { ChessBoard } from './ChessBoard'
import { BoardState } from './BoardState'
import { useBoardPreferences } from '../../stores/boardPreferences'

describe('ChessBoard preview', () => {
  afterEach(() => cleanup())
  beforeEach(() => useBoardPreferences.setState({ flipped: false }))

  it('puts Black at the top and Red at the bottom', () => {
    render(<ChessBoard board={BoardState.initial()} label="Test board" />)
    expect(screen.getByRole('button', { name: /Tướng đen e10/ })).toHaveStyle({ top: '5%' })
    expect(screen.getByRole('button', { name: /Tướng đỏ e1/ })).toHaveStyle({ top: '95%' })
  })

  it('can flip the board orientation without moving a piece', () => {
    render(<ChessBoard board={BoardState.initial()} label="Test board" />)
    fireEvent.click(screen.getByRole('button', { name: 'Đảo chiều bàn cờ' }))
    expect(screen.getByRole('button', { name: /Tướng đen e10/ })).toHaveStyle({ top: '95%' })
    expect(screen.getByRole('button', { name: /Tướng đỏ e1/ })).toHaveStyle({ top: '5%' })
  })
})
