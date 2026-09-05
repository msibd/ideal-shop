using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Optical.Application.Abstractions.Identity;

namespace Optical.Infrastructure.Identity;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => httpContextAccessor.HttpContext?.User.Identity?.Name;

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
