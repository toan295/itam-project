using ITAM.API.Filters;

namespace ITAM.Tests.Filters;

public class AuditActionFilterTests
{
    [Theory]
    [InlineData("POST", new string[0], "Create")]
    [InlineData("PUT", new[] { "5" }, "Update")]
    [InlineData("PATCH", new[] { "5" }, "Update")]
    [InlineData("DELETE", new[] { "5" }, "Delete")]
    [InlineData("PATCH", new[] { "5", "status" }, "UpdateStatus")]
    [InlineData("PATCH", new[] { "5", "priority" }, "UpdatePriority")]
    [InlineData("PATCH", new[] { "5", "assign" }, "Assign")]
    [InlineData("POST", new[] { "5", "return" }, "Return")]
    [InlineData("POST", new[] { "5", "assign" }, "Assign")]
    [InlineData("DELETE", new[] { "5", "assign", "9" }, "Unassign")]
    [InlineData("GET", new[] { "5", "document" }, "Print")]
    public void DeriveAction_MapsMethodAndPath(string method, string[] segments, string expected) =>
        Assert.Equal(expected, AuditActionFilter.DeriveAction(method, segments));

    [Theory]
    [InlineData("GET", new string[0])]
    [InlineData("GET", new[] { "5" })]
    [InlineData("GET", new[] { "search" })]
    public void DeriveAction_ReadOnlyRequests_AreNotAudited(string method, string[] segments) =>
        Assert.Null(AuditActionFilter.DeriveAction(method, segments));
}
