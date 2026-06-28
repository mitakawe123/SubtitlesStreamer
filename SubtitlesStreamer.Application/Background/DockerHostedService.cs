using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SubtitlesStreamer.Application.Background;

public sealed class DockerHostedService(
    IConfiguration configuration,
    ILogger<DockerHostedService> logger) : IHostedLifecycleService
{
    private const string LibreTranslateContainerName = "libretranslate";
    private const string LibreTranslateImageName = "libretranslate/libretranslate";
    private const string LibreTranslateImageTag = "latest";

    private readonly DockerClient _docker = CreateDockerClient();

    public async Task StartAsync(CancellationToken token)
    {
        var languages = configuration
            .GetSection("StreamingOptions:SupportedLanguages")
            .Get<string[]>() ?? ["en"];

        var loadOnly = string.Join(",", languages);

        logger.LogInformation("Ensuring all containers are ready before app starts...");

        await Task.WhenAll(
            EnsureContainerAsync(
                LibreTranslateContainerName,
                LibreTranslateImageName,
                imageTag: LibreTranslateImageTag,
                cmd: ["--load-only", loadOnly],
                env: [],
                portBindings: new Dictionary<string, IList<PortBinding>>
                {
                    ["5000/tcp"] = [new PortBinding { HostPort = "5000" }]
                },
                exposedPorts: new Dictionary<string, EmptyStruct>
                {
                    ["5000/tcp"] = default
                },
                healthUrl: "http://localhost:5000/languages",
                token));
    }

    private async Task EnsureContainerAsync(
        string containerName,
        string imageName,
        string imageTag,
        IList<string> cmd,
        IList<string> env,
        Dictionary<string, IList<PortBinding>> portBindings,
        Dictionary<string, EmptyStruct> exposedPorts,
        string healthUrl,
        CancellationToken token)
    {
        await EnsureImageExistsAsync(imageName, imageTag, token);

        var containerId = await GetExistingContainerIdAsync(containerName, token);
        logger.LogInformation("[{Container}] Existing container id: {ContainerId}", containerName, containerId ?? "null");

        if (containerId is not null)
        {
            ContainerInspectResponse container;

            try
            {
                container = await _docker.Containers.InspectContainerAsync(containerId, token);
            }
            catch (DockerContainerNotFoundException)
            {
                logger.LogWarning("[{Container}] Not found during inspect, will recreate.", containerName);
                await CreateAndStartContainerAsync(containerName, imageName, imageTag, cmd, env, portBindings, exposedPorts, healthUrl, token);
                return;
            }

            if (container.State.Running)
            {
                logger.LogInformation("[{Container}] Already running", containerName);
                await WaitUntilReadyAsync(containerName, healthUrl, token);
                return;
            }

            // Stopped container — remove and recreate to avoid stale port binding
            logger.LogInformation("[{Container}] Found stopped — removing to recreate...", containerName);
            await _docker.Containers.RemoveContainerAsync(containerId,
                new ContainerRemoveParameters { Force = true }, token);
        }

        await CreateAndStartContainerAsync(containerName, imageName, imageTag, cmd, env, portBindings, exposedPorts, healthUrl, token);
    }

    private async Task EnsureImageExistsAsync(string imageName, string tag, CancellationToken token)
    {
        var images = await _docker.Images.ListImagesAsync(new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["reference"] = new Dictionary<string, bool> { [$"{imageName}:{tag}"] = true }
            }
        }, token);

        if (images.Count != 0)
            return;

        logger.LogInformation("Pulling {Image}:{Tag} image...", imageName, tag);

        await _docker.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = imageName, Tag = tag },
            null,
            new Progress<JSONMessage>(m => logger.LogDebug("Docker pull: {Status}", m.Status)),
            token);
    }

    private async Task<string?> GetExistingContainerIdAsync(string containerName, CancellationToken token)
    {
        var containers = await _docker.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [$"/{containerName}"] = true }
            }
        }, token);

        return containers.FirstOrDefault()?.ID;
    }

    private async Task CreateAndStartContainerAsync(
        string containerName,
        string imageName,
        string imageTag,
        IList<string> cmd,
        IList<string> env,
        Dictionary<string, IList<PortBinding>> portBindings,
        Dictionary<string, EmptyStruct> exposedPorts,
        string healthUrl,
        CancellationToken token)
    {
        logger.LogInformation("[{Container}] Creating container...", containerName);

        var response = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = containerName,
            Image = $"{imageName}:{imageTag}",
            Cmd = cmd,
            Env = env,
            HostConfig = new HostConfig
            {
                PortBindings = portBindings,
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped }
            },
            ExposedPorts = exposedPorts
        }, token);

        await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), token);

        logger.LogInformation("[{Container}] Started, waiting for readiness...", containerName);
        await WaitUntilReadyAsync(containerName, healthUrl, token);
    }

    private async Task WaitUntilReadyAsync(string containerName, string healthUrl, CancellationToken token)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(5);

        var deadline = DateTime.UtcNow.AddMinutes(10);

        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var response = await http.GetAsync(healthUrl, token);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("[{Container}] Is ready", containerName);
                    return;
                }
            }
            catch
            {
                // still booting
            }

            await Task.Delay(2000, token);
        }

        throw new TimeoutException($"[{containerName}] Did not become ready within 10 minutes");
    }

    private static DockerClient CreateDockerClient()
    {
        var desktopSocket = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".docker/desktop/docker.sock");

        var uri = File.Exists(desktopSocket)
            ? new Uri($"unix://{desktopSocket}")
            : new Uri("unix:///var/run/docker.sock");

        return new DockerClientConfiguration(uri).CreateClient();
    }

    public Task StartedAsync(CancellationToken token) => Task.CompletedTask;
    public Task StartingAsync(CancellationToken token) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken token) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken token) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken token)
    {
        var containerId = await GetExistingContainerIdAsync(LibreTranslateContainerName, token);
        if (containerId is not null)
        {
            await _docker.Containers.StopContainerAsync(containerId, 
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 }, token);
        }
        _docker.Dispose();
    }
}