using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Application.Background;

namespace SubtitlesStreamer.Application.Extensions.ServiceCollection;

public static class HttpClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddLibreTranslate(IConfiguration configuration)
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

        public IServiceCollection AddGroq(IConfiguration configuration)
        {
            services.AddHttpClient("Groq", client =>
            {
                client.BaseAddress = new Uri("https://api.groq.com");
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", configuration["Groq:ApiKey"]);
            });

            return services;
        }
    }
}