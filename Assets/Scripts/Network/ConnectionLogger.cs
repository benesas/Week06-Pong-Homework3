// Pong — prints every connection event to the Console / Player.log (Step 3).
// Read these logs on the SERVER to see clients come and go, and on the CLIENT to
// see the lifecycle: Starting -> Started -> (Stopping) -> Stopped.
//
// Week 5, Step 2: when the client reaches Started and this same process is also the
// server, this is a HOST. The line is printed from OnClientState on purpose: at that
// moment ServerManager.Started is already true, whichever way the two calls raced.
using System.Collections;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionLogger : MonoBehaviour
{
    private NetworkManager _nm;

    private void Start()
    {
        _nm = InstanceFinder.NetworkManager;
        if (_nm == null) { Debug.LogError("[Log] No NetworkManager in the scene."); return; }

        _nm.ServerManager.OnServerConnectionState += OnServerState;     // this process as a server
        _nm.ServerManager.OnRemoteConnectionState += OnRemoteState;     // a client, seen from the server
        _nm.ClientManager.OnClientConnectionState += OnClientState;     // this process as a client
    }

    private void OnDestroy()
    {
        if (_nm == null) return;
        _nm.ServerManager.OnServerConnectionState -= OnServerState;
        _nm.ServerManager.OnRemoteConnectionState -= OnRemoteState;
        _nm.ClientManager.OnClientConnectionState -= OnClientState;
    }

    private void OnServerState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[Server] {args.ConnectionState}");
        if (args.ConnectionState == LocalConnectionState.Started)
            Debug.Log($"[Server] Listening on UDP port {_nm.TransportManager.Transport.GetPort()}, " +
                      $"tick rate {_nm.TimeManager.TickRate}.");
    }

    private void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        // Runs on the server only. conn.ClientId is the number the server gave this client.
        string verb = args.ConnectionState == RemoteConnectionState.Started ? "connected" : "disconnected";
        Debug.Log($"[Server] Client {conn.ClientId} {verb}. Now {_nm.ServerManager.Clients.Count} client(s).");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[Client] {args.ConnectionState}");
        if (args.ConnectionState == LocalConnectionState.Started)
            StartCoroutine(LogStartedIdentity());          // ClientId is assigned just after this callback
    }

    /// One frame later FishNet has finished the connection handshake, so ClientId is no longer -1.
    private IEnumerator LogStartedIdentity()
    {
        for (int frame = 0; frame < 120 && _nm.ClientManager.Connection.ClientId == -1; frame++)
            yield return null;

        Debug.Log($"[Client] Connected. The server calls me client {_nm.ClientManager.Connection.ClientId}.");
        if (_nm.ServerManager.Started)
            Debug.Log("[Host] This process is the server AND a client. RTT is 0 ms.");
    }
}
