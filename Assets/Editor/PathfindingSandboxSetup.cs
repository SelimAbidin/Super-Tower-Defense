using System;
using System.IO;
using SuperTowerDefense.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PathfindingSandboxSetup
{
    public const string ScenePath = "Assets/Scenes/PathfindingSandbox.unity";

    [MenuItem("Tools/Super Tower Defense/Create Pathfinding Sandbox")]
    public static void CreateAndValidate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 16, -16);
        camera.transform.LookAt(new Vector3(0, 0, 1));
        camera.fieldOfView = 49;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
        var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(55, -25, 0);
        light.intensity = 1;
        Color[] colors =
        {
            new Color(0.13f, 0.19f, 0.24f), new Color(0.16f, 0.23f, 0.28f),
            new Color(0.35f, 0.4f, 0.48f), new Color(0.1f, 0.8f, 0.9f),
            new Color(0.96f, 0.65f, 0.2f), new Color(0.96f, 0.37f, 0.45f),
            new Color(0.6f, 0.5f, 1), new Color(0.2f, 0.85f, 0.5f),
            new Color(0.2f, 0.65f, 1), Color.white
        };
        Directory.CreateDirectory("Assets/Materials/Pathfinding");
        var materials = new Material[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            string path = "Assets/Materials/Pathfinding/Palette" + i + ".mat";
            materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (materials[i] == null)
            {
                materials[i] = new Material(Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(materials[i], path);
            }
            materials[i].color = colors[i];
            EditorUtility.SetDirty(materials[i]);
        }
        string linePath = "Assets/Materials/Pathfinding/Routes.mat";
        var routeMaterial = AssetDatabase.LoadAssetAtPath<Material>(linePath);
        if (routeMaterial == null)
        {
            routeMaterial = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(routeMaterial, linePath);
        }
        var demo = new GameObject("Pathfinding Sandbox", typeof(PathfindingSandbox)).GetComponent<PathfindingSandbox>();
        demo.Configure(materials, routeMaterial);
        demo.ResetSandbox();
        EditorSceneManager.SaveScene(scene, ScenePath);
        // Keep the gameplay starter and its build configuration untouched.
        AssetDatabase.SaveAssets();
        PathfindingValidation.Run();
        ValidateScene();
        Capture(camera);
        Debug.Log("STD-6 SCENE PASS: retained PathfindingSandbox scene, primitive previews and materials, screenshot captured; Main scene/build settings untouched.");
    }

    [MenuItem("Tools/Super Tower Defense/Open Pathfinding Sandbox")]
    public static void Open()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }

    private static void ValidateScene()
    {
        var demo = UnityEngine.Object.FindFirstObjectByType<PathfindingSandbox>();
        if (demo == null || Camera.main == null || Camera.main.orthographic)
            throw new InvalidOperationException("Sandbox requires its simulation and perspective camera.");
        var board = demo.transform.Find("Board");
        if (board == null || board.Find("Agent 5") == null || board.Find("Exit 5") == null || board.Find("Tile 16,10") == null)
            throw new InvalidOperationException("Missing retained grid/marker preview objects.");
        for (int i = 0; i < demo.Simulation.AgentCount; i++)
        {
            Vector3 expected = demo.World(demo.Simulation.Position(i), 0.45f);
            if (board.Find("Agent " + (i + 1)).position != expected) throw new InvalidOperationException("Marker mismatch.");
        }
    }

    private static void Capture(Camera camera)
    {
        Directory.CreateDirectory("Documentation");
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24);
        var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            image.Apply();
            File.WriteAllBytes("Documentation/pathfinding-sandbox.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
