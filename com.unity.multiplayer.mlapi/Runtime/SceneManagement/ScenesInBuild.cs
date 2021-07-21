using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace MLAPI
{
    public class ScenesInBuild : ScriptableObject
    {
        static internal bool IsTesting;

        //[HideInInspector]
        [SerializeField]
        internal List<string> Scenes;

#if UNITY_EDITOR
        internal static ScenesInBuild InitializeScenesInBuild(NetworkManager networkManager)
        {
            var foundScenesInBuildList = AssetDatabase.FindAssets("ScenesInBuildList");
            if (foundScenesInBuildList.Length > 0)
            {
                if (foundScenesInBuildList.Length > 1)
                {
                    var message = "There are multiple instances of your ScenesInBuildList:\n";

                    foreach (var entry in foundScenesInBuildList)
                    {
                        message += $"{AssetDatabase.GUIDToAssetPath(entry)}\n";
                    }
                    message += "Using first entry.  Please remove one of the instances if that is not the right asset path!";
                    Debug.LogError(message);
                }
                networkManager.DefaultScenesInBuildAssetNameAndPath = AssetDatabase.GUIDToAssetPath(foundScenesInBuildList[0]);
            }
            var scenesInBuild = (ScenesInBuild)AssetDatabase.LoadAssetAtPath(networkManager.DefaultScenesInBuildAssetNameAndPath, typeof(ScenesInBuild));
            if(scenesInBuild == null)
            {
                scenesInBuild = CreateInstance<ScenesInBuild>();
                AssetDatabase.CreateAsset(scenesInBuild, networkManager.DefaultScenesInBuildAssetNameAndPath);
            }
            return scenesInBuild;
        }

        internal void PopulateScenesInBuild()
        {
            if(Scenes != null && Scenes.Count > 0 && IsTesting)
            {
                return;
            }
            Scenes = new List<string>();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                if(IsTesting && i >= EditorBuildSettings.scenes.Length)
                {
                    continue;
                }
                var scene = EditorBuildSettings.scenes[i];
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                Scenes.Add(sceneAsset.name);
            }
        }

        private void OnValidate()
        {
            PopulateScenesInBuild();
        }
#endif
    }
}
