using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Identity.Contracts.Agents;
using Microsoft.Win32;

namespace IAM.Agent;

[SupportedOSPlatform("windows")]
public static class MachineCollector
{
    public static MachineInventory Collect(Guid installationId)
    {
        var warnings = new List<string>();
        var system = Query("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem", warnings);
        var bios = Query("SELECT SerialNumber FROM Win32_BIOS", warnings);
        var cpu = Query("SELECT Name FROM Win32_Processor", warnings);
        var disks = new List<DiskInventory>();
        foreach (var disk in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Take(64))
        {
            try { if (disk.IsReady) disks.Add(new(disk.Name, disk.TotalSize, disk.AvailableFreeSpace)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warnings.Add("Some disk information could not be read."); }
        }
        var software = new List<InstalledSoftware>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var uninstall = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                foreach (var name in uninstall?.GetSubKeyNames() ?? [])
                {
                    using var entry = uninstall!.OpenSubKey(name);
                    var display = Clean(entry?.GetValue("DisplayName"), 200);
                    if (display.Length > 0) software.Add(new(
                        display,
                        Clean(entry?.GetValue("DisplayVersion"), 100),
                        Clean(entry?.GetValue("Publisher"), 200),
                        SoftwareInventoryMetadata.ReadInstallDate(entry?.GetValue("InstallDate")),
                        SoftwareInventoryMetadata.ReadEstimatedSizeBytes(entry?.GetValue("EstimatedSize")),
                        Environment.Is64BitOperatingSystem && view == RegistryView.Registry64 ? "64-bit" : "32-bit"));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { warnings.Add("Some machine-wide installed software could not be read."); }
        }
        warnings.Add("Software inventory covers machine-wide uninstall registrations; per-user and portable apps are excluded.");
        var all = software.Distinct().OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (all.Length > 2000) warnings.Add("Software list truncated at 2000 entries.");
        _ = long.TryParse(system.GetValueOrDefault("TotalPhysicalMemory"), out var memory);
        var systemUptime = Math.Max(0, Environment.TickCount64 / 1000);
        using var process = Process.GetCurrentProcess();
        var agentUptime = Math.Clamp((long)(DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds, 0, systemUptime);
        var windowsUsers = WindowsUserCollector.Collect(warnings);
        return new(installationId, DateTimeOffset.UtcNow, Environment.MachineName, AgentWeb.Version,
            RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture.ToString(),
            new(system.GetValueOrDefault("Manufacturer", ""), system.GetValueOrDefault("Model", ""),
                bios.GetValueOrDefault("SerialNumber", ""), cpu.GetValueOrDefault("Name", ""), Environment.ProcessorCount, memory, disks.ToArray()),
            all.Take(2000).ToArray(), warnings.Distinct().Take(20).ToArray(), new(systemUptime, agentUptime), windowsUsers);
    }

    private static Dictionary<string, string> Query(string query, List<string> warnings)
    {
        var values = new Dictionary<string, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope("root\\CIMV2"), new ObjectQuery(query),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = false });
            using var results = searcher.Get();
            foreach (ManagementBaseObject result in results)
            {
                using (result) foreach (PropertyData property in result.Properties) values[property.Name] = Clean(property.Value, 200);
                break;
            }
        }
        catch (Exception e) when (e is ManagementException or COMException or UnauthorizedAccessException)
        { warnings.Add("Some hardware information could not be read from Windows WMI."); }
        return values;
    }
    private static string Clean(object? value, int length) => new((value?.ToString() ?? "").Where(c => !char.IsControl(c)).Take(length).ToArray());
}
