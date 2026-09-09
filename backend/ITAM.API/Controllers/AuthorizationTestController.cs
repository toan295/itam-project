using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Temporary Week 2 endpoints used only to verify authorization behavior.
// Remove them after real module controllers apply the policies.
[ApiController]
[Route("api/v1/authorization-test")]
public class AuthorizationTestController : ControllerBase
{
    [Authorize(Roles = "Admin IT")]
    [HttpGet("admin-only")]
    public IActionResult AdminOnly()
    {
        return Ok(new { message = "Admin IT authorization succeeded." });
    }

    [Authorize(Policy = "SameDepartmentOnly")]
    [HttpGet("departments/{departmentId:int}")]
    public IActionResult SameDepartment(int departmentId)
    {
        return Ok(new
        {
            message = "Department authorization succeeded.",
            departmentId
        });
    }
}
