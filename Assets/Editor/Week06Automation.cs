using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class Week06Automation
{
    private static bool _running;

    [MenuItem("Week06/Build and play automatically (two players)")]
    public static async void PlayAutomatically()
    {
        if (_running) return;
        _running = true;
        Process host = null, guest = null;
        try
        {
            Week06Setup.PrepareAndBuild();
            string executable = Path.GetFullPath("Builds/Windows/Pong.exe");
            string results = Path.GetFullPath("Assets/Week06/AutoMatches/Run-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(results);
            string hostLog = Path.Combine(results, "host.log");
            host = Launch(executable, "-host -auto-role host -auto-report " + Quote(results) + " -logFile " + Quote(hostLog));
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline && !host.HasExited)
            {
                if (File.Exists(hostLog) && File.ReadAllText(hostLog).Contains("LEFT.")) break;
                await Task.Delay(250);
            }
            if (host.HasExited || !File.Exists(hostLog) || !File.ReadAllText(hostLog).Contains("LEFT."))
                throw new Exception("Host LEFT raketi oluşmadı. Port 7770 kullanan eski oyunu kapatıp tekrar dene.");
            guest = Launch(executable, "-connect 127.0.0.1:7770 -auto-role guest -auto-report " + Quote(results) + " -logFile " + Quote(Path.Combine(results, "guest.log")));
            deadline = DateTime.UtcNow.AddSeconds(190);
            while (DateTime.UtcNow < deadline && (!host.HasExited || !guest.HasExited)) await Task.Delay(500);
            if (!host.HasExited || !guest.HasExited) throw new Exception("Otomatik maç zaman aşımı.");
            if (host.ExitCode != 0 || guest.ExitCode != 0) throw new Exception("Maç tamamlanamadı; sonuç klasöründeki loglara bak.");
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[Week06] Otomatik maç tamamlandı. Sonuçlar: " + results);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[Week06] " + ex.Message);
            if (host != null && !host.HasExited) host.Kill();
            if (guest != null && !guest.HasExited) guest.Kill();
        }
        finally { _running = false; host?.Dispose(); guest?.Dispose(); }
    }

    private static string Quote(string value) => "\"" + value + "\"";
    private static Process Launch(string executable, string arguments)
        => Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false });
}
