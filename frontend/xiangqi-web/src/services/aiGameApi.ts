import { apiClient } from './apiClient'

export type AiDifficulty = 'Easy' | 'Medium' | 'Hard'
export type AiSide = 'Red' | 'Black'
export type AiGame = {
  gameId: string; difficulty: AiDifficulty; humanSide: AiSide; aiSide: AiSide; fen: string
  currentTurn: AiSide; status: string; aiTurnStatus: string; version: number; winnerSide: AiSide | null
  resultReason: string | null; endedAtUtc: string | null; error: string | null; isCurrentTurnInCheck: boolean
}
export type AiMove = { ply: number; fromSquare: string; toSquare: string; piece: string; capturedPiece: string | null; isAiMove: boolean; createdAtUtc: string }
export type AiHistory = { game: AiGame; moves: AiMove[] }
export type AiAnalysis = { gameId: string; status: string; completedDepth: number; elapsedMs: number; nodesSearched: number; version: number }
type Envelope<T> = { success: boolean; data: T; requestId: string }

async function data<T>(request: Promise<{ data: Envelope<T> }>): Promise<T> { return (await request).data.data }

export const aiGameApi = {
  create: (difficulty: AiDifficulty, humanSide: AiSide) => data(aiClient.post<Envelope<AiGame>>('/ai-games', { difficulty, humanSide })),
  get: (id: string) => data(aiClient.get<Envelope<AiGame>>(`/ai-games/${id}`)),
  history: (id: string) => data(aiClient.get<Envelope<AiHistory>>(`/ai-games/${id}/moves`)),
  analysis: (id: string) => data(aiClient.get<Envelope<AiAnalysis>>(`/ai-games/${id}/analysis-status`)),
  move: (id: string, fromFile: number, fromRank: number, toFile: number, toRank: number, expectedVersion: number) => {
    const clientMoveId = crypto.randomUUID()
    return data(aiClient.post<Envelope<unknown>>(`/ai-games/${id}/moves`, { fromFile, fromRank, toFile, toRank, expectedVersion, clientMoveId }, { headers: { 'Idempotency-Key': clientMoveId } }))
  },
  resign: (id: string, expectedVersion: number) => data(aiClient.post<Envelope<AiGame>>(`/ai-games/${id}/resign`, { expectedVersion })),
  restart: (id: string, expectedVersion: number) => data(aiClient.post<Envelope<AiGame>>(`/ai-games/${id}/restart`, { expectedVersion })),
}

// Keep a dedicated name for the AI API surface while sharing the authenticated client.
const aiClient = apiClient
