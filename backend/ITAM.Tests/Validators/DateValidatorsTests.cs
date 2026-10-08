using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Validators;

namespace ITAM.Tests.Validators;

public class DateValidatorsTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ---- Ngày mua tài sản ----

    private static CreateAssetRequestDto Asset(DateOnly? purchase) => new()
    {
        AssetCode = "PC-1", Name = "PC", CategoryId = 1, DepartmentId = 1, PurchaseDate = purchase,
    };

    [Fact]
    public void CreateAsset_PurchaseDateInFarFuture_IsInvalid()
    {
        var result = new CreateAssetRequestValidator().Validate(Asset(Today.AddYears(1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAssetRequestDto.PurchaseDate));
    }

    [Theory]
    [InlineData(-3650)]
    [InlineData(0)]
    [InlineData(1)]   // dung sai múi giờ: ngày "hôm nay" của người dùng UTC+7 có thể là ngày mai theo UTC.
    public void CreateAsset_PurchaseDateTodayOrPast_IsValid(int offsetDays)
    {
        var result = new CreateAssetRequestValidator().Validate(Asset(Today.AddDays(offsetDays)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateAsset_NoPurchaseDate_IsValid()
    {
        Assert.True(new CreateAssetRequestValidator().Validate(Asset(null)).IsValid);
    }

    [Fact]
    public void UpdateAsset_PurchaseDateInFuture_IsInvalid()
    {
        var dto = new UpdateAssetRequestDto
        {
            AssetCode = "PC-1", Name = "PC", CategoryId = 1, DepartmentId = 1, Status = "InUse",
            PurchaseDate = Today.AddMonths(6),
        };

        Assert.False(new UpdateAssetRequestValidator().Validate(dto).IsValid);
    }

    // ---- Phân bổ / thu hồi ----

    [Fact]
    public void CreateAllocation_AllocatedDateInFuture_IsInvalid()
    {
        var dto = new CreateAssetAllocationDto
        {
            AssetId = 1, EmployeeId = 1, AllocatedDate = Today.AddYears(10),
        };

        Assert.False(new CreateAssetAllocationValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void CreateAllocation_AllocatedDateToday_IsValid()
    {
        var dto = new CreateAssetAllocationDto
        {
            AssetId = 1, EmployeeId = 1, AllocatedDate = Today,
        };

        Assert.True(new CreateAssetAllocationValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void ReturnAllocation_ReturnedDateInFuture_IsInvalid()
    {
        var dto = new ReturnAssetAllocationDto { ReturnedDate = Today.AddDays(30), Condition = "Good" };

        var result = new ReturnAssetAllocationValidator().Validate(dto);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReturnAssetAllocationDto.ReturnedDate));
    }

    [Fact]
    public void ReturnAllocation_ReturnedDateToday_IsValid()
    {
        var dto = new ReturnAssetAllocationDto { ReturnedDate = Today, Condition = "Damaged" };

        Assert.True(new ReturnAssetAllocationValidator().Validate(dto).IsValid);
    }
}
