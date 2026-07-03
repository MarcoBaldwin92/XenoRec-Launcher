using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ErectRoom
{
    class Program
    {
        private const string TargetProcessName = "Recroom_WindowsPlatformless";
        private const string TargetExeName = $"{TargetProcessName}.exe";
        private const string EmbeddedDllResourceName = "ErectRoom.Hook.dll";

        static async Task Main(string[] args)
        {
            Console.Title = "]>    Rec Room Revival Launcher";
            Console.WriteLine("[INFO]>    Rec Room Revival Launcher");

            if (!IsAdmin())
            {
                Console.WriteLine("[ERROR]>    Please run as Administrator (needed for port 443 and hosts file).");
                Console.ReadKey();
                return;
            }

            var hostsManager = new HostsManager();
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                Console.WriteLine("[INFO]>    Backing up and modifying hosts file...");
                hostsManager.BackupAndModifyHosts();

                Console.WriteLine("[INFO]>    Starting HTTPS reverse proxy on port 443...");
                var proxyTask = ProxyServer.StartAsync(cts.Token);

                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetExeName);

                if (!File.Exists(exePath))
                {
                    Console.WriteLine($"[ERROR]>    Could not find '{TargetExeName}' in the launcher's directory.");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"[INFO]>    Launching {TargetExeName}...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        Console.WriteLine($"[INFO]>    Successfully launched {TargetProcessName} (PID: {process.Id})");

                        // Set up automatic shutdown when the game exits
                        process.EnableRaisingEvents = true;
                        process.Exited += (sender, e) =>
                        {
                            Console.WriteLine($"\n[INFO]>    {TargetExeName} has closed.");
                            cts.Cancel(); // Triggers the cancellation token to exit the wait loop
                        };

                        Console.WriteLine("[INFO]>    Waiting 1500ms to ensure engine and modules are loaded...");
                        await Task.Delay(1500, cts.Token);

                        try
                        {
                            string tempDllPath = ExtractEmbeddedDll(EmbeddedDllResourceName);
                            Console.WriteLine($"[INFO]>    Extracted payload to: {tempDllPath}");

                            DllInjector.Inject(process.Id, tempDllPath);
                            Console.WriteLine("[INFO]>    Injection successful!");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR]>    Injection failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ERROR]>    Failed to start the process.");
                        return;
                    }
                }

                Console.WriteLine("[INFO]>    Running actively. Press Ctrl+C or close the game to exit cleanly.");

                try
                {
                    // Wait until either Ctrl+C is pressed OR the game process closes
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // Expected execution thread stop when game closes or Ctrl+C is hit
                }
            }
            finally
            {
                Console.WriteLine("\n[INFO]>    Restoring original hosts file...");
                hostsManager.RestoreHosts();
                Console.WriteLine("[INFO]>    Goodbye!");

                // Give the console a brief moment to show the goodbye message before closing the window
                await Task.Delay(1500);
            }
        }

        static string ExtractEmbeddedDll(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            string actualResourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

            if (actualResourceName == null)
            {
                throw new FileNotFoundException($"[ERROR]>    Embedded resource '{resourceName}' not found in assembly.");
            }

            string tempPath = Path.Combine(Path.GetTempPath(), $"RevivalPayload_{Guid.NewGuid():N}.dll");

            using (Stream stream = assembly.GetManifestResourceStream(actualResourceName))
            using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }

            return tempPath;
        }

        static bool IsAdmin()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
    }
}