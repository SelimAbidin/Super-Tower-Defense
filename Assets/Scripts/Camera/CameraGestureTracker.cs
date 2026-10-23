using UnityEngine;

namespace SuperTowerDefense
{
    /// <summary>Rebases gestures when their pointer identity, count or viewport changes.</summary>
    public sealed class CameraGestureTracker
    {
        public readonly struct Motion
        {
            public readonly bool HasDelta;
            public readonly Vector2 PreviousCenter;
            public readonly Vector2 Center;
            public readonly float ZoomRatio;

            public Motion(Vector2 previousCenter, Vector2 center, float zoomRatio)
            {
                HasDelta = true;
                PreviousCenter = previousCenter;
                Center = center;
                ZoomRatio = zoomRatio;
            }
        }

        private int previousCount;
        private int previousFirstId;
        private int previousSecondId;
        private Vector2 previousSize;
        private Vector2 previousCenter;
        private float previousSeparation;
        private const float MinimumPinchSeparation = 4;

        public void Reset() => previousCount = 0;

        // Pointer order is normalized so platform touch-array reordering cannot create a jump.
        public Motion Sample(int count, int firstId, Vector2 first, int secondId, Vector2 second, Vector2 viewportSize)
        {
            if (count <= 0)
            {
                Reset();
                return default;
            }
            if (count > 1 && firstId > secondId)
            {
                (firstId, secondId) = (secondId, firstId);
                (first, second) = (second, first);
            }
            var center = count > 1 ? (first + second) * 0.5f : first;
            var separation = count > 1 ? Vector2.Distance(first, second) : 0;
            var continuous = previousCount == count && previousFirstId == firstId &&
                (count == 1 || previousSecondId == secondId) && previousSize == viewportSize;
            var ratio = count > 1 && previousSeparation >= MinimumPinchSeparation && separation >= MinimumPinchSeparation
                ? previousSeparation / separation : 1;
            var motion = continuous ? new Motion(previousCenter, center, ratio) : default;
            previousCount = count;
            previousFirstId = firstId;
            previousSecondId = secondId;
            previousSize = viewportSize;
            previousCenter = center;
            previousSeparation = separation;
            return motion;
        }
    }
}
