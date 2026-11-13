// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// using FluentValidation;
// using ITAM.API.Models.DTOs.Forecasts;
//
// namespace ITAM.API.Validators;
//
// public class UpsertExpansionPlanRequestValidator : AbstractValidator<UpsertExpansionPlanRequestDto>
// {
//     public const int MaxHeadcount = 5000;
//     public const int MinHeadcount = -5000;
//     public const int NotesMaxLength = 500;
//
//     public UpsertExpansionPlanRequestValidator()
//     {
//         RuleFor(x => x.AdditionalHeadcount)
//             .InclusiveBetween(MinHeadcount, MaxHeadcount)
//             .WithMessage($"Thay đổi nhân sự phải từ {MinHeadcount} đến {MaxHeadcount} (âm = giảm).");
//
//         RuleFor(x => x.Version).GreaterThan(0).When(x => x.Version.HasValue).WithMessage("Version không hợp lệ.");
//
//         RuleFor(x => x.Notes)
//             .MaximumLength(NotesMaxLength)
//             .WithMessage($"Ghi chú tối đa {NotesMaxLength} ký tự.");
//     }
// }
