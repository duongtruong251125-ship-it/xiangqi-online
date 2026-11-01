# Xiangqi GameEngine foundation

## Board coordinates

- 9 files (`0–8`), 10 ranks (`0–9`); rank 0 is Black's back rank, rank 9 is Red's.
- Red moves first. Board storage order is `rank * 9 + file`.
- A position outside those ranges is rejected. Initial board has 32 pieces, 16 per side.
- GameEngine is a .NET class library with no database or UI dependency. It provides board setup, JSON serialization, FEN, and a server-side `IXiangqiRules` implementation. `TryApplyMove` returns a `MoveResult` with an error code rather than mutating the original board. It validates all seven piece types, palace/river limits, horse legs, elephant eyes, cannon screens, check, facing generals, checkmate and stalemate.

## FEN

Piece letters follow common Xiangqi FEN notation: `k` general, `a` advisor, `b` elephant, `n` horse, `r` chariot, `c` cannon, `p` soldier. Lowercase is Black; uppercase is Red. Ranks are separated by `/`, digits encode consecutive empty files, and active color is `w` (Red) or `b` (Black). Output includes compatible placeholder fields: `- - 0 1`.

FEN preserves board placement and side to move. Repetition history, clocks, draw counters and other adjudication metadata require separate persisted state; FEN alone does not represent them. Parser requires exactly ten ranks of nine files each and exactly one general per color.

`FenSerializer` exposes `Serialize`, `Deserialize`, and `Validate`. Phase 3 currently treats a side with no legal moves as losing, including stalemate. Repetition, perpetual-check/chase adjudication, move-limit rules and tournament-specific draw rules are not implemented; do not treat this engine as enforcing every competition rule.

## Initial formation

Back ranks for both sides: Chariot, Horse, Elephant, Advisor, General, Advisor, Elephant, Horse, Chariot. Cannons occupy files 1 and 7 on ranks 2/7. Soldiers occupy files 0, 2, 4, 6, 8 on ranks 3/6.

Vue renders the current FEN and asks the authenticated API for legal destinations. The API verifies membership, side to move, game version, and the move using `IXiangqiRules` before persisting `GameMoves` and updating the current FEN. No legal-move rule is trusted from the browser.

For local play, the authenticated owner controls alternating Red/Black turns on the same interface. The API stores each accepted move in `GameMoves`, returns the updated state, and rejects duplicate or stale-version requests. Restart creates a fresh local game and retains the previous game and its history.
