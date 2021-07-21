using UnityEngine;
using MLAPI;
#if ENABLE_RELAY_SERVICE
using Unity.Services.Core;
using Unity.Services.Authentication;
#endif
using MLAPI.Transports;

public class UIController : MonoBehaviour
{
    public NetworkManager NetworkManager;
    public GameObject ButtonsRoot;
    public GameObject AuthButton;
    public GameObject JoinCode;

    public UTPTransport Transport;

    private void Awake()
    {
#if ENABLE_RELAY_SERVICE
        if (Transport.Protocol == UTPTransport.ProtocolType.RelayUnityTransport) {
            HideButtons();
            JoinCode.SetActive(false);
        }
#endif
    }

    public void StartServer()
    {
        NetworkManager.StartServer();
        HideButtons();
    }

    public void StartHost()
    {
        NetworkManager.StartHost();
        HideButtons();
    }

    public void StartClient()
    {
        NetworkManager.StartClient();
        HideButtons();
    }

    private void HideButtons()
    {
        ButtonsRoot.SetActive(false);
    }


    public async void OnSignIn()
    {
#if ENABLE_RELAY_SERVICE
        await UnityServices.Initialize();
        Debug.Log("OnSignIn");
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"Logging in with PlayerID {AuthenticationService.Instance.PlayerId}");

        if (AuthenticationService.Instance.IsSignedIn) {
            ButtonsRoot.SetActive(true);
            JoinCode.SetActive(true);
            AuthButton.SetActive(false);
        }
#endif
    }
}
