# Kế hoạch chuyển đổi UI sang MVC + Razor + Vue

## Mục tiêu và ranh giới

- Host giao diện bằng ASP.NET Core MVC/Razor trong API project hiện tại; Vue 3 global runtime chạy trực tiếp từ asset đã vendored trong `wwwroot`.
- Giữ solution 3 tầng, API `/api/v1`, contracts, controllers API, JWT/refresh tokens, database/schema/migrations, GameEngine và React frontend trong suốt thời gian chuyển đổi.
- Không chuyển sang Identity cookie. Tính năng nghiệp vụ chỉ được triển khai theo Jira `SS` và từng acceptance; không chạy migration lên database chính trong đợt này.
- Trước khi thay app code: tạo/khôi phục baseline Git và có SQL backup đã restore thử như yêu cầu trong [migration-audit.md](migration-audit.md).

## Kiến trúc đích

```text
Browser
  MVC GET routes -> ASP.NET Core MVC -> Razor shell + Vue mount points
  Vue bundles    -> wwwroot
  Vue API calls  -> existing /api/v1 controllers (JWT bearer + refresh HttpOnly cookie)
                                      -> Application -> Infrastructure/EF Core -> existing SQL Server
```

`Program.cs` đăng ký MVC views (`AddControllersWithViews`) và giữ cấu hình validation API; bật static files, conventional UI route và tiếp tục map attribute API controllers. Không gắn React và Vue lên cùng DOM subtree. Vite dev server chỉ phục vụ quá trình phát triển; acceptance build phải tạo asset do MVC host phục vụ.

Theo yêu cầu code gọn dựa trên bài mẫu, dùng Vue 3 global runtime đã pin phiên bản và lưu local tại `wwwroot/lib/vue`; logic giao diện ở một file `wwwroot/js/app.js`, Razor views chỉ có mount point và `data-page`. Không thêm Vite, SFC hay package manager thứ hai. React vẫn nằm riêng để rollback/đối chiếu. Không dùng URL CDN không pin phiên bản.

## Session và bảo mật UI

- Vue gọi endpoint hiện tại bằng relative `/api/v1`; giữ access JWT trong biến JavaScript memory của app, không dùng localStorage/sessionStorage.
- Refresh token tiếp tục là cookie HttpOnly. Refresh được gọi qua endpoint hiện có; kiểm thử luồng refresh/cookie trước khi cutover UI.
- Xác thực hiện tại là **client-side**. Razor chỉ render shell công khai; không nhúng dữ liệu hồ sơ/phòng vào HTML trước khi Vue gọi API. API authorization bảo vệ dữ liệu.
- Refresh/logout yêu cầu Origin khớp allowlist. Khi host MVC, thêm origin chính xác của MVC development/production vào `Cors:AllowedOrigins` nếu endpoint validation dùng allowlist đó. Không nới wildcard.
- UI forms gửi JSON tới API và hiển thị loading/validation/error. Bất kỳ MVC POST/antiforgery form nào phát sinh sau này phải dùng antiforgery; không chuyển refresh cookie thành auth cookie.
- Không tạo `[Authorize]` Razor route dựa trên access token memory. Nếu bắt buộc server-side Razor authorization, cần thiết kế/phê duyệt một cơ chế tương thích riêng trước khi triển khai.

## Phạm vi Jira và chuyển theo từng trang

CSV `Jira (1).csv` có hai project key. Với dự án Xiangqi hiện tại dùng 61 ticket `SS` làm backlog chính (5 epics, 13 stories, các task con; tất cả đang `To Do` trong bản export). Các 305 issue `SCRUM` là backlog khác/legacy; không trộn trạng thái hoặc tự coi đã xong. Functional acceptance lấy từ descriptions của stories `SS-303` đến `SS-307`, còn kiến trúc host/client theo yêu cầu chuyển MVC + Vue.

| Epic Jira | Phạm vi | Điều kiện chính |
|---|---|---|
| `SS-303` Tài khoản và truy cập | Đăng ký, đăng nhập, session, lobby/logout | Form + API thật, route protection phía client, login giữ username khi sai; bcrypt cho hash mới, legacy hash tiếp tục đăng nhập. |
| `SS-304` Phòng chơi và lời mời | Tạo room, mã 6 ký tự, join, chờ/copy link | Unique code, trim/uppercase, 2 player max, lỗi not found/full, state lưu SQL. |
| `SS-305` Bàn cờ và luật | 9×10, 32 quân, chọn/highlight, validate 7 loại | Engine C# là nguồn luật; chặn ăn quân mình và Tướng lộ mặt. |
| `SS-306` Quản lý ván và AI | Lưu/replay, bot | Lưu lịch sử; AI chỉ sau khi legal-move engine có tests. |
| `SS-307` Thi đấu realtime/tương tác | Nước đi, lượt, reconnect 60s, chat, WebRTC | Server kiểm tra membership/lượt/luật trước broadcast; không sao chép Hub thiếu auth hoặc thread/cache trong bài mẫu. |

