using IAM.Agent;
using IAM.Agent.Core;
using System.Text.Json;

if (args.Length == 2 && args[0] == "--create-identity")
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows is required.");
    Console.WriteLine(JsonSerializer.Serialize(DeviceKey.CreateOrRead(Path.GetFullPath(args[1])), AgentSettings.Json));
    return;
}

var configIndex = Array.IndexOf(args, "--config");
if (configIndex < 0 || configIndex + 1 >= args.Length)
    throw new InvalidOperationException("Start IAM.Agent through its installed Windows service or provide --config.");
var settings = AgentSettings.Load(Path.GetFullPath(args[configIndex + 1]));
var healthToken = Environment.GetEnvironmentVariable("IAM_AGENT_HEALTH_TOKEN")
    ?? throw new InvalidOperationException("The supervisor health token is required.");
await AgentWeb.Build(settings, healthToken, directory: Path.GetDirectoryName(Path.GetFullPath(args[configIndex + 1]))).RunAsync();
