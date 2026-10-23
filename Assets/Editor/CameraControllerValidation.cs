using System;
using System.IO;
using SuperTowerDefense;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Deterministic checks against the runtime gesture tracker and perspective camera.</summary>
public static class CameraControllerValidation
{
    private static int assertions;
    private static readonly Vector2 Viewport = new Vector2(1600, 900);

    public static void ConfigureScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var camera = Camera.main;
        var bounds = Object.FindFirstObjectByType<CameraMovementBounds>();
        if (bounds == null)
            bounds = new GameObject("Camera Movement Bounds", typeof(CameraMovementBounds)).GetComponent<CameraMovementBounds>();
        var controller = camera.GetComponent<CameraController>();
        if (controller == null)
            controller = camera.gameObject.AddComponent<CameraController>();
        controller.MovementBounds = bounds;
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        EditorSceneManager.SaveScene(camera.gameObject.scene);
        AssetDatabase.SaveAssets();
        ValidateAndCapture();
    }

    [MenuItem("Tools/Super Tower Defense/Validate Camera Controller")]
    public static void ValidateAndCapture()
    {
        assertions = 0;
        ValidateGestures();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var camera = Camera.main;
        var controller = camera.GetComponent<CameraController>();
        Require(controller != null && controller.MovementBounds != null, "Main scene wires controller and bounds");
        var originalPosition = camera.transform.position;
        var originalRotation = camera.transform.rotation;
        var bounds = controller.MovementBounds;
        var boundsPosition = bounds.transform.position;
        var boundsSize = bounds.Size;
        var target = new RenderTexture((int)Viewport.x, (int)Viewport.y, 24);
        camera.targetTexture = target;
        camera.aspect = Viewport.x / Viewport.y;
        try
        {
            Require(controller.InitializeFromCamera(), "Perspective camera initializes");
            Near(controller.Focus, Vector3.zero, "Initial camera looks at origin");
            Capture(camera, "Documentation/camera-start.png");
            var center = Viewport * 0.5f;
            var anchor = center + new Vector2(120, -40);
            var before = GroundPoint(camera, anchor, bounds.transform.position.y);
            var oldDistance = controller.Distance;
            controller.ZoomAt(anchor, 0.7f);
            Near(controller.Distance, oldDistance * 0.7f, "Spread/wheel-in dollies closer");
            Near(GroundPoint(camera, anchor, bounds.transform.position.y), before, "Zoom preserves point under cursor");
            before = GroundPoint(camera, center, bounds.transform.position.y);
            var dragEnd = center + new Vector2(200, 80);
            controller.Pan(center, dragEnd);
            Near(GroundPoint(camera, dragEnd, bounds.transform.position.y), before, "Drag keeps ground beneath pointer");
            Capture(camera, "Documentation/camera-pan-zoom.png");
            controller.ZoomAt(center, 1000);
            Near(controller.Distance, controller.MaximumDistance, "Zoom-out limit");
            controller.ZoomAt(center, 0.0001f);
            Near(controller.Distance, controller.MinimumDistance, "Zoom-in limit");
            var validDistance = controller.Distance;
            controller.ZoomAt(center, float.NaN);
            controller.ZoomAt(center, float.PositiveInfinity);
            controller.ZoomAt(center, -1);
            Near(controller.Distance, validDistance, "Invalid zoom does not corrupt camera");
            controller.ZoomAt(center, 1.7f);
            // Reach each edge with valid ground rays, then zoom using extreme anchors.
            foreach (var delta in new[] { new Vector2(300, 0), new Vector2(-300, 0), new Vector2(0, 150), new Vector2(0, -150) })
            {
                for (var i = 0; i < 30; i++)
                    controller.Pan(center, center + delta);
                Near(controller.Focus, bounds.Clamp(controller.Focus), "Pan remains within bounds " + delta);
                var edge = delta.x != 0 ? Mathf.Abs(controller.Focus.x) : Mathf.Abs(controller.Focus.z);
                Near(edge, 9, "Pan reaches boundary " + delta);
                controller.ZoomAt(center + delta * 100, 0.6f);
                Near(controller.Focus, bounds.Clamp(controller.Focus), "Anchored zoom remains within bounds " + delta);
            }
            bounds.transform.position = new Vector3(25, 2, -15);
            bounds.Size = new Vector2(6, 4);
            controller.ZoomAt(center, 1);
            Near(controller.Focus, bounds.Clamp(controller.Focus), "Moving/resizing bounds constrains focus on new plane");
            Near(camera.transform.position + camera.transform.forward * controller.Distance, controller.Focus, "Camera keeps fixed pitch and distance");
            bounds.Size = Vector2.zero;
            controller.Pan(center, center + new Vector2(300, 200));
            Near(controller.Focus, bounds.transform.position, "Zero-size bounds lock focus");
            // Use the actual runtime motion application for a one-to-two-to-one touch sequence.
            bounds.transform.position = boundsPosition;
            bounds.Size = boundsSize;
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            controller.InitializeFromCamera();
            var tracker = new CameraGestureTracker();
            controller.ApplyMotion(tracker.Sample(1, 10, center, 0, default, Viewport));
            controller.ApplyMotion(tracker.Sample(1, 10, center + new Vector2(100, 0), 0, default, Viewport));
            var beforeTransition = camera.transform.position;
            controller.ApplyMotion(tracker.Sample(2, 10, center, 20, center + new Vector2(200, 0), Viewport));
            Near(camera.transform.position, beforeTransition, "Adding second finger does not jump camera");
            var pinchDistance = controller.Distance;
            controller.ApplyMotion(tracker.Sample(2, 10, center - new Vector2(100, 0), 20, center + new Vector2(300, 0), Viewport));
            Require(controller.Distance < pinchDistance, "Actual two-touch spread zooms in");
            beforeTransition = camera.transform.position;
            controller.ApplyMotion(tracker.Sample(1, 20, center + new Vector2(300, 0), 0, default, Viewport));
            Near(camera.transform.position, beforeTransition, "Lifting first finger does not jump camera");
            foreach (var size in new[] { new Vector2Int(1950, 900), new Vector2Int(1200, 900) })
            {
                camera.targetTexture = null;
                target.Release();
                target.width = size.x;
                target.height = size.y;
                target.Create();
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                controller.ZoomAt(new Vector2(size.x, size.y) * 0.5f, 1.2f);
                Near(controller.Focus, bounds.Clamp(controller.Focus), "Bounded at mobile aspect " + size);
                Capture(camera, $"Documentation/camera-{size.x}x{size.y}.png");
            }
            var report = $"STD-2 camera validation passed: {assertions} assertions. Gesture identity/count/viewport/cancel transitions; pan and anchored perspective zoom; min/max zoom; all bounds edges, moved/resized/zero bounds; actual touch-sequence motion; desktop, phone and tablet renders. Editor synthetic input and renders only; no physical touch device testing.\n";
            File.WriteAllText("Documentation/camera-validation.txt", report);
            Debug.Log(report);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(target);
            // Never persist validation poses or changed bounds in the starter scene.
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }
    }

    private static void ValidateGestures()
    {
        var tracker = new CameraGestureTracker();
        var p = new Vector2(400, 400);
        Require(!tracker.Sample(1, -1, p, 0, default, Viewport).HasDelta, "Mouse down rebases");
        var motion = tracker.Sample(1, -1, p + Vector2.one * 20, 0, default, Viewport);
        Require(motion.HasDelta && motion.Center - motion.PreviousCenter == Vector2.one * 20, "Mouse drag delta");
        Near(motion.ZoomRatio, 1, "Single pointer never pinches");
        Require(!tracker.Sample(0, 0, default, 0, default, Viewport).HasDelta, "Release ends drag");
        Require(!tracker.Sample(1, 1, p, 0, default, Viewport).HasDelta, "First touch rebases");
        Require(!tracker.Sample(2, 1, p, 2, p + Vector2.right * 100, Viewport).HasDelta, "Second touch rebases");
        motion = tracker.Sample(2, 2, p + Vector2.right * 200, 1, p, Viewport);
        Require(motion.HasDelta, "Reordered touch array remains continuous");
        Near(motion.ZoomRatio, 0.5f, "Spread gives inverse distance ratio");
        motion = tracker.Sample(2, 1, p, 2, p + Vector2.right * 100, Viewport);
        Near(motion.ZoomRatio, 2, "Pinch together zooms out");
        Require(!tracker.Sample(2, 1, p, 3, p + Vector2.right * 100, Viewport).HasDelta, "Replacement ID rebases");
        Require(!tracker.Sample(3, 1, p, 3, p + Vector2.right * 100, Viewport).HasDelta, "Third finger rebases");
        Require(!tracker.Sample(2, 1, p, 3, p + Vector2.right * 100, Viewport).HasDelta, "Third finger lifted rebases");
        Require(!tracker.Sample(1, 3, p, 0, default, Viewport).HasDelta, "Two-to-one transition rebases");
        Require(!tracker.Sample(1, -1, p, 0, default, Viewport).HasDelta, "Touch-to-mouse transition rebases");
        Require(!tracker.Sample(1, -1, p, 0, default, new Vector2(900, 1600)).HasDelta, "Viewport resize rebases");
        tracker.Reset();
        Require(!tracker.Sample(1, -1, p, 0, default, Viewport).HasDelta, "Focus/pause/reset rebases");
        tracker.Sample(0, 0, default, 0, default, Viewport);
        Require(!tracker.Sample(1, 9, p, 0, default, Viewport).HasDelta, "Canceled/ended touch cannot resume old drag");
        tracker.Sample(2, 1, p, 2, p, Viewport);
        motion = tracker.Sample(2, 1, p, 2, p + Vector2.right * 100, Viewport);
        Near(motion.ZoomRatio, 1, "Coincident fingers do not cause invalid zoom");
    }

    private static Vector3 GroundPoint(Camera camera, Vector2 point, float y)
    {
        var ray = camera.ScreenPointToRay(point);
        Require(new Plane(Vector3.up, new Vector3(0, y, 0)).Raycast(ray, out var enter), "Pointer ray intersects ground");
        return ray.GetPoint(enter);
    }

    private static void Capture(Camera camera, string path)
    {
        camera.Render();
        var target = camera.targetTexture;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(image);
    }

    private static void Require(bool condition, string description)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("STD-2 camera assertion failed: " + description);
    }
    private static void Near(Vector3 actual, Vector3 expected, string description) => Require(Vector3.Distance(actual, expected) < 0.003f, description + $" ({actual} vs {expected})");
    private static void Near(float actual, float expected, string description) => Require(Mathf.Abs(actual - expected) < 0.003f, description + $" ({actual} vs {expected})");
}
