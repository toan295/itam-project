using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ITAM.API.Middlewares.Authorization;

public sealed class DepartmentAuthorizationHandler : AuthorizationHandler<DepartmentRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DepartmentRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);

        // Admin IT can access resources from every department.
        if (string.Equals(role, "Admin IT", StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Only Manager/Technician are evaluated by DepartmentId.
        if (!string.Equals(role, "Manager", StringComparison.Ordinal)
            && !string.Equals(role, "Technician", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var departmentClaim = context.User.FindFirst("DepartmentId")?.Value;
        if (!int.TryParse(departmentClaim, out var userDepartmentId))
        {
            return Task.CompletedTask;
        }

        // Week 2 demonstration: compare the token's DepartmentId with
        // the {departmentId} route value. Real modules will later obtain
        // the resource department from the database instead.
        if (context.Resource is HttpContext httpContext
            && httpContext.Request.RouteValues.TryGetValue("departmentId", out var routeValue)
            && int.TryParse(routeValue?.ToString(), out var resourceDepartmentId)
            && userDepartmentId == resourceDepartmentId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
