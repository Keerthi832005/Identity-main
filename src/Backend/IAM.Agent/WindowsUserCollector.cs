using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Identity.Contracts.Agents;

namespace IAM.Agent;

/// <summary>Interactive Windows accounts and profile metadata only; never the service identity or profile contents.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsUserCollector
{
    public static WindowsUserInventory Collect(List<string> warnings)
    {
        var sessions = new List<WindowsSession>();
        var sessionsReported = WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, out var buffer, out var count);
        if (sessionsReported)
        {
            try
            {
                var console = WTSGetActiveConsoleSessionId();
                var size = Marshal.SizeOf<SessionInfo>();
                for (var i = 0; i < count && sessions.Count < 128; i++)
                {
                    var entry = Marshal.PtrToStructure<SessionInfo>(IntPtr.Add(buffer, i * size));
                    var user = ReadSession(entry.Id, 5);
                    if (user is null) { sessionsReported = false; continue; }
                    if (user.Length == 0 || entry.Id == 0) continue;
                    var domain = ReadSession(entry.Id, 7);
                    if (domain is null) sessionsReported = false;
                    sessions.Add(new(entry.Id, Clean(string.IsNullOrEmpty(domain) ? user : $"{domain}\\{user}", 256),
                        entry.State switch { 0 => "Active", 1 => "Connected", 4 => "Disconnected", _ => "Other" },
                        (uint)entry.Id == console));
                }
                if (count > 128) warnings.Add("Windows session inventory may be truncated at 128 user sessions.");
            }
            finally { WTSFreeMemory(buffer); }
        }
        if (!sessionsReported) warnings.Add("Some Windows sessions could not be read.");

        var profiles = new List<WindowsProfile>();
        var profilesReported = true;
        var profilesNamed = true;
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope("root\\CIMV2"),
                new ObjectQuery("SELECT SID, LocalPath, Loaded FROM Win32_UserProfile WHERE Special = FALSE"),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = false });
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    if (profiles.Count == 256) { warnings.Add("Windows profile inventory truncated at 256 profiles."); break; }
                    var sid = Clean(item["SID"], 184);
                    if (sid.Length == 0) continue;
                    // A deleted/unreachable domain account can still have a local profile. Keep its SID and folder name.
                    var name = Clean(Path.GetFileName(Clean(item["LocalPath"], 1024).TrimEnd('\\')), 256);
                    try { name = Clean(new SecurityIdentifier(sid).Translate(typeof(NTAccount)).Value, 256); }
                    // Win32Exception covers a broken domain trust, which fails every SID rather than one.
                    catch (Exception e) when (e is IdentityNotMappedException or ArgumentException
                        or System.Security.SecurityException or Win32Exception)
                    { profilesNamed = false; /* The profile remains identifiable without domain resolution. */ }
                    profiles.Add(new(sid, name.Length > 0 ? name : sid, item["Loaded"] is true));
                }
            }
        }
        catch (Exception e) when (e is ManagementException or COMException or UnauthorizedAccessException or Win32Exception)
        { profilesReported = false; warnings.Add("Some Windows user profiles could not be read."); }
        if (!profilesNamed) warnings.Add("Some Windows profiles show their SID because the account could not be resolved.");

        var active = sessions.Where(s => s.State == "Active").ToArray();
        var current = active.FirstOrDefault(s => s.IsConsole)?.UserName
            ?? (sessionsReported && active.Length == 1 ? active[0].UserName : null);
        return new(current, sessions.ToArray(), profiles.OrderBy(p => p.UserName, StringComparer.OrdinalIgnoreCase).ToArray(),
            sessionsReported, profilesReported);
    }

    private static string? ReadSession(int id, int field)
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, id, field, out var value, out _)) return null;
        try { return Clean(Marshal.PtrToStringUni(value), 256); }
        finally { WTSFreeMemory(value); }
    }
    private static string Clean(object? value, int max) => new((value?.ToString() ?? "").Where(c => !char.IsControl(c)).Take(max).ToArray());
    [StructLayout(LayoutKind.Sequential)]
    private struct SessionInfo { public int Id; public IntPtr Station; public int State; }
    [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSEnumerateSessionsW(IntPtr server, int reserved, int version, out IntPtr sessions, out int count);
    [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int field, out IntPtr value, out int bytes);
    [DllImport("wtsapi32.dll", ExactSpelling = true)]
    private static extern void WTSFreeMemory(IntPtr memory);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint WTSGetActiveConsoleSessionId();
}
