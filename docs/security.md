# Bảo mật Phase 2

- Mật khẩu được băm bằng ASP.NET Core Identity `PasswordHasher<User>`; giá trị thô không lưu.
- Access JWT được ký bằng khóa đối xứng Base64 tối thiểu 256 bit; kiểm tra chữ ký, issuer, audience và hạn dùng. Khóa lấy từ User Secrets hoặc environment, không đặt trong source/config mẫu.
- Refresh token là chuỗi ngẫu nhiên; SQL chỉ lưu SHA-256 hash. Refresh xoay token trong transaction Serializable. Reuse sau thời gian grace 15 giây thu hồi refresh sessions còn hoạt động của tài khoản.
- Cookie refresh dùng `HttpOnly`, `SameSite=Strict`, path `/api/v1/auth`, thời hạn 7 ngày khi “nhớ đăng nhập” được chọn; `Secure` bật ngoài Development.
- Refresh/logout yêu cầu Origin khớp chính xác `Cors:AllowedOrigins`. CORS chỉ cho origin cấu hình và credentials.
- Rate limit theo IP mặc định: login 5/phút, register 5/10 phút, refresh 10/phút. Có thể đặt `RateLimits:AuthLoginPerMinute`, `RateLimits:AuthRegisterPer10Minutes`, `RateLimits:AuthRefreshPerMinute` qua environment.
- Lỗi đăng nhập không phân biệt username/email không tồn tại với mật khẩu sai. DTO không nhận user id để quyết định danh tính.

## Trước production

Đặt HTTPS, issuer/audience/allowed origins production riêng; dùng secret store/rotation cho signing key; cấu hình rate limit phù hợp với proxy và IP forwarding tin cậy; bật giám sát đăng nhập, cảnh báo replay và chính sách backup/retention. Xác minh header Origin qua reverse proxy trước khi triển khai.
