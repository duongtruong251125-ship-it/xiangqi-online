# Audit chuyển React sang ASP.NET Core MVC + Razor + Vue

Ngày audit: 2026-10-31. Phạm vi: source workspace hiện tại, SQL Server local `XiangqiOnlineDB`, migration đang có và file mẫu `C:\Users\duong\Downloads\23DTHD5_Demo.zip`. Đây là audit chỉ đọc; không sửa code ứng dụng, không chạy migration và không thay đổi dữ liệu.

## Hiện trạng repository

- Workspace `C:\Users\duong\Downloads\QLDACNTT` hiện không có thư mục `.git`; `git status` và `git rev-parse` đều báo không phải Git repository. Không có baseline commit/branch để diff, tạo backup branch hoặc rollback bằng Git. Không tự khởi tạo repository trong audit này.
- Solution `XiangqiOnline.sln` có 9 project .NET 8. Ranh giới hiện tại: API/Contracts ở Presentation; Application/Domain/GameEngine ở Business; Infrastructure/EF Core ở Data Access; ba project kiểm thử riêng.
- Project references: API tham chiếu Application, Infrastructure, Contracts; Application tham chiếu Domain; Infrastructure tham chiếu Domain và Application; GameEngine độc lập. Không có project MVC riêng và chưa có tham chiếu SignalR runtime.
- API hiện đăng ký `AddControllers`, middleware request-id/error, HTTPS redirect, CORS allowlist, Swagger, JWT bearer, rate limit và SQL health check; chỉ map attribute controllers. Chưa đăng ký MVC views, static files, conventional MVC routes hoặc SignalR hub.
- `AppDbContext` là custom context, không phải `IdentityDbContext`; có các bảng Users, RefreshTokens, Rooms, RoomMembers, Games, GameMoves, GameSnapshots, Invitations, Messages và GameEvents. User là entity riêng; không dùng ASP.NET Identity User/Identity cookie.
- Bốn migration hiện có: `InitialCreate`, `AddRoomMemberChecks`, `Phase2RefreshTokenRotation`, `Phase2RefreshTokenPersistence`. Hai migration Phase 2 thêm cột/FK/index cho rotation và remember-me; migration đầu tạo schema ban đầu. Không reset migration hoặc chạy `Down` của `InitialCreate` trên DB có dữ liệu.
- Controllers API hiện có: Auth, Users/me, Health. Authentication dùng `PasswordHasher<User>`, JWT access token 15 phút và refresh cookie HttpOnly; refresh token chỉ lưu SHA-256 hash, có rotation/replay handling. Origin và CORS allowlist phải tiếp tục khớp khi host UI cùng API.
- React có routes Home, Login, Register, Profile, Lobby, Room và Game; auth store dùng Zustand; access token giữ trong memory; Axios refresh single-flight và API contracts ở `/api/v1`.
- Bàn cờ React tạo 9 cột × 10 hàng, 32 quân khai cuộc, cung Tướng, sông, lật bàn và responsive SVG. Hiện chỉ chọn quân, chưa xử lý nước đi hợp lệ. C# GameEngine có board/FEN/serialization và interface `IXiangqiRules`, chưa có implementation luật.
- Không tìm thấy `AddSignalR`, `MapHub`, Hub server hoặc `@microsoft/signalr` trong source hiện tại. Chỉ có payload contracts và tài liệu SignalR dự kiến; SignalR chưa phải tính năng đang hoạt động.
- Lần xác minh gần nhất: .NET Release 33/33, frontend 10/10, lint/build qua; API local trả health SQL `Healthy` và Swagger HTTP 200. Kết quả đó không thay thế test MVC/Vue sau triển khai.

## Database và backup

- Truy vấn chỉ đọc xác nhận SQL Server Developer Edition 64-bit, DB `XiangqiOnlineDB` `ONLINE`, 10 bảng ứng dụng cộng `__EFMigrationsHistory` (11 bảng tổng), 17 foreign keys và đủ bốn migration trong history.
- Snapshot local tại thời điểm audit có 0 hàng ở cả 10 bảng ứng dụng (bao gồm `Users`, `RefreshTokens`, phòng và ván đấu). Đây chỉ là số liệu DB đang kết nối; không chứng minh DB khác hoặc backup ngoài máy không có dữ liệu.
- Lịch sử `msdb.dbo.backupset` không có full backup cho `XiangqiOnlineDB`. Không có bằng chứng backup ngoài SQL Server hoặc restore đã thử thành công. Trạng thái backup/restore **chưa được xác nhận**.
- Không có thay đổi schema cần thiết chỉ để phục vụ Razor/Vue theo phương án đề xuất. Không chạy `database update`, không dùng connection string mẫu từ bài giảng và không drop/truncate bảng.

