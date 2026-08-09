using Raccoon.AutoPortForward;

using Serilog;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

// Service
builder.Services
    .AddWindowsService()
    .AddSystemd();

// Logging
builder.Logging.ClearProviders();
builder.Services.AddSerilog(options =>
{
    options.ReadFrom.Configuration(builder.Configuration);
});

// Setting
builder.Services.AddOptions<SshSetting>()
    .Bind(builder.Configuration.GetSection("SSH"))
    .Validate(static o => !String.IsNullOrWhiteSpace(o.Host), "SSH:Host is required.")
    .Validate(static o => o.Port is >= 1 and <= 65535, "SSH:Port must be between 1 and 65535.")
    .Validate(static o => !String.IsNullOrWhiteSpace(o.Username), "SSH:Username is required.")
    .Validate(static o => !String.IsNullOrWhiteSpace(o.PrivateKey), "SSH:PrivateKey is required.")
    .Validate(static o => o.PortForwards is { Length: > 0 }, "SSH:PortForwards must contain at least one entry.")
    .Validate(
        static o => (o.PortForwards is null) || Array.TrueForAll(o.PortForwards, static f => !String.IsNullOrWhiteSpace(f.Host)),
        "SSH:PortForwards:Host is required.")
    .Validate(
        static o => (o.PortForwards is null) || Array.TrueForAll(o.PortForwards, static f => f.Remote || IPAddress.TryParse(f.BoundHost, out _)),
        "SSH:PortForwards:BoundHost must be a valid IP address for a local forward.")
    .ValidateOnStart();

// Worker
builder.Services.AddHostedService<Worker>();

// Build
var host = builder.Build();

var log = host.Services.GetRequiredService<ILogger<Program>>();

// Startup information
ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
log.InfoServiceStart();
log.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.Version, Environment.CurrentDirectory);
log.InfoServiceSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);
log.InfoServiceSettingsThreadPool(workerThreads, completionPortThreads);

// Run
await host.RunAsync();
