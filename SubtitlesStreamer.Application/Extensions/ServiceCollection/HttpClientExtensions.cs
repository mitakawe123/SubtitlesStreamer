using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Application.Background;

namespace SubtitlesStreamer.Application.Extensions.ServiceCollection;

public static class HttpClientExtensions
{
    public static IServiceCollection AddLibreTranslate(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHostedService<DockerHostedService>();

        services
            .AddHttpClient("LibreTranslate", client =>
            {
                client.BaseAddress = new Uri(configuration["LibreTranslate:BaseUrl"] ?? "http://localhost:5000");
                client.Timeout = TimeSpan.FromSeconds(
                    configuration.GetValue("LibreTranslate:TimeoutSeconds", 30));
            });

        return services;
    }
    
    public static IServiceCollection AddFasterWhisper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient("FasterWhisper", client =>
        {
            client.BaseAddress = new Uri(configuration["FasterWhisper:BaseUrl"]
                                         ?? "http://localhost:8000");
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue("FasterWhisper:TimeoutSeconds", 300));
        });

        return services;
    }
}