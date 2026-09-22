using System.Text.Json;
using KosManager.Application.Auth;
using Microsoft.AspNetCore.Diagnostics;

namespace KosManager.Api.Middleware;

// Domain exception -> HTTP status. Controller tetap tipis tanpa try/catch.
public static class ExceptionMapper
{
    public static void UseDomainExceptions(this WebApplication app) =>
        app.UseExceptionHandler(err => err.Run(async ctx =>
        {
            var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
            var (code, message) = ex switch
            {
                ConflictException e => (409, e.Message),
                UnauthorizedException e => (401, e.Message),
                ForbiddenException e => (403, e.Message),
                NotFoundException e => (404, e.Message),
                BadRequestException e => (400, e.Message),
                _ => (500, "kesalahan server"),
            };
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }));
}
