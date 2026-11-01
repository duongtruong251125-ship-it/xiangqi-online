/** Contract boundary only. Phase 1 intentionally does not create a Hub connection. */
export type RealtimeAdapterStatus = 'not-configured'
export const signalRAdapter = {
  endpoint: '/hubs/game',
  status: 'not-configured' as RealtimeAdapterStatus,
  connect: async (): Promise<never> => { throw new Error('Realtime is not available in Phase 1.') },
}