Triển khai theo dependency; từng epic giữ trạng thái chưa hoàn tất nếu thiếu backend, tests hoặc UI acceptance. Không tuyên bố full Jira hoàn tất khi các stories AI/WebRTC/realtime còn chưa làm.

## Chuyển theo từng trang

Mỗi lát cắt: đối chiếu acceptance ticket; cập nhật Razor page và Vue template; nối cùng API; kiểm tra route/API/error; ghi rõ các giới hạn chưa làm. React vẫn là phương án rollback.

| Thứ tự | MVC view/route dự kiến | Vue module | Kiểm tra trọng tâm |
|---|---|---|---|
| 1 | `Home/Index` — `/` | `HomePage.vue` | Nội dung, navigation, board image, link-safe. |
| 2 | `Account/Login` — `/Account/Login` | `LoginForm.vue` | Username/email, remember-me, credentials/rate-limit error, loading. |
| 3 | `Account/Register` — `/Account/Register` | `RegisterForm.vue` | Client/server validation, duplicate identity, success rồi chuyển Login. |
| 4 | `Profile/Index` — `/Profile/Index` | `ProfilePage.vue` | Refresh/bootstrap, `GET/PATCH /users/me`, password change, logout/logout-all. |
| 5 | `Lobby/Index` — `/Lobby/Index` | Template `lobby` trong `app.js` | Hồ sơ/thống kê thật; tạo phòng và nhập mã qua API. |
| 6 | `Room/Index/{id?}` | Template `room` trong `app.js` | Hiển thị thành viên/trạng thái từ API; kiểm soát quyền thành viên. |
| 7 | `Game/Index/{id?}` | Game template và board component trong `wwwroot/js/app.js` | Đã có 9×10, 32 quân, cung/sông, responsive, chọn/đi quân qua API; không có luật phía client. |

Routes UI dùng controller/action conventional hoặc explicit route; giữ API controllers attribute-routed dưới `/api/v1`. Thêm test xác nhận không xung đột route: `/`, `/Account/*`, `/Profile/*`, `/Lobby/*`, `/Room/*`, `/Game/*` và `/api/v1/*`.

## Board adapter

- Viết adapter có test chuyển `BoardState`/API payload sang kiểu Vue; giữ quy ước file 0–8, rank 0–9, màu và loại quân.
- Tái tạo SVG intersections, cung Tướng, sông và marker đúng 9 cột × 10 hàng, danh sách 32 quân khai cuộc.
- Flip chỉ đổi mapping hiển thị; model/serialization nguồn không đổi. Test orientation và viewport hẹp.
- Vue chỉ chọn/highlight quân trong đợt chuyển UI. Không viết luật cờ trong browser; `XiangqiRules` phía server đã kiểm tra legal moves; API trả các ô đích hợp lệ và lưu nước đi theo `expectedVersion` để tránh ghi đè khi đồng thời.

## Giai đoạn và cổng nghiệm thu

0. **Safety gate:** xác nhận repository Git sạch/backup source; tạo SQL full backup và restore vào DB cách ly; ghi baseline 4 migrations, row counts và API/test. Không chạy DB update.
1. **Host:** cấu hình MVC/static files/routing trong API host, thêm MVC smoke tests; chạy `/api/v1/health`, auth integration và Swagger; xác nhận API contracts không đổi.
2. **Vue shell:** thêm Vue global runtime local, Razor layout và test route/assets; không cần Vite cho giao diện MVC đơn giản này. Giữ React.
3. **Trang 1–3:** Home, Login, Register; mỗi trang có render/browser tests, API integration cùng `/api/v1`, error và redirect tests.
4. **Trang 4:** Profile; kiểm tra refresh cookie, hồ sơ, đổi mật khẩu và logout trên DB test cách ly.
5. **Trang 5–7:** Lobby, Room, Game board nối API; kiểm tra 32 quân, orientation, lượt chơi và viewport.
6. **Cutover thử:** chạy ASP.NET MVC làm UI entry point trong Development/staging trong khi React source vẫn giữ; kiểm thử route, auth, API, responsive và .NET regressions.
7. **Nghiệm thu:** xác nhận tất cả tiêu chí dưới; lưu release/React baseline và rollback window. Chỉ sau phê duyệt riêng mới đề xuất xóa React.

