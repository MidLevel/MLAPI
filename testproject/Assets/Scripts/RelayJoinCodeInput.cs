using UnityEngine;
using MLAPI.Transports;
using UnityEngine.UI;

public class RelayJoinCodeInput : MonoBehaviour
{
    public UTPTransport Transport;
    private InputField m_TextInput;

    private void Start()
    {
        m_TextInput = GetComponent<InputField>();
    }

    private void Update()
    {
        if (m_TextInput.IsInteractable()) {
            if (!string.IsNullOrEmpty(Transport.RelayJoinCode)) {
                m_TextInput.text = Transport.RelayJoinCode;
                m_TextInput.readOnly = true;
            }
        }
    }

    public void SetJoinCode()
    {
        Transport.SetRelayJoinCode(m_TextInput.text);
    }
}
