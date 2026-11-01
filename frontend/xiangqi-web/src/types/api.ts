export type ApiError = { type?: string; title: string; status: number; detail?: string; requestId?: string; code?: string }
export type HealthResponse = { status: 'Healthy' | 'Unhealthy' | 'Degraded'; checks: { name: string; status: string }[] }
export type SignalREnvelope<TType extends string, TPayload> = { roomId?: string; gameId?: string; sequence: number; correlationId: string; eventType: TType; payload: TPayload }
export type SubmitMoveRequest = { gameId: string; clientMoveId: string; fromSquare: string; toSquare: string; expectedGameVersion: number }
export type MoveApplied = { gameId: string; clientMoveId: string; fromSquare: string; toSquare: string; fen: string; gameVersion: number; sequence: number }
export type MoveRejected = { gameId: string; clientMoveId: string; errorCode: string; message: string; currentGameVersion: number }
