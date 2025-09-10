using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public enum ConnectionType
    {
        udp,
        wss,
        dtls,
    }
    public static async Task<string> StartHostWithRelay(int maxConnections, string connectionType)
    {
        try
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log("relay join code : " + joinCode);

            return NetworkManager.Singleton.StartHost() ? joinCode : null;
        }
        catch(Exception e)
        {
            Debug.LogException(e);

            return null;
        }
    }

    public static async Task<bool> StartClientWithRelay(string joinCode, string connectionType)
    {
        try
        {
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            return !string.IsNullOrEmpty(joinCode) && NetworkManager.Singleton.StartClient();
        }
        catch(Exception e)
        {
            Debug.LogException(e);

            return false;
        }
    }

    public static async Task TrySignInAnonymously()
    {
        await UnityServices.InitializeAsync();

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log("Signed in succesfully");
    }

    private void Start()
    {
        _ = TrySignInAnonymously();
    }

}
