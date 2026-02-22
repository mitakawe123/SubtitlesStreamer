using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Extensions.Channels;

public static class StreamChannels
{
    public static IServiceCollection AddChannelStreamer(this IServiceCollection services)
    {
        var streamContextChannel = Channel.CreateUnbounded<StreamContext>();
        var languageContextChannel = Channel.CreateUnbounded<LanguageContext>();
        var audioChannel = Channel.CreateUnbounded<AudioDto>();
        
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
        
        return services;
    }
}