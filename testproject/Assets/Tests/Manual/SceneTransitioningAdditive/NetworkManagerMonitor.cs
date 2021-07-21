using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

public class NetworkManagerMonitor : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
        var networkManagerInstances = FindObjectsOfType<NetworkManager>();
        foreach (var instance in networkManagerInstances)
        {
            if (instance.IsListening)
            {
                if (gameObject != instance.gameObject)
                {
                    var networkManager = GetComponent<NetworkManager>();
                    Destroy(gameObject);
                }
            }
        }
    }
}
