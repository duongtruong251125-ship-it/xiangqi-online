# Database SQL Server

Database: `XiangqiOnlineDB`. Model được khai báo trong Domain; Fluent API ở `AppDbContext`.

## Bảng và ràng buộc

| Bảng | Ràng buộc chính |
|---|---|
| Users | PK; unique `NormalizedUsername`, `NormalizedEmail` |
| RefreshTokens | FK User; unique `TokenHash`; chỉ lưu SHA-256 hash; lưu trạng thái xoay vòng và remember-me |
| Rooms | FK Owner; unique Code; MaxPlayers = 2 |
| RoomMembers | FK Room/User; unique active (RoomId, UserId); unique (RoomId, Side) cho người chơi đang hoạt động; Side null dành cho spectator |
| Games | FK Room/User cho hai bên và Winner; `Version` là concurrency token; AI metadata, human side, lease/retry/status, result side; AI không cần User giả |
| GameMoves | FK Game; unique (GameId, Ply), unique (GameId, ClientMoveId) cho idempotency; `IsAiMove` phân biệt lượt AI |
| GameSnapshots | FK Game; unique (GameId, Ply) |
| Invitations | FK Room/Inviter/Invitee; unique TokenHash khi có token |
| Messages | FK Room/Sender; index RoomId + CreatedAt |
| GameEvents | FK Game; index GameId + CreatedAt |
| ChatMessages | FK Game/Sender; unique (GameId, SenderUserId, ClientMessageId); index GameId + CreatedAtUtc + Id |
| CallSessions | FK Game/Caller/Callee; unique filtered GameId cho trạng thái active; Version concurrency |

Các FK dùng `Restrict` để giữ lịch sử. Không dùng cascade delete. Spectator không chiếm một trong hai Side; Side chỉ được dùng cho Red/Black. Mọi thời gian ghi UTC.

## Local setup

Mặc định local dùng Windows Authentication với default SQL Server instance (`.`). SQL Server Developer Edition và Express đều được hỗ trợ. Với Express, đổi server thành `localhost\SQLEXPRESS`:

```text
Server=.;Database=XiangqiOnlineDB;Trusted_Connection=True;TrustServerCertificate=True
```

Connection string mẫu nằm trong cấu hình local, không chứa password. Có thể ghi đè bằng User Secrets tại project API:

```powershell
cd src/XiangqiOnline.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:XiangqiOnline" "Server=.;Database=XiangqiOnlineDB;Trusted_Connection=True;TrustServerCertificate=True"
```

## Migration

```powershell
dotnet tool install --global dotnet-ef --version 8.0.20
$env:ConnectionStrings__XiangqiOnline = "Server=.;Database=XiangqiOnlineDB;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef migrations add InitialCreate --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API
dotnet ef database update --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API
```

Xem bảng, index và FK trong SSMS bằng cách kết nối default instance `.` (hoặc `localhost\SQLEXPRESS` nếu dùng Express), mở `XiangqiOnlineDB` → Tables. Không cần Docker hoặc WSL.

Các migration gồm `InitialCreate`, `AddRoomMemberChecks`, `Phase2RefreshTokenRotation`, `Phase2RefreshTokenPersistence`, `Phase4RoomLobbyInvitations`, `Phase5OnlineGameClocks`, `Phase6ChatAndVideoCalls` và `Phase7HumanVsAi`. Phase 4–7 đã được áp dụng local sau backup; Phase 7 backup là `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\XiangqiOnlineDB_before_Phase7_20261101.bak`, đã xác minh `RESTORE VERIFYONLY WITH CHECKSUM`. `20261101053848_Phase7HumanVsAi` chỉ thêm cột/index/FK vào Games/GameMoves; không reset database hoặc xóa dữ liệu. Các migration tự chạy trong database integration cô lập. Xác minh drift bằng `dotnet ef migrations has-pending-model-changes --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API`.
