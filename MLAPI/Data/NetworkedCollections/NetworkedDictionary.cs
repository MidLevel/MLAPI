﻿using System.Collections;
using System.Collections.Generic;
using MLAPI.MonoBehaviours.Core;
using MLAPI.NetworkingManagerComponents.Binary;

namespace MLAPI.Data.NetworkedCollections
{
    /// <summary>
    /// Event based networkedVar container for syncing Lists
    /// </summary>
    /// <typeparam name="TKey">The type for the dictionary keys</typeparam>
    /// <typeparam name="TValue">The type for the dctionary values</typeparam>
    public class NetworkedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INetworkedVar
    {
        internal struct NetworkedDictionaryEvent<TKey, TValue>
        {
            internal enum NetworkedListEventType
            {
                Add,
                Remove,
                RemovePair,
                Clear,
                Value
            }

            internal NetworkedListEventType eventType;
            internal TKey key;
            internal TValue value;
        }

        /// <summary>
        /// Gets the last time the variable was synced
        /// </summary>
        public float LastSyncedTime { get; internal set; }   
        /// <summary>
        /// The settings for this container
        /// </summary>
        public readonly NetworkedVarSettings Settings = new NetworkedVarSettings();
        private readonly IDictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        private NetworkedBehaviour networkedBehaviour;
        private readonly List<NetworkedDictionaryEvent<TKey, TValue>> dirtyEvents = new List<NetworkedDictionaryEvent<TKey, TValue>>();
        
        public NetworkedDictionary()
        {
            
        }
        
        public NetworkedDictionary(NetworkedVarSettings settings)
        {
            this.Settings = settings;
        }
        
        public NetworkedDictionary(NetworkedVarSettings settings, IDictionary<TKey, TValue> value)
        {
            this.Settings = settings;
            this.dictionary = value;
        }
        
        public NetworkedDictionary(IDictionary<TKey, TValue> value)
        {
            this.dictionary = value;
        }

        /// <inheritdoc />
        public void ResetDirty()
        {
            dirtyEvents.Clear();
            LastSyncedTime = NetworkingManager.singleton.NetworkTime;
        }

        /// <inheritdoc />
        public string GetChannel()
        {
            return Settings.SendChannel;
        }

        /// <inheritdoc />
        public void ReadDelta(BitReader reader)
        {
            ushort deltaCount = reader.ReadUShort();
            for (int i = 0; i < deltaCount; i++)
            {
                NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType eventType = (NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType)reader.ReadBits(3);
                switch (eventType)
                {
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Add:
                    {
                        TKey key = reader.ReadValueTypeOrString<TKey>();
                        TValue value = reader.ReadValueTypeOrString<TValue>();
                        dictionary.Add(key, value);
                    }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Remove:
                        {
                            TKey key = reader.ReadValueTypeOrString<TKey>();
                            dictionary.Remove(key);
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.RemovePair:
                        {
                            TKey key = reader.ReadValueTypeOrString<TKey>();
                            TValue value = reader.ReadValueTypeOrString<TValue>();
                            dictionary.Remove(new KeyValuePair<TKey, TValue>(key, value));
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Clear:
                        {
                            //read nothing
                            dictionary.Clear();
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Value:
                        {
                            TKey key = reader.ReadValueTypeOrString<TKey>();
                            TValue value = reader.ReadValueTypeOrString<TValue>();
                            if (dictionary.ContainsKey(key))
                                dictionary[key] = value;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <inheritdoc />
        public void ReadField(BitReader reader)
        {
            dictionary.Clear();
            ushort entryCount = reader.ReadUShort();
            for (int i = 0; i < entryCount; i++)
            {
                TKey key = reader.ReadValueTypeOrString<TKey>();
                TValue value = reader.ReadValueTypeOrString<TValue>();
                dictionary.Add(key, value);
            }
        }

        /// <inheritdoc />
        public void SetNetworkedBehaviour(NetworkedBehaviour behaviour)
        {
            networkedBehaviour = behaviour;
        }

        /// <inheritdoc />
        public bool TryGetValue(TKey key, out TValue value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        /// <inheritdoc />
        public void WriteDelta(BitWriter writer)
        {
            writer.WriteUShort((ushort)dirtyEvents.Count);
            for (int i = 0; i < dirtyEvents.Count; i++)
            {
                writer.WriteBits((byte)dirtyEvents[i].eventType, 3);
                switch (dirtyEvents[i].eventType)
                {
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Add:
                        {
                            writer.WriteValueTypeOrString(dirtyEvents[i].key);
                            writer.WriteValueTypeOrString(dirtyEvents[i].value);
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Remove:
                        {
                            writer.WriteValueTypeOrString(dirtyEvents[i].key);
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.RemovePair:
                        {
                            writer.WriteValueTypeOrString(dirtyEvents[i].key);
                            writer.WriteValueTypeOrString(dirtyEvents[i].value);
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Clear:
                        {
                            //write nothing
                        }
                        break;
                    case NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Value:
                        {
                            writer.WriteValueTypeOrString(dirtyEvents[i].key);
                            writer.WriteValueTypeOrString(dirtyEvents[i].value);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <inheritdoc />
        public void WriteField(BitWriter writer)
        {
            writer.WriteUShort((ushort)dictionary.Count);
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                writer.WriteValueTypeOrString(pair.Key);
                writer.WriteValueTypeOrString(pair.Value);
            }
        }

        /// <inheritdoc />
        public bool CanClientWrite(uint clientId)
        {
            switch (Settings.WritePermission)
            {
                case NetworkedVarPermission.Everyone:
                    return true;
                case NetworkedVarPermission.ServerOnly:
                    return false;
                case NetworkedVarPermission.OwnerOnly:
                    return networkedBehaviour.OwnerClientId == clientId;
                case NetworkedVarPermission.Custom:
                    {
                        if (Settings.WritePermissionCallback == null) return false;
                        return Settings.WritePermissionCallback(clientId);
                    }
            }

            return true;
        }

        /// <inheritdoc />
        public bool CanClientRead(uint clientId)
        {
            switch (Settings.ReadPermission)
            {
                case NetworkedVarPermission.Everyone:
                    return true;
                case NetworkedVarPermission.ServerOnly:
                    return false;
                case NetworkedVarPermission.OwnerOnly:
                    return networkedBehaviour.OwnerClientId == clientId;
                case NetworkedVarPermission.Custom:
                    {
                        if (Settings.ReadPermissionCallback == null) return false;
                        return Settings.ReadPermissionCallback(clientId);
                    }
            }
            return true;
        }
        
        /// <inheritdoc />
        public bool IsDirty()
        {
            if (dirtyEvents.Count == 0) return false;
            if (Settings.SendTickrate <= 0) return true;
            if (NetworkingManager.singleton.NetworkTime - LastSyncedTime >= (1f / Settings.SendTickrate)) return true;
            return false;
        }


        /// <inheritdoc />
        public TValue this[TKey key]
        {
            get
            {
                return dictionary[key];
            }
            set
            {
                dictionary[key] = value;
                dirtyEvents.Add(new NetworkedDictionaryEvent<TKey, TValue>()
                {
                    eventType = NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Value,
                    key = key,
                    value = value
                });
            }
        }

        /// <inheritdoc />
        public ICollection<TKey> Keys => dictionary.Keys;

        /// <inheritdoc />
        public ICollection<TValue> Values => dictionary.Values;

        /// <inheritdoc />
        public int Count => dictionary.Count;

        /// <inheritdoc />
        public bool IsReadOnly => dictionary.IsReadOnly;

        /// <inheritdoc />
        public void Add(TKey key, TValue value)
        {
            dictionary.Add(key, value);
            dirtyEvents.Add(new NetworkedDictionaryEvent<TKey, TValue>()
            {
                eventType = NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Add,
                key = key,
                value = value
            });
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            dictionary.Add(item);
            dirtyEvents.Add(new NetworkedDictionaryEvent<TKey, TValue>()
            {
                eventType = NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Add,
                key = item.Key,
                value = item.Value
            });
        }

        /// <inheritdoc />
        public void Clear()
        {
            dictionary.Clear();
            dirtyEvents.Add(new NetworkedDictionaryEvent<TKey, TValue>()
            {
                eventType = NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Clear
            });
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return dictionary.Contains(item);
        }

        /// <inheritdoc />
        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            dictionary.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        /// <inheritdoc />
        public bool Remove(TKey key)
        {
            bool state = dictionary.Remove(key);
            if (state)
            {
                dirtyEvents.Add(new NetworkedDictionaryEvent<TKey, TValue>()
                {
                    eventType = NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.Remove,
                    key = key
                });
            }
            return state;
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            bool state = dictionary.Remove(item);
            if (state)
            {
                dirtyEvents.Add(new NetworkedDictionaryEvent<TKey, TValue>()
                {
                    eventType = NetworkedDictionaryEvent<TKey, TValue>.NetworkedListEventType.RemovePair,
                    key = item.Key,
                    value = item.Value
                });
            }
            return state;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }
    }
}
