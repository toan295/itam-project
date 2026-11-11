using ITAM.API.Models.DTOs;
using ITAM.API.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Filters;

// Xung đột phiên bản (kiểm tra ở Service hoặc do EF phát hiện lúc lưu khi hai request ghi gần như đồng thời) -> 409.
public class ConcurrencyExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ConcurrencyConflictException or DbUpdateConcurrencyException)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(ConcurrencyConflictException.DefaultMessage))
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
            context.ExceptionHandled = true;
        }
    }
}
