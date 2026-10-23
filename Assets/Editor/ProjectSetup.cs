using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rebuild the small starter scene via Tools > Super Tower Defense > Create Starter Scene.
public static class ProjectSetup
{
    [MenuItem("Tools/Super Tower Defense/Create Starter Scene")]
    public static void CreateStarterScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 60;
        camera.transform.position = new Vector3(0, 6, -9);
        camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.16f, 0.22f);

        var lightObject = new GameObject("Directional Light", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.42f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2, 1, 2);
        ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Ground", new Color(0.22f, 0.30f, 0.38f));
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Starter Cube";
        marker.transform.position = new Vector3(0, 0.5f, 0);
        marker.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Starter Cube", new Color(0.18f, 0.65f, 0.58f));
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        AssetDatabase.SaveAssets();
        CaptureAndValidate();
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Directory.CreateDirectory("Assets/Materials");
        var path = "Assets/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Glossiness", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void CaptureAndValidate()
    {
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/Main.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var camera = Camera.main;
        if (camera == null || camera.orthographic || camera.GetComponent<AudioListener>() == null)
            throw new System.InvalidOperationException("Starter scene requires a perspective Main Camera and audio listener.");
        if (Object.FindFirstObjectByType<Light>() == null || !EditorBuildSettings.scenes[0].enabled)
            throw new System.InvalidOperationException("Starter scene requires a light and enabled build scene.");
        Directory.CreateDirectory("Documentation");
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        texture.Apply();
        File.WriteAllBytes("Documentation/starter-scene.png", texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(target);
        Debug.Log("STD-1 validation passed: 3D starter scene, perspective camera, audio listener, light, enabled build scene and rendered evidence.");
    }
}
