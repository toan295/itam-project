using ITAM.API.Models.DTOs.Disposals;

namespace ITAM.API.Services.Interfaces;

public interface IDisposalStatusService
{
    Task<IReadOnlyList<DisposalStatusDto>> GetAllAsync();
    Task<DisposalStatusDto> CreateAsync(UpsertDisposalStatusRequestDto dto, int currentUserId);
    Task<DisposalStatusDto> UpdateAsync(int id, UpsertDisposalStatusRequestDto dto, int currentUserId);
    Task DeleteAsync(int id, int currentUserId);

    // Tạo lại các bước chính mặc định đã bị xoá (không đụng tới bước còn tồn tại); trả về danh sách đầy đủ sau khi khôi phục.
    Task<IReadOnlyList<DisposalStatusDto>> RestoreDefaultsAsync(int currentUserId);
}
