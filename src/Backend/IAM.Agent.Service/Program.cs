using IAM.Agent.Core;
using IAM.Agent.Service;

if (args.Length == 4 && args[0] == "--verify")
{
    var release = AgentRelease.Verify(File.ReadAllBytes(args[1]), File.ReadAllText(args[2]), "0.0.0", DateTimeOffset.UtcNow);
    await release.VerifyPackage(args[3]);
    Console.WriteLine(release.Version);
    return;
}
if (args.Length == 3 && args[0] == "--extract")
{
    AgentRelease.ExtractPackage(args[1], args[2]);
    return;
}
var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IAM.Agent");
var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IAM.Agent");
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "IAM.Agent");
builder.Services.AddSingleton(new SupervisorPaths(root, data));
builder.Services.AddHostedService<AgentSupervisor>();
await builder.Build().RunAsync();
