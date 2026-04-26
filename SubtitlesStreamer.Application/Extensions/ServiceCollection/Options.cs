using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Domain.Options;

namespace SubtitlesStreamer.Application.Extensions.ServiceCollection;

public static class Options
{
    public static IServiceCollection AddSettingsOptions(this IServiceCollection services, IConfiguration configuration)
    {
        return services.Configure<StreamingOptions>(
            configuration.GetSection(nameof(StreamingOptions)));
    }
}