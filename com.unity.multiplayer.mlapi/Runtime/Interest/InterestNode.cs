using System;
using System.Collections;
using System.Collections.Generic;
using MLAPI.Connection;
using UnityEngine;
using UnityEngine.Serialization;

namespace MLAPI.Interest
{
    public abstract class InterestNode : ScriptableObject
    {
        public abstract void QueryFor(in NetworkClient client, HashSet<NetworkObject> results);
        public abstract void AddObject(in NetworkObject obj);
        public abstract void RemoveObject(in NetworkObject obj);
        public abstract void UpdateObject(in NetworkObject obj);
    };
}
