using ITAM.API.Data;
using ITAM.API.Repositories.Interfaces;
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

        // Câu SQL là hằng theo từng loại ổ khoá (không ghép chuỗi từ đầu vào); id truyền bằng tham số.
        var lockSql = target switch
        {
            LockTarget.Asset => "SELECT Id FROM `Assets` WHERE Id = {0} FOR UPDATE",
            LockTarget.SoftwareLicense => "SELECT Id FROM `SoftwareLicenses` WHERE Id = {0} FOR UPDATE",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

        // Nếu work ném lỗi, DisposeAsync của transaction sẽ rollback toàn bộ thay đổi và nhả khoá.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlRawAsync(lockSql, id);

        var result = await work();
        await transaction.CommitAsync();
        return result;
    }
}