Không tạo EF migration cho UI. Nếu phát hiện nhu cầu schema, dừng lát cắt đó và review riêng thiết kế + SQL script additive/non-destructive.

## Acceptance criteria trước khi loại React

- Cả 7 trang render từ MVC, Vue mount đúng; React được giữ riêng trong rollback window.
- Register/login/refresh/logout/logout-all/me/profile/password vẫn qua API hiện có; cookie/token/Origin behaviors không suy giảm.
- GameEngine, API contracts, DB schema, migration history, IDs/password hashes/refresh records và tests được bảo toàn.
- JavaScript syntax check và solution restore/build/test pass; MVC route/API integration tests pass; browser comparison/responsive checks cần được xác minh trước nghiệm thu.
- DB integrity, backup restore và rollback diễn tập trên môi trường kiểm thử được ghi lại.
- Có source Git baseline, UI release cũ và React app trong suốt rollback window.

## Bằng chứng

Mỗi giai đoạn ghi commit/hash, files changed, build/test output, routes/endpoints đã kiểm tra và viewport/screenshot vào `DEV_STATE.md`. Không báo pass cho browser/UI/database/backup khi chưa có bằng chứng.


## Tiến độ triển khai Phase 3 (2026-10-31)

- Đã thêm MVC/Razor routes và Vue local-runtime shell; React chưa bị xóa.
- Đã nối đăng ký/đăng nhập/profile/stats và các page controller đã có.
- Phase 3 baseline khi đó có các route tạo/join/đọc room; vòng đời waiting/ready/start được hoàn thiện trong Phase 4 bên dưới.
- Jira `SS-305` Phase 3 đã có `XiangqiRules` phía server cho 7 quân, cung/sông, chặn ngựa/tượng, pháo, chiếu/Tướng lộ mặt, chiếu bí và xử lý thua khi hết nước hợp lệ. Vue game đọc FEN, lấy nước đi hợp lệ từ API và gửi nước đi đã chọn.
- Chế độ local tạo ván gắn với chủ sở hữu, luân phiên hai bên, lưu `GameMoves`, truy vấn lịch sử và restart sang ván mới. Optimistic version và idempotency key ngăn cập nhật trùng/cũ; kiểm thử tích hợp bao gồm hai request đồng thời.
- Kiểm thử tại thời điểm baseline Phase 3: 11 unit + 20 GameEngine + 28 integration = 59/59 pass; `node --check` pass. Phase 4 bổ sung migration cho room lifecycle/invitations.
- Đã xem MVC homepage trên trình duyệt và kiểm tra bàn 9×10, sông, cung, 32 quân. Chưa thực hiện nước đi qua trình duyệt với tài khoản đăng nhập; không coi Jira được Done. Các mục tiếp theo ngoài Phase 3: lời mời, replay navigation, AI, SignalR/reconnect/chat/WebRTC và cutover.
- Safety tại thời điểm baseline Phase 3 (2026-10-31): workspace chưa có `.git`; có source snapshot ZIP ngoài workspace; chưa xác nhận backup/restore nên migration chưa chạy. Phase 4 sau đó được áp dụng ngày 2026-11-01 sau full backup đã verify; xem tiến độ Phase 4 bên dưới.

## Tiến độ Phase 4 (2026-11-01)

- Đã bổ sung lobby public/private, search/filter/paging, join bằng code hoặc phòng public, room side/ready/leave/host transfer/start, và invitation SQL-backed với expiry/accept/decline.
- Room chỉ bắt đầu sau khi có hai người ở hai phe khác nhau cùng Ready; chủ phòng duy nhất có thể start. Join/ready/side/start/accept dùng transaction, Room `Version` và SQL row locks để xử lý request đồng thời.
- Hub xác thực tại `/hubs/rooms`; quyền thành viên được kiểm tra lại khi JoinRoomGroup/RequestRoomSync. Group event chỉ mang room ID/version; state đầy đủ tải qua REST. Lobby nhận lời mời realtime và inbox vẫn đọc được khi offline.
- Migration additive `Phase4RoomLobbyInvitations` đã áp dụng trên `XiangqiOnlineDB` local ngày 2026-11-01 sau full backup; backup checksum verify, migration history/schema đã kiểm tra. Integration tests tiếp tục dùng database cách ly.
- Báo cáo implementation, Jira và test tại [phase-4-report.md](phase-4-report.md).
