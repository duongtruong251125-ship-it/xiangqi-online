import { useBoardPreferences } from '../stores/boardPreferences'

export function useBoardOrientation(override?: boolean) {
  const current = useBoardPreferences((state) => state.flipped)
  const toggle = useBoardPreferences((state) => state.toggleOrientation)
  return { flipped: override ?? current, toggle }
}
