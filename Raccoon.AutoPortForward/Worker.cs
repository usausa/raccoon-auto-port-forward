namespace Raccoon.AutoPortForward;

using Microsoft.Extensions.Options;

using Renci.SshNet;
using Renci.SshNet.Common;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> log;

    private readonly SshSetting setting;

    private SshClient? client;

    private TaskCompletionSource<bool>? disconnectSignal;

    public Worker(
        ILogger<Worker> log,
        IOptions<SshSetting> setting)
    {
        this.log = log;
        this.setting = setting.Value;
    }

    public override void Dispose()
    {
        base.Dispose();

        client?.Dispose();
    }

#pragma warning disable CA1031
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectDelay = TimeSpan.FromSeconds(setting.ReconnectDelay);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if ((client is null) || !client.IsConnected)
                {
                    await SetupConnection(stoppingToken);
                }

                if (client is not null)
                {
                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    disconnectSignal = tcs;

                    while (client.IsConnected)
                    {
                        var delayTask = Task.Delay(1000, stoppingToken);
                        var completed = await Task.WhenAny(tcs.Task, delayTask);
                        if (completed == tcs.Task)
                        {
                            break;
                        }
                    }

                    disconnectSignal = null;
                }
            }
            catch (Exception e)
            {
                log.ErrorUnhandledException(e);
            }

            client?.Dispose();
            client = null;

            await Task.Delay(reconnectDelay, stoppingToken);
        }
    }
#pragma warning restore CA1031

    private async ValueTask SetupConnection(CancellationToken stoppingToken)
    {
        try
        {
            var ci = new ConnectionInfo(setting.Host, setting.Port, setting.Username, new PrivateKeyAuthenticationMethod(setting.Username, new PrivateKeyFile(setting.PrivateKey, setting.Passphrase)))
            {
                Timeout = TimeSpan.FromSeconds(setting.ConnectTimeout)
            };
            if (setting.DisableCompression)
            {
                ci.CompressionAlgorithms.Clear();
                ci.CompressionAlgorithms.Add("none", null);
            }

            client = new SshClient(ci)
            {
                KeepAliveInterval = TimeSpan.FromSeconds(setting.KeepAliveInterval)
            };
            client.ErrorOccurred += ClientOnErrorOccurred;

            await client.ConnectAsync(stoppingToken);

            log.InfoClientConnected();

            var forwardedPorts = new List<ForwardedPort>(setting.PortForwards.Length);
            foreach (var forward in setting.PortForwards)
            {
#pragma warning disable CA2000
                var forwardedPort = forward.Remote
                    ? (ForwardedPort)new ForwardedPortRemote(forward.BoundHost, forward.BoundPort, forward.Host, forward.Port)
                    : new ForwardedPortLocal(forward.BoundHost, forward.BoundPort, forward.Host, forward.Port);
#pragma warning restore CA2000
                client.AddForwardedPort(forwardedPort);
                forwardedPorts.Add(forwardedPort);
            }

            Parallel.ForEach(forwardedPorts, forwardedPort => forwardedPort.Start());
        }
        catch (Exception e)
        {
            log.ErrorConnectFailed(e);

            client?.Dispose();
            client = null;
            throw;
        }
    }

    private void ClientOnErrorOccurred(object? sender, ExceptionEventArgs e)
    {
        log.ErrorErrorOccurred(e.Exception);

        disconnectSignal?.TrySetResult(true);
    }
}
