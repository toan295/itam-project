namespace ITAM.API.Models.DTOs.Lifecycle;

public class TicketTrendPointDto
{
    public int Year { get; set; }
    public int TicketCount { get; set; }
    public bool IsPartialYear { get; set; }   // năm hiện tại: chỉ tính từ 01/01 đến AsOfDate, KHÔNG phải dự báo cả năm
}
