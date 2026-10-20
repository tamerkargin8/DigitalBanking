using System.Net;
using System.Text.Json;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.Exceptions;

namespace DigitalBanking.API.Middleware;

public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled request exception.");

            if (context.Response.HasStarted)
                throw;

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new ApiResponse
        {
            Success = false,
            Message = exception switch
            {
                NotFoundException => exception.Message,
                BusinessRuleException => exception.Message,
                ConcurrencyException => exception.Message,
                TooManyRequestsException => exception.Message,
                ForbiddenException => exception.Message,
                ConflictException => exception.Message,
                UnauthorizedAccessException => "Authentication is required.",
                _ => "Internal server error."
            }
        };

        context.Response.StatusCode = exception switch
        {
            NotFoundException => (int)HttpStatusCode.NotFound,
            BusinessRuleException => (int)HttpStatusCode.BadRequest,
            ConcurrencyException => (int)HttpStatusCode.Conflict,
            TooManyRequestsException => (int)HttpStatusCode.TooManyRequests,
            ForbiddenException => (int)HttpStatusCode.Forbidden,
            ConflictException => (int)HttpStatusCode.Conflict,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            _ => (int)HttpStatusCode.InternalServerError
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}
