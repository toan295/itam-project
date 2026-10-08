using ITAM.API.Helpers;

namespace ITAM.Tests.Helpers;

public class PagingTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, -1, 1, 20)]
    [InlineData(3, 100, 3, 100)]
    [InlineData(3, 101, 3, 20)]    // ngoài [1, 100] quay về mặc định (quy ước hiện có của các module).
    public void Normalize_OutOfRange_FallsBackToDefaults(int page, int pageSize, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), Paging.Normalize(page, pageSize));
    }

    [Fact]
    public void Normalize_HugePage_NeverOverflowsSkipArithmetic()
    {
        // Hồi quy: page = int.MaxValue làm (page - 1) * pageSize tràn số -> Skip() ném lỗi -> HTTP 500 ở mọi API danh sách.
        var (page, pageSize) = Paging.Normalize(int.MaxValue, 100);

        var offset = checked((page - 1) * pageSize);   // checked: tràn số sẽ ném OverflowException làm test fail.
        Assert.True(offset >= 0);
    }
}
