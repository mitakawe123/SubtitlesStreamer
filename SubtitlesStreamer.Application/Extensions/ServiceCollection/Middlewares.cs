using Microsoft.AspNetCore.Builder;

namespace SubtitlesStreamer.Application.Extensions.ServiceCollection;

public static class Middlewares
{
    public static void RegisterMiddlewares(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "SubtitlesStreamer"));

        app.UseHttpsRedirection();
    }
}