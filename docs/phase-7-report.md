# Phase 7 — Human vs AI

## Trạng thái

Đã triển khai và kiểm chứng backend, frontend, SQL migration và luồng AI cơ bản trên SQL Server local. Repository hiện dùng React/Vite và API .NET 8 theo các tầng Domain/Application/Infrastructure/API; Phase 7 bám kiến trúc này thay cho cấu trúc Razor/Vue trong prompt cũ. Không tạo project/solution mới và không dùng Docker.

## Phần đã làm

- GameEngine: Minimax reference, Negamax Alpha-Beta, iterative deepening, fallback hợp lệ, node/time/cancellation budgets, move ordering, evaluation theo vật chất/vị trí/mobility/threats/chiếu, quiescence và Zobrist transposition table bounded 2^18 entry mặc định.
- Preset: Dễ depth 2 / 300 ms / 12.000 nodes và giảm lực có kiểm soát trong nhóm nước gần tốt nhất; Vừa depth 4 / 1.500 ms / 120.000 nodes; Khó depth 6 / 5.000 ms / 500.000 nodes. Budget có thể dừng trước target depth; nước fallback vẫn hợp lệ.
- Game service/API: tạo ván và chọn phe/độ khó, server kiểm luật nước người, AI mở đầu nếu người chọn Đen, history, reload, analysis status, resign và restart. Mọi thao tác chỉ chủ ván; không có tài khoản AI giả. AI được kiểm lại bằng `IXiangqiRules` trước commit.
- Worker: bounded channel 64, một search chạy đồng thời trên mỗi API process, lease/version để claim và commit, recovery các lượt Queued/lease hết hạn sau restart, retry tối đa hai lần. DB connection/transaction không được giữ khi tìm kiếm.
- Database: additive migration `20261101053848_Phase7HumanVsAi`; thêm metadata AI, lease/retry, result side và `IsAiMove`. Backup copy-only + checksum được `RESTORE VERIFYONLY` trước khi apply.
- UI: `/ai` chọn độ khó/phe; `/ai/:id` dùng lại `ChessBoard`, cập nhật từ REST polling, chỉ cho nước khi tới lượt người, hiển thị trạng thái suy nghĩ, chiếu Tướng, highlight nước mới nhất, history, đầu hàng và chơi lại.

## Kiểm thử đã chạy

- `dotnet build XiangqiOnline.sln -c Release --no-restore`: PASS, 0 warning/error.
- `.NET tests`: engine 29, unit 16, SQL Server integration 44 — tổng 89 PASS. Integration có tạo ván Đỏ/Đen, nước AI khai cuộc, kiểm luật nước người, lưu history AI/người, ownership, resign/restart và hai scoped processor tranh claim một AI turn; chỉ một AI move được lưu. Sau khi thêm assertion duplicate-worker, integration case AI được chạy lại riêng và PASS.
- `npm run build`: PASS.
- `npm run lint`: PASS, no warnings.
- `npm test -- --reporter=dot`: PASS, 10 test.
- `dotnet ef migrations has-pending-model-changes`: PASS, snapshot/model không lệch.
- SQL Server `XiangqiOnlineDB`: migration Phase 7 được ghi nhận trong `__EFMigrationsHistory`; kiểm tra cột `Games.AiTurnStatus` và `GameMoves.IsAiMove` thành công.

## Benchmark thực đo

Benchmark xUnit dùng Release, .NET runtime 8.0.20, CPU Intel Core i5-9300H 2.40 GHz (4 cores / 8 logical), độ sâu 2, cùng FEN cho bốn biến thể. Thời gian là thời gian test trên máy local; lần đo ngắn có thể bị ảnh hưởng bởi JIT/cache. RSS là process working set tại thời điểm đo, không phải phần tăng riêng của mỗi search.

| Vị trí | Biến thể | Nodes | ms | Nodes/s | Cutoffs | TT hits |
|---|---|---:|---:|---:|---:|---:|
| Khai cuộc | Minimax | 1.964 | 667 | 2.944 | 0 | 0 |
| Khai cuộc | Alpha-Beta | 1.380 | 181 | 7.624 | 42 | 0 |
| Khai cuộc | Alpha-Beta + ordering | 172 | 19 | 9.052 | 43 | 0 |
| Khai cuộc | Alpha-Beta + ordering + TT | 172 | 19 | 9.052 | 43 | 0 |
| Trung cuộc | Minimax | 1.112 | 107 | 10.392 | 0 | 0 |
| Trung cuộc | Alpha-Beta | 305 | 18 | 16.944 | 31 | 0 |
| Trung cuộc | Alpha-Beta + ordering | 105 | 18 | 5.833 | 34 | 0 |
| Trung cuộc | Alpha-Beta + ordering + TT | 105 | 10 | 10.500 | 34 | 0 |

Benchmark cũng chạy tàn cuộc, bên đang bị chiếu và vị trí có ăn quân, xác nhận nước trả về hợp lệ. Ở độ sâu 2 TT không có hit thực đo trong các vị trí này; lợi ích TT chưa được chứng minh bởi bộ benchmark hiện tại. Benchmark không đo riêng chi phí AI end-to-end theo độ khó.

## Jira và phần còn lại

CSV Jira gốc vẫn giữ trạng thái To Do; không có kết nối Jira được dùng để đổi issue. Các mục liên quan được đối chiếu là Epic `SS-306`, task `SS-308`, story `SS-351`, task thuật toán `SS-352`, task giao diện `SS-353`; các epic engine `SS-305` và work item luật `SS-335`–`SS-338` là nền dùng chung. File/kiểm thử hiện thực tương ứng được ghi ở các phần trên.

Chưa chạy load test 1/5/10 ván đồng thời, benchmark preset depth 4/6 trên nhiều máy, kiểm thử phục hồi bằng cách dừng/khởi động tiến trình trong lúc search, hoặc kiểm thử thao tác UI trực tiếp trong trình duyệt. Worker hiện xử lý một ván mỗi lần trên mỗi process; cơ chế lease/version chống claim trùng ở mức SQL nhưng chưa có test tải đa instance. Vì các mục này chưa được xác minh, chưa kết luận Phase 7 đã đạt toàn bộ Definition of Done trong prompt.

## Chạy trong Visual Studio 2022

1. Mở `XiangqiOnline.sln`; giữ SQL Server local đang chạy và connection string `XiangqiOnline` đã cấu hình.
2. Nếu database chưa được cập nhật, chạy `dotnet ef database update --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API`.
3. Cấu hình JWT qua User Secrets/environment như hướng dẫn `README.md`, rồi chạy API project.
4. Chạy giao diện bằng `cd frontend/xiangqi-web; npm install; npm run dev`, đăng nhập và mở mục **Đấu với máy**.
