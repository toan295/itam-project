using ITAM.API.Models.DTOs.AssetCategories;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class AssetCategoryService : IAssetCategoryService
{
    private readonly IAssetCategoryRepository _repo;

    public AssetCategoryService(IAssetCategoryRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<AssetCategoryResponseDto>> GetAllAsync()
    {
        var categories = await _repo.GetAllAsync();
        return categories.Select(MapToDto).ToList();
    }

    public async Task<AssetCategoryResponseDto> GetByIdAsync(int id)
    {
        var category = await _repo.GetByIdAsync(id)
            ?? throw new AssetCategoryNotFoundException(id);
        return MapToDto(category);
    }

    public async Task<AssetCategoryResponseDto> CreateAsync(CreateAssetCategoryRequestDto dto)
    {
        var name = dto.Name.Trim();
        var existed = await _repo.GetByNameAsync(name);
        if (existed is not null)
        {
            throw new AssetCategoryNameAlreadyExistsException(name);
        }

        var category = new AssetCategory { Name = name };
        await _repo.AddAsync(category);
        await SaveChangesGuardingNameConflictAsync(name);

        return MapToDto(category);
    }

    public async Task<AssetCategoryResponseDto> UpdateAsync(int id, UpdateAssetCategoryRequestDto dto)
    {
        var category = await _repo.GetByIdAsync(id)
            ?? throw new AssetCategoryNotFoundException(id);

        var name = dto.Name.Trim();
        if (!string.Equals(name, category.Name, StringComparison.Ordinal))
        {
            var existed = await _repo.GetByNameAsync(name);
            if (existed is not null)
            {
                throw new AssetCategoryNameAlreadyExistsException(name);
            }
        }

        category.Name = name;
        _repo.Update(category);
        await SaveChangesGuardingNameConflictAsync(name);

        return MapToDto(category);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _repo.GetByIdAsync(id)
            ?? throw new AssetCategoryNotFoundException(id);

        // UC-04 E1: không xoá danh mục đang được tài sản tham chiếu.
        if (await _repo.IsReferencedByAssetsAsync(id))
        {
            throw new AssetCategoryInUseException(id);
        }

        _repo.Remove(category);

        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race hiếm: asset mới được gán vào category này ngay giữa lúc kiểm tra và xoá.
            throw new AssetCategoryInUseException(id);
        }
    }

    private async Task SaveChangesGuardingNameConflictAsync(string name)
    {
        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new AssetCategoryNameAlreadyExistsException(name);
        }
    }

    private static AssetCategoryResponseDto MapToDto(AssetCategory c) => new()
    {
        Id = c.Id,
        Name = c.Name,
    };
}
