# Báo cáo Phase 4 – Room Management, Invitations & Multiplayer Lobby

Ngày kiểm tra: 2026-11-01. Đã audit các endpoint tạo/join/đọc phòng có sẵn và mở rộng tiếp trên kiến trúc API → Application → Infrastructure. Không tạo project mới, không sửa Authentication hoặc GameEngine Phase 3.

## Đối chiếu Jira SS-304

| Jira | Nội dung | Triển khai | Kiểm chứng | Trạng thái Jira |
|---|---|---|---|---|
| `SS-326` | Tạo phòng, lấy mã để mời bạn | MVC lobby tạo phòng public/private có tên; API tạo mã 6 ký tự bảo mật | API integration tạo phòng và đọc mã/phòng | Không đổi, giữ `To Do` |
| `SS-327` | API tạo phòng và sinh mã duy nhất | `RoomService`, `EfRoomRepository`, unique index `Rooms.Code`, Room entity và migration | SQL integration dùng migration trong database test | Không đổi, giữ `To Do` |
| `SS-328` | Waiting screen và copy mã | `Views/Room/Index.cshtml`, Vue room template, nút copy code, chọn phe/ready/start/leave/invite | Vue template smoke; các API tương ứng integration tested | Không đổi, giữ `To Do` |
| `SS-329` | Tham gia phòng bằng mã | MVC lobby có modal nhập mã, kiểm tra 6 ký tự và hiển thị lỗi API | API test mã hợp lệ/không hợp lệ và full room | Không đổi, giữ `To Do` |
| `SS-330` | API join và giới hạn sức chứa | Join bằng mã/public room; SQL `UPDLOCK` + transaction, unique membership/side index; hai request tranh slot chỉ có tối đa một thành viên thành công | Integration test concurrent join, capacity, private code | Không đổi, giữ `To Do` |
| `SS-331` | Modal nhập mã và báo lỗi | Modal accessible trong lobby, trạng thái loading/error, giữ lại mã khi lỗi | Vue lobby render smoke; API lỗi kiểm thử integration | Không đổi, giữ `To Do` |

## Phạm vi Phase 4 ngoài các ticket Jira

- Lobby đọc phòng public từ SQL, hỗ trợ tìm theo tên/mã, lọc Waiting/InGame/Closed và phân trang 12 phòng. Room riêng không xuất hiện trong danh sách; join theo ID chỉ nhận phòng public.
- Room chờ có chọn phe, reset Ready khi đổi phe, sẵn sàng/hủy sẵn sàng. Chỉ chủ phòng được start sau khi đủ hai người ở hai phe khác nhau và cả hai Ready.
- Start tạo tối đa một `Game` từ FEN của GameEngine. `Room.Version`, SQL transaction và lock trên hàng Room serialize join, đổi phe/ready, start và leave. Khi chủ phòng rời lúc chờ, quyền chuyển cho thành viên còn lại; phòng rỗng chuyển sang Closed.
- Invitation lưu SQL, gửi tới username có thật, hết hạn sau 24 giờ, chặn tự mời và pending duplicate, giới hạn tốc độ theo IP. Inbox vẫn đọc được khi người nhận offline. Accept kiểm tra sức chứa và thêm membership trong cùng transaction; phòng đầy thì trả `ROOM_FULL` và giữ lời mời Pending.
- `/hubs/rooms` yêu cầu JWT. Join/resync kiểm tra membership qua database. Group chỉ phát `{ roomId, version }` để client tải state qua REST authorization; invitation realtime được gửi vào group riêng theo claim user. Reconnect yêu cầu sync lại; mất kết nối không xóa membership.
- MVC lobby/waiting room dùng Vue; SignalR JS client 8.0.29 được vendored tại `wwwroot/lib/signalr`. React cũ được giữ nguyên.

## API và migration

Đã thêm `GET /api/v1/rooms`, `POST /api/v1/rooms/join-public`, các route `/side`, `/ready`, `/leave`, `/start`, và `/api/v1/invitations` cùng các route accept/decline. API hợp đồng tại [api-contract.md](api-contract.md); hub tại [signalr-contract.md](signalr-contract.md).

Migration additive `20261031170759_Phase4RoomLobbyInvitations` thêm tên/phiên bản/ngày đóng phòng, trạng thái ready, thời gian phản hồi lời mời và index. Ngày 2026-11-01 đã tạo full copy-only backup `XiangqiOnlineDB_before_Phase4_20261101.bak` trong SQL Server Backup directory; `RESTORE VERIFYONLY WITH CHECKSUM` xác nhận hợp lệ. Migration đã áp dụng lên database local `XiangqiOnlineDB`; migration history, cột/index, trạng thái ONLINE và tổng 0 dòng dữ liệu ứng dụng sau cập nhật đã được xác minh. `dotnet ef migrations has-pending-model-changes` báo không còn thay đổi model chưa có migration.

## Kết quả kiểm thử

- `dotnet build XiangqiOnline.sln -c Release --no-restore`: pass.
- `dotnet test XiangqiOnline.sln -c Release --no-restore`: **72/72 pass** — 16 unit, 20 GameEngine, 36 integration.
- Integration tests bao gồm tạo/join, private visibility, search/filter, ready/start gate, owner-only start, host transfer, invitation create/list/accept/decline/expiry/full-room behavior, concurrent join/start/accept, authorization và hub negotiate.
- `node --check src/XiangqiOnline.API/wwwroot/js/app.js`: pass.
- `npm test`, `npm run lint`, `npm run build` in `frontend/xiangqi-web`: pass (10 legacy React tests; the React rollback client remains separate from the MVC/Vue Phase 4 UI).
- SQL-backed integration fixture tạo và dọn database cách ly riêng. Không dùng Docker.
- MVC home mở trực tiếp trên browser ở cổng smoke riêng; `/Lobby` chuyển tới trang đăng nhập, `/api/v1/health` trả Healthy, `/api/v1/rooms` trả 401 khi chưa xác thực, và app.js/SignalR/CSS trả 200. Không tạo tài khoản hoặc dữ liệu thử trên database người dùng; các thao tác có đăng nhập đã được kiểm chứng bằng integration tests.
- Jira `SS-304` và `SS-326`–`SS-331` chưa được chuyển trạng thái.

## Chạy ở Visual Studio 2022

1. Sao lưu database đang dùng.
2. Trong Package Manager Console hoặc terminal từ solution root, áp migration: `dotnet ef database update --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API`.
3. Mở `XiangqiOnline.sln`, chọn `XiangqiOnline.API` làm Startup Project và chạy bằng F5.
4. Đăng nhập → Sảnh chơi → tạo phòng public/private. Người thứ hai tham gia bằng mã hoặc từ danh sách public, chọn phe; cả hai bấm sẵn sàng; chủ phòng bấm bắt đầu.
5. Để mời trực tiếp, chủ phòng/thành viên player chọn **Mời người chơi** và nhập username. Người nhận chấp nhận/từ chối ở inbox trên lobby.

## Còn cần nghiệm thu ở môi trường của bạn

Mã nguồn, build/test, migration và smoke local đã hoàn tất. Đăng nhập rồi tạo phòng/lời mời bằng tài khoản người dùng chưa chạy trong browser; để dùng giao diện, mở Visual Studio bằng Development profile, đăng nhập hoặc đăng ký tài khoản và thao tác lobby. Jira vẫn giữ `To Do` do chưa cập nhật Jira.
