# EAIMS — Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp

Backend ASP.NET Core Web API (.NET 9, 3 lớp Controller–Service–Repository, EF Core + MySQL 8) và frontend HTML/CSS/JS thuần (Bootstrap 5).

## Yêu cầu

- .NET SDK 9
- MySQL 8.0
- Công cụ EF Core: `dotnet tool install --global dotnet-ef`
- Một static server bất kỳ cho `frontend/` (ví dụ VS Code Live Server, cổng 5500)
  - Đặt `Email:FrontendBaseUrl` (trong `appsettings.Development.json`) khớp cách bạn serve: serve riêng thư mục `frontend/` → `http://localhost:5500`; mở Live Server ở gốc repo → `http://localhost:5500/frontend`. Sai giá trị này thì nút trong email kích hoạt báo "Cannot GET /pages/activate-account.html".

## Cài đặt backend

1. Tạo file cấu hình riêng (file này bị `.gitignore`, **không bao giờ commit**):

   ```bash
   cp backend/ITAM.API/appsettings.Development.json.example backend/ITAM.API/appsettings.Development.json
   ```

   Sửa `ConnectionStrings:DefaultConnection` cho đúng MySQL của bạn và đặt `JwtSettings:Secret` là chuỗi ngẫu nhiên **từ 32 byte trở lên** (ứng dụng từ chối khởi động nếu ngắn hơn).
   Ở môi trường thật cấp các giá trị này qua biến môi trường (`ConnectionStrings__DefaultConnection`, `JwtSettings__Secret`...) hoặc secret manager.

2. Tạo/cập nhật schema:

   ```bash
   cd backend
   dotnet ef database update --project ITAM.API
   ```

3. Chạy API (mặc định `http://localhost:5140`, Swagger ở `/swagger`):

   ```bash
   dotnet run --project ITAM.API
   ```

   Ở môi trường Development, ứng dụng tự nạp dữ liệu mẫu (vai trò, phòng ban, tài khoản demo, tài sản...). Tài khoản quản trị mẫu: `admin@gmail.com` (mật khẩu xem trong `Data/DbSeeder.cs`). Email mẫu giữ tên cũ, đổi đuôi thành `@gmail.com` (vd `admin@gmail.com`, `manager.it@gmail.com`). Lưu ý: đây là địa chỉ Gmail thật có thể thuộc người khác — không bấm “Gửi lại email” cho tài khoản mẫu nếu đã bật SMTP thật.  (vai trò Employee, mật khẩu `Demo@123`): `nv0001@gmail.com`, `nv0007@gmail.com`, `nv0014@gmail.com`. **Dữ liệu mẫu chỉ có ở Development**; môi trường khác phải tự tạo tài khoản Admin IT đầu tiên.

## Triển khai môi trường thật (Production)

Dữ liệu mẫu **chỉ** nạp ở Development. Ở môi trường khác cần cấu hình qua biến môi trường (tên dùng `__` thay cho `:`):

| Biến | Bắt buộc | Ý nghĩa |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Có | Chuỗi kết nối MySQL (đã chạy `dotnet ef database update`). |
| `JwtSettings__Secret`, `__Issuer`, `__Audience`, `__ExpiryMinutes` | Có | Cấu hình JWT; `Secret` tối thiểu 32 byte, ngẫu nhiên. |
| `Cors__AllowedOrigins__0`, `__1`... | Có | Origin của frontend. |
| `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` | Lần đầu | Tạo **Admin IT đầu tiên** (kèm 3 vai trò và phòng ban nếu thiếu). Mật khẩu phải đạt chính sách (≥ 8 ký tự, có chữ và số). Tuỳ chọn: `Bootstrap__AdminFullName`, `Bootstrap__DepartmentName`. |

Tài khoản Bootstrap buộc đổi mật khẩu ở lần đăng nhập đầu. Nếu đã có Admin IT đang hoạt động thì cấu hình Bootstrap bị bỏ qua (không bao giờ ghi đè) — **hãy xoá các biến `Bootstrap__*` sau khi đăng nhập thành công**.

Lưu ý vận hành:

- **Chống dò mật khẩu:** sai 5 lần với cùng (IP, email) bị chặn 5 phút (HTTP 429, có `Retry-After`); thêm giới hạn 30 request/phút theo IP cho endpoint xác thực. Bộ đếm nằm trong bộ nhớ của một instance — chạy nhiều instance cần chuyển sang kho dùng chung (Redis/DB).
- **Đứng sau reverse proxy/load balancer:** cần bật Forwarded Headers (`UseForwardedHeaders`) để API thấy IP thật của client; nếu không mọi người cùng chung IP của proxy và bị tính chung giới hạn.

## Chạy frontend

Phục vụ thư mục `frontend/` bằng static server rồi mở `pages/login.html`. Địa chỉ API cấu hình ở `frontend/shared/api-client.js`; origin của frontend phải nằm trong `Cors:AllowedOrigins`.

## Kiểm thử

```bash
cd backend
dotnet build
dotnet test
```

Collection Postman đầy đủ endpoint ở `docs/postman_collection.json`; sơ đồ cơ sở dữ liệu ở `docs/itam.dbml` (cập nhật cùng mỗi migration).

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `backend/ITAM.API` | Controllers, Services, Repositories, Models (Entities/DTOs), Validators, Data (DbContext, Migrations, Seeder) |
| `backend/ITAM.Tests` | Unit test (xUnit + Moq) |
| `frontend/` | Trang HTML, JS từng trang (`assets/js`), thành phần dùng chung (`shared/`) |
| `docs/` | ERD (`erd.png`), mã DBML (`itam.dbml`), collection Postman (`postman_collection.json`) |

## Quy ước làm việc

- Mỗi phần việc là một Pull Request riêng, có người còn lại review thật sự trước khi merge; không push thẳng vào `main`.
- Commit theo Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`...).
- Đổi schema: kiểm tra file migration sinh ra chỉ chứa đúng thay đổi dự kiến và cập nhật `docs/itam.dbml` trong cùng PR.

## Phân quyền

Quyền hạn theo ma trận **vai trò × chức năng**, lưu trong bảng `RolePermissions` và chỉnh được ở trang **Phân quyền** (chỉ Admin IT). Endpoint kiểm tra bằng `[HasPermission(Permissions.X)]`; danh mục quyền nằm ở `Authorization/PermissionCatalog.cs` (khi thêm quyền mới phải khai báo ở đó và dùng ở ít nhất một endpoint — có test kiểm tra). Mặc định giữ đúng phân quyền cố định trước đây. Quyền hệ thống (quản lý người dùng, phân quyền, nhật ký) chỉ dành cho Admin IT và không gỡ được; vai trò Nhân viên bị khoá. Phạm vi dữ liệu (phòng ban, phiếu được phân công, tài sản được cấp) vẫn cố định theo vai trò. Sau khi lấy code mới cần chạy `dotnet ef database update`; ma trận mặc định tự tạo khi API khởi động.

## Tài khoản & mật khẩu (không có mật khẩu mặc định)

Hệ thống **không có mật khẩu mặc định**. Mọi tài khoản mới (Admin IT, Manager, Technician, Nhân viên) tạo ở **Quản lý người dùng** đều đi qua cùng một luồng: Admin chọn một **nhân viên có sẵn** + vai trò → tạo tài khoản → hệ thống tạo liên kết kích hoạt (băm SHA-256, dùng một lần, hết hạn sau `Email:ActivationValidDays` = 3 ngày) → gửi email → người dùng bấm "Kích hoạt tài khoản" (`pages/activate-account.html`) → **tự đặt mật khẩu** (8–72 ký tự, gồm chữ và số) → tài khoản dùng được. Admin không đặt và không biết mật khẩu của ai; tài khoản chưa kích hoạt không đăng nhập được (cả token cũ cũng bị chặn).

- **Mọi tài khoản gắn 1-1 với một hồ sơ trong Danh mục nhân viên** (đang hoạt động, đã có email, chưa có tài khoản); họ tên/email/SĐT/phòng ban lấy từ hồ sơ đó và **chỉ chỉnh ở Danh mục nhân viên** (tự đồng bộ sang tài khoản). Ở Quản lý người dùng chỉ đổi vai trò (giữa Admin IT / Manager / Technician) và khoá/mở khoá; không đổi sang/khỏi vai trò Nhân viên. Khi API khởi động, tài khoản chưa có hồ sơ (dữ liệu mẫu, Admin IT khởi tạo từ `Bootstrap__*`, tài khoản cũ) được tự nối với hồ sơ trùng email hoặc tạo hồ sơ mới.
- Hồ sơ gắn tài khoản Admin IT / Manager / Technician chỉ **Admin IT** sửa/xoá được (Manager bị chặn); không ngừng hoạt động hồ sơ của Admin IT đang hoạt động cuối cùng.
- **Đặt lại mật khẩu** (Admin IT): mật khẩu cũ mất hiệu lực ngay, tài khoản quay về "chờ kích hoạt" và người dùng nhận liên kết đặt mật khẩu mới.
- **Gửi lại email kích hoạt** cách nhau 60 giây, liên kết cũ vô hiệu; đổi email nhân viên (ở Danh mục nhân viên) khi tài khoản còn chờ kích hoạt cũng vô hiệu liên kết đã gửi tới địa chỉ cũ — Admin bấm "Gửi lại email kích hoạt" để gửi tới địa chỉ mới.
- **Xoá tài khoản** (Admin IT, `DELETE /users/{id}`) là xoá mềm: người đó không đăng nhập được nữa (kể cả phiên đang mở), email được giải phóng để tạo lại, hồ sơ nhân viên vẫn còn (chỉ tách liên kết), lịch sử thao tác/phiếu/biên bản giữ nguyên. Không tự xoá chính mình, không xoá Admin IT đang hoạt động cuối cùng, không xoá kỹ thuật viên còn phiếu bảo trì đang chờ xử lý. Muốn tạm dừng có thể mở lại thì dùng "Khoá".
- Người dùng vẫn tự **đổi mật khẩu** được ở mục "Đổi mật khẩu". Admin IT đầu tiên (cấu hình `Bootstrap__*`) buộc đổi mật khẩu ở lần đăng nhập đầu.
- Tài khoản mẫu trong seed (Development) có mật khẩu cố định `Admin@123` / `Demo@123` để đăng nhập thử, đã ở trạng thái kích hoạt.
- Sau khi lấy code mới cần `dotnet ef database update` (migrations `AddAccountActivations`, `RemoveSystemSettings`).

## Phiên đăng nhập

Token không tự hết hạn theo thời gian, nhưng gắn với **lần chạy hiện tại của máy chủ**: chạy lại `dotnet run` thì mọi người phải đăng nhập lại. Đăng xuất hoặc bị khoá tài khoản thì mất phiên ngay.

## Xoá dữ liệu (xoá mềm)

Bấm "Xoá" ở Phòng ban, Loại tài sản, License, Nhân viên, Trạng thái thanh lý, Người dùng chỉ **đánh dấu đã xoá** (`IsDeleted`), dòng vẫn còn trong CSDL và không hiện lại khi chạy lại hệ thống (dữ liệu mẫu không tạo lại dòng đã xoá). Vẫn chặn xoá khi còn dữ liệu đang tham chiếu. Tạo lại đúng tên/mã đã xoá được.

## Sao lưu & phục hồi, báo cáo, tên file xuất

- **Sao lưu & phục hồi** (Admin IT): dùng `mysqldump`/`mysql`; cấu hình mục `Backup` (`Directory`, `MysqlBinPath`, `TimeoutSeconds`) trong `appsettings`. Phục hồi ghi đè toàn bộ dữ liệu và tự tạo bản "trước khi phục hồi".
- **Báo cáo** (Excel/PDF): tồn kho thiết bị, sắp bảo trì, vòng đời ngắn, phần mềm & giấy phép.
- **Tên file xuất** thống nhất cho mọi chức năng: `eaims-{tên-chức-năng}-{yyyyMMdd-HHmmss}.{đuôi}` (giờ Việt Nam), cài ở `Helpers/ExportFileNames.cs`.

## Đồng bộ code giữa các máy trong nhóm

Mỗi lần lấy code mới từ `main`:

```bash
git pull origin main
cd backend
dotnet ef database update --project ITAM.API   # áp dụng migration mới (bắt buộc, app không tự migrate)
dotnet build && dotnet test
dotnet run --project ITAM.API
```

Lưu ý:

- `appsettings.Development.json` **không** nằm trong git (chứa mật khẩu/secret). Nếu chưa có hoặc file mẫu `appsettings.Development.json.example` có mục mới (vd `Backup`), hãy so sánh và bổ sung vào file riêng của bạn.
- Dữ liệu mẫu chỉ nạp ở Development khi API khởi động; CSDL của mỗi người là riêng. Muốn có cùng dữ liệu thì dùng chức năng **Sao lưu** trên máy này rồi **Phục hồi** trên máy kia (cùng phiên bản code/migration).
- Máy cần `mysqldump` và `mysql` (đi kèm MySQL Client) nếu dùng sao lưu; nếu không nằm trong PATH, khai báo `Backup:MysqlBinPath`.
- Nếu `dotnet ef database update` báo lỗi vì CSDL cũ lệch migration, xoá schema Development rồi chạy lại lệnh để tạo mới (chỉ làm với dữ liệu thử).
- Cập nhật `frontend/shared/api-client.js` (`API_BASE_URL`) nếu API chạy cổng khác `5140`.

