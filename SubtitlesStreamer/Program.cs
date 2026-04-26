using SubtitlesStreamer.Application.Extensions.Channels;
using SubtitlesStreamer.Application.Extensions.Endpoints;
using SubtitlesStreamer.Application.Extensions.ServiceCollection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .ConfigureServices()
    .AddSettingsOptions(builder.Configuration)
    .AddChannels()
    .AddBackgroundServices();

var app = builder.Build();

app.RegisterMiddlewares();
app.RegisterStreamEndpoints();

app.Run();