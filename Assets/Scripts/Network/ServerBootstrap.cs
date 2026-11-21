// Pong — starts the server automatically when this process is a server (Step 3, edge-ready in Week 4).
// Put it on the NetworkManager object. It decides it IS a server if any of these is true:
//   1. the build was made with the Dedicated Server target      (UNITY_SERVER define, never in the editor)
//   2. the game was launched with -batchmode                    (headless normal build)
//   3. the game was launched with the -server argument
//   4. (editor only) this Multiplayer Play Mode instance has the tag "Server"
//
// Which UDP port it listens on, first match wins:
//   a. -port 7777                                  command line
//   b. ARBITRIUM_PORT_GAMEPORT_INTERNAL=7770       environment variable: Edgegap sets it for the port you
//                                                  named "gameport" in the app version. Docker local test sets it by hand.
//   c. whatever Tugboat has in the Inspector       (7770)
// On Edgegap it also prints the public address players type into the HUD (ARBITRIUM_PUBLIC_IP + external port).
//
// Untick ServerManager > "Start On Headless" so this script is the only thing starting the server.
//
// The Multiplayer Play Mode check is done through reflection so that this file compiles whether
// or not the com.unity.multiplayer.playmode package is installed. With the package, a Play Mode
// instance tagged "Server" starts the server; without it, the check simply returns false.
using System;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class ServerBootstrap : MonoBehaviour
{
    [SerializeField] private int headlessTargetFrameRate = 60;   // don't let a headless server spin at 1000 fps
    [SerializeField] private string edgegapPortName = "gameport"; // the port name in the Edgegap app version

    private void Start()
    {
        NetworkManager nm = GetComponent<NetworkManager>();
        string reason = WhyAmIAServer();
        if (reason == null) return;                 // a normal client instance: do nothing, the HUD takes over

        Transport t = nm.TransportManager.Transport;
        ushort port = ReadPort(t.GetPort(), out string portSource);
        t.SetPort(port);
        t.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);   // listen on every interface: inside a container "localhost" is not reachable

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
        // Only a real server build. UNITY_SERVER is also defined in the editor while the active
        // platform is Dedicated Server, and the editor must stay a client there (press Play, Start Client).
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

    /// Editor only. True if this Multiplayer Play Mode instance carries the tag. Uses reflection so the
    /// script compiles without the package: Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags().
    private static bool HasPlayModeTag(string tag)
    {
        try
        {
            Type currentPlayer = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer", throwOnError: false))
                .FirstOrDefault(t => t != null);
            if (currentPlayer == null) return false;                          // package not installed

            MethodInfo readOnlyTags = currentPlayer.GetMethod("ReadOnlyTags", BindingFlags.Public | BindingFlags.Static);
            if (readOnlyTags == null) return false;

            object tags = readOnlyTags.Invoke(null, null);
            if (tags is System.Collections.IEnumerable list)
                foreach (object t in list) if (t as string == tag) return true;
        }
        catch (Exception) { /* any API change: behave as "no tag" */ }
        return false;
    }

    /// -port argument, then the Edgegap environment variable, then the Inspector value.
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

    /// On Edgegap: print the address a player types into the HUD. Elsewhere: silent.
    private void LogEdgegapAddress()
    {
        string ip       = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP");
        string external = Environment.GetEnvironmentVariable($"ARBITRIUM_PORT_{edgegapPortName.ToUpperInvariant()}_EXTERNAL");
        string request  = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");
        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(external)) return;
        Debug.Log($"[Server] Edgegap deployment {request}: players connect to {ip}:{external}  (or {request}.pr.edgegap.net:{external}).");
    }
}
