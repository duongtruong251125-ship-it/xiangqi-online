import { apiClient } from './apiClient'

type ApiEnvelope<T> = { success: boolean; data: T }

export type RoomMember = {
  userId: string
  username: string
  displayName: string
  side: 'Red' | 'Black' | null
  isReady: boolean
  joinedAt: string
}

export type Room = {
  id: string
  code: string
  name: string
  ownerId: string
  isPrivate: boolean
  status: 'Waiting' | 'InGame' | 'Closed'
  maxPlayers: number
  version: number
  gameId: string | null
  members: RoomMember[]
}

export type PagedRooms = {
  items: Room[]
  page: number
  pageSize: number
  totalCount: number
}

export type RoomInvitation = {
  id: string
  roomId: string
  roomCode: string
  roomName: string
  inviterUserId: string
  inviterUsername: string
  expiresAt: string
}

export type PlayerStats = {
  played: number
  wins: number
  losses: number
}

export type GameSnapshot = {
  id: string
  roomId: string | null
  mode: 'Local' | 'Online'
  status: 'InProgress' | 'Finished'
  currentFen: string
  currentTurn: 'Red' | 'Black'
  playerSide: 'Red' | 'Black' | 'Local'
  version: number
  ply: number
  isInCheck: boolean
  winnerId: string | null
  resultReason: string | null
  redRemainingMs?: number
  blackRemainingMs?: number
}

export type GameMoveItem = {
  ply: number
  fromFile: number
  fromRank: number
  toFile: number
  toRank: number
  fromSquare: string
  toSquare: string
  piece: string
  capturedPiece?: string | null
}

export type LegalTarget = {
  file: number
  rank: number
}

export const roomAndGameApi = {
  stats: async () => (await apiClient.get<ApiEnvelope<PlayerStats>>('/users/me/stats')).data.data,
  listRooms: async (search = '', status = 'Waiting', page = 1) =>
    (await apiClient.get<ApiEnvelope<PagedRooms>>('/rooms', { params: { search, status, page } })).data.data,
  getRoom: async (id: string) => (await apiClient.get<ApiEnvelope<Room>>(`/rooms/${encodeURIComponent(id)}`)).data.data,
  createRoom: async (name: string, isPrivate: boolean) =>
    (await apiClient.post<ApiEnvelope<Room>>('/rooms', { name, isPrivate })).data.data,
  joinByCode: async (code: string) =>
    (await apiClient.post<ApiEnvelope<Room>>('/rooms/join', { code })).data.data,
  joinPublic: async (roomId: string) =>
    (await apiClient.post<ApiEnvelope<Room>>('/rooms/join-public', { roomId })).data.data,
  chooseSide: async (roomId: string, side: 'Red' | 'Black', expectedVersion: number) =>
    (await apiClient.post<ApiEnvelope<Room>>(`/rooms/${encodeURIComponent(roomId)}/side`, { side, expectedVersion })).data.data,
  setReady: async (roomId: string, isReady: boolean, expectedVersion: number) =>
    (await apiClient.post<ApiEnvelope<Room>>(`/rooms/${encodeURIComponent(roomId)}/ready`, { isReady, expectedVersion })).data.data,
  startRoom: async (roomId: string, expectedVersion: number) =>
    (await apiClient.post<ApiEnvelope<Room>>(`/rooms/${encodeURIComponent(roomId)}/start`, { expectedVersion })).data.data,
  leaveRoom: async (roomId: string) =>
    (await apiClient.post<ApiEnvelope<{ left: boolean }>>(`/rooms/${encodeURIComponent(roomId)}/leave`, {})).data.data,
  invitePlayer: async (roomId: string, username: string) =>
    (await apiClient.post<ApiEnvelope<RoomInvitation>>(`/rooms/${encodeURIComponent(roomId)}/invitations`, { username })).data.data,
  listInvitations: async () =>
    (await apiClient.get<ApiEnvelope<RoomInvitation[]>>('/invitations')).data.data,
  respondInvitation: async (invitationId: string, accept: boolean) =>
    (await apiClient.post<ApiEnvelope<{ roomId?: string }>>(`/invitations/${encodeURIComponent(invitationId)}/${accept ? 'accept' : 'decline'}`, {})).data.data,
  createLocalGame: async () =>
    (await apiClient.post<ApiEnvelope<GameSnapshot>>('/games/local', {})).data.data,
  getGame: async (gameId: string) =>
    (await apiClient.get<ApiEnvelope<GameSnapshot>>(`/games/${encodeURIComponent(gameId)}`)).data.data,
  getMoves: async (gameId: string) =>
    (await apiClient.get<ApiEnvelope<GameMoveItem[]>>(`/games/${encodeURIComponent(gameId)}/moves`)).data.data,
  getLegalMoves: async (gameId: string, fromFile: number, fromRank: number) =>
    (await apiClient.get<ApiEnvelope<LegalTarget[]>>(`/games/${encodeURIComponent(gameId)}/legal-moves`, { params: { fromFile, fromRank } })).data.data,
  moveLocal: async (gameId: string, fromFile: number, fromRank: number, toFile: number, toRank: number, expectedVersion: number) =>
    (await apiClient.post<ApiEnvelope<GameSnapshot>>('/games/local/move', {
      gameId, fromFile, fromRank, toFile, toRank, expectedVersion, clientMoveId: crypto.randomUUID(),
    })).data.data,
  restartGame: async (gameId: string, expectedVersion: number) =>
    (await apiClient.post<ApiEnvelope<GameSnapshot>>(`/games/${encodeURIComponent(gameId)}/restart`, { expectedVersion })).data.data,
}
