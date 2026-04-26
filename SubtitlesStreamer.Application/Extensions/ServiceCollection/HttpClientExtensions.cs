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
        services.AddHostedService<LibreTranslateHostedService>();

        services
            .AddHttpClient("LibreTranslate", client =>
            {
                client.BaseAddress = new Uri(configuration["LibreTranslate:BaseUrl"] ?? "http://localhost:5000");
                client.Timeout = TimeSpan.FromSeconds(
                    configuration.GetValue("LibreTranslate:TimeoutSeconds", 30));
            });

        return services;
    }
}