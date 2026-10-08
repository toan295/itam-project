# EAIMS — Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp

Backend ASP.NET Core Web API (.NET 9, 3 lớp Controller–Service–Repository, EF Core + MySQL 8) và frontend HTML/CSS/JS thuần (Bootstrap 5).

## Yêu cầu

- .NET SDK 9
- MySQL 8.0
- Công cụ EF Core: `dotnet tool install --global dotnet-ef`
- Một static server bất kỳ cho `frontend/` (ví dụ VS Code Live Server, cổng 5500)

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

   Ở môi trường Development, ứng dụng tự nạp dữ liệu mẫu (vai trò, phòng ban, tài khoản demo, tài sản...). Tài khoản quản trị mẫu: `admin@eaims.local` (mật khẩu xem trong `Data/DbSeeder.cs`). **Dữ liệu mẫu chỉ có ở Development**; môi trường khác phải tự tạo tài khoản Admin IT đầu tiên.

## Triển khai môi trường thật (Production)

Dữ liệu mẫu **chỉ** nạp ở Development. Ở môi trường khác cần cấu hình qua biến môi trường (tên dùng `__` thay cho `:`):

| Biến | Bắt buộc | Ý nghĩa |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Có | Chuỗi kết nối MySQL (đã chạy `dotnet ef database update`). |
| `JwtSettings__Secret`, `__Issuer`, `__Audience`, `__ExpiryMinutes` | Có | Cấu hình JWT; `Secret` tối thiểu 32 byte, ngẫu nhiên. |
| `Cors__AllowedOrigins__0`, `__1`... | Có | Origin của frontend. |
| `DataProtection__KeysPath` | **Rất nên** | Thư mục **bền vững** (volume) lưu khoá mã hoá mật khẩu mặc định. Nếu không đặt, khoá có thể mất khi khởi động lại (đặc biệt trên Linux/container); khi đó mật khẩu mặc định không giải mã được và Admin IT phải nhập lại trong màn hình Người dùng. |
| `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` | Lần đầu | Tạo **Admin IT đầu tiên** (kèm 3 vai trò và phòng ban nếu thiếu). Mật khẩu phải đạt chính sách (≥ 8 ký tự, có chữ và số). Tuỳ chọn: `Bootstrap__AdminFullName`, `Bootstrap__DepartmentName`. |

Tài khoản Bootstrap buộc đổi mật khẩu ở lần đăng nhập đầu. Nếu đã có Admin IT đang hoạt động thì cấu hình Bootstrap bị bỏ qua (không bao giờ ghi đè) — **hãy xoá các biến `Bootstrap__*` sau khi đăng nhập thành công**.

Lưu ý vận hành:

- **Mật khẩu mặc định** được mã hoá (ASP.NET Core Data Protection) trong bảng `SystemSettings`; giá trị cũ dạng văn bản thuần tự được mã hoá khi khởi động. Admin IT vẫn xem được giá trị để báo người dùng mới.
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
| `docs/` | Tài liệu tham chiếu, kế hoạch từng tuần, báo cáo đánh giá, ERD/DBML, Postman |

## Quy ước làm việc

- Mỗi phần việc là một Pull Request riêng, có người còn lại review thật sự trước khi merge; không push thẳng vào `main`.
- Commit theo Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`...).
- Đổi schema: kiểm tra file migration sinh ra chỉ chứa đúng thay đổi dự kiến và cập nhật `docs/itam.dbml` trong cùng PR.
