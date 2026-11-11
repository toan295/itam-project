namespace ITAM.API.Auditing;

/// <summary>
/// Danh mục đối tượng/hành động có thể xuất hiện trong nhật ký, đối chiếu với TOÀN BỘ chức năng ghi dữ liệu của EAIMS
/// (không dựa vào những gì đang có sẵn trong dữ liệu). Giao diện lấy danh mục từ API, không tự giữ bản sao.
/// Mã không có trong danh mục (dữ liệu cũ/phiên bản khác) vẫn được liệt kê kèm số lượng, nhãn = chính mã.
/// </summary>
public static class AuditCatalog
{
    public static readonly IReadOnlyDictionary<string, string> Entities = new Dictionary<string, string>
    {
        ["User"] = "Người dùng (tài khoản)",
        ["Employee"] = "Nhân viên",
        ["Asset"] = "Tài sản",
        ["AssetCategory"] = "Loại tài sản",
        ["Department"] = "Phòng ban",
        ["AssetAllocation"] = "Phân bổ tài sản",
        ["MaintenanceTicket"] = "Phiếu bảo trì",
        ["SoftwareLicense"] = "Giấy phép phần mềm",
        ["DisposalRequest"] = "Phiếu thanh lý",
        ["DisposalStatus"] = "Trạng thái thanh lý",
        ["Role"] = "Vai trò (phân quyền)",
        ["BudgetForecast"] = "Dự báo ngân sách",
        ["ReferencePrice"] = "Đơn giá tham khảo",
        ["ExpansionPlan"] = "Kế hoạch mở rộng phòng ban",
        ["DeviceRatio"] = "Định mức thiết bị/người",
        ["LifecyclePolicy"] = "Ngưỡng vòng đời theo loại",
        ["Report"] = "Báo cáo xuất file",
        ["Backup"] = "Sao lưu & phục hồi dữ liệu",
    };

    public static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>
    {
        ["Login"] = "Đăng nhập",
        ["Logout"] = "Đăng xuất",
        ["LoginFailed"] = "Đăng nhập thất bại",
        ["ChangePassword"] = "Đổi mật khẩu",
        ["ResetPassword"] = "Đặt lại mật khẩu (Admin)",
        ["ActivateAccount"] = "Kích hoạt tài khoản",
        ["ResendActivation"] = "Gửi lại email kích hoạt",
        ["Create"] = "Thêm mới",
        ["Update"] = "Cập nhật",
        ["Delete"] = "Xoá",
        ["Upsert"] = "Thiết lập (thêm/sửa)",
        ["UpdateStatus"] = "Cập nhật trạng thái / kết quả",
        ["UpdatePriority"] = "Đổi mức độ khẩn",
        ["Assign"] = "Gán / phân công",
        ["Unassign"] = "Gỡ gán",
        ["Return"] = "Thu hồi tài sản",
        ["Print"] = "In biên bản",
        ["Propose"] = "Đề xuất thanh lý",
        ["Approve"] = "Duyệt",
        ["Reject"] = "Từ chối",
        ["Complete"] = "Hoàn tất thanh lý",
        ["SetSubStatus"] = "Gán trạng thái phụ",
        ["RestoreDefaults"] = "Khôi phục bước mặc định",
        ["UpdatePermissions"] = "Sửa phân quyền",
        ["ResetPermissions"] = "Khôi phục phân quyền mặc định",
        ["Generate"] = "Tạo dự báo",
        ["Export"] = "Xuất báo cáo",
        ["Backup"] = "Tạo bản sao lưu",
        ["Restore"] = "Phục hồi dữ liệu",
        ["Upload"] = "Tải lên bản sao lưu",
        ["Download"] = "Tải bản sao lưu về máy",
    };

    public static readonly IReadOnlyDictionary<string, string> Outcomes = new Dictionary<string, string>
    {
        ["Success"] = "Thành công",
        ["Rejected"] = "Bị từ chối (không đổi dữ liệu)",
        ["Failed"] = "Thất bại (lỗi hệ thống)",
    };
}
