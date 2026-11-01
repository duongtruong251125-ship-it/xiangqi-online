# Deploy Xiangqi Online công khai bằng Cloudflare Tunnel trên Windows

Hướng dẫn này dùng Quick Tunnel để cấp URL HTTPS tạm thời, không mở cổng router/Windows Firewall và không đưa SQL Server ra Internet. Kestrel và SQL Server vẫn chạy trên máy Windows; dừng máy, ASP.NET Core, SQL Server hoặc cloudflared thì website sẽ offline.

## Kết quả audit trên máy hiện tại

- Startup project: `src/XiangqiOnline.API/XiangqiOnline.API.csproj`; solution target .NET 8.
- HTTP port trong `Properties/launchSettings.json`: `5080`; tunnel chỉ kết nối `127.0.0.1`.
- Health endpoint hiện trả `Healthy` cho SQL Server.
- Connection string đang dùng `Server=.;Database=XiangqiOnlineDB;Trusted_Connection=True;TrustServerCertificate=True`; database `XiangqiOnlineDB` đang `ONLINE`.
- Máy hiện có dịch vụ `MSSQLSERVER`, và `SERVERPROPERTY('Edition')` báo **Developer Edition**, không phải Express. Không đổi instance, không chạy migration, không xóa hoặc di chuyển dữ liệu trong quá trình deploy.
- JWT signing key đã có trong .NET User Secrets. Script local lấy key vào môi trường của tiến trình Production, không ghi key ra file hoặc log.
- UI gọi API và SignalR bằng đường dẫn tương đối (`/api/v1`, `/hubs/rooms`, `/hubs/game`). Hub yêu cầu JWT. RTC dùng STUN Google; chưa cấu hình TURN.
- CORS chỉ cho các origin frontend tách biệt đã cấu hình. Refresh-cookie cho phép origin cấu hình hoặc đúng same-origin hiện tại; host filtering chỉ nhận localhost và `*.trycloudflare.com`.
- SignalR hubs: `/hubs/rooms`, `/hubs/game`. WebSocket được Cloudflare hỗ trợ; cần xác minh bằng phiên đã đăng nhập, không kết luận chỉ từ `negotiate`.

## Điều kiện và lưu ý

- .NET 8 SDK/runtime, SQL Server đang chạy, database online, JWT User Secret đã cấu hình.
- Cài `cloudflared` từ nguồn Cloudflare bằng `winget install --id Cloudflare.cloudflared -e --scope user`. Nếu trình cài đặt yêu cầu Administrator, dừng và nhờ chủ máy cài; không tắt firewall.
- Quick Tunnel không cần tài khoản Cloudflare hoặc tên miền. URL có dạng `https://<random>.trycloudflare.com`, có thể đổi sau khi khởi động lại tunnel.
- Đây là tunnel thử nghiệm/demo. URL công khai cho phép người Internet truy cập website; chỉ dùng tài khoản test, không chia sẻ khóa, và dừng tunnel khi demo xong.
- Không cần mở cổng 1433, không cần router port forwarding. Không chạy migration tự động.
- STUN-only WebRTC có thể không kết nối được qua một số NAT/firewall; khi đó cần TURN server có thông tin xác thực an toàn. Cloudflare Tunnel không phải TURN server.

## Chạy lại

Mở PowerShell tại thư mục repository:

```powershell
.\scripts\deploy\check-prerequisites.ps1
.\scripts\deploy\build-release.ps1
.\scripts\deploy\start-local.ps1
```

`start-local.ps1` chạy bản Release trong môi trường Production, bind `127.0.0.1` và chờ health check SQL. Nếu port 5080 đang được Visual Studio sử dụng, script không dừng tiến trình đó; chọn port khác, ví dụ:

```powershell
.\scripts\deploy\start-local.ps1 -Port 5081
```

Mở PowerShell thứ hai và dùng cùng port:

```powershell
.\scripts\deploy\start-tunnel.ps1 -Port 5081
```

Script chỉ mở tunnel sau khi local `/api/v1/health` healthy và xác nhận Swagger Development không mở. Nó hiển thị URL Quick Tunnel thật; không tự dựng URL. Chia sẻ URL HTTPS đó với người thử.

Kiểm tra từ máy chạy ứng dụng:

```powershell
.\scripts\deploy\verify-public.ps1 -PublicUrl "https://<URL-QUICK-TUNNEL>"
```

