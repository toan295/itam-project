using ITAM.API.Repositories.Interfaces;

namespace ITAM.Tests.Helpers;

// Bản giả của IExclusiveSection cho unit test: chạy thẳng đoạn nghiệp vụ (không có DB để khoá) và ghi lại
// "ổ khoá" mà Service yêu cầu để test khẳng định đúng tài sản/license bị khoá.
public class RecordingExclusiveSection : IExclusiveSection
{
    public List<(LockTarget Target, int Id)> Calls { get; } = new();

    public Task<T> RunAsync<T>(LockTarget target, int id, Func<Task<T>> work)
    {
        Calls.Add((target, id));
        return work();
    }
}
