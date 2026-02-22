using SubtitlesStreamer.Application.Extensions.Channels;
using SubtitlesStreamer.Application.Extensions.Endpoints;
using SubtitlesStreamer.Application.Extensions.ServiceCollection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .ConfigureServices()
    .AddChannelStreamer()
    .AddBackgroundServices();

var app = builder.Build();

app.RegisterMiddlewares();
app.RegisterStreamEndpoints();

app.Run();