using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/lifecycle")]
[Authorize(Roles = "Admin IT")]
public class LifecycleController : ControllerBase
{
    private readonly ILifecycleAnalysisService _service;
    private readonly IValidator<UpsertLifecyclePolicyRequestDto> _policyValidator;

    public LifecycleController(
        ILifecycleAnalysisService service,
        IValidator<UpsertLifecyclePolicyRequestDto> policyValidator)
    {
        _service = service;
        _policyValidator = policyValidator;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<LifecycleStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats([FromQuery] int? departmentId = null)
    {
        var result = await _service.GetStatsAsync(departmentId);
        return Ok(ApiResponse<LifecycleStatsDto>.Ok(result));
    }

    [HttpGet("replacement-candidates")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<ReplacementRecommendationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReplacementCandidates(
        [FromQuery] int? departmentId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string sortBy = "score",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _service.GetCurrentReplacementCandidatesAsync(
                departmentId,
                categoryId,
                sortBy,
                page,
                pageSize);
            return Ok(ApiResponse<PagedResultDto<ReplacementRecommendationDto>>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<UpcomingReplacementDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUpcoming(
        [FromQuery] int? departmentId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] int months = 12,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _service.GetUpcomingAsync(
                departmentId,
                categoryId,
                months,
                page,
                pageSize);
            return Ok(ApiResponse<PagedResultDto<UpcomingReplacementDto>>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // M2: route "assets/{assetId:int}" không đụng các route cố định (stats, upcoming, policies...).
    [HttpGet("assets/{assetId:int}")]
    [ProducesResponseType(typeof(ApiResponse<AssetLifecycleDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssetDetail(int assetId)
    {
        try
        {
            var result = await _service.GetAssetDetailAsync(assetId);
            return Ok(ApiResponse<AssetLifecycleDetailDto>.Ok(result));
        }
        catch (LifecycleAssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("policies")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LifecyclePolicyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicies()
    {
        var result = await _service.GetPoliciesAsync();
        return Ok(ApiResponse<IReadOnlyList<LifecyclePolicyDto>>.Ok(result));
    }

    [HttpPut("policies/{categoryId:int}")]
    [ProducesResponseType(typeof(ApiResponse<LifecyclePolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertPolicy(
        int categoryId,
        UpsertLifecyclePolicyRequestDto dto)
    {
        var validation = await _policyValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.",
                validation.Errors.Select(error => error.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _service.UpsertPolicyAsync(categoryId, dto);
            return Ok(ApiResponse<LifecyclePolicyDto>.Ok(result, "Đã lưu ngưỡng vòng đời theo loại tài sản."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetCategoryNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("policies/{categoryId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePolicy(int categoryId)
    {
        try
        {
            await _service.DeletePolicyAsync(categoryId);
            return NoContent();
        }
        catch (LifecyclePolicyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
