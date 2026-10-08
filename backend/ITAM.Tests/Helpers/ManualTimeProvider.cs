namespace ITAM.Tests.Helpers;

// Đồng hồ điều khiển được bằng tay để test các quy tắc theo thời gian (khoá đăng nhập sai...) mà không phải chờ thật.
public class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
