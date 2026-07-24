namespace Raccoon.AutoPortForward;

internal sealed class PortForwardsSetting
{
    public bool Remote { get; set; }

    public string BoundHost { get; set; } = "127.0.0.1";

    public uint BoundPort { get; set; }

    public string Host { get; set; } = default!;

    public uint Port { get; set; }
}

internal sealed class SshSetting
{
    public string Host { get; set; } = default!;

    public int Port { get; set; }

    public string Username { get; set; } = default!;

    public string PrivateKey { get; set; } = default!;

    public string? Passphrase { get; set; }

    public bool DisableCompression { get; set; } = true;

    public int KeepAliveInterval { get; set; } = 30;

    public int ReconnectDelay { get; set; } = 10;

    public PortForwardsSetting[] PortForwards { get; set; } = default!;
}
