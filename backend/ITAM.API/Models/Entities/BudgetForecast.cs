namespace ITAM.API.Models.Entities;

public class BudgetForecast
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public int EstimatedReplacementCount { get; set; }
    public decimal EstimatedBudget { get; set; }
    public string? Notes { get; set; }

    public Department Department { get; set; } = null!;
}
