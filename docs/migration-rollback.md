# Kế hoạch rollback chuyển đổi UI

## Trạng thái hiện tại

Audit ngày 2026-10-31 không thay đổi code ứng dụng hay database. Workspace không có `.git`; chưa có backup commit/branch có thể checkout. Lịch sử SQL Server không ghi nhận full backup của local `XiangqiOnlineDB`, và chưa có bằng chứng backup ngoài máy đã restore thành công. Chưa xác nhận an toàn để thực hiện migration/cutover có thể mất dữ liệu.

## Trước khi triển khai

1. Khôi phục hoặc thiết lập Git repository chính thức có remote/baseline; kiểm tra `git status` sạch, tạo tag/branch baseline riêng cho migration UI. Không dựa vào Git branch trên workspace đang thiếu `.git`.
2. Tạo full SQL backup theo chính sách, lưu ngoài workspace và bảo vệ quyền truy cập; restore vào database cách ly, xác minh online, migration history, bảng/FK/index và row counts.
3. Ghi checksum/release source hiện tại, cách chạy React + API và tên config keys. Không ghi secret values vào Git.
4. Triển khai thử trên local/staging; không dùng migration tự động trên production.

## Rollback UI/API host

- Giữ `frontend/xiangqi-web` chạy độc lập xuyên suốt. React/Vite tiếp tục gọi cùng API `/api/v1`; đưa người dùng về React bằng cách phục hồi release frontend cũ hoặc chạy UI host cũ.
- Nếu MVC startup/route lỗi: redeploy binary/source trước MVC hoặc restore source từ baseline Git. Với trạng thái hiện tại cần tạo source backup ngoài workspace trước khi bắt đầu.
- Nếu ASP.NET Core host cùng phục vụ MVC, rollback UI bằng deployment artifact trước migration; không cần rollback DB khi chỉ thêm views/routes/static files.
- Giữ routes API, auth middleware, CORS, JWT config, User Secrets và database connection. Không đổi sang Identity cookie khi rollback.
- Giữ asset version cũ và React source cho tới khi kết thúc rollback window; không xóa React trước nghiệm thu.

## Rollback database

- Phương án UI không cần EF migration; rollback ứng dụng, không chạm DB.
- Không chạy `dotnet ef database update 0`, không xóa migration, không drop/recreate DB/bảng Users/RefreshTokens, không sửa UserId hoặc PasswordHash.
- Nếu phase sau được duyệt thêm schema: ưu tiên forward fix. Nếu bắt buộc rollback schema, dừng ghi dữ liệu, xác định migration target, kiểm tra `Down`/SQL script, thử trên bản restore riêng và đối chiếu counts/integrity. Production chỉ restore backup đã chứng minh khả năng restore theo quy trình DBA.
- Tránh migration `Down` xóa cột/bảng sau khi dữ liệu mới được ghi; `Down` không khôi phục giá trị đã xóa. Trước khi chuyển traffic từ bản restore, xác minh DB online, bảng, FK/index, migration history và dữ liệu.

## Điều kiện dừng hoặc rollback ngay

- Mismatch UserId/password login, refresh rotation, API contract, FK, migration history hoặc row count không giải thích được.
- Backup chưa restore thử; build/auth tests lỗi; route UI bắt nhầm `/api/v1/*`; refresh cookie/Origin bị từ chối; hoặc trang trả dữ liệu cá nhân trước khi API xác thực.
- Không thể đưa traffic về React/API release cũ trong thời hạn rollback mục tiêu.

## Nghiệm thu rollback

Ghi release phục hồi, health endpoint, auth integration, DB online/migration history/row counts, test results và thời gian hoàn tất. Chỉ xác nhận rollback thành công sau khi các kiểm tra đã thực sự pass.
