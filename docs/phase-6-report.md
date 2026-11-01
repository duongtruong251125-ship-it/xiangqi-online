# Phase 6 — Realtime chat và video call

## Phạm vi đã triển khai

- Chat online theo game, lưu SQL trong `ChatMessages`, giới hạn 1000 ký tự, trim, idempotency theo `ClientMessageId`, phân trang cursor và kiểm tra đúng người chơi. Vue interpolation hiển thị nội dung dạng text.
- Mở rộng `/hubs/game`: chat chỉ phát event sau commit, có ack/reject và rate limit. Reconnect join lại game, tải lịch sử chat và loại message trùng.
- WebRTC 1–1: Ringing/Accepted/Connecting/Connected/Rejected/Missed/Ended/Failed, timeout 30 giây, một cuộc gọi active/game, optimistic concurrency. Offer/Answer/ICE chỉ chuyển giữa hai connection đã ghép; không lưu SDP/ICE trong SQL hoặc log.
- UI chỉ hỏi quyền camera/micro sau khi bấm Gọi hoặc Nhận; hỗ trợ từ chối, kết thúc, bật/tắt mic/camera và dừng media tracks khi kết thúc/ngắt hub.
- ICE servers đọc từ `WebRtc:IceServers`; endpoint cấu hình yêu cầu đăng nhập. Mặc định có STUN công khai. TURN có thể thêm qua config; nên dùng credential ngắn hạn.
- Migration `Phase6ChatAndVideoCalls` chỉ thêm `ChatMessages` và `CallSessions` cùng index/FK.

## Kiểm chứng

- `dotnet build XiangqiOnline.sln -c Release --no-restore`: thành công, 0 warning/error.
- `dotnet test XiangqiOnline.sln -c Release --no-restore`: 78/78 thành công (20 engine, 16 unit, 42 integration SQL Server). Integration mới kiểm tra chat persist/broadcast/deduplicate/authorization và call invite/accept/offer signaling giữa hai SignalR client.
- `node --check src/XiangqiOnline.API/wwwroot/js/app.js`: thành công.
- Runtime smoke `GET /api/v1/health`: HTTP 200, SQL Server `Healthy`; lần chạy smoke dùng JWT test key chỉ trong process vì User Secrets local hiện chưa có signing key.
- Automated tests không cấp quyền camera/micro hoặc tạo ICE thật trên hai thiết bị. Media smoke cần HTTPS/localhost với hai trình duyệt; STUN không đảm bảo vượt NAT/firewall hạn chế nếu chưa cấu hình TURN.

## Jira

Phạm vi bao phủ SS-348/349 (chat), SS-354/355/356 (video/signaling/UI) và SS-357/358/359 (kiểm thử). Jira export trong workspace vẫn ghi `To Do`; Jira chưa được cập nhật từ ứng dụng ngoài.
