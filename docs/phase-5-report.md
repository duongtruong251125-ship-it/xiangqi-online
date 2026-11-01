# Phase 5 — Realtime online multiplayer

## Kết quả

Đã triển khai game online dùng SignalR trên kiến trúc ba tầng hiện có. SQL Server giữ trạng thái chuẩn; cả REST và Hub gọi cùng `IGameplayService`, cùng GameEngine và cùng repository EF Core. Máy chủ kiểm tra quyền, lượt, phiên bản, giờ còn lại và luật trước khi lưu. Chỉ sau khi SaveChanges/transaction thành công mới phát `MoveApplied` hoặc `GameFinished`.

Đã giữ SQL Server Developer Edition hiện tại; không thêm Socket.IO, Docker hay dịch vụ database giả. `Phase5OnlineGameClocks` là migration additive, có cập nhật mốc UTC cho ván online đang chơi từ schema cũ. Trước khi cập nhật `XiangqiOnlineDB`, đã tạo full copy-only backup `XiangqiOnlineDB_before_Phase5_20261101_010608.bak` trong SQL Server Backup directory và `RESTORE VERIFYONLY WITH CHECKSUM` thành công. Migration đã áp dụng và xác minh trong local DB cùng các database cô lập do integration fixture tạo/hủy. Các bảng ứng dụng có 0 dòng trước và sau migration.

## Thay đổi chính

- `Game` có time control, số mili giây còn lại và mốc bắt đầu lượt. `GameMove` lưu người đi, FEN trước nước đi và đồng hồ sau nước đi. Snapshot lưu lượt, clocks và version.
- `GameplayService` là điểm dùng chung cho REST/SignalR: kiểm tra luật, lượt, quyền, version, đồng hồ, kết quả, idempotency, đầu hàng và timeout. EF concurrency token và unique indexes bảo vệ hai request cùng version và nước gửi lặp.
- `/hubs/game` yêu cầu đăng nhập và có `JoinGame`, `LeaveGame`, `RequestGameSync`, `SubmitMove`, `ResignGame`. Chỉ người chơi của ván được vào group hoặc đọc sync. Rate limit theo user và method.
- Server phát `GameStateSynced`, `ClockSynced`, `MoveApplied`, `MoveRejected`, `PlayerConnectionChanged` và `GameFinished`. Thông báo realtime chạy sau khi commit; client có thể tải lại snapshot nếu bỏ lỡ event.
- Worker kiểm tra timeout mỗi giây và kết thúc ván bằng cập nhật cạnh tranh version. Mất kết nối không tự xử thua. Presence hiện lưu trong bộ nhớ và chỉ phù hợp một instance.
- Razor route `/games/{id}` dùng Vue hiện tại; trang online có clocks, trạng thái kết nối, lịch sử, xác nhận đầu hàng và modal kết quả. Client tự resync theo version; retry giữ nguyên `ClientMoveId`.

## File tạo mới và cập nhật

Tạo mới:

- `src/XiangqiOnline.API/Hubs/GameHub.cs`, `src/XiangqiOnline.API/Hubs/GameHubRateLimitFilter.cs`
- `src/XiangqiOnline.API/Infrastructure/GameEventNotifier.cs`, `src/XiangqiOnline.API/Infrastructure/GameTimeoutWorker.cs`
- `src/XiangqiOnline.API/wwwroot/css/phase5.css`
- `src/XiangqiOnline.Infrastructure/Persistence/Migrations/20261031175313_Phase5OnlineGameClocks.cs` và file Designer
- `docs/phase-5-report.md`

Cập nhật:

- `src/XiangqiOnline.Domain/Entities/Game.cs`
- `src/XiangqiOnline.Contracts/Games/GameContracts.cs`, `src/XiangqiOnline.Contracts/RealtimeContracts.cs`
- `src/XiangqiOnline.Application/Games/GameplayService.cs`
- `src/XiangqiOnline.Infrastructure/Games/EfGameplayRepository.cs`, `src/XiangqiOnline.Infrastructure/Persistence/AppDbContext.cs`, `src/XiangqiOnline.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`, `src/XiangqiOnline.Infrastructure/Rooms/EfRoomRepository.cs`
- `src/XiangqiOnline.API/Program.cs`, `src/XiangqiOnline.API/Controllers/GamesController.cs`, `src/XiangqiOnline.API/wwwroot/js/app.js`, `src/XiangqiOnline.API/Views/Shared/_Layout.cshtml`, `src/XiangqiOnline.API/appsettings.json`
- `tests/XiangqiOnline.IntegrationTests/XiangqiOnline.IntegrationTests.csproj`, `tests/XiangqiOnline.IntegrationTests/RoomEndpointTests.cs`, `tests/XiangqiOnline.IntegrationTests/MvcRouteTests.cs`
- `README.md`, `DEV_STATE.md`, `docs/api-contract.md`, `docs/database.md`, `docs/signalr-contract.md`, `docs/phase-roadmap.md`

## Kiểm tra

