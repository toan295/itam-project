# Tuần 4 - Software License

## Phần đã triển khai

- CRUD `/api/v1/software-licenses`.
- `CurrentUsage` được tính bằng `COUNT()` trên `AssetSoftwareLicenses`, không thêm cột mới vào entity/database.
- Chặn trùng `LicenseKey` bằng business logic và unique index hiện có.
- Gán license: `POST /api/v1/software-licenses/{id}/assign`.
- Gỡ license: `DELETE /api/v1/software-licenses/{id}/assign/{assetId}`.
- Chặn gán trùng cùng Asset và chặn vượt `MaxUsage`.
- Toàn bộ controller dùng `[Authorize(Roles = "Admin IT")]`.
- Cảnh báo sắp hết hạn: `GET /api/v1/software-licenses/expiring-soon?days=30`.
- DTO trả về có `usagePercentage`, `isExpired`, `isExpiringSoon`, `isNearUsageLimit`.
- Giao diện demo: `frontend/pages/software-licenses.html`.
- 4 unit test: `backend/ITAM.Tests/SoftwareLicenseServiceTests.cs`.

## Chạy backend

```powershell
cd backend\ITAM.API
dotnet restore
dotnet run
```

Swagger Development mặc định:

- `https://localhost:7070/swagger`
- hoặc `http://localhost:5140/swagger`

Đăng nhập bằng tài khoản Admin IT đã seed để lấy JWT, sau đó bấm **Authorize** trong Swagger và dán token.

## Chạy test

```powershell
cd backend
dotnet test ITAM.sln
```

Mục tiêu: 4 test trong `SoftwareLicenseServiceTests` đều Passed.

## Kịch bản test nghiệp vụ chính

1. POST license mới với `MaxUsage = 2` -> 201.
2. POST lại đúng `LicenseKey` -> 409.
3. Gán license cho Asset A -> 200, `currentUsage = 1`.
4. Gán license cho Asset B -> 200, `currentUsage = 2`.
5. Gán thêm Asset C -> 409 vì vượt `MaxUsage`.
6. Gán lại license cho Asset A -> 409 vì trùng.
7. DELETE gỡ Asset B -> 204; GET license -> `currentUsage = 1`.
8. Dùng token Manager hoặc Technician -> 403.

> Lưu ý: project hiện chưa có module CRUD Assets trong source được gửi. Muốn test assign/unassign, database phải có sẵn Asset hợp lệ.

## Database

Không tạo migration mới. Schema `SoftwareLicenses` và `AssetSoftwareLicenses` đã có sẵn đúng theo `itam.dbml` và migration hiện tại.

## Lưu ý về frontend

Theo phân công trong tài liệu, CORS/cấu hình chung `Program.cs` thuộc phần của Toàn nên phần này **không tự thêm CORS**. Hai file frontend đã được chuẩn bị đúng module; khi chạy bằng Live Server ở một origin khác API, cần dùng cấu hình CORS chung của nhóm hoặc phục vụ frontend cùng origin với backend.
