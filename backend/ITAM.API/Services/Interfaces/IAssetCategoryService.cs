using ITAM.API.Models.DTOs.AssetCategories;

namespace ITAM.API.Services.Interfaces;

public interface IAssetCategoryService
{
    Task<List<AssetCategoryResponseDto>> GetAllAsync();
    Task<AssetCategoryResponseDto> GetByIdAsync(int id);
    Task<AssetCategoryResponseDto> CreateAsync(CreateAssetCategoryRequestDto dto);
    Task<AssetCategoryResponseDto> UpdateAsync(int id, UpdateAssetCategoryRequestDto dto);
    Task DeleteAsync(int id);
}
