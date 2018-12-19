---
title: Installation
permalink: /wiki/installation/
---

### Installer
To get started with the MLAPI. You need to install the library. The easiest way is to use the Editor installer. Simply download the MLAPI_Installer unity package from the [here](https://github.com/TwoTenPvP/MLAPI/releases). Then press window at the top of your editor and select MLAPI. Once in the MLAPI window, select the version you wish to use and press install.


![Video showing the install process](https://i.imgur.com/zN63DlJ.gif)


Once imported into the Unity Engine, you will be able to use the components that it offers. To get started, you need a GameObject with the NetworkingManager component. Once you have that, use the Initializing the library articles to continue.


### Files
The MLAPI comes with 3 main components
##### MLAPI.dll + IntXLib.dll
These two DLL's are the runtime portion. The actual library. *IntXLib.dll is the BigInt library used for the ECDHE implementation*. These files are thus **required**.
##### MLAPI-Editor.unitypackage
This unitypackage includes the source files for all the Editor scripts. The UnityPackage will automatically place these source files in the Editor folder to avoid it being included in a build. **While the MLAPI will function without this, it is not designed to work without the editor part and is thus recommended for all users**.
##### MLAPI-Installer.unitypackage
This unitypackage includes the source file for the installer. This component is totally optional. The Installer can help you manage versions. If you don't want to use the installer, you can simply place the MLAPI.dll, IntXLib.dll and the Editor source files in your project and it will work just as well.



### Important note
_The wiki, API references, readme and other documentation like information is not updated on a per commit basis. They are being updated on a per release basis. Thus using in development features on the master branch is not discouraged but there might not be any documentation except the commit messages._