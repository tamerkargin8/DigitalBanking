using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Filters;

/// <summary>
/// Enables Idempotency-Key handling for a money-moving or approval action.
/// Clients may omit the header; when supplied, a repeated request with the same
/// key and payload returns the original response instead of applying money twice.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : TypeFilterAttribute
{
    public IdempotentAttribute() : base(typeof(IdempotencyFilter))
    {
    }
}
