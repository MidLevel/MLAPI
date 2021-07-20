using System;
using MLAPI.NetworkVariable;
using UnityEngine;

namespace MLAPI.Prototyping
{
    /// <summary>
    /// Server authority only allows the server to update this transform
    /// Client authority only allows the client owner to update this transform
    /// </summary>
    public enum Authority
    {
        Server = 0, // default
        Owner,
    }

    /// <summary>
    /// Contains utility functions for implementing NetworkBehaviours which support multiple authority modes.
    /// </summary>
    public static class AuthorityUtility
    {
        /// <summary>
        /// Check whether the given <see cref="NetworkBehaviour"/> has authority based on <see cref="NetworkObject"/> ownership.
        /// </summary>
        /// <param name="networkBehaviour">The NetworkBehaviour to check for authority.</param>
        /// <param name="authority">The authority mode.</param>
        /// <returns>True if the networkBehaviour has authority; Else false.</returns>
        public static bool HasAuthority(this NetworkBehaviour networkBehaviour, Authority authority)
        {
            return (networkBehaviour.IsClient && authority == Authority.Owner && networkBehaviour.IsOwner) || (networkBehaviour.IsServer && authority == Authority.Server);
        }

        /// <summary>
        /// Updates the read and write permissions of a network variable based on authority.
        /// </summary>
        /// <param name="variableToUpdate">The NetworkVariable for which to change the permissions.</param>
        /// <param name="authority">The new authority mode.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="NotImplementedException"></exception>
        public static void UpdateStateVariablePermission<T>(this NetworkVariable<T> variableToUpdate, Authority authority)
        {
            switch (authority)
            {
                case Authority.Owner:
                    variableToUpdate.Settings.WritePermission = NetworkVariablePermission.OwnerOnly;
                    variableToUpdate.Settings.ReadPermission = NetworkVariablePermission.Everyone;
                    break;
                case Authority.Server:
                    variableToUpdate.Settings.WritePermission = NetworkVariablePermission.ServerOnly;
                    variableToUpdate.Settings.ReadPermission = NetworkVariablePermission.Everyone;
                    break;
                default:
                    throw new NotImplementedException($"{authority} is not handled");
            }
        }
    }
}
