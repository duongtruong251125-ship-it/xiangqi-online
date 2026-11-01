# SignalR contract

## Đã triển khai Phase 4

Hub có xác thực: `/hubs/rooms` (`XiangqiOnline.API/Hubs/RoomHub.cs`). JWT được đọc từ Authorization header; SignalR JS client dùng `accessTokenFactory` để lấy access token mới. Query token chỉ được chấp nhận ở đúng đường dẫn hub do browser WebSocket không thể gửi Authorization header. Token không được ghi vào log.

Identity lấy từ claim `sub`; client không gửi user ID. `JoinRoomGroup` và `RequestRoomSync` gọi RoomService để xác minh user vẫn là thành viên active trước khi vào group và nhận snapshot. Mất mạng không xóa thành viên khỏi database. Sau reconnect, client gọi lại `RequestRoomSync` để tải trạng thái SQL mới.

| Client method | Payload | Kết quả | Quyền |
|---|---|---|---|
| `JoinRoomGroup` | `{ roomId }` | Tham gia group và nhận `RoomStateSynced` | Thành viên active; server kiểm tra lại |
| `LeaveRoomGroup` | `{ roomId }` | Gỡ connection khỏi group | Connection hiện tại; chỉ rời luồng nhận tin |
| `RequestRoomSync` | `{ roomId }` | Vào lại group, nhận `RoomStateSynced` mới | Thành viên active; server kiểm tra lại |

| Server event | Payload | Nội dung |
|---|---|---|
| `RoomChanged` | `{ roomId, version }` | Tín hiệu không chứa dữ liệu phòng; client lấy state qua REST để server kiểm tra quyền hiện tại |
| `RoomClosed` | `{ roomId }` | Phòng đã đóng |
| `GameStarted` | `{ roomId }` | Client tải lại phòng để lấy `gameId` |
| `RoomStateSynced` | `RoomResponse` | Snapshot mới nhất chỉ gửi cho connection đã xác minh membership |
| `InvitationReceived` | `InvitationResponse` | Lời mời gửi tới group riêng của user nhận |

Group chỉ dùng định tuyến tín hiệu, không phải dữ liệu gốc hoặc quyền truy cập. Sự kiện group chỉ có ID/phiên bản; state phòng đầy đủ lấy qua REST, nơi authorization kiểm tra membership lần nữa. Điều này cũng giới hạn dữ liệu lộ đến connection cũ nếu thành viên đã rời group nhưng socket còn mở.

Frontend MVC dùng `@microsoft/signalr` 8.0.29 đã vendored trong `wwwroot/lib/signalr`. Tự kết nối lại dùng `withAutomaticReconnect`; nếu kết nối ban đầu thất bại hoặc hết các lần retry, UI tiếp tục thử lại sau 5 giây. Khi kết nối lại thành công ở trang phòng, client yêu cầu đồng bộ lại. Lobby duy trì kết nối để nhận lời mời realtime và vẫn tải inbox từ SQL khi mở trang.

## Đã triển khai Phase 5 — game online

Hub có xác thực tại `/hubs/game` (`XiangqiOnline.API/Hubs/GameHub.cs`). Identity chỉ lấy từ JWT claim `sub`. Mỗi lệnh tải trạng thái và kiểm tra thành viên qua `IGameplayService`; group chỉ dùng để phát tín hiệu và không cấp quyền.

| Client method | Payload | Kết quả | Quyền |
|---|---|---|---|
| `JoinGame` | `gameId` | Vào group và nhận `GameStateSynced`, `ClockSynced` | Người Đỏ hoặc Đen của ván |
| `LeaveGame` | `gameId` | Rời group; presence cập nhật khi connection cuối rời | Người chơi của ván |
| `RequestGameSync` | `gameId` | Tham gia lại group và nhận snapshot SQL hiện tại | Người chơi của ván |
| `SubmitMove` | `SubmitMoveRequest` | Gọi cùng GameplayService như REST; trả accepted/rejected | Người chơi đúng lượt, ván đang chạy |
| `ResignGame` | `gameId` | Lưu kết quả đầu hàng rồi phát `GameFinished` | Người chơi của ván, ván đang chạy |

