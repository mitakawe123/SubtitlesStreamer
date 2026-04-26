using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SubtitlesStreamer.Application.Background;

public sealed class LibreTranslateHostedService(
    IConfiguration configuration,
    ILogger<LibreTranslateHostedService> logger) : IHostedLifecycleService
{
    private const string ContainerName = "libretranslate";
    private const string ImageName = "libretranslate/libretranslate";

    private readonly DockerClient _docker = new DockerClientConfiguration(
            new Uri("unix:///var/run/docker.sock"))  // Linux
        .CreateClient();
    
    public async Task StartAsync(CancellationToken token)
    {
        var languages = configuration
            .GetSection("StreamingOptions:SupportedLanguages")
            .Get<string[]>() ?? ["en"];

        var loadOnly = string.Join(",", languages);

        logger.LogInformation("Ensuring LibreTranslate is ready before app starts...");

        await EnsureImageExistsAsync(token);

        var containerId = await GetExistingContainerIdAsync(token);

        if (containerId is not null)
            await EnsureContainerRunningAsync(containerId, token);
        else
            await CreateAndStartContainerAsync(loadOnly, token);
    }

    private async Task EnsureImageExistsAsync(CancellationToken token)
    {
        var images = await _docker.Images.ListImagesAsync(new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["reference"] = new Dictionary<string, bool> { [ImageName] = true }
            }
        }, token);

        if (images.Count != 0)
            return;

        logger.LogInformation("Pulling {Image} image...", ImageName);

        await _docker.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = ImageName, Tag = "latest" },
            null,
            new Progress<JSONMessage>(m => logger.LogDebug("Docker pull: {Status}", m.Status)),
            token);
    }

    private async Task<string?> GetExistingContainerIdAsync(CancellationToken token)
    {
        var containers = await _docker.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [$"/{ContainerName}"] = true }
            }
        }, token);

        return containers.FirstOrDefault()?.ID;
    }

    private async Task EnsureContainerRunningAsync(string containerId, CancellationToken token)
    {
        var container = await _docker.Containers.InspectContainerAsync(containerId, token);

        if (container.State.Running)
        {
            logger.LogInformation("LibreTranslate container is already running");
            return;
        }

        logger.LogInformation("Starting existing LibreTranslate container...");
        await _docker.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), token);
        await WaitUntilReadyAsync(token);
    }

    private async Task CreateAndStartContainerAsync(string loadOnly, CancellationToken token)
    {
        logger.LogInformation("Creating LibreTranslate container...");

        var response = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = ContainerName,
            Image = ImageName,
            Cmd = ["--load-only", loadOnly],
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["5000/tcp"] = [new PortBinding { HostPort = "5000" }]
                },
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped }
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["5000/tcp"] = default
            }
        }, token);

        await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), token);

        logger.LogInformation("LibreTranslate container started, waiting for readiness...");
        await WaitUntilReadyAsync(token);
    }

    private async Task WaitUntilReadyAsync(CancellationToken token)
    {
        using var http = new HttpClient();

        var deadline = DateTime.UtcNow.AddMinutes(5);

        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var response = await http.GetAsync("http://localhost:5000/languages", token);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("LibreTranslate is ready");
                    return;
                }
            }
            catch
            {
                // still booting
            }

            await Task.Delay(2000, token);
        }

        throw new TimeoutException("LibreTranslate did not become ready within 5 minutes");
    }
    
    public Task StartedAsync(CancellationToken token) => Task.CompletedTask;
    
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken token) => Task.CompletedTask;
    
    public Task StoppedAsync(CancellationToken token) => Task.CompletedTask;

    public Task StopAsync(CancellationToken token)
    {
        _docker.Dispose();
        return Task.CompletedTask;
    }
}