using UnityEngine;

namespace SuperTowerDefense
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField, Tooltip("Movable world-space rectangle that limits the ground focus, not the viewport edges.")]
        private CameraMovementBounds movementBounds;
        [SerializeField, Min(0.1f), Tooltip("Closest distance along the camera's fixed viewing direction.")]
        private float minimumDistance = 5;
        [SerializeField, Min(0.1f), Tooltip("Farthest distance along the camera's fixed viewing direction.")]
        private float maximumDistance = 18;
        [SerializeField, Min(0)] private float wheelSensitivity = 0.15f;

        private readonly CameraGestureTracker gestures = new CameraGestureTracker();
        private Camera controlledCamera;
        private Vector3 focus;
        private float distance;
        private bool initialized;
        private bool hasFocus = true;

        public CameraMovementBounds MovementBounds
        {
            get => movementBounds;
            set
            {
                movementBounds = value;
                initialized = false;
                gestures.Reset();
            }
        }
        public Vector3 Focus => focus;
        public float Distance => distance;
        public float MinimumDistance => minimumDistance;
        public float MaximumDistance => maximumDistance;

        private Plane GroundPlane => new Plane(Vector3.up, movementBounds.transform.position);

        private void OnEnable()
        {
            initialized = false;
            gestures.Reset();
        }

        private void OnDisable() => gestures.Reset();
        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            gestures.Reset();
        }
        private void OnApplicationPause(bool paused) => gestures.Reset();

        private void OnValidate()
        {
            minimumDistance = Mathf.Max(0.1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            wheelSensitivity = Mathf.Max(0, wheelSensitivity);
            initialized = false;
            gestures.Reset();
        }

        public bool InitializeFromCamera()
        {
            gestures.Reset();
            controlledCamera = GetComponent<Camera>();
            if (movementBounds == null || controlledCamera.orthographic ||
                !GroundPlane.Raycast(new Ray(transform.position, transform.forward), out var initialDistance))
            {
                initialized = false;
                return false;
            }
            distance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
            focus = movementBounds.Clamp(transform.position + transform.forward * initialDistance);
            initialized = true;
            ApplyPosition();
            return true;
        }

        private void Update()
        {
            if (!hasFocus || movementBounds == null || (!initialized && !InitializeFromCamera()))
                return;
            // Reapply bounds each frame so moving/resizing the bounds also constrains an idle camera.
            ApplyPosition();
            ReadPointers(out var count, out var firstId, out var first, out var secondId, out var second);
            var motion = gestures.Sample(count, firstId, first, secondId, second, new Vector2(Screen.width, Screen.height));
            ApplyMotion(motion);
            // Never process the mouse events Unity may synthesize from touches.
            if (Input.touchCount == 0 && Input.mouseScrollDelta.y != 0)
                ZoomAt(Input.mousePosition, Mathf.Exp(-Input.mouseScrollDelta.y * wheelSensitivity));
        }

        private static void ReadPointers(out int count, out int firstId, out Vector2 first, out int secondId, out Vector2 second)
        {
            count = 0;
            firstId = int.MaxValue;
            secondId = int.MaxValue;
            first = second = default;
            if (Input.touchCount > 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        continue;
                    count++;
                    // Keep the two lowest IDs stable even with a third finger on screen.
                    if (touch.fingerId < firstId)
                    {
                        secondId = firstId;
                        second = first;
                        firstId = touch.fingerId;
                        first = touch.position;
                    }
                    else if (touch.fingerId < secondId)
                    {
                        secondId = touch.fingerId;
                        second = touch.position;
                    }
                }
                return;
            }
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                count = 1;
                firstId = -1;
                first = Input.mousePosition;
            }
        }

        public void ApplyMotion(CameraGestureTracker.Motion motion)
        {
            if (!motion.HasDelta)
                return;
            Pan(motion.PreviousCenter, motion.Center);
            ZoomAt(motion.Center, motion.ZoomRatio);
        }

        public void Pan(Vector2 previousScreenPoint, Vector2 screenPoint)
        {
            if ((!initialized && !InitializeFromCamera()) ||
                !TryGroundPoint(previousScreenPoint, out var previous) || !TryGroundPoint(screenPoint, out var current))
                return;
            focus += previous - current;
            ApplyPosition();
        }

        public void ZoomAt(Vector2 screenPoint, float ratio)
        {
            if ((!initialized && !InitializeFromCamera()) || ratio <= 0 || float.IsNaN(ratio) || float.IsInfinity(ratio))
                return;
            var anchored = TryGroundPoint(screenPoint, out var before);
            distance = Mathf.Clamp(distance * ratio, minimumDistance, maximumDistance);
            ApplyPosition();
            if (anchored && TryGroundPoint(screenPoint, out var after))
            {
                focus += before - after;
                ApplyPosition();
            }
        }

        private bool TryGroundPoint(Vector2 screenPoint, out Vector3 point)
        {
            var ray = controlledCamera.ScreenPointToRay(screenPoint);
            if (GroundPlane.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        private void ApplyPosition()
        {
            focus = movementBounds.Clamp(focus);
            transform.position = focus - transform.forward * distance;
        }
    }
}
