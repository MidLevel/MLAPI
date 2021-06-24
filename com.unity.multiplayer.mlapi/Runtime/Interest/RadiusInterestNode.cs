using System;
using System.Collections;
using System.Collections.Generic;
using MLAPI.Connection;
using UnityEngine;

namespace MLAPI.Interest
{
    [CreateAssetMenu(fileName = "RadiusInterestNode", menuName = "Interest/Nodes/Radius", order = 1)]
    [Serializable]
    public class RadiusInterestNode : InterestNodeStatic
    {
        public float Radius = 0.0f;
        public override void QueryFor(in NetworkClient client, HashSet<NetworkObject> results)
        {
            foreach (var obj in ManagedObjects)
            {
                if (Vector3.Distance(obj.transform.position, client.PlayerObject.transform.position) <= Radius)
                {
                    results.Add(obj.GetComponent<NetworkObject>());
                }
            }
        }
    }
}
