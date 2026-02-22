using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Application.Services.FfmpegProcessor;
using SubtitlesStreamer.Application.Services.PlaywrightService;

namespace SubtitlesStreamer.Application.Extensions.ServiceCollection;

public static class Services
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        return services
            .AddOpenApi()
            .AddSingleton<IPlaywrightService, PlaywrightService>()
            .AddSingleton<IFfmpegProcessorService, FfmpegProcessorService>();
    } 
}