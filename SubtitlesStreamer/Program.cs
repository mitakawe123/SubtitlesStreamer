using SubtitlesStreamer.Application.Extensions.Channels;
using SubtitlesStreamer.Application.Extensions.Endpoints;
using SubtitlesStreamer.Application.Extensions.ServiceCollection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services
    .ConfigureServices()
    .AddSettingsOptions(builder.Configuration)
    .AddChannels()
    .AddBackgroundServices();

builder.Services
    .AddLibreTranslate(builder.Configuration)
    .AddFasterWhisper(builder.Configuration);

var app = builder.Build();

app.RegisterMiddlewares();
app.RegisterStreamEndpoints();

app.Run();