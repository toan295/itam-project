// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// using FluentValidation;
// using ITAM.API.Models.DTOs.Forecasts;
//
// namespace ITAM.API.Validators;
//
// public class UpsertReferencePriceRequestValidator : AbstractValidator<UpsertReferencePriceRequestDto>
// {
//     public const decimal MaxUnitPrice = 1_000_000_000_000m;   // vẫn nằm trong decimal(18,2)
//
//     public UpsertReferencePriceRequestValidator()
//     {
//         RuleFor(x => x.UnitPrice)
//             .GreaterThan(0).WithMessage("Đơn giá phải lớn hơn 0.")
//             .LessThanOrEqualTo(MaxUnitPrice).WithMessage("Đơn giá vượt quá giới hạn cho phép.");
//
//         RuleFor(x => x.UnitPrice)
//             .Must(x => decimal.Round(x, 2) == x).WithMessage("Đơn giá tối đa 2 chữ số thập phân.");
//
//         RuleFor(x => x.Version).GreaterThan(0).When(x => x.Version.HasValue).WithMessage("Version không hợp lệ.");
//     }
// }
