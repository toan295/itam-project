using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IAccountActivationRepository
{
    Task AddAsync(AccountActivation activation);

    // Không tracking, kèm User(+Role,Department): dùng cho luồng công khai bằng token.
    Task<AccountActivation?> GetByTokenHashAsync(string tokenHash);

    // Các lần gửi còn Pending của một tài khoản (có tracking) — để đánh dấu Superseded khi gửi lần mới.
    Task<List<AccountActivation>> GetPendingByUserAsync(int userId);

    // Lần gửi mới nhất của từng tài khoản (không tracking). Tài khoản chưa từng gửi không có trong kết quả.
    Task<Dictionary<int, AccountActivation>> GetLatestByUsersAsync(IEnumerable<int> userIds);

    // Một transaction: Pending (chưa hết hạn) -> Activated VÀ đặt mật khẩu mới + bỏ cờ chờ kích hoạt cho tài khoản.
    // Trả false nếu liên kết không còn dùng được (hai lần bấm đồng thời chỉ một lần thắng).
    Task<bool> TryActivateAsync(int activationId, int userId, string passwordHash, DateTime now);

    Task<int> SaveChangesAsync();
}
