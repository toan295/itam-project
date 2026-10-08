namespace ITAM.API.Repositories.Interfaces;

// Bản ghi dùng làm "ổ khoá" để tuần tự hoá các thao tác kiểu "kiểm tra điều kiện rồi mới ghi".
public enum LockTarget
{
    Asset,
    SoftwareLicense,
}

// Chạy một đoạn nghiệp vụ trong transaction, sau khi khoá dòng (SELECT ... FOR UPDATE) của tài sản/license liên quan.
// Dùng cho các quy tắc mà ràng buộc UNIQUE của DB không diễn đạt được (mỗi tài sản chỉ có 1 phân bổ đang mở, 1 phiếu
// thanh lý đang mở, license không vượt MaxUsage): nếu không khoá, 2 request đồng thời cùng thấy "chưa có" rồi cùng ghi.
public interface IExclusiveSection
{
    Task<T> RunAsync<T>(LockTarget target, int id, Func<Task<T>> work);
}
