using ITAM.API.Services.Implementations;
using ITAM.Tests.Helpers;

namespace ITAM.Tests.Services;

public class LoginAttemptTrackerTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly LoginAttemptTracker _sut;

    public LoginAttemptTrackerTests()
    {
        _sut = new LoginAttemptTracker(_time);
    }

    private void Fail(string key, int times)
    {
        for (var i = 0; i < times; i++)
        {
            _sut.RecordFailure(key);
        }
    }

    [Fact]
    public void FewerThanMaxFailures_NotBlocked()
    {
        Fail("k", LoginAttemptTracker.MaxFailures - 1);

        Assert.False(_sut.IsBlocked("k", out _));
    }

    [Fact]
    public void MaxFailures_BlocksForBlockDuration()
    {
        Fail("k", LoginAttemptTracker.MaxFailures);

        Assert.True(_sut.IsBlocked("k", out var retryAfter));
        Assert.Equal(LoginAttemptTracker.BlockDuration, retryAfter);

        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(_sut.IsBlocked("k", out retryAfter));
        Assert.Equal(LoginAttemptTracker.BlockDuration - TimeSpan.FromMinutes(2), retryAfter);

        _time.Advance(LoginAttemptTracker.BlockDuration);
        Assert.False(_sut.IsBlocked("k", out _));
    }

    [Fact]
    public void FailuresOutsideTheWindow_AreNotAccumulated()
    {
        Fail("k", LoginAttemptTracker.MaxFailures - 1);
        _time.Advance(LoginAttemptTracker.FailureWindow + TimeSpan.FromSeconds(1));

        Fail("k", LoginAttemptTracker.MaxFailures - 1);

        Assert.False(_sut.IsBlocked("k", out _));   // 4 cũ + 4 mới nhưng cách nhau quá cửa sổ -> không cộng dồn.
    }

    [Fact]
    public void AfterABlockExpires_CounterStartsFromZero()
    {
        Fail("k", LoginAttemptTracker.MaxFailures);
        _time.Advance(LoginAttemptTracker.BlockDuration + TimeSpan.FromSeconds(1));

        _sut.RecordFailure("k");

        Assert.False(_sut.IsBlocked("k", out _));   // sai 1 lần sau khi hết chặn không bị chặn lại ngay.
    }

    [Fact]
    public void Reset_ClearsFailures()
    {
        Fail("k", LoginAttemptTracker.MaxFailures - 1);

        _sut.Reset("k");
        _sut.RecordFailure("k");

        Assert.False(_sut.IsBlocked("k", out _));
    }

    [Fact]
    public void Keys_AreIndependent()
    {
        Fail("a", LoginAttemptTracker.MaxFailures);

        Assert.True(_sut.IsBlocked("a", out _));
        Assert.False(_sut.IsBlocked("b", out _));
    }

    [Fact]
    public void UnknownKey_IsNotBlocked()
    {
        Assert.False(_sut.IsBlocked("never-seen", out var retryAfter));
        Assert.Equal(TimeSpan.Zero, retryAfter);
    }

    [Fact]
    public async Task ConcurrentFailures_DoNotLoseCounts()
    {
        await Task.WhenAll(Enumerable.Range(0, LoginAttemptTracker.MaxFailures).Select(_ => Task.Run(() => _sut.RecordFailure("k"))));

        Assert.True(_sut.IsBlocked("k", out _));
    }
}
