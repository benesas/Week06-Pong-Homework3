// Pong — a tiny debug HUD to start/stop the server and client, Week 2 version.
// Uses Unity's old IMGUI (OnGUI) so there is no Canvas to set up. Replace it with
// real UI later; the calls into FishNet stay the same.
//
//   NetworkManager.ServerManager.StartConnection()              -> become a server
//   NetworkManager.ClientManager.StartConnection(address, port) -> connect as a client
//
// Week 2 adds: the score (read straight from GameState's SyncVars), whose serve it is, which
// side I play, the feed (a SyncList), a one-line flash for RPC events, and the bytes per
// second FishNet's StatisticsManager measures (Step 10). Nothing in this file writes state.
//
// Week 4 (edge): the address field accepts "host:port", because a cloud server (Edgegap) hands you a
// host name and a 5-digit external port that is NOT 7770. A client can also be started from the command
// line with  -connect host:port  and joins by itself (handy on lab PCs).
//
// Week 5 (host): Start Host is the two Week 2 calls side by side in ONE process:
//     ServerManager.StartConnection()                 -> become the server
//     ClientManager.StartConnection("127.0.0.1", port) -> connect to it in memory
// The client goes through the same Tugboat UDP path, but loopback, so its RTT is 0 ms.
// Stop Host closes the client first, then the server; nothing exists unless the server says so.
//
// Bytes per second, FishNet 4.7.3: StatisticsManager > Network Traffic > Enable Mode = Development,
// Update Client and Update Server ticked, AND Run In Release ticked: while the active platform is Dedicated Server
// (after a server build) the editor compiles with UNITY_SERVER and FishNet refuses to collect statistics otherwise.
// Update Client and Update Server ticked. FishNet then raises OnNetworkTraffic once per TICK with
// the bytes of that tick; this HUD adds them up into one-second windows. The per-tick counters are
// internal to FishNet (they feed its Network Profiler window), so they are read by reflection here.
using System;
using System.Reflection;
using FishNet;
using FishNet.Editing.NetworkProfiler;
using FishNet.Managing;
using FishNet.Managing.Statistic;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionHud : MonoBehaviour
{
    [SerializeField] private string address = "127.0.0.1";   // "host" or "host:port". Paste the Edgegap address here.
    [SerializeField] private ushort port    = 7770;          // used when the address has no ":port" part (Tugboat's default, UDP)

    private string _joinCode = "";
    private string _relayCode = "";
    private bool _relayBusy;
    private bool _relaySession;
    private string _relayStatus = "";

    private async void HostViaRelay()
    {
        if (_relayBusy) return;
        _relayBusy = true;
        _relaySession = true;
        _relayStatus = "Relay host hazırlanıyor...";
        try { _relayCode = await RelayLink.Host(_nm); _relayStatus = "Kodu arkadaşınla paylaş."; }
        catch (Exception ex) { _relayCode = ""; _relayStatus = "Host failed: " + ex.Message; Debug.LogError("[Relay] " + _relayStatus); }
        finally { _relayBusy = false; }
    }

    private async void JoinViaRelay()
    {
        if (_relayBusy) return;
        _relayBusy = true;
        _relaySession = true;
        _relayStatus = "Relay bağlantısı hazırlanıyor...";
        try { await RelayLink.Join(_nm, _joinCode); _relayStatus = "Bağlantı isteği gönderildi."; }
        catch (Exception ex) { _relayStatus = "Join failed: " + ex.Message; Debug.LogError("[Relay] " + _relayStatus); }
        finally { _relayBusy = false; }
    }

    private NetworkManager _nm;
    private LocalConnectionState _serverState = LocalConnectionState.Stopped;
    private LocalConnectionState _clientState = LocalConnectionState.Stopped;

    private NetworkTrafficStatistics _traffic;               // null when statistics are disabled in the Inspector
    private ulong _inBytes, _outBytes;                       // last full second, this process as a client
    private ulong _srvInBytes, _srvOutBytes;                 // last full second, this process as a server
    private ulong _winIn, _winOut, _winSrvIn, _winSrvOut;    // the second being counted
    private float _winStart;

    private static string _flash = "";
    private static float  _flashUntil;

    /// Called from RPC handlers (PaddleController) to show a one-line message for two seconds.
    public static void Flash(string text) { _flash = text; _flashUntil = Time.time + 2f; }

    private void Start()
    {
        _nm = InstanceFinder.NetworkManager;       // finds the NetworkManager in the scene
        if (_nm == null)
        {
            Debug.LogError("[HUD] No NetworkManager in the scene. Add one first (Step 2).");
            enabled = false;
            return;
        }
        _nm.ServerManager.OnServerConnectionState += OnServerState;
        _nm.ClientManager.OnClientConnectionState += OnClientState;

        // Step 10: bytes per second. Needs the StatisticsManager component on the NetworkManager
        // object with Network Traffic > Enable Mode = Development and Update Client / Update Server ticked.
        if (_nm.StatisticsManager != null && _nm.StatisticsManager.TryGetNetworkTrafficStatistics(out _traffic))
        {
            _traffic.OnNetworkTraffic += OnTraffic;
            _winStart = Time.time;
        }

        // Week 4: "-connect host:port" on the command line joins without clicking (clients only; a headless server has no HUD).
        if (!Application.isBatchMode)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-connect") { address = args[i + 1]; StartClient(); break; }

            // Week 5: "-host" does what one click on Start Host does (handy on lab PCs and for testing).
            if (Array.IndexOf(args, "-host") >= 0 && _serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped)
                StartHost();
        }
    }

    private void OnDestroy()
    {
        if (_nm == null) return;
        _nm.ServerManager.OnServerConnectionState -= OnServerState;
        _nm.ClientManager.OnClientConnectionState -= OnClientState;
        if (_traffic != null) _traffic.OnNetworkTraffic -= OnTraffic;
    }

    private void OnServerState(ServerConnectionStateArgs args) => _serverState = args.ConnectionState;
    private void OnClientState(ClientConnectionStateArgs args) => _clientState = args.ConnectionState;

    /// Week 5, Step 2: one click, one process, two connections. Server first, then the client
    /// connects to that server through loopback. This is the Edgegap server and a normal guest
    /// in one window; the only difference from a dedicated server is that someone can play here.
    private void StartHost()
    {
        Debug.Log("[HUD] Start Host: server first, then the client to 127.0.0.1.");
        _nm.ServerManager.StartConnection();
        _nm.ClientManager.StartConnection("127.0.0.1", port);
    }

    /// Week 5, Step 2: the client leaves first, then the server. Same order as a guest leaving,
    /// so the server-side cleanup (paddles, ball, score) runs before the server itself goes away.
    private void StopHost()
    {
        _relayCode = "";
        Debug.Log("[HUD] Stop Host: client first, then the server.");
        _nm.ClientManager.StopConnection();
        _nm.ServerManager.StopConnection(true);
    }

    /// Splits "host:port" (port optional) and starts the client. "[::1]:7770" style IPv6 is not handled; use a host name.
    private void StartClient()
    {
        string host = address.Trim();
        ushort p = port;
        int colon = host.LastIndexOf(':');
        if (colon > 0 && ushort.TryParse(host.Substring(colon + 1), out ushort parsed)) { p = parsed; host = host.Substring(0, colon); }
        if (host.Length == 0) host = "127.0.0.1";
        Debug.Log($"[Client] Connecting to {host} on UDP {p}.");
        _nm.ClientManager.StartConnection(host, p);
    }

    // Once per tick: the bytes of THAT tick, in and out, for the server side and the client side of this process.
    private void OnTraffic(uint tick, BidirectionalNetworkTraffic serverTraffic, BidirectionalNetworkTraffic clientTraffic)
    {
        _winSrvIn  += BytesOf(serverTraffic, inbound: true);
        _winSrvOut += BytesOf(serverTraffic, inbound: false);
        _winIn     += BytesOf(clientTraffic, inbound: true);
        _winOut    += BytesOf(clientTraffic, inbound: false);

        if (Time.time - _winStart >= 1f)             // roll the window: what the HUD shows is the last full second
        {
            _srvInBytes = _winSrvIn;  _srvOutBytes = _winSrvOut;  _inBytes = _winIn;  _outBytes = _winOut;
            _winSrvIn = _winSrvOut = _winIn = _winOut = 0;
            _winStart = Time.time;
        }
    }

    // FishNet keeps BidirectionalNetworkTraffic.InboundTraffic / OutboundTraffic (and their .Bytes) internal.
    private static FieldInfo _inField, _outField, _bytesField;
    private static ulong BytesOf(BidirectionalNetworkTraffic traffic, bool inbound)
    {
        if (traffic == null) return 0;
        if (_inField == null)
        {
            _inField  = typeof(BidirectionalNetworkTraffic).GetField("InboundTraffic",  BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            _outField = typeof(BidirectionalNetworkTraffic).GetField("OutboundTraffic", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        object side = (inbound ? _inField : _outField)?.GetValue(traffic);
        if (side == null) return 0;
        if (_bytesField == null) _bytesField = side.GetType().GetField("Bytes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return _bytesField == null ? 0 : (ulong)_bytesField.GetValue(side);
    }

    private void OnGUI()
    {
        if (_nm == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20));   // tall enough for everything; the box below hugs its content
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("<b>PONG — Week 6: Host & Relay</b>", Rich());

        if (_serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped && !_relayBusy)
        {
            if (GUILayout.Button("Host via Relay")) HostViaRelay();
            _joinCode = GUILayout.TextField(_joinCode, 6).ToUpperInvariant();
            if (GUILayout.Button("Join via Relay")) JoinViaRelay();
        }
        if (_relayBusy) GUILayout.Label("Relay: lütfen bekle...");
        if (!string.IsNullOrEmpty(_relayStatus)) GUILayout.Label(_relayStatus);
        if (!string.IsNullOrEmpty(_relayCode) && _serverState == LocalConnectionState.Started)
            GUILayout.Label($"<b><size=26>JOIN CODE: {_relayCode}</size></b>", Rich());
        if (_relaySession && !_relayBusy)
            GUILayout.Label("Yerel/IP bağlantıya dönmek için Play'i durdurup tekrar başlat.");
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !_relayBusy;

        // --- host: both halves in this one process (Week 5, Step 2) ---
        bool hostRunning = _serverState == LocalConnectionState.Started && _clientState == LocalConnectionState.Started;
        if (_serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped)
        {
            GUI.enabled = previousEnabled && !_relayBusy && !_relaySession;
            if (GUILayout.Button("Start Host")) StartHost();
            GUI.enabled = previousEnabled && !_relayBusy;
        }
        else if (hostRunning)
        {
            if (GUILayout.Button("Stop Host")) StopHost();
        }

        // --- server ---
        if (_serverState == LocalConnectionState.Stopped)
        {
            GUI.enabled = previousEnabled && !_relayBusy && !_relaySession;
            if (GUILayout.Button("Start Server")) _nm.ServerManager.StartConnection();
            GUI.enabled = previousEnabled && !_relayBusy;
        }
        else if (GUILayout.Button($"Stop Server  [{_serverState}]"))
        {
            _nm.ServerManager.StopConnection(true);
        }

        // --- client ---
        GUILayout.BeginHorizontal();
        GUILayout.Label("Server host:port", GUILayout.Width(100));
        address = GUILayout.TextField(address);
        GUILayout.EndHorizontal();

        if (_clientState == LocalConnectionState.Stopped)
        {
            GUI.enabled = previousEnabled && !_relayBusy && !_relaySession;
            if (GUILayout.Button("Start Client")) StartClient();
            GUI.enabled = previousEnabled && !_relayBusy;
        }
        else if (GUILayout.Button($"Stop Client  [{_clientState}]"))
        {
            _nm.ClientManager.StopConnection();
        }

        GUI.enabled = previousEnabled;
        GUILayout.Label("W/S veya ↑/↓: hareket | Space: servis");
        GUILayout.Label("C: spam | X: büyük yön | T: teleport | G: skor 9");
        // --- status ---
        GUILayout.Space(6);
        string stats = _traffic == null ? "  (statistics disabled: StatisticsManager ▸ Run In Release + Network Traffic ▸ Enable Mode)" : "";
        if (_serverState == LocalConnectionState.Started)
        {
            GUILayout.Label($"Server: {_nm.ServerManager.Clients.Count} client(s) on UDP {_nm.TransportManager.Transport.GetPort()}");
            GUILayout.Label($"Server traffic: in {_srvInBytes} B/s, out {_srvOutBytes} B/s{stats}");
        }
        if (_clientState == LocalConnectionState.Started)
        {
            GUILayout.Label($"Client: my id = {_nm.ClientManager.Connection.ClientId}");
            GUILayout.Label($"RTT (ping): {_nm.TimeManager.RoundTripTime} ms");
            GUILayout.Label($"Client traffic: in {_inBytes} B/s, out {_outBytes} B/s{stats}");
        }
        // Week 5, Step 3: on a host the same paddle runs OnStartServer AND OnStartClient.
        // Without the All.Contains guard this would say 2 for one paddle; it must say 1.
        GUILayout.Label($"Paddles known here: {PaddleController.All.Count}");
        GUILayout.Label($"Tick {_nm.TimeManager.Tick}  @ {_nm.TimeManager.TickRate} ticks/s");

        // --- score: nothing here is computed locally; every number is a SyncVar the server wrote ---
        GUILayout.Space(6);
        GameState gs = GameState.Instance;
        if (gs != null)
        {
            GUILayout.Label($"<b><size=22>LEFT  {gs.LeftScore.Value}  :  {gs.RightScore.Value}  RIGHT</size></b>", Rich());
            PaddleController me = PaddleController.Local;
            string mine = me == null ? "not playing" : PaddleController.SideName(me.Side.Value);
            bool myServe = me != null && me.Side.Value == gs.ServingSide.Value;
            GUILayout.Label($"I am: {mine}   Serve: {PaddleController.SideName(gs.ServingSide.Value)}{(myServe ? "  ◀ press Space" : "")}");
        }

        // --- feed: a SyncList on the GameState scene object ---
        if (gs != null && gs.Feed.Count > 0)
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>Feed</b>", Rich());
            foreach (string line in gs.Feed) GUILayout.Label(line);
        }

        if (Time.time < _flashUntil)
        {
            GUILayout.Space(6);
            GUILayout.Label($"<b><color=#ffcc55>{_flash}</color></b>", Rich());
        }
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private static GUIStyle Rich()
    {
        GUIStyle s = new GUIStyle(GUI.skin.label) { richText = true };
        return s;
    }
}
