using System.Security.Claims;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Dashboard;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private const string AdminRole = "Admin IT";
    private readonly IAssetAllocationService _allocationService;
    private readonly ISoftwareLicenseService _licenseService;
    private readonly ILifecycleAnalysisService _lifecycleService;

    public DashboardController(
        IAssetAllocationService allocationService,
        ISoftwareLicenseService licenseService,
        ILifecycleAnalysisService lifecycleService)
    {
        _allocationService = allocationService;
        _licenseService = licenseService;
        _lifecycleService = lifecycleService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        int? departmentId = int.TryParse(User.FindFirstValue("DepartmentId"), out var parsedDepartmentId)
            ? parsedDepartmentId
            : null;

        var summary = new DashboardSummaryDto();
        if (role is "Admin IT" or "Manager")
        {
            var allocations = await _allocationService.GetPagedAsync(
                null, "Allocated", 1, 1, role, departmentId);
            summary.ActiveAllocations = allocations.TotalItems;
        }
        if (string.Equals(role, AdminRole, StringComparison.Ordinal))
        {
            var expiringLicenses = await _licenseService.GetExpiringSoonAsync(30);
            var allLicenses = await _licenseService.GetPagedAsync(1, 1);
            var replacementCandidates = await _lifecycleService.GetCurrentReplacementCandidatesAsync(
                null, null, "score", 1, 1);
            summary.SoftwareLicenseCount = allLicenses.TotalItems;
            summary.ExpiringSoftwareLicenses = expiringLicenses.Count;
            summary.ExpiringSoftwareLicenseItems = expiringLicenses.Take(6).Select(item => new DashboardExpiringLicenseDto
            {
                SoftwareName = item.SoftwareName,
                LicenseKey = item.LicenseKey,
                ExpiryDate = item.ExpiryDate,
                IsExpired = item.IsExpired,
                IsNearUsageLimit = item.IsNearUsageLimit,
            }).ToList();
            summary.ReplacementCandidates = replacementCandidates.TotalItems;
        }

        return Ok(ApiResponse<DashboardSummaryDto>.Ok(summary));
    }
}
