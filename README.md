# Super Tower Defense

Unity 3D starter project using Unity **6000.6.7f1** and the built-in render pipeline.

Open this repository folder in Unity Hub with the version in `ProjectSettings/ProjectVersion.txt`, then open `Assets/Scenes/Main.unity` and press Play. The scene contains a perspective Main Camera (60° field of view), audio listener, directional light, ground plane and starter cube. `Main` is the enabled build scene. No gameplay is implemented yet.

The cube and plane provide a simple 3D reference for future work. Recreate the scene with **Tools > Super Tower Defense > Create Starter Scene**. This replaces the current scene; save any scene work first.

Validation can also run from the command line:

```sh
Unity -batchmode -quit -projectPath "$PWD" -executeMethod ProjectSetup.CaptureAndValidate -logFile /tmp/std1-validation.log
```

The command verifies the camera, light and build scene and renders `Documentation/starter-scene.png`.

![Starter scene](Documentation/starter-scene.png)