Script kiểm tra HTTPS, trang chủ, Vue/JavaScript/CSS, health SQL và các phản hồi 401 mong đợi từ API/SignalR endpoints cần đăng nhập. 401 ở bước kiểm tra truy cập nặc danh là kết quả bảo mật mong đợi. Bài kiểm tra này không xác nhận WebSocket đã kết nối bằng token.

## Kiểm thử trên trình duyệt

1. Mở URL HTTPS public trên thiết bị test; đăng ký/đăng nhập tài khoản test.
2. Với hai người chơi, dùng hai trình duyệt/thiết bị và hai tài khoản test; tạo phòng, tham gia, ready, bắt đầu và đi quân qua lại.
3. Mở DevTools → Network → WS, xác nhận `/hubs/rooms` hoặc `/hubs/game` nâng cấp thành WebSocket `101` và trạng thái kết nối ổn định.
4. Thử chat, reload/reconnect và AI. Không dùng tài khoản thật hoặc chia sẻ dữ liệu cá nhân trong demo.
5. Thử video/audio trên hai client và ghi lại trạng thái ICE. STUN có thể không đủ nếu NAT/firewall chặn P2P; chưa có TURN nên không cam kết mọi mạng.

Nếu chưa có thiết bị hoặc tài khoản test thứ hai, các bước multiplayer/video được ghi là **BLOCKED**, không đánh dấu PASS.

## Dừng

```powershell
.\scripts\deploy\stop-tunnel.ps1
.\scripts\deploy\stop-local.ps1
```

Các script chỉ dừng PID được chính chúng tạo, sau khi xác minh executable path và thời điểm khởi chạy. Log/PID được lưu trong `%LOCALAPPDATA%\XiangqiOnline\deploy`, không nằm trong Git.

Máy phải không Sleep và SQL Server phải tiếp tục chạy trong lúc demo. Không thay đổi Power Plan tự động; cấu hình lại thủ công sau khi cân nhắc bảo mật.

## Named Tunnel (tùy chọn)

Quick Tunnel URL là tạm thời. Để dùng hostname cố định cần tài khoản Cloudflare và tên miền phù hợp; cấu hình Named Tunnel/Windows Service chỉ thực hiện khi chủ máy chọn, không tự đăng ký tài khoản hoặc mua domain.

## Ghi nhận kiểm tra lần deploy này

Ngày 01-11-2026: Release build PASS; 94 .NET tests PASS; PowerShell syntax check PASS; local Production trả SQL health `Healthy`; Swagger tắt; Host lạ bị chặn; HTTPS forwarded từ loopback có HSTS. Quick Tunnel cấp URL thật và trang chủ, login, register, Vue, JavaScript, CSS, health và API bảo vệ đã được kiểm tra qua HTTPS. URL thay đổi theo từng Quick Tunnel nên không lưu cố định trong tài liệu hoặc source.

Chưa xác minh bằng browser thật: đăng nhập với test account, hai-client SignalR WebSocket có JWT, nước cờ/reconnect/chat đồng thời, và WebRTC audio/video. Các mục này cần hai tài khoản/clients; STUN-only cũng có thể bị NAT/firewall giới hạn. Không tạo tài khoản hay ván đấu trên database hiện tại trong lượt kiểm tra này.

## Lỗi thường gặp

- **Local health thất bại:** kiểm tra `MSSQLSERVER`, database `XiangqiOnlineDB`, Windows account chạy ASP.NET Core và log trong `%LOCALAPPDATA%\XiangqiOnline\deploy`.
- **Port đang sử dụng:** không kill process lạ; dùng `-Port 5081` cho cả start-local và start-tunnel.
- **Tunnel không cấp URL:** kiểm tra `cloudflared` và kết nối Internet; xem `tunnel.err.log`. Chưa có URL thật thì chưa public.
- **Redirect lặp/HTTP thay vì HTTPS:** middleware tin `X-Forwarded-For` và `X-Forwarded-Proto` từ loopback duy nhất với giới hạn một hop; không bật `ASPNETCORE_FORWARDEDHEADERS_ENABLED` toàn cục.
- **Refresh trả `CSRF_ORIGIN_INVALID`:** kiểm tra `Request.Scheme` đã là `https` sau forwarded headers, `Host` kết thúc bằng `.trycloudflare.com`, và URL mở là HTTPS.
- **SignalR disconnect:** kiểm tra hub mapping, JWT, WSS trong Network tab và tiến trình tunnel. Cloudflare hỗ trợ WebSockets; chỉ negotiate 401 nặc danh không chứng minh phiên xác thực hoạt động.
- **Video ICE failed:** kiểm tra quyền camera/microphone, HTTPS và NAT. STUN-only không thay thế TURN.
