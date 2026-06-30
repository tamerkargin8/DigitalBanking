using System.Net;
using System.Text.Json;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.Exceptions;

namespace DigitalBanking.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
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
            _logger.LogError(ex, ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            NotFoundException =>
                new ApiResponse
                {
                    Success = false,
                    Message = exception.Message
                },

            BusinessRuleException =>
                new ApiResponse
                {
                    Success = false,
                    Message = exception.Message
                },

            ConcurrencyException =>
                new ApiResponse
                {
                    Success = false,
                    Message = exception.Message
                },

            _ =>
                new ApiResponse
                {
                    Success = false,
                    Message = "Internal server error."
                }
        };

        context.Response.StatusCode = exception switch
        {
            NotFoundException => (int)HttpStatusCode.NotFound,
            BusinessRuleException => (int)HttpStatusCode.BadRequest,
            ConcurrencyException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}