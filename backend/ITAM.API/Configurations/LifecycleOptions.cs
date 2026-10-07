namespace ITAM.API.Configurations;

public class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    public int DefaultMaxAgeYears { get; set; } = 5;
    public int DefaultMaxFailureCount { get; set; } = 3;
}
