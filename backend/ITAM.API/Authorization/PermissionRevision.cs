using System.Security.Cryptography;
using System.Text;

namespace ITAM.API.Authorization;

/// <summary>
/// Dấu phiên bản của tập quyền một vai trò (băm của danh sách mã đã sắp xếp). Giao diện gửi lại giá trị đã thấy khi lưu:
/// nếu trong lúc đó người khác đã đổi quyền vai trò này thì revision lệch -> 409, tránh ghi đè mất thay đổi của người kia.
/// </summary>
public static class PermissionRevision
{
    public static string Compute(IEnumerable<string> codes)
    {
        var joined = string.Join("|", codes.OrderBy(c => c, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)))[..16];
    }
}
