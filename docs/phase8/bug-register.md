# Phase 8 bug register

Audit date: 2026-11-01. Scope: phases 1–7, based on the checked-out solution, Jira CSV export, current test suite, and attached phase prompts. Status values distinguish fixed code findings from verification limitations.

| ID | Severity | Finding | Evidence / impact | Action | Status |
|---|---|---|---|---|---|
| P8-01 | High | AI play was exposed in the React/Vite client but not in the retained MVC/Razor/Vue host. | API and AI engine existed; MVC had no AI routes or page, leaving that app path without Phase 7 access. | Added `/AiGame/Index`, `/AiGame/Play/{id}`, Vue AI lobby/game screens, API calls, move polling, resign/restart, and route/resource integration assertions. | Fixed; rerun Phase 8 verification |
| P8-02 | Medium | Shared MVC layout referenced `wwwroot/css/phase7.css`, but the asset was absent. | Direct asset lookup would return 404; AI view styling was unavailable. | Added responsive Phase 7 styles and an integration assertion for the asset. | Fixed; rerun Phase 8 verification |
| P8-03 | Verification gap | Real two-browser media negotiation and audio/video exchange are not covered by current automated tests. | Integration coverage verifies signaling/session APIs and hub workflows, not camera permission, ICE connectivity, or media tracks. | Marked manual media verification as not executed; requires two clients and suitable STUN/TURN/network conditions. | Open verification item |
| P8-04 | Verification gap | Browser end-to-end coverage is not configured. | Existing suites exercise APIs, engine, and static MVC routes; they do not run a real browser interaction flow. | Added route/resource integration checks; browser interaction remains an explicit skipped gate. | Open verification item |
| P8-05 | Process gap | Jira export lists all scoped epics and stories as `To Do`. | `Jira (1).csv`, exported rows SS-303–SS-356. A local export does not confirm or update live Jira. | Use the matrix as implementation evidence; no Jira status edits were made. | Open project tracking item |
| P8-06 | High | Vite was served at `127.0.0.1:5173`, but the development origin allowlist only included `localhost:5173`. | `POST /api/v1/auth/refresh` returned `403 CSRF_ORIGIN_INVALID` for the browser's loopback IP origin. A page reload therefore discarded session state and returned the user to login. | Added the exact loopback IP origin to `appsettings.Development.json`; added `RefreshAcceptsViteLoopbackIpOrigin` regression coverage. | Fixed and verified |

No database was reset or deleted, and no schema change was needed for these UI parity corrections.
