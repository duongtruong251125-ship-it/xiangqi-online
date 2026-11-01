import { create } from 'zustand'

type BoardPreferences = { flipped: boolean; toggleOrientation: () => void }
export const useBoardPreferences = create<BoardPreferences>((set) => ({ flipped: false, toggleOrientation: () => set((state) => ({ flipped: !state.flipped })) }))
