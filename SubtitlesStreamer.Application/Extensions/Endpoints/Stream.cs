using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SubtitlesStreamer.Domain.DTOs;
using SubtitlesStreamer.Domain.Options;

namespace SubtitlesStreamer.Application.Extensions.Endpoints;
public static class StreamEndpoints
{
    public static void RegisterStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/stream/start", HandleStreamStart);
        return;

        static async Task<IResult> HandleStreamStart(
            [FromBody] StreamContext stream,
            [FromServices] ChannelWriter<StreamContext> writer,
            [FromServices] IOptions<StreamingOptions> options,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stream.Url))
                return Results.BadRequest($"{nameof(stream.Url)} is empty");

            var supported = options.Value.SupportedLanguages;

            if (!supported.Contains(stream.SourceLanguage))
                return Results.BadRequest($"Unsupported source language: {stream.SourceLanguage}");

            if (!supported.Contains(stream.TargetLanguage))
                return Results.BadRequest($"Unsupported target language: {stream.TargetLanguage}");

            await writer.WriteAsync(stream, cancellationToken);

            return Results.Accepted();
        }
    }
}