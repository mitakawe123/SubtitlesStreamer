using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SubtitlesStreamer.Domain.DTOs;

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
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stream.Url))
                return Results.BadRequest($"{nameof(stream.Url)} is empty");

            await writer.WriteAsync(stream, cancellationToken);
            return Results.Accepted();
        }
    }
}