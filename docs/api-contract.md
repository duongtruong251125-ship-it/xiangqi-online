# REST API Contract

Prefix: `/api/v1`. Payload JSON camelCase. Success dùng `{ success, data, requestId }`; lỗi dùng `{ success: false, data: null, error: { code, message }, requestId }`. Timestamps là ISO-8601 UTC.

Đã triển khai health, authentication/profile, thống kê, quản lý phòng/lobby/lời mời theo Jira `SS-304`, engine Phase 3 và luồng phòng online bắt đầu trận. Route chưa có controller được giữ là hợp đồng tương lai, không có controller giả.

| Phase | Method + URL | Request → Response | Xác thực / phân quyền | Validation / lỗi dự kiến |
|---|---|---|---|---|
| 1 | `GET /health` | Không body → `{ status, checks[] }` | Public; không cần auth | `200` khi SQL truy vấn thành công; `503` nếu không; không trả connection string |
| 2 | `POST /auth/register` | `{ username, email, password, confirmPassword }` → `{ id, username, displayName }` | Public | username/email duy nhất; `USERNAME_ALREADY_EXISTS`, `EMAIL_ALREADY_EXISTS`, `VALIDATION_ERROR` |
| 2 | `POST /auth/login` | `{ identifier, password, rememberMe }` → `{ accessToken, expiresIn, user }`; refresh token trong HttpOnly cookie | Public | lỗi chung `AUTH_INVALID_CREDENTIALS`; giới hạn theo IP |
| 2 | `POST /auth/refresh` | Không body; cookie → access token mới, refresh cookie được xoay | Cookie refresh; Origin phải nằm trong allowlist | token không hợp lệ/hết hạn/đã thu hồi; replay thu hồi phiên |
| 2 | `POST /auth/logout` | Cookie → `{ loggedOut: true }` | Refresh cookie; Origin phải nằm trong allowlist | thu hồi idempotent và xóa cookie |
| 2 | `POST /auth/logout-all` | Không body → `{ loggedOut: true }` | Bearer; thu hồi mọi phiên của user | `401 AUTH_UNAUTHORIZED` |
| 2 | `GET /users/me/stats` | Không body → `{ wins, losses, played }` | Bearer; số liệu từ games đã lưu | Không nhận user ID từ client |
| 2 | `GET /users/me` | Không body → `UserResponse` | Bearer | `401 AUTH_UNAUTHORIZED` |
| 2 | `PATCH /users/me` | `{ displayName }` → `UserResponse` | Bearer; danh tính lấy từ JWT | độ dài 2–80 |
| 2 | `PUT /users/me/password` | `{ currentPassword, newPassword, confirmNewPassword }` → `{ changed: true, ... }` | Bearer; thu hồi refresh sessions | mật khẩu hiện tại phải đúng; phiên access hiện tại hết hạn tối đa sau 15 phút |
| 4 | `POST /rooms` | `{ name, isPrivate }` → `RoomResponse` | Bearer; người tạo thành owner, mặc định Đỏ | Mã 6 ký tự bằng CSPRNG, index unique; `201`, `VALIDATION_ERROR` |
| 4 | `GET /rooms?search=&status=&page=` | `{ items, page, pageSize, totalCount }` → phòng công khai | Bearer; phòng riêng không xuất hiện; mặc định `Waiting` | page 1–1000; page size 12; `INVALID_PAGINATION` |
| 4 | `GET /rooms/{id}` | Không body → phòng + thành viên active + version | Bearer; thành viên active | `404 ROOM_NOT_FOUND` nếu không có quyền/không tồn tại |
| 4 | `POST /rooms/join` | `{ code }` → `RoomResponse` | Bearer; mã hợp lệ cho public/private | `ROOM_INVALID_CODE`, `ROOM_NOT_FOUND`, `ROOM_FULL`, `ROOM_ALREADY_STARTED` |
| 4 | `POST /rooms/join-public` | `{ roomId }` → `RoomResponse` | Bearer; chỉ phòng công khai đang chờ | `ROOM_NOT_FOUND`, `ROOM_FULL`, `ROOM_ALREADY_STARTED` |
| 4 | `POST /rooms/{id}/side` | `{ side, expectedVersion }` → `RoomResponse` | Bearer; thành viên tự chọn phe, không trùng phe | `ROOM_SIDE_TAKEN`, `ROOM_CONFLICT`, `ROOM_NOT_MEMBER` |
| 4 | `POST /rooms/{id}/ready` | `{ isReady, expectedVersion }` → `RoomResponse` | Bearer; thành viên có phe; chỉ đổi ready của mình | `ROOM_SIDE_TAKEN`, `ROOM_CONFLICT`, `ROOM_NOT_MEMBER` |
| 4 | `POST /rooms/{id}/leave` | Không body → `204` | Bearer; thành viên active; chủ rời thì chuyển quyền hoặc đóng phòng | `ROOM_NOT_MEMBER`, `ROOM_ALREADY_STARTED`, `ROOM_CONFLICT` |
| 4 | `POST /rooms/{id}/start` | `{ expectedVersion }` → `RoomResponse` có `gameId` | Bearer; chỉ owner; đủ hai phe và cả hai Ready | `ROOM_HOST_ONLY`, `ROOM_NOT_READY`, `GAME_ALREADY_STARTED`, `ROOM_CONFLICT` |
| 4 | `POST /rooms/{id}/invitations` | `{ username }` → `InvitationResponse` | Bearer; thành viên player/owner của phòng chờ | Có hạn 24 giờ, unique pending, rate limit; `INVITATION_NOT_ALLOWED`, `ROOM_FULL`, `INVITATION_ALREADY_PENDING` |
| 4 | `GET /invitations` | Lời mời đến còn pending | Bearer; tài khoản nhận | Tự chuyển các lời mời hết hạn sang `Expired` |
| 4 | `POST /invitations/{id}/accept` | Không body → `InvitationResponse` | Bearer; đúng người nhận; tham gia phòng cùng transaction | Nếu phòng đầy trả `ROOM_FULL`, lời mời vẫn `Pending`; `INVITATION_EXPIRED`, `INVITATION_ALREADY_HANDLED` |
| 4 | `POST /invitations/{id}/decline` | Không body → `InvitationResponse` | Bearer; đúng người nhận | `INVITATION_NOT_FOUND`, `INVITATION_EXPIRED`, `INVITATION_ALREADY_HANDLED` |
| 4 | SignalR `/hubs/rooms` | `JoinRoomGroup(roomId)`, `LeaveRoomGroup(roomId)`, `RequestRoomSync(roomId)` | JWT; server kiểm tra membership mỗi lần join/resync; sub claim định danh user | Sự kiện `RoomChanged`, `RoomClosed`, `GameStarted`, `InvitationReceived`; reconnect resync qua API/Hub |
| 3 | `POST /games/local` | Không body → `GameResponse` | Bearer; người tạo là chủ sở hữu và điều khiển cả hai bên | `201`; tạo trạng thái ban đầu từ server, không nhận FEN từ client |
| 3 | `POST /games/local/move` | `{ gameId, fromFile, fromRank, toFile, toRank, expectedVersion, clientMoveId }` → `GameResponse` | Bearer; chỉ chủ local game; server luân phiên lượt Red/Black | `MOVE_INVALID`, `VERSION_CONFLICT`, `MOVE_ALREADY_APPLIED`, `GAME_FINISHED`, `GAME_FORBIDDEN` |
| 3 | `GET /games/{id}/legal-moves?fromFile=&fromRank=` | Không body → danh sách tọa độ đích hợp lệ | Bearer; game local: chủ sở hữu; online: người chơi đến lượt | `POSITION_INVALID`, `MOVE_WRONG_TURN`, `GAME_FINISHED`, `GAME_NOT_FOUND` |
| 3 | `GET /games/{id}` | Không body → trạng thái, FEN, lượt, version, ply, check/result | Bearer; local owner hoặc thành viên game online | `404 GAME_NOT_FOUND` nếu không có quyền/không tồn tại |
| 3 | `GET /games/{id}/moves` | Danh sách `GameMoveResponse` theo ply | Bearer; local owner hoặc thành viên game online | `404 GAME_NOT_FOUND` nếu không có quyền/không tồn tại |
| 3 | `POST /games/{id}/restart` | `{ expectedVersion }` → `GameResponse` mới | Bearer; chỉ chủ local game | Tạo game mới và giữ game/lịch sử cũ; `VERSION_CONFLICT`, `GAME_FORBIDDEN`, `GAME_NOT_FOUND` |
| 3–5 | `POST /games/{id}/moves` | `{ fromFile, fromRank, toFile, toRank, expectedVersion, clientMoveId }` → trạng thái sau commit | Bearer; phải là player tới lượt; REST và Hub dùng chung gameplay service, engine và concurrency | `MOVE_INVALID`, `MOVE_WRONG_TURN`, `VERSION_CONFLICT`, `MOVE_IDEMPOTENCY_CONFLICT`, `GAME_TIMEOUT`, `GAME_FINISHED` |
| 5 | `GET /games/{id}/snapshot` | Không body → state, clocks, người chơi và lịch sử đã lưu | Bearer; quyền xem ván | `404 GAME_NOT_FOUND` nếu không có quyền/không tồn tại |
| 5 | `GET /games/{id}/status` | Không body → kết quả, version và clocks server | Bearer; quyền xem ván | `404 GAME_NOT_FOUND` nếu không có quyền/không tồn tại |
| 5–6 | SignalR `/hubs/game` | Phase 5 methods cộng `JoinInteraction`, chat và call methods | JWT; identity từ `sub`; membership, caller/callee và endpoint được kiểm tra server-side | Chat broadcast sau commit; giới hạn 1000 ký tự/idempotent; Offer/Answer/ICE chỉ đến connection đã ghép |
| 6 | `GET /games/{id}/messages` | `?before=<messageId>&limit=1..50` → `{ items, nextCursor, hasMore }` | Bearer; chỉ hai người chơi của ván online | `403 GAME_FORBIDDEN`, `400 CHAT_CURSOR_INVALID` |
| 6 | `GET /rtc/ice-servers` | Không body → danh sách ICE servers cấu hình phía server | Bearer; tải khi người chơi bắt đầu cuộc gọi | STUN mặc định; TURN credentials đặt trong secrets/config |
| 7 | `POST /ai-games` | `{ difficulty: Easy\|Medium\|Hard, humanSide: Red\|Black }` → `AiGameResponse` (`201`) | Bearer; chủ ván lấy từ JWT | AI đi trước nếu người chọn Đen; không nhận FEN/trạng thái từ client |
| 7 | `GET /ai-games/{id}` | Trạng thái ván, FEN, lượt, version, check/result | Bearer; chỉ chủ ván | `404 AI_GAME_NOT_FOUND` |
| 7 | `POST /ai-games/{id}/moves` | `{ fromFile, fromRank, toFile, toRank, expectedVersion, clientMoveId }` → trạng thái sau nước người | Bearer; chỉ chủ ván; áp dụng qua `IXiangqiRules` | Idempotency + optimistic version; `AI_MOVE_INVALID`, `AI_WRONG_TURN`, `AI_GAME_VERSION_CONFLICT` |
| 7 | `GET /ai-games/{id}/moves` | Trạng thái và lịch sử Human/AI theo ply | Bearer; chỉ chủ ván | `404 AI_GAME_NOT_FOUND` |
| 7 | `POST /ai-games/{id}/restart` | `{ expectedVersion }` → ván mới, ván cũ được giữ lại | Bearer; chỉ chủ ván | Hủy tính toán cũ và tăng version ván cũ |
| 7 | `POST /ai-games/{id}/resign` | `{ expectedVersion }` → kết quả đầu hàng | Bearer; chỉ chủ ván | Có thể đầu hàng cả khi AI đang tính hoặc lỗi |
| 7 | `GET /ai-games/{id}/analysis-status` | `{ status, completedDepth, elapsedMs, nodesSearched, version }` | Bearer; chỉ chủ ván | Trạng thái lấy từ SQL; UI polling khi AI chờ/tính |

Các DTO response chi tiết sẽ đóng băng cùng từng phase trước khi triển khai. Mọi route thay đổi trạng thái phải kiểm tra ownership/quyền ở Business layer. `UserId` không được nhận từ client làm danh tính.
