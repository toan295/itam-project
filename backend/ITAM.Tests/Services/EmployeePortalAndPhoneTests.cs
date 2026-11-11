using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using ITAM.API.Validators;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0912345678")]
    [InlineData("0912 345 678")]
    [InlineData("0912.345.678")]
    [InlineData("0912-345-678")]
    [InlineData("(091) 234 5678")]
    [InlineData("+84912345678")]
    [InlineData("02838123456")]      // máy bàn
    public void IsValid_AcceptsVietnamesePhoneNumbersWithSeparators(string input) =>
        Assert.True(PhoneNumber.IsValid(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("abcdefghij")]
    [InlineData("0912345")]          // quá ngắn
    [InlineData("09123456789012")]   // quá dài
    [InlineData("+1912345678")]      // không phải +84
    public void IsValid_RejectsGarbage(string input) =>
        Assert.False(PhoneNumber.IsValid(input));

    [Fact]
    public void Normalize_StripsSeparatorsAndTurnsBlankIntoNull()
    {
        Assert.Equal("0912345678", PhoneNumber.Normalize(" 0912 345-678 "));
        Assert.Null(PhoneNumber.Normalize("   "));
        Assert.Null(PhoneNumber.Normalize(null));
    }
}

public class PhoneValidatorTests
{
    [Fact]
    public void CreateUser_NeedsRoleAndEmployee_NothingTyped()
    {
        Assert.False(new CreateUserRequestValidator().Validate(new CreateUserRequestDto { RoleId = 0, EmployeeId = 5 }).IsValid);
        Assert.False(new CreateUserRequestValidator().Validate(new CreateUserRequestDto { RoleId = 2, EmployeeId = 0 }).IsValid);
        Assert.True(new CreateUserRequestValidator().Validate(new CreateUserRequestDto { RoleId = 2, EmployeeId = 5 }).IsValid);
    }

    [Fact]
    public void UpdateUser_OnlyNeedsARole()
    {
        Assert.False(new UpdateUserRequestValidator().Validate(new UpdateUserRequestDto { RoleId = 0, IsActive = true }).IsValid);
        Assert.True(new UpdateUserRequestValidator().Validate(new UpdateUserRequestDto { RoleId = 3, IsActive = false }).IsValid);
    }

    [Fact]
    public void UpsertEmployee_PhoneIsRequiredAndValidated()
    {
        var dto = new UpsertEmployeeRequestDto { FullName = "A", DepartmentId = 1, Email = "a@gmail.com", Phone = "" };
        Assert.False(new UpsertEmployeeRequestValidator().Validate(dto).IsValid);

        dto.Phone = "123";
        Assert.False(new UpsertEmployeeRequestValidator().Validate(dto).IsValid);

        dto.Phone = "0912345678";
        Assert.True(new UpsertEmployeeRequestValidator().Validate(dto).IsValid);
    }
}

public class EmployeePortalServiceTests
{
    private readonly Mock<IEmployeeRepository> _employeeRepo = new();
    private readonly Mock<IMaintenanceTicketRepository> _ticketRepo = new();
    private readonly EmployeePortalService _sut;

    public EmployeePortalServiceTests()
    {
        _sut = new EmployeePortalService(_employeeRepo.Object, _ticketRepo.Object);
    }

    private static AssetAllocation Allocation(int id, int assetId, string code) => new()
    {
        Id = id,
        AssetId = assetId,
        EmployeeId = 5,
        AllocatedDate = new DateOnly(2026, 1, 10),
        Asset = new Asset
        {
            Id = assetId, AssetCode = code, Name = "Laptop", SerialNumber = "SN-" + code, Status = AssetStatus.InUse,
            Category = new AssetCategory { Id = 1, Name = "Máy tính" },
        },
    };

    [Fact]
    public async Task GetMyAssetsAsync_ReturnsOnlyTheEmployeesOpenAllocations_AndFlagsPendingTickets()
    {
        _employeeRepo.Setup(r => r.GetByUserIdAsync(77)).ReturnsAsync(new Employee { Id = 5, UserId = 77, IsActive = true });
        _employeeRepo.Setup(r => r.GetOpenAllocationsWithAssetAsync(5))
            .ReturnsAsync(new List<AssetAllocation> { Allocation(1, 10, "PC-001"), Allocation(2, 11, "MH-001") });
        _employeeRepo.Setup(r => r.GetAssetIdsWithPendingTicketsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new HashSet<int> { 11 });

        var result = await _sut.GetMyAssetsAsync(77);

        Assert.Equal(new[] { "PC-001", "MH-001" }, result.Select(a => a.AssetCode));
        Assert.False(result[0].HasPendingTicket);
        Assert.True(result[1].HasPendingTicket);
        Assert.Equal("Máy tính", result[0].CategoryName);
    }

    [Fact]
    public async Task GetMyAssetsAsync_AccountWithoutEmployee_ReturnsNothing()
    {
        _employeeRepo.Setup(r => r.GetByUserIdAsync(77)).ReturnsAsync((Employee?)null);

        Assert.Empty(await _sut.GetMyAssetsAsync(77));
        _employeeRepo.Verify(r => r.GetOpenAllocationsWithAssetAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetMyAssetsAsync_InactiveEmployee_ReturnsNothing()
    {
        _employeeRepo.Setup(r => r.GetByUserIdAsync(77)).ReturnsAsync(new Employee { Id = 5, UserId = 77, IsActive = false });

        Assert.Empty(await _sut.GetMyAssetsAsync(77));
    }

    [Fact]
    public async Task GetMyTicketsAsync_QueriesOnlyTicketsReportedByTheCaller()
    {
        _ticketRepo.Setup(r => r.GetByReporterPagedAsync(77, 1, 10)).ReturnsAsync((
            new List<MaintenanceTicket>
            {
                new()
                {
                    Id = 3, IssueDescription = "Hỏng", Status = TicketStatus.Pending, Priority = TicketPriority.High,
                    ReportedDate = new DateTime(2026, 10, 1), Asset = new Asset { AssetCode = "PC-001", Name = "Laptop" },
                },
            }, 1));

        var result = await _sut.GetMyTicketsAsync(77, 1, 10);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal("PC-001", result.Items[0].AssetCode);
        Assert.Equal("Pending", result.Items[0].Status);
        _ticketRepo.Verify(r => r.GetByReporterPagedAsync(77, 1, 10), Times.Once);
    }
}