- `dotnet build XiangqiOnline.sln -c Release --no-restore`: PASS, 0 warning, 0 error.
- `node --check src/XiangqiOnline.API/wwwroot/js/app.js`: PASS.
- Runtime smoke after starting ASP.NET Core in Development at `http://127.0.0.1:5082`: `/api/v1/health` 200 with SQL connected; `/` 200 and Razor output includes Vue runtime and Phase 5 stylesheet. Host was stopped after the check.
- `dotnet test XiangqiOnline.sln -c Release --no-build`: PASS, 76 tổng (16 unit, 20 GameEngine, 40 integration), 0 failed.
- SQL Server integration test tạo database riêng, áp dụng toàn bộ migrations và dọn database kiểm thử khi kết thúc.
- SignalR E2E dùng hai `HubConnection` thật qua Long Polling: hai người join, Đỏ đi trước, Đen nhận event, ngắt/kết nối lại khôi phục FEN/version, Đen đi tiếp, API trả cùng FEN/version cho cả hai, rồi đầu hàng được đồng bộ.
- Integration tests còn kiểm tra retry cùng `ClientMoveId`, payload xung đột, hai nước online cùng version chỉ commit một, sai lượt, nước sai luật, hết giờ, đầu hàng và trạng thái lưu SQL.
- Chưa chạy thao tác game bằng hai trình duyệt thật; kiểm thử SignalR E2E xác minh backend transport, SQL persistence, reconnect, move sync, clock events và results qua TestServer.

## Jira đối chiếu

CSV gốc và CSV ánh xạ đều ghi các issue dưới đây là `To Do`. Không có kết nối Jira để cập nhật issue, nên trạng thái Jira vẫn `To Do`; “kết quả mã nguồn” mô tả trạng thái trong repository.

| Jira key | Hạng mục | Kết quả mã nguồn | Kiểm tra | Jira sau |
|---|---|---|---|---|
| SS-307 | Thi đấu trực tuyến và tương tác | SignalR game, sync, move, clock, reconnect, kết thúc ván | Hub E2E + integration | To Do (chưa cập nhật tracker) |
| SS-342 | Đồng bộ nước đi thời gian thực | Đã triển khai | Hub E2E hai người | To Do |
| SS-343 | Socket.IO Server điều phối nước đi | Chuyển sang SignalR theo yêu cầu công nghệ của dự án | Hub E2E hai người | To Do |
| SS-344 | Socket.IO Client và khóa tương tác | Chuyển sang SignalR/Vue; khóa pending và chỉ cho đi đúng lượt | Build + Hub E2E; chưa browser UI smoke | To Do |
| SS-345 | Nhận biết chiếu/chiếu bí kết thúc ván | Tái sử dụng GameEngine; kết quả persist qua service chung | GameEngine suite; chưa có online checkmate E2E riêng | To Do |
| SS-346 | Thuật toán chiếu Tướng/chiếu bí | Đã hoàn tất từ Phase 3, không viết lại | 20 GameEngine tests | To Do |
| SS-347 | Modal tổng kết ván | Đã thêm Vue result modal | JS syntax + runtime page smoke; chưa browser visual QA | To Do |
| SS-348 | Chat và giữ phòng khi mất mạng | Reconnect/resync làm trong Phase 5; chat giữ ngoài phạm vi đến Phase 6 | Reconnect E2E | To Do |
| SS-350 | Reconnect khôi phục ván | Đã triển khai tự kết nối lại, join/sync và retry idempotent | Disconnect/reconnect E2E | To Do |
| SS-306 | Lưu và xem lại ván | Lưu move/snapshot tiếp tục dùng bảng hiện hữu; replay giữ ngoài Phase 5 | SQL persistence integration | To Do |
| SS-339 | Lưu ván và lịch sử nước đi | Move history và snapshot được lưu/đọc | SQL integration + sync E2E | To Do |
| SS-340 | API lưu ván và lấy danh sách đã chơi | Game API hiện hữu được hoàn thiện; endpoint danh sách/replay vẫn nằm ngoài Phase 5 | API snapshot/status integration | To Do |
| SS-341 | Danh sách ván và Replay | Ngoài phạm vi Phase 5 | Chưa kiểm thử | To Do |
| SS-349 | Chat thời gian thực | Ngoài phạm vi Phase 5, thuộc Phase 6 | Chưa làm | To Do |

## Giới hạn còn lại

- Presence là in-memory một instance; khi scale nhiều instance cần backplane hoặc distributed presence.
- Chưa có transactional outbox. Nếu broadcast lỗi sau commit, trạng thái bền vững và `RequestGameSync` khôi phục được khi client kết nối lại; không đảm bảo gửi event ngay lập tức.
- Bộ luật Phase 3 chưa hỗ trợ lặp lại, chiếu liên tục hoặc đuổi bắt theo luật thi đấu; Phase 5 không tuyên bố các luật đó.
- Chưa có kiểm thử browser trực quan bằng hai phiên đăng nhập, và chưa kiểm thử production/reverse proxy. Trước khi demo, nên mở hai phiên trình duyệt và chơi thử qua giao diện MVC/Vue.

## Chạy trong Visual Studio 2022

Mở `XiangqiOnline.sln`, đặt `XiangqiOnline.API` làm Startup Project. Mặc định `appsettings.json` kết nối trusted Windows tới SQL Server `.`; User Secrets phải có JWT signing key. Trước khi chạy ứng dụng với database cũ, sao lưu DB rồi chạy:

```powershell
dotnet ef database update --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API
```

Mở `/Lobby`, tạo hai tài khoản, tạo phòng, cho cả hai chọn phe và sẵn sàng; chủ phòng bắt đầu trận. Hai người mở liên kết `/games/{id}` ở hai phiên trình duyệt riêng.
