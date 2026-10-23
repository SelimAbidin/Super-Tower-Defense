using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MobileBuild
{
    [MenuItem("Tools/Super Tower Defense/Configure Mobile")]
    public static void Configure()
    {
        PlayerSettings.companyName = "SelimAbidin";
        PlayerSettings.productName = "Super Tower Defense";
        foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
        {
            PlayerSettings.SetApplicationIdentifier(target, "com.selimabidin.supertowerdefense");
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
        }
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        // Keep desktop quality intact; the existing Medium tier is the mobile default.
        QualitySettings.SetQualityLevel(2, true);
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowDistance = 20;
        QualitySettings.antiAliasing = 2;
        QualitySettings.pixelLightCount = 1;
        AssetDatabase.SaveAssets();
        Debug.Log("Mobile configuration saved: landscape, ARM64 IL2CPP, GLES3/Metal, Android 26+, iOS 15+.");
    }

    public static void CaptureAndValidate()
    {
        ProjectSetup.CaptureAndValidate();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var camera = Camera.main;
        Directory.CreateDirectory("Documentation");
        foreach (var size in new[] { new Vector2Int(1600, 900), new Vector2Int(1950, 900), new Vector2Int(1200, 900) })
        {
            var target = new RenderTexture(size.x, size.y, 24);
            camera.targetTexture = target;
            camera.aspect = (float)size.x / size.y;
            var bounds = GameObject.Find("Starter Cube").GetComponent<Renderer>().bounds;
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var view = camera.WorldToViewportPoint(corner);
                if (view.z <= 0 || view.x <= 0 || view.x >= 1 || view.y <= 0 || view.y >= 1)
                    throw new InvalidOperationException("Starter cube clipped at " + size);
            }
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            File.WriteAllBytes($"Documentation/mobile-{size.x}x{size.y}.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
        Debug.Log("Mobile aspect validation passed at 16:9, 19.5:9 and 4:3 (editor rendering only).");
    }

    public static void AndroidApk()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Install Android Build Support, SDK/NDK and OpenJDK through Unity Hub.");
        EditorUserBuildSettings.buildAppBundle = false;
        Build("Builds/Android/SuperTowerDefense.apk", BuildTarget.Android);
    }

    public static void IosProject()
    {
        Build("Builds/iOS", BuildTarget.iOS);
    }

    private static void Build(string path, BuildTarget target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, path, target, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"{target} build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
        Debug.Log($"Mobile build succeeded: {path} ({report.summary.totalSize} bytes).");
    }
}
