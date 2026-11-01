# Báo cáo triển khai Phase 3 – Bàn cờ và luật Cờ Tướng

Ngày kiểm tra: 2026-10-31. Phạm vi code bám Jira `SS-305` và giữ nguyên kiến trúc 3 tầng hiện tại. Các issue Jira không bị cập nhật trạng thái.

## Kết quả theo Jira

| Jira | Nội dung | Triển khai | Kết quả kiểm tra | Trạng thái Jira |
|---|---|---|---|---|
| `SS-332` | Bàn 9×10, 32 quân, sông, cung, highlight | Vue board trong `src/XiangqiOnline.API/wwwroot/js/app.js`; giao diện trong `src/XiangqiOnline.API/wwwroot/css/site.css` | Đã mở MVC homepage và kiểm tra trực quan vị trí 9×10, cung, sông, 32 quân | Không sửa; giữ nguyên export |
| `SS-333` | Component bàn cờ responsive | Board SVG và quân cờ nằm trên giao điểm; board co giãn theo viewport | Kiểm tra trực quan trên desktop; chưa ghi nhận UI test tự động/mobile browser | Không sửa; giữ nguyên export |
| `SS-334` | Chọn quân, lấy nước hợp lệ, chọn đích/bỏ chọn | Vue yêu cầu legal-moves từ API, highlight đích, gửi nước đi và chỉ cập nhật sau phản hồi server | API kiểm tra tích hợp; chưa thực hiện lượt đi tương tác qua browser có đăng nhập | Không sửa; giữ nguyên export |
| `SS-335` | Luật và trạng thái ván | `src/XiangqiOnline.GameEngine/XiangqiRules.cs`, `BoardState.cs`, `FenSerializer.cs`; dùng `MoveResult` không làm biến đổi board gốc | Bộ GameEngine 20/20; gồm chiếu, Tướng lộ mặt, không có nước hợp lệ | Không sửa; giữ nguyên export |
| `SS-336` | Tướng, Sĩ, Tượng | Quy tắc cung, đường chéo, biên sông và an toàn Tướng ở `XiangqiRules` | GameEngine tests pass | Không sửa; giữ nguyên export |
| `SS-337` | Xe, Pháo, Mã | Đường đi, quân chắn, ngòi pháo và chân Mã ở `XiangqiRules` | GameEngine tests pass | Không sửa; giữ nguyên export |
| `SS-338` | Tốt và ăn quân | Hướng tiến theo màu, qua sông mới được đi ngang; chặn ăn quân cùng màu | GameEngine tests pass | Không sửa; giữ nguyên export |

## Game local, API và dữ liệu

- `POST /api/v1/games/local`: tạo ván ban đầu từ server, gắn chủ sở hữu.
- `GET /api/v1/games/{id}` và `GET /api/v1/games/{id}/legal-moves`: tải trạng thái và nước hợp lệ.
- `POST /api/v1/games/local/move`: chủ sở hữu đi luân phiên cả Đỏ/Đen; server đọc FEN đang lưu, kiểm tra luật, version và idempotency key.
- `GET /api/v1/games/{id}/moves`: đọc lịch sử đã lưu.
- `POST /api/v1/games/{id}/restart`: tạo game mới, giữ nguyên game và lịch sử cũ.

Luồng nghiệp vụ nằm trong `GameplayService`; persistence nằm trong `EfGameplayRepository`; API không nhận FEN mới từ client và không trả EF entity. Phase này tái sử dụng `Game`, `GameMove`, `CurrentFen`, `CurrentTurn` và `Version` đang có. Không thêm migration, không chạy migration lên database chính và không xóa database.

## Engine và giới hạn luật

Engine kiểm tra bảy loại quân, quân chắn, ăn quân đối phương, cung, sông, chân Mã, mắt Tượng, pháo cần đúng một ngòi khi ăn, chiếu, Tướng lộ mặt, chiếu bí và hết nước hợp lệ. Khi bên đến lượt không có nước đi hợp lệ, bên đó thua kể cả không bị chiếu.

Lặp lại thế cờ, chiếu liên tục, đuổi quân liên tục, giới hạn nước đi và quy tắc hòa theo từng giải chưa được triển khai. FEN lưu vị trí quân và bên đi; không thay thế lịch sử ván.

## Kiểm tra đã chạy

- `dotnet test XiangqiOnline.sln -c Release --no-restore`: **59/59 pass** — 11 unit, 20 GameEngine, 28 integration.
- `dotnet build XiangqiOnline.sln -c Release --no-restore`: pass, 0 warning, 0 error.
- Integration tests chạy với SQL test fixture cách ly; fixture tự dọn database sau test. Có coverage tạo local game, lượt hai bên, lịch sử, quyền chủ sở hữu, restart và hai request đồng thời cùng version.
- `node --check src/XiangqiOnline.API/wwwroot/js/app.js`: pass.
- MVC homepage tại `http://127.0.0.1:5080/` đã được mở để xem board. Không đăng nhập hoặc gửi nước đi qua browser. Phiên smoke test dùng cấu hình database không thể kết nối, không ghi dữ liệu.
- Không thực hiện Git/GitHub; workspace hiện không có metadata `.git`.

## Chạy trong Visual Studio 2022

1. Mở `XiangqiOnline.sln`, cấu hình User Secrets theo hướng dẫn trong `README.md`, dùng database/schema đã có.
2. Đặt `XiangqiOnline.API` làm Startup Project và chạy bằng F5.
3. Đăng nhập trên MVC, vào Lobby, chọn **Chơi hai người tại máy này**; thay phiên điều khiển Đỏ và Đen trên cùng giao diện.
4. Dùng nút chơi lại để mở ván mới. Lịch sử ván cũ vẫn được lưu.

## Mức sẵn sàng

Phase 3 có implementation backend, board UI và kiểm tra unit/integration. Trước khi xem đây là nghiệm thu đầy đủ trên máy người dùng, cần đăng nhập và thao tác ván local trực tiếp trong browser với SQL Server cấu hình thật. Jira vẫn giữ nguyên trạng thái gốc. Hướng Phase 4 có thể tiếp tục từ game/history API; realtime hoặc AI vẫn là các phạm vi riêng.
