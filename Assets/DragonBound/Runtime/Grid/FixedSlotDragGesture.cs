using System;

namespace DragonBound.Grid
{
    public sealed class FixedSlotDragGesture
    {
        private const int NoPointer = int.MinValue;

        private float pointerDownX;
        private float pointerDownY;
        private bool pointerIsDown;
        private int activePointerId = NoPointer;

        public bool IsDragging { get; private set; }
        public bool HasActivePointer => pointerIsDown;

        public bool PointerDown(float x, float y)
        {
            return PointerDown(0, x, y);
        }

        public bool PointerDown(int pointerId, float x, float y)
        {
            if (pointerIsDown)
            {
                return false;
            }

            pointerDownX = x;
            pointerDownY = y;
            pointerIsDown = true;
            activePointerId = pointerId;
            IsDragging = false;
            return true;
        }

        public bool OwnsPointer(int pointerId)
        {
            return pointerIsDown && activePointerId == pointerId;
        }

        public bool TryBeginDrag(float x, float y, float thresholdPixels)
        {
            return TryBeginDrag(0, x, y, thresholdPixels);
        }

        public bool TryBeginDrag(int pointerId, float x, float y, float thresholdPixels)
        {
            if (!OwnsPointer(pointerId) || IsDragging)
            {
                return false;
            }

            if (thresholdPixels < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(thresholdPixels));
            }

            var deltaX = x - pointerDownX;
            var deltaY = y - pointerDownY;
            if ((deltaX * deltaX) + (deltaY * deltaY) < thresholdPixels * thresholdPixels)
            {
                return false;
            }

            IsDragging = true;
            return true;
        }

        public bool PointerUp()
        {
            return PointerUp(0);
        }

        public bool PointerUp(int pointerId)
        {
            if (!OwnsPointer(pointerId))
            {
                return false;
            }

            var isTap = pointerIsDown && !IsDragging;
            Cancel();
            return isTap;
        }

        public void Cancel()
        {
            pointerIsDown = false;
            activePointerId = NoPointer;
            IsDragging = false;
        }
    }
}
