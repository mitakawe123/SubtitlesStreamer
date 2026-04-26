using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Extensions.Channels;

public static class StreamChannels
{
    public static IServiceCollection AddChannels(this IServiceCollection services)
    {
        var streamContextChannel = Channel.CreateUnbounded<StreamContext>();
        var languageContextChannel = Channel.CreateUnbounded<LanguageContext>();
        var audioChannel = Channel.CreateUnbounded<AudioDto>();
        var translationResult = Channel.CreateBounded<TranslationResult>(options: new BoundedChannelOptions(100)
        {
            Capacity = 20,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        var translationTask = Channel.CreateBounded<TranslationTask>(options: new BoundedChannelOptions(100)
        {
            Capacity = 20,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        
        services
            .AddSingleton(streamContextChannel)
            .AddSingleton(streamContextChannel.Reader)
            .AddSingleton(streamContextChannel.Writer);

        services
            .AddSingleton(languageContextChannel)
            .AddSingleton(languageContextChannel.Reader)
            .AddSingleton(languageContextChannel.Writer);

        services
            .AddSingleton(audioChannel)
            .AddSingleton(audioChannel.Reader)
            .AddSingleton(audioChannel.Writer);

        services
            .AddSingleton(translationResult)
            .AddSingleton(translationResult.Reader)
            .AddSingleton(translationResult.Writer);
        
        services
            .AddSingleton(translationTask)
            .AddSingleton(translationTask.Reader)
            .AddSingleton(translationTask.Writer);
        
        return services;
    }
}