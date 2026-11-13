using ITAM.API.Data;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class ExclusiveSection : IExclusiveSection
{
    private readonly AppDbContext _db;

    public ExclusiveSection(AppDbContext db)
    {
        _db = db;
    }

    public async Task<T> RunAsync<T>(LockTarget target, int id, Func<Task<T>> work)
    {
        // Đã nằm trong một transaction (gọi lồng nhau) thì dùng luôn, không mở transaction thứ hai.
        if (_db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        // [ĐÃ ĐÓNG]
        // if (target == LockTarget.ForecastYear)
        // {
            // return await RunWithNamedLockAsync($"eaims-forecast-{id}", () => new ForecastBusyException(id), work);
        // }

        // Câu SQL là hằng theo từng loại ổ khoá (không ghép chuỗi từ đầu vào); id truyền bằng tham số.
        var lockSql = target switch
        {
            LockTarget.Asset => "SELECT Id FROM `Assets` WHERE Id = {0} FOR UPDATE",
            LockTarget.SoftwareLicense => "SELECT Id FROM `SoftwareLicenses` WHERE Id = {0} FOR UPDATE",
            LockTarget.Role => "SELECT Id FROM `Roles` WHERE Id = {0} FOR UPDATE",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

        // Nếu work ném lỗi, DisposeAsync của transaction sẽ rollback toàn bộ thay đổi và nhả khoá.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlRawAsync(lockSql, id);

        var result = await work();
        await transaction.CommitAsync();
        return result;
    }

    // Không có dòng nào đại diện cho "năm dự báo" để SELECT ... FOR UPDATE nên dùng khoá tên của MySQL (GET_LOCK, theo phiên
    // kết nối). Giữ kết nối mở trong suốt đoạn làm việc và LUÔN nhả khoá ở finally để kết nối trả về pool không còn khoá treo.
    // Transaction bắt đầu SAU khi có khoá nên mọi lần đọc đầu vào thấy ảnh chụp nhất quán và thấy kết quả của lần chạy trước.
    private async Task<T> RunWithNamedLockAsync<T>(string lockName, Func<Exception> onBusy, Func<Task<T>> work)
    {
        await _db.Database.OpenConnectionAsync();
        try
        {
            var acquired = (await _db.Database.SqlQueryRaw<int?>("SELECT GET_LOCK({0}, 30) AS `Value`", lockName).ToListAsync()).FirstOrDefault();
            if (acquired != 1)
            {
                throw onBusy();
            }

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var result = await work();
                await transaction.CommitAsync();
                return result;
            }
            finally
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT RELEASE_LOCK({0})", lockName);
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
