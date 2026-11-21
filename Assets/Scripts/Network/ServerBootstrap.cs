using System;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class ServerBootstrap : MonoBehaviour
{
    [SerializeField] private int headlessTargetFrameRate = 60;
    [SerializeField] private string edgegapPortName = "gameport";

    private void Start()
    {
        NetworkManager nm = GetComponent<NetworkManager>();
        string reason = WhyAmIAServer();
        if (reason == null) return;

        Transport t = nm.TransportManager.Transport;
        ushort port = ReadPort(t.GetPort(), out string portSource);
        t.SetPort(port);
        t.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);

        if (Application.isBatchMode)
        {
            Application.targetFrameRate = headlessTargetFrameRate;
            QualitySettings.vSyncCount = 0;
        }

        if (!nm.ServerManager.Started) nm.ServerManager.StartConnection();
        Debug.Log($"[Server] Auto-started ({reason}) on UDP port {port} ({portSource}).");
        LogEdgegapAddress();
    }

    private static string WhyAmIAServer()
    {
#if UNITY_SERVER && !UNITY_EDITOR

        return "Dedicated Server build";
#else
        string[] args = Environment.GetCommandLineArgs();
        if (Application.isBatchMode)                          return "-batchmode";
        if (args.Contains("-server"))                          return "-server argument";
#if UNITY_EDITOR
        if (HasPlayModeTag("Server"))                          return "Multiplayer Play Mode tag 'Server'";
#endif
        return null;
#endif
    }

    private static bool HasPlayModeTag(string tag)
    {
        try
        {
            Type currentPlayer = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer", throwOnError: false))
                .FirstOrDefault(t => t != null);
            if (currentPlayer == null) return false;

            MethodInfo readOnlyTags = currentPlayer.GetMethod("ReadOnlyTags", BindingFlags.Public | BindingFlags.Static);
            if (readOnlyTags == null) return false;

            object tags = readOnlyTags.Invoke(null, null);
            if (tags is System.Collections.IEnumerable list)
                foreach (object t in list) if (t as string == tag) return true;
        }
        catch (Exception) {                                          }
        return false;
    }

    private ushort ReadPort(ushort fallback, out string source)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-port" && ushort.TryParse(args[i + 1], out ushort p)) { source = "-port argument"; return p; }

        string envName = $"ARBITRIUM_PORT_{edgegapPortName.ToUpperInvariant()}_INTERNAL";
        string env = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrEmpty(env) && ushort.TryParse(env, out ushort e)) { source = envName; return e; }

        source = "Inspector";
        return fallback;
    }

    private void LogEdgegapAddress()
    {
        string ip       = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP");
        string external = Environment.GetEnvironmentVariable($"ARBITRIUM_PORT_{edgegapPortName.ToUpperInvariant()}_EXTERNAL");
        string request  = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");
        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(external)) return;
        Debug.Log($"[Server] Edgegap deployment {request}: players connect to {ip}:{external}  (or {request}.pr.edgegap.net:{external}).");
    }
}