## Audit bài mẫu giảng viên

Đã đọc Program, HomeController, CoTuongController, ChessHub, Razor layout/Home view, `cotuong.js`, ApplicationDbContext, Room, repository/service, CacheManage, project và appsettings trong `23DTHD5_Demo.zip`. Không giải nén, build hay chạy file mẫu. Archive chứa `.vs`, `bin`, `obj`; appsettings có connection-string setting chứa thuộc tính password và integrated-security. Giá trị không được đưa vào báo cáo; coi archive là dữ liệu nhạy cảm và không sao chép cấu hình.

| Chủ đề | Mẫu giảng viên làm | Đánh giá khi áp dụng |
|---|---|---|
| MVC/Razor | `AddControllersWithViews`, conventional route, shared layout và static files | Học cách ghép host/layout; thêm vào API host hiện có, giữ API attribute routes `/api/v1/...`. |
| Vue | Vue 3 global build từ CDN; logic trong một JS lớn, view chứa markup/CSS | Dùng Vue 3 SFC + Vite build vào `wwwroot`; không dựa Vue CDN không khóa version. |
| EF/Repository/Service | DbContext, Room repository, BookingService | Giữ phân lớp; tái dùng Application abstractions và `EfAuthRepository`. Tránh synchronous generic repository và tạo repo bằng `new`. |
| SQL | connection string trong appsettings và `IdentityDbContext` trong project Libs | Không dùng context/migrations của mẫu: khác schema hiện tại và có thể sinh Identity tables mới. |
| SignalR | Hub nhận room ID, username, nước đi/chat/SDP từ client rồi phát lại | Thiếu kiểm tra membership/quyền/danh tính/luật cờ; không sao chép. |
| Trạng thái phòng | `static Dictionary<Guid, Room>` trong IMemoryCache; thread vòng lặp vô hạn mỗi phòng | Không dùng thread-per-room, dictionary không đồng bộ hoặc cache process-local làm trạng thái bền vững. Dùng use case + SQL; realtime chỉ phát trạng thái đã xác thực/lưu. |
| Controller/API | Bắt lỗi rồi trả HTTP 200; phân trang hardcode; room ID cố định | Giữ status/error semantics API hiện có; không biến lỗi thành response thành công hay tin định danh client. |
| Bàn cờ | Markup/CSS trong view, Vue logic một file; room/user cố định | Tạo board adapter từ BoardState/API payload; Vue chỉ hiển thị/chọn/lật, server/GameEngine xác thực luật. |

## Phân loại source theo giai đoạn

### Giữ nguyên

- `XiangqiOnline.sln`, projects Domain/Application/Infrastructure/Contracts/GameEngine và test projects.
- `AppDbContext`, entities, migrations, IDs, password hashes, refresh records, FK/index và API contracts.
- Auth/Users/Health API controllers, JWT validation, password/token services, middleware, CORS/rate-limit/security behavior.
- `frontend/xiangqi-web` và React sources/tests cho đến nghiệm thu.
- GameEngine C#, board/FEN contract và regression tests.

### Dự kiến chỉnh sửa sau khi được duyệt triển khai

- `src/XiangqiOnline.API/Program.cs`: thêm MVC/views, static files và conventional UI route; giữ API attribute routes, auth middleware, Swagger, health, CORS, rate limit và DI.
- `src/XiangqiOnline.API/XiangqiOnline.API.csproj` chỉ nếu thật sự cần package bổ sung; MVC có thể dùng ASP.NET Core shared framework.
- Cấu hình allowed origins và Vite proxy/build output để Origin validation cho refresh/logout chạy trên host mới.
- `.gitignore`, README/DEV_STATE và docs kiến trúc sau khi baseline Git và quy trình build asset được chốt.
- Vite config/scripts của Vue app riêng; output build dưới `wwwroot`.

### Dự kiến tạo

- MVC controllers Home, Account, Profile, Lobby, Room, Game; views `_Layout`, Home/Index, Account/Login/Register, Profile/Index, Lobby/Index, Room/Index, Game/Index, `_ViewImports`, `_ViewStart`.
- Vue 3/Vite app riêng với SFC, auth store/composables, API client, board adapter và Vue tests.
- `wwwroot` CSS/ảnh/icon/Vue bundle; MVC route/Razor tests và E2E smoke tests.
- Ba tài liệu `migration-audit.md`, `migration-plan.md`, `migration-rollback.md`.

### Chỉ cân nhắc loại bỏ sau nghiệm thu và hết rollback window

- `frontend/xiangqi-web`, React dependencies, Vite config và React-only assets. Không xóa trước acceptance criteria.
- Không xóa/đổi tên/di chuyển backend, GameEngine, migration, entity hoặc API endpoints trong đợt chuyển UI.

## Rủi ro và phụ thuộc

- Không có Git metadata nên thiếu baseline/rollback lặp lại; cần khôi phục hoặc thiết lập repository chính thức trước khi sửa app code.
- Không xác nhận backup/restore; không triển khai database migration hoặc cutover có khả năng mất dữ liệu trước khi xác nhận.
- JWT access token trong memory chỉ xác thực API calls từ Vue; không tự xác thực request tải Razor. Razor chỉ làm shell công khai; dữ liệu cá nhân phải qua API `[Authorize]`.
- Cookie refresh dùng `SameSite=Strict` và Origin allowlist; host MVC cần origin chính xác trong cấu hình. Không đổi JWT sang Identity cookie.
- Bài mẫu có connection string credential, concurrency/threading, auth/authorization và validation rủi ro; chỉ dùng làm tham khảo tổ chức.
- Không có SignalR hub thực tế để “giữ”; thêm realtime implementation thuộc phase riêng.

## Đối chiếu Jira

- `Jira (1).csv` có 366 records thuộc hai project: `SS` (61 tickets) và `SCRUM` (305 tickets). Dùng project `SS` làm scope Xiangqi hiện tại vì các epic/stories khớp plan của workspace: 5 epics, 13 stories; trong export tất cả ticket `SS` đang `To Do`. Hai ticket `In Progress` thuộc `SCRUM`, không được dùng làm bằng chứng tiến độ source này.
- Epic `SS-303`–`SS-307` lần lượt yêu cầu account/access, room/invitation, board/rules, game/replay/AI, realtime/chat/video. Nhiều story hiện là backlog chức năng chưa có trong code; chúng không được hiểu là đã hoàn thành chỉ vì có Razor placeholder.
- `SS-318` nêu bcrypt. Source hiện dùng `PasswordHasher<User>` (PBKDF2). Chuyển hash format trực tiếp sẽ làm hỏng login cũ; hướng an toàn là ghi bcrypt cho mật khẩu mới và duy trì dual verification cho hash cũ, không tự rewrite hash cũ.
- User yêu cầu giữ code chuyển đổi đơn giản theo mẫu: Vue 3 runtime local + một `app.js` dùng chung, Razor mount points riêng; không dùng Vite/SFC ở triển khai ban đầu. Đây là quyết định đơn giản hóa UI build, không phải lý do sao chép các lỗi room/realtime từ mẫu.

## Dependencies có nguy cơ bị ảnh hưởng

- Frontend hiện dùng React/React DOM 19, React Router 7, Zustand 5, Axios, TypeScript 7, Vite 8, Vitest và Oxlint. Tạo Vue app riêng để giữ nguyên lockfile/build/test của React trong giai đoạn đối chiếu; chỉ gỡ các dependency React sau acceptance.
- Backend dùng ASP.NET Core Web SDK/.NET 8, EF Core SQL Server 8.0.20, JwtBearer 8.0.20 và Swashbuckle. Chuyển UI không cần đổi package EF/auth.
- User Secrets chứa signing key và SQL connection config cho local Development; giữ nguyên, không copy sang config trong archive hay đưa vào Git.
- Config `Cors:AllowedOrigins`, Vite proxy, API base URL, static asset path và MVC route mapping cần phối hợp; sai Origin có thể làm refresh/logout trả 403 dù login/API khác vẫn hoạt động.
