# Xiangqi Online

Nền tảng Cờ Tướng theo mô hình **3 tầng**: Presentation, Business và Data Access. API và giao diện MVC/Razor + Vue cùng chạy trong ASP.NET Core; React được giữ làm bản đối chiếu/rollback. Phase 3 có bàn cờ và engine luật phía server. Phase 4 bổ sung lobby, phòng chờ, lời mời và SignalR room state. Phase 5 bổ sung ván online SignalR, đồng hồ server, timeout, đầu hàng, đồng bộ lại và lưu lịch sử. Chat/video và AI thuộc các phase sau.

## Cấu trúc solution

```text
XiangqiOnline.sln
├── 01 Presentation
│   ├── XiangqiOnline.API
│   └── XiangqiOnline.Contracts
├── 02 Business
│   ├── XiangqiOnline.Application
│   ├── XiangqiOnline.Domain
│   └── XiangqiOnline.GameEngine
├── 03 Data Access
│   └── XiangqiOnline.Infrastructure
└── 04 Tests
    ├── XiangqiOnline.UnitTests
    ├── XiangqiOnline.IntegrationTests
    └── XiangqiOnline.GameEngine.Tests

frontend/xiangqi-web
docs/
```

Dependency injection được ghép ở điểm khởi động API; GameEngine và Domain không phụ thuộc UI hay EF Core.

## Yêu cầu máy

- Windows 10/11 và Visual Studio 2022 Community.
- .NET 8 SDK/runtime để chạy target `net8.0`.
- SQL Server Database Engine và SSMS. Developer Edition dùng được cho local; Express cũng được hỗ trợ.
- Node.js 22.12 trở lên, npm.
- Không cần Docker hoặc WSL.

Lưu ý: .NET 8 hết hỗ trợ ngày 10-11-2026; cần lên kế hoạch nâng lên .NET 10 LTS trước ngày đó. [Lịch hỗ trợ .NET chính thức](https://learn.microsoft.com/dotnet/core/releases-and-support)

## Cấu hình SQL Server local

1. Dùng SQL Server Developer Edition đã cài sẵn cùng SSMS. SQL Express không bắt buộc.
2. Kiểm tra service SQL Server đang chạy. Với default instance dùng `.`.
3. Kết nối SSMS bằng Windows Authentication.
4. Mở `src/XiangqiOnline.API` và cấu hình User Secrets nếu instance khác default instance:

```powershell
dotnet user-secrets init --project src/XiangqiOnline.API
dotnet user-secrets set "ConnectionStrings:XiangqiOnline" "Server=.;Database=XiangqiOnlineDB;Trusted_Connection=True;TrustServerCertificate=True" --project src/XiangqiOnline.API
```

5. Thiết lập khóa JWT bằng User Secrets (khóa bí mật ngẫu nhiên Base64 tối thiểu 32 byte). Connection string mặc định đã trỏ tới SQL Server `.` bằng Windows Authentication:

```powershell
$bytes = [byte[]]::new(64)
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:SigningKeyBase64" $key --project src/XiangqiOnline.API
Remove-Variable key, bytes
```

6. Cài EF CLI và cập nhật database từ các migration. Phase 4 và 5 có migration additive; sao lưu database trước khi cập nhật schema đang dùng:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.20
dotnet ef database update --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API
```

Schema/tên cột được mô tả ở [docs/database.md](docs/database.md). Không dùng password SQL, refresh token thô hoặc thông tin đăng nhập hardcode.

## Mở trong Visual Studio 2022

1. Mở `XiangqiOnline.sln`.
2. Restore NuGet packages.
3. Cấu hình User Secrets nếu không dùng default instance `.`; chạy EF database update.
4. Đặt `XiangqiOnline.API` làm Startup Project và chạy bằng F5.
5. Swagger Development: `https://localhost:<port>/swagger`.
6. Health API: `https://localhost:<port>/api/v1/health`. API trả 200 sau khi truy vấn SQL Server thành công và 503 nếu database không truy cập được.
7. Chạy frontend theo mục kế tiếp.

## Chạy giao diện MVC/Vue

Chạy `XiangqiOnline.API` trong Visual Studio hoặc bằng `dotnet run --project src/XiangqiOnline.API`. Mở địa chỉ HTTPS hiển thị từ launch profile. Razor phục vụ giao diện, Vue runtime được lưu trong `wwwroot`; các request dùng API `/api/v1`.

React cũ vẫn nằm trong `frontend/xiangqi-web` để đối chiếu/rollback. MVC/Vue hiển thị bàn 9×10 với 32 quân; luật, version, idempotency và clock được kiểm tra ở server. Lobby Phase 4 hỗ trợ tạo/tham gia phòng, ready, lời mời và room sync. Phase 5 dùng `/hubs/game` để gửi nước, cập nhật clocks/history/result và reconnect; client không được quyết định trạng thái ván. Sao lưu rồi áp dụng migration trước khi chạy với database cũ.

## Chạy kiểm tra

```powershell
dotnet restore XiangqiOnline.sln
dotnet build XiangqiOnline.sln
dotnet test XiangqiOnline.sln
cd frontend/xiangqi-web
npm run lint
npm run build
npm run test
```

Integration test health dùng SQL Server thật cho trường hợp thành công và một địa chỉ cục bộ không tồn tại cho trường hợp 503. Migration phải được áp dụng trên SQL Server trước khi báo cáo đã kiểm thử schema.

## Tài liệu

- [Kiến trúc 3 tầng](docs/architecture.md)
- [Database và migration](docs/database.md)
- [REST API contracts](docs/api-contract.md)
- [Xác thực và quản lý phiên](docs/authentication.md)
- [Bảo mật](docs/security.md)
- [SignalR contracts](docs/signalr-contract.md)
- [Luật và FEN](docs/xiangqi-rules.md)
- [Roadmap Phase 1–8](docs/phase-roadmap.md)

## Trạng thái triển khai theo Jira

Đã có MVC/Razor + Vue shell, auth/profile/stats, Phase 3 engine/local game, Phase 4 room/lobby/invitation và Phase 5 realtime online. Migration Phase 4 đã áp dụng trên database local sau backup; migration Phase 5 cần được áp dụng sau backup trước khi chạy local. Xem [báo cáo Phase 5](docs/phase-5-report.md), [báo cáo Phase 4](docs/phase-4-report.md) và [DEV_STATE.md](DEV_STATE.md). Replay, chat/video, AI và production cutover còn lại.
