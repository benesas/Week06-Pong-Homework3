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

        _nm.ServerManager.OnServerConnectionState += OnServerState;
        _nm.ServerManager.OnRemoteConnectionState += OnRemoteState;
        _nm.ClientManager.OnClientConnectionState += OnClientState;
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

        string verb = args.ConnectionState == RemoteConnectionState.Started ? "connected" : "disconnected";
        Debug.Log($"[Server] Client {conn.ClientId} {verb}. Now {_nm.ServerManager.Clients.Count} client(s).");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[Client] {args.ConnectionState}");
        if (args.ConnectionState == LocalConnectionState.Started)
            StartCoroutine(LogStartedIdentity());
    }

    private IEnumerator LogStartedIdentity()
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (_nm.ClientManager.Connection.ClientId == -1 && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (_nm.ClientManager.Connection.ClientId == -1) yield break;

        Debug.Log($"[Client] Connected. The server calls me client {_nm.ClientManager.Connection.ClientId}.");
        if (_nm.ServerManager.Started)
            Debug.Log("[Host] This process is the server AND a client. RTT is 0 ms.");
    }
}
