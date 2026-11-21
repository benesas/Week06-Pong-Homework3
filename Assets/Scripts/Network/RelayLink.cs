#if WEEK06_RELAY_READY
using System;
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public static class RelayLink
{
    private static async Task Initialize()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {

            var options = new InitializationOptions();
            options.SetProfile("pong_" + System.Diagnostics.Process.GetCurrentProcess().Id);
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private static UnityTransport Transport(NetworkManager manager)
    {
        var transport = manager.TransportManager.GetTransport<UnityTransport>();
        if (transport == null || manager.TransportManager.Transport != transport)
            throw new InvalidOperationException("TransportManager üzerinde UnityTransport seçilmelidir.");
        return transport;
    }

    public static async Task<string> Host(NetworkManager manager)
    {
        var transport = Transport(manager);
        await Initialize();
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
        string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var endpoint = allocation.ServerEndpoints.Find(e => e.ConnectionType == "dtls");
        if (endpoint == null) throw new InvalidOperationException("Relay DTLS endpoint bulunamadı.");
        transport.SetRelayServerData(endpoint.Host, (ushort)endpoint.Port,
            allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
            allocation.ConnectionData, true);

        if (!manager.ServerManager.StartConnection())
            throw new InvalidOperationException("Relay sunucusu başlatılamadı.");
        if (!manager.ClientManager.StartConnection())
        {
            manager.ServerManager.StopConnection(true);
            throw new InvalidOperationException("Host istemcisi başlatılamadı.");
        }
        Debug.Log("[Relay] Join code: " + code);
        return code;
    }

    public static async Task Join(NetworkManager manager, string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length != 6) throw new ArgumentException("Altı karakterli katılım kodunu gir.");
        var transport = Transport(manager);
        await Initialize();
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
        var endpoint = allocation.ServerEndpoints.Find(e => e.ConnectionType == "dtls");
        if (endpoint == null) throw new InvalidOperationException("Relay DTLS endpoint bulunamadı.");
        transport.SetRelayServerData(endpoint.Host, (ushort)endpoint.Port,
            allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
            allocation.HostConnectionData, true);
        if (!manager.ClientManager.StartConnection())
            throw new InvalidOperationException("Relay istemcisi başlatılamadı.");
    }
}

#else
using System;
using System.Threading.Tasks;
using FishNet.Managing;

public static class RelayLink
{
    public static Task<string> Host(NetworkManager manager) => Task.FromException<string>(
        new InvalidOperationException("Week06 > Install required Relay packages seç ve derlemeyi bekle."));
    public static Task Join(NetworkManager manager, string code) => Task.FromException(
        new InvalidOperationException("Week06 > Install required Relay packages seç ve derlemeyi bekle."));
}
#endif
