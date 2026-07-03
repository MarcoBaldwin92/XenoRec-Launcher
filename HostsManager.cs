using System;
using System.IO;
using System.Text;

namespace ErectRoom
{
    public class HostsManager
    {
        private static readonly string HostsPath = GetHostsPath();
        private static readonly string BackupPath = GetBackupPath();

        private static readonly string[] Entries =
        {
            "127.0.0.1 api.rec.net",
            "127.0.0.1 auth.rec.net",
            "127.0.0.1 ns.rec.net",
            "127.0.0.1 econ.rec.net",
            "127.0.0.1 accounts.rec.net",
            "127.0.0.1 bugreporting.rec.net",
            "127.0.0.1 cdn.rec.net",
            "127.0.0.1 cms.rec.net",
            "127.0.0.1 cards.rec.net",
            "127.0.0.1 chat.rec.net",
            "127.0.0.1 clubs.rec.net",
            "127.0.0.1 commerce.rec.net",
            "127.0.0.1 data.rec.net",
            "127.0.0.1 datacollection.rec.net",
            "127.0.0.1 discovery.rec.net",
            "127.0.0.1 gamelogs.rec.net",
            "127.0.0.1 geo.rec.net",
            "127.0.0.1 img.rec.net",
            "127.0.0.1 leaderboard.rec.net",
            "127.0.0.1 link.rec.net",
            "127.0.0.1 lists.rec.net",
            "127.0.0.1 match.rec.net",
            "127.0.0.1 moderation.rec.net",
            "127.0.0.1 notify.rec.net",
            "127.0.0.1 platformnotifications.rec.net",
            "127.0.0.1 playersettings.rec.net",
            "127.0.0.1 roomcomments.rec.net",
            "127.0.0.1 roomieintegrations.rec.net",
            "127.0.0.1 rooms.rec.net",
            "127.0.0.1 storage.rec.net",
            "127.0.0.1 strings.rec.net",
            "127.0.0.1 strings-cdn.rec.net",
            "127.0.0.1 studio.rec.net",
            "127.0.0.1 thorn.rec.net",
            "127.0.0.1 videos.rec.net"
        };

        private static string GetHostsPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string steamRoot = Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH");
                if (!string.IsNullOrEmpty(steamRoot))
                    return Path.Combine(steamRoot, "pfx/drive_c/windows/system32/drivers/etc/hosts");
                return "/etc/hosts";
            }
            return @"C:\Windows\System32\drivers\etc\hosts";
        }

        private static string GetBackupPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string steamRoot = Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH");
                if (!string.IsNullOrEmpty(steamRoot))
                    return Path.Combine(steamRoot, "pfx/drive_c/windows/system32/drivers/etc/hosts.revival.bak");
                return "/etc/hosts.revival.bak";
            }
            return @"C:\Windows\System32\drivers\etc\hosts.revival.bak";
        }

        public void BackupAndModifyHosts()
        {
            try
            {
                if (!File.Exists(BackupPath))
                    File.Copy(HostsPath, BackupPath);

                var content = File.ReadAllText(HostsPath);
                var sb = new StringBuilder(content);

                foreach (var entry in Entries)
                {
                    if (!content.Contains(entry))
                        sb.AppendLine(entry);
                }

                File.WriteAllText(HostsPath, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HostsManager] Failed to patch hosts file: {ex.Message}");
            }
        }

        public void RestoreHosts()
        {
            try
            {
                if (File.Exists(BackupPath))
                {
                    File.Copy(BackupPath, HostsPath, overwrite: true);
                    File.Delete(BackupPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HostsManager] Failed to restore hosts file: {ex.Message}");
            }
        }
    }
}