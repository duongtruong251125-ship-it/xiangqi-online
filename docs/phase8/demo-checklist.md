# Phase 8 demo checklist

Use two browsers/devices for online and call demonstrations. Fill the actual result after running; do not mark manual scenarios complete based on API tests.

| # | Scenario | Expected result | Actual result | Status |
|---|---|---|---|---|
| 1 | Register, log in, refresh, and open the lobby | Session persists and protected pages redirect when signed out | Automated auth and MVC route tests; browser flow not run | See JSON / browser skipped |
| 2 | Create/join a room, ready both users, start a game | Both clients see the same room/game state | SignalR/API integration covered; two-browser demo not run | Browser skipped |
| 3 | Make legal and illegal Xiangqi moves | Server accepts legal moves and rejects invalid moves; turns/clocks update | Engine and backend integration suites | Verify JSON |
| 4 | Create AI game for Red and Black, make move, reload, resign/restart | Human side, AI opening, history, persisted game and controls work | AI API integration plus MVC route/resource checks; UI interaction not run | Browser skipped |
| 5 | Chat, disconnect/reconnect, and call between clients | Messages/signaling recover; camera/microphone media connects | SignalR/signaling APIs are integration-tested; real media not tested | Manual required |
| 6 | Build and run full automated suite | Release build and .NET/frontend checks pass | Recorded by `scripts/verify-phase8.ps1` | See `TestResults/phase8-verification.json` |

## Manual media evidence to capture

Record two clients, browser permission outcomes, ICE candidate pair state, and a brief audio/video exchange. If peers are on restrictive networks, supply a working TURN service and verify its configuration without exposing credentials.
