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
    private static readonly IList<string> FasterWhisperEnv =
    [
        "WHISPER_MODEL_TTL=0",
        "WHISPER_PRELOAD_MODEL=Systran/faster-whisper-tiny",
        "WHISPER_COMPUTE_TYPE=int8",
        "OMP_NUM_THREADS=4",
        "WHISPER_INFERENCE_DEVICE=cpu"
    ];
    
    private const string LibreTranslateContainerName = "libretranslate";
    private const string LibreTranslateImageName = "libretranslate/libretranslate";
    private const string LibreTranslateImageTag = "latest";

    private const string FasterWhisperContainerName = "faster-whisper";
    private const string FasterWhisperImageName = "fedirz/faster-whisper-server";
    private const string FasterWhisperImageTag = "latest-cpu";
    
    private readonly DockerClient _docker = CreateDockerClient();

    private string _loadOnly = string.Empty;

    public async Task StartAsync(CancellationToken token)
    {
        var languages = configuration
            .GetSection("StreamingOptions:SupportedLanguages")
            .Get<string[]>() ?? ["en"];

        _loadOnly = string.Join(",", languages);

        logger.LogInformation("Ensuring all containers are ready before app starts...");

        await Task.WhenAll(
            EnsureContainerAsync(
                LibreTranslateContainerName,
                LibreTranslateImageName,
                imageTag: LibreTranslateImageTag,
                cmd: ["--load-only", _loadOnly],
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
                token),
            EnsureContainerAsync(
                FasterWhisperContainerName,
                FasterWhisperImageName,
                imageTag: FasterWhisperImageTag,
                cmd: [],
                env: FasterWhisperEnv,
                portBindings: new Dictionary<string, IList<PortBinding>>
                {
                    ["8000/tcp"] = [new PortBinding { HostPort = "8000" }]
                },
                exposedPorts: new Dictionary<string, EmptyStruct>
                {
                    ["8000/tcp"] = default
                },
                healthUrl: "http://localhost:8000/health",
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
            await EnsureContainerRunningAsync(containerName, containerId, healthUrl, token);
        else
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

    private async Task EnsureContainerRunningAsync(
        string containerName,
        string containerId,
        string healthUrl,
        CancellationToken token)
    {
        ContainerInspectResponse container;

        try
        {
            container = await _docker.Containers.InspectContainerAsync(containerId, token);
        }
        catch (DockerContainerNotFoundException)
        {
            logger.LogWarning("[{Container}] Not found, will recreate.", containerName);
            return;
        }

        if (container.State.Running)
        {
            logger.LogInformation("[{Container}] Already running", containerName);
            await WaitUntilReadyAsync(containerName, healthUrl, token);
            return;
        }

        logger.LogInformation("[{Container}] Starting existing container...", containerName);
        await _docker.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), token);
        await WaitUntilReadyAsync(containerName, healthUrl, token);
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

    public Task StopAsync(CancellationToken token)
    {
        _docker.Dispose();
        return Task.CompletedTask;
    }
}