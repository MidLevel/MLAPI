[![](https://i.imgur.com/d0amtqs.png)](https://mlapi.network/)

[![GitHub Release](https://img.shields.io/github/release/MidLevel/MLAPI.svg?logo=github)](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/releases/latest)
[![Github All Releases](https://img.shields.io/github/downloads/MidLevel/MLAPI/total.svg?logo=github&color=informational)](https://github.com/Unity-Technologies/com.unity.multiplayer.mlapi/releases)

[![Forums](https://img.shields.io/badge/unity--forums-multiplayer-blue)](https://forum.unity.com/forums/multiplayer.26/)
[![Discord](https://img.shields.io/discord/449263083769036810.svg?label=discord&logo=discord&color=informational)](https://discord.gg/FM8SE9E)


[![Website](https://img.shields.io/badge/docs-website-informational.svg)](https://docs-multiplayer.unity3d.com/)
[![Api](https://img.shields.io/badge/docs-api-informational.svg)](https://docs-multiplayer.unity3d.com/docs/mlapi-api/introduction)


The Unity MLAPI (Mid level API) is a framework that simplifies building networked games in Unity. It offers **low level** access to core networking while at the same time providing **high level** abstractions. The MLAPI aims to remove the repetitive tasks and reduces the network code dramatically, no matter how many of the **modular** features you use.

### Getting Started
To get started, check the [Multiplayer Docs Site](https://docs-multiplayer.unity3d.com/).

### Community and Feedback
For general questions, networking advice or discussions about MLAPI, please join our [Discord Community](https://discord.gg/FM8SE9E) or create a post in the [Unity Multiplayer Forum](https://forum.unity.com/forums/multiplayer.26/).

### Compatibility
The MLAPI supports all major Unity platforms. To use the WebGL platform a custom WebGL transport based on web sockets is needed.

MLAPI is compatible with Unity 2019 and newer versions.

### Development
We follow the [Gitflow Workflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow). The master branch contains our latest stable release version while the develop branch tracks our current work.

This repository is broken into multiple components, each one implemented as a Unity Package.
```
    .
    ├── com.unity.multiplayer.mlapi            # The core netcode SDK unity package (source + tests)
    └── testproject                            # A Unity project with various test implementations & scenes which exercise the features in the above package(s).
```

### Contributing
The MLAPI is an open-source project and we encourage and welcome
contributions. If you wish to contribute, be sure to review our
[contribution guidelines](CONTRIBUTING.md)

### Issues and missing features
If you have an issue, bug or feature request, please follow the information in our [contribution guidelines](CONTRIBUTING.md) to submit an issue.

### Example
Here is a sample MonoBehaviour showing a chat script where everyone can write and read from. This shows the basis of the MLAPI and the abstractions it adds.

```csharp
public class Chat : NetworkBehaviour
{
    private NetworkList<string> ChatMessages = new NetworkList<string>(new MLAPI.NetworkVariable.NetworkVariableSettings()
    {
        ReadPermission = MLAPI.NetworkVariable.NetworkVariablePermission.Everyone,
        WritePermission = MLAPI.NetworkVariable.NetworkVariablePermission.Everyone,
        SendTickrate = 5
    }, new List<string>());

    private string textField = "";

    private void OnGUI()
    {
        if (IsClient)
        {
            textField = GUILayout.TextField(textField, GUILayout.Width(200));
            
            if (GUILayout.Button("Send") && !string.IsNullOrWhiteSpace(textField))
            {
                ChatMessages.Add(textField);
                textField = "";
            }

            for (int i = ChatMessages.Count - 1; i >= 0; i--)
            {
                GUILayout.Label(ChatMessages[i]);
            }
        }
    }  
} 
```   
