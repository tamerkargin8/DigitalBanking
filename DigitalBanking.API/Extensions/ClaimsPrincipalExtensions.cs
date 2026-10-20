using System.Security.Claims;
using DigitalBanking.Application.Common;
using DigitalBanking.Domain.Enums;

namespace DigitalBanking.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("User id claim was not found.");

        if (!Guid.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException("Invalid user id claim.");

        return userId;
    }

    public static string GetUsername(this ClaimsPrincipal user)
    {
        var username = user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username))
            throw new UnauthorizedAccessException("Username claim was not found.");

        return username;
    }

    public static string GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role)
            ?? throw new UnauthorizedAccessException("Role claim was not found.");
    }

    /// <summary>
    /// Builds the authenticated actor used by services. All values come from the
    /// validated JWT; no request body value can impersonate another user.
    /// </summary>
    public static ActorContext GetActor(this ClaimsPrincipal user)
    {
        var roleText = user.GetRole();
        if (!Enum.TryParse<UserRole>(roleText, ignoreCase: true, out var role) || role == UserRole.None)
            throw new UnauthorizedAccessException("Invalid role claim.");

        return new ActorContext(user.GetUserId(), user.GetUsername(), role);
    }
}
