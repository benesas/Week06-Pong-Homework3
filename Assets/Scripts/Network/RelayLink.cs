using System;
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// Relay yalnızca paketleri taşır; oyun kurallarını hâlâ host uygular.
public static class RelayLink
{
    private static async Task Initialize()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            // Ayrı süreçler farklı anonim oyuncu profili kullanır.
            // Editör/MPPM aynı PC'de aynı oyuncu kimliğiyle bağlanmasın.
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
        // DTLS uç noktasını seç; anahtar/bağlantı verilerini loglama veya dosyaya yazma.
        var endpoint = allocation.ServerEndpoints.Find(e => e.ConnectionType == "dtls");
        if (endpoint == null) throw new InvalidOperationException("Relay DTLS endpoint bulunamadı.");
        transport.SetRelayServerData(endpoint.Host, (ushort)endpoint.Port,
            allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
            allocation.ConnectionData, true);
        // Önce Relay ayarı, sonra sunucu, en son hostun yerel istemcisi.
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
