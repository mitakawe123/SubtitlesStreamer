using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SubtitlesStreamer.Domain.Options;

namespace SubtitlesStreamer.Application.Extensions.Endpoints;

public static class LanguageEndpoints
{
    public static void RegisterLanguageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/languages", ([FromServices] IOptions<StreamingOptions> options) =>
            Results.Ok(options.Value.SupportedLanguages));
    }
}