using System.Collections.Concurrent;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

// Lưu trong bộ nhớ (singleton): đủ cho 1 instance API. Chạy nhiều instance thì mỗi instance đếm riêng — khi đó cần
// chuyển sang kho dùng chung (Redis/DB); đã ghi trong README.
public class LoginAttemptTracker : ILoginAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(5);

    private const int PruneThreshold = 10_000;

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public LoginAttemptTracker(TimeProvider time)
    {
        _time = time;
    }

    private sealed class Entry
    {
        public int Failures;
        public DateTimeOffset FirstFailureAt;
        public DateTimeOffset BlockedUntil;
    }

    public bool IsBlocked(string key, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        lock (entry)
        {
            var remaining = entry.BlockedUntil - _time.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            retryAfter = remaining;
            return true;
        }
    }

    public void RecordFailure(string key)
    {
        var now = _time.GetUtcNow();
        var entry = _entries.GetOrAdd(key, _ => new Entry { FirstFailureAt = now });

        lock (entry)
        {
            // Cửa sổ đếm đã hết hạn (hoặc lần chặn trước đã xong) -> bắt đầu đếm lại.
            if (now - entry.FirstFailureAt > FailureWindow || (entry.BlockedUntil != default && entry.BlockedUntil <= now))
            {
                entry.Failures = 0;
                entry.FirstFailureAt = now;
                entry.BlockedUntil = default;
            }

            entry.Failures++;
            if (entry.Failures >= MaxFailures)
            {
                entry.BlockedUntil = now + BlockDuration;
            }
        }

        if (_entries.Count > PruneThreshold)
        {
            Prune(now);
        }
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);

    // Dọn các mục đã hết hạn để bộ nhớ không phình khi bị quét nhiều email/IP khác nhau.
    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _entries)
        {
            if (now - pair.Value.FirstFailureAt > FailureWindow && pair.Value.BlockedUntil <= now)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }
}
