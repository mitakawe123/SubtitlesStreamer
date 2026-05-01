using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Application.Background;

namespace SubtitlesStreamer.Application.Extensions.ServiceCollection;

public static class Background
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        return services
            .AddHostedService<StreamInitializerJob>()
            .AddHostedService<AudioReaderJob>()
            .AddHostedService<AudioWriterJob>()
            .AddHostedService<TranslationJob>();
    }
}