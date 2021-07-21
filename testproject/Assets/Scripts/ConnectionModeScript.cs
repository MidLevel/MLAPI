using System;
using System.Collections;
using UnityEngine;
using MLAPI;
using MLAPI.Transports;
#if ENABLE_RELAY_SERVICE
using Unity.Services.Core;
using Unity.Services.Authentication;
#endif

/// <summary>
/// Used in tandem with the ConnectModeButtons prefab asset in test project
/// </summary>
public class ConnectionModeScript : MonoBehaviour
{
    [SerializeField]
    private GameObject m_ConnectionModeButtons;

    [SerializeField]
    private GameObject m_AuthenticationButtons;

    [SerializeField]
    private GameObject m_JoinCodeInput;

    private CommandLineProcessor m_CommandLineProcessor;
    internal void SetCommandLineHandler(CommandLineProcessor commandLineProcessor)
    {
        m_CommandLineProcessor = commandLineProcessor;
        if(m_CommandLineProcessor.AutoConnectEnabled())
        {
            StartCoroutine(WaitForNetworkManager());
        }
    }

    public delegate void OnNotifyConnectionEventDelegateHandler();

    public event OnNotifyConnectionEventDelegateHandler OnNotifyConnectionEventServer;
    public event OnNotifyConnectionEventDelegateHandler OnNotifyConnectionEventHost;
    public event OnNotifyConnectionEventDelegateHandler OnNotifyConnectionEventClient;


    private IEnumerator WaitForNetworkManager()
    {
        while(true)
        {
            yield return new WaitForSeconds(0.5f);
            try
            {
                if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening)
                {
                    m_ConnectionModeButtons.SetActive(false);
                    m_CommandLineProcessor.ProcessCommandLine();
                    break;
                }
            }
            catch { }
        }
        yield return null;
    }

    // Start is called before the first frame update
    private void Start()
    {
        //If we have a NetworkManager instance and we are not listening and m_ConnectionModeButtons is not null then show the connection mode buttons
        if (m_ConnectionModeButtons && m_AuthenticationButtons)
        {
#if ENABLE_RELAY_SERVICE
            if (NetworkManager.Singleton.GetComponent<UTPTransport>().Protocol == UTPTransport.ProtocolType.RelayUnityTransport)
            {
                m_JoinCodeInput.SetActive(true);
                m_ConnectionModeButtons.SetActive(false || AuthenticationService.Instance.IsSignedIn);
                m_AuthenticationButtons.SetActive(NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && !AuthenticationService.Instance.IsSignedIn);
            }
            else
#endif
            {
                m_JoinCodeInput.SetActive(false);
                m_AuthenticationButtons.SetActive(false);
                m_ConnectionModeButtons.SetActive(NetworkManager.Singleton && !NetworkManager.Singleton.IsListening);
            }
        }
    }

    /// <summary>
    /// Handles starting MLAPI in server mode
    /// </summary>
    public void OnStartServer()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
            NetworkManager.Singleton.StartServer();
            OnNotifyConnectionEventServer?.Invoke();
            m_ConnectionModeButtons.SetActive(false);

        }
    }

    /// <summary>
    /// Handles starting MLAPI in host mode
    /// </summary>
    public void OnStartHost()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
            NetworkManager.Singleton.StartHost();
            OnNotifyConnectionEventHost?.Invoke();
            m_ConnectionModeButtons.SetActive(false);
        }
    }

    /// <summary>
    /// Handles starting MLAPI in client mode
    /// </summary>
    public void OnStartClient()
    {
        if (NetworkManager.Singleton && !NetworkManager.Singleton.IsListening && m_ConnectionModeButtons)
        {
            NetworkManager.Singleton.StartClient();
            OnNotifyConnectionEventClient?.Invoke();
            m_ConnectionModeButtons.SetActive(false);
        }
    }

    /// <summary>
    /// Handles autenticating UnityServices, needed for Relay
    /// </summary>
    public async void OnSignIn()
    {
#if ENABLE_RELAY_SERVICE
        await UnityServices.Initialize();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"Logging in with PlayerID {AuthenticationService.Instance.PlayerId}");

        if (AuthenticationService.Instance.IsSignedIn)
        {
            m_ConnectionModeButtons.SetActive(true);
            m_AuthenticationButtons.SetActive(false);
        }
#endif
    }
}