| Server event | Nội dung |
|---|---|
| `GameStateSynced` | Snapshot FEN, lượt, version, người chơi, đồng hồ, lịch sử và kết quả |
| `ClockSynced` | Đồng hồ hiện tại, thời điểm server, bên đang chạy và version |
| `MoveApplied` | Nước đã commit, FEN mới, lượt kế, version và đồng hồ |
| `MoveRejected` | Mã/lời nhắn lỗi và version hiện tại |
| `PlayerConnectionChanged` | Presence tạm thời của người chơi |
| `GameFinished` | Bên thắng, lý do, FEN cuối, version cuối và đồng hồ |

Move và kết quả được commit trước khi phát event. REST và Hub chia sẻ cùng service/repository để cùng kiểm tra luật, clock, idempotency và optimistic concurrency. Nếu client thiếu version, cần gọi `RequestGameSync`. Đồng hồ ở browser chỉ hiển thị; server UTC quyết định timeout. Presence in-memory hỗ trợ nhiều connection cho một user trên một instance; chưa cấu hình scale-out.

Vue dùng client `@microsoft/signalr` local với access token factory và automatic reconnect. Sau reconnect, nó join/sync, thay snapshot, so version, cập nhật clocks/history; nước đang chờ chỉ retry bằng cùng `ClientMoveId`.

## Phase 6 — chat và WebRTC signaling

Phase 6 dùng lại connection `/hubs/game`. `JoinInteraction(gameId)` kiểm tra đúng một trong hai người chơi của game online rồi thêm connection vào group game. Khi reconnect client join lại, tải chat từ REST theo cursor và deduplicate theo message ID.

| Client method | Kết quả | Quyền / giới hạn |
|---|---|---|
| `JoinInteraction(gameId)` | Vào group tương tác và nhận call state hiện tại | Chỉ hai người chơi; identity từ JWT `sub` |
| `SendChatMessage({ gameId, clientMessageId, content })` | Ack; event chỉ phát sau commit SQL | Tối đa 1000 ký tự sau trim; cùng ID/cùng nội dung idempotent; khác nội dung bị từ chối; 20 lần/phút |
| `StartCall(gameId)` | Tạo Ringing tối đa 30 giây, gửi `IncomingCall` đến endpoint đối thủ | Chỉ hai người chơi; một call active/game; 3 lần/phút |
| `AcceptCall(callId)`, `RejectCall(callId)`, `EndCall(callId)` | Chuyển trạng thái có version/concurrency và báo hai đầu | Callee nhận/từ chối; hai đầu kết thúc; endpoint phải được ghép |
| `SendCallSignal({ callId, gameId, payload }, kind)` | Chuyển SDP/ICE đến đúng endpoint peer | Offer/Answer/IceCandidate; kiểm tra vai trò/trạng thái/kích thước; 120 lần/phút |
| `CallConnected(callId)` | Chuyển sang Connected | Chỉ một trong hai endpoint đã ghép |

SQL giữ chat và metadata trạng thái cuộc gọi; không lưu media, SDP hoặc ICE. WebRTC media đi trực tiếp giữa trình duyệt. `GET /api/v1/rtc/ice-servers` trả cấu hình ICE cho người dùng đã đăng nhập. Mặc định chỉ có STUN công khai; cần TURN cho một số NAT/firewall. Khi endpoint SignalR ngắt, phiên được đánh dấu Failed; client đóng PeerConnection và dừng media tracks.

Event mới: `ChatMessageReceived`, `ChatMessageRejected`, `IncomingCall`, `CallStateChanged`, `CallSignalReceived`. SDP/ICE không ghi vào log. UI chỉ xin quyền camera/micro sau khi bấm Gọi hoặc Nhận.
