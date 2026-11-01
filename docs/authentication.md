# Xác thực và quản lý người dùng

## Vòng đời phiên

1. `POST /api/v1/auth/register` tạo tài khoản; người dùng đăng nhập riêng sau khi đăng ký.
2. `POST /api/v1/auth/login` chấp nhận username hoặc email cùng mật khẩu. API trả access JWT 15 phút trong JSON và refresh token trong cookie `xiangqi_refresh`.
3. Frontend giữ access token trong bộ nhớ. Khi tải lại trang hoặc access token hết hạn, client gọi refresh bằng cookie HttpOnly rồi tải `/users/me`.
4. Mỗi lần refresh, server thu hồi token cũ và phát token mới. Hai request đồng thời có thể gặp replay trong cửa sổ 15 giây; reuse token cũ sau cửa sổ này sẽ thu hồi các phiên refresh đang hoạt động của tài khoản.
5. Logout thu hồi phiên hiện tại; logout-all và đổi mật khẩu thu hồi toàn bộ refresh sessions của tài khoản.

`rememberMe=false` dùng cookie phiên trình duyệt; `rememberMe=true` đặt thời hạn cookie 7 ngày. Thời hạn refresh token ở server là 7 ngày. Đổi mật khẩu không thu hồi access JWT đã phát; chúng tự hết hạn trong tối đa 15 phút.

## API người dùng

- `GET /api/v1/users/me`: đọc hồ sơ theo `sub` trong access JWT.
- `PATCH /api/v1/users/me`: cập nhật display name (2–80 ký tự).
- `PUT /api/v1/users/me/password`: xác minh mật khẩu hiện tại, đặt mật khẩu mới và thu hồi refresh sessions.

Username dài 3–64 ký tự, gồm chữ Unicode, số, `.`, `_`, `-`; email và normalized username/email có unique index. Mật khẩu dài 10–256 ký tự và chặn một danh sách nhỏ mật khẩu phổ biến.

## Chạy local

API cần `Jwt:SigningKeyBase64` trong User Secrets hoặc environment. Tạo khóa 64 byte ngẫu nhiên, không commit giá trị:

```powershell
$bytes = [byte[]]::new(64)
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:SigningKeyBase64" $key --project src/XiangqiOnline.API
Remove-Variable key, bytes
```

Frontend dùng Vite proxy tới API Development; đặt `Cors:AllowedOrigins` đúng origin của frontend. Xem [security.md](security.md) để biết cookie, CORS và giới hạn request.
