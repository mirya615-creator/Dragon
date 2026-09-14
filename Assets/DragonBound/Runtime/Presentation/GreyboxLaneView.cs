using System;
using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using UnityEngine;

namespace DragonBound.Presentation
{
    public sealed class GreyboxLaneView : MonoBehaviour
    {
        [SerializeField] private RectTransform enemyMarker;
        [SerializeField] private RectTransform[] waypoints;
        [SerializeField] private EnemyView enemyViewTemplate;
        [SerializeField] private EnemyView enemyViewPrefab;
        [SerializeField] private RectTransform enemyViewContainer;
        [SerializeField] private float travelSeconds = 12f;
        [SerializeField] private bool reverseDirection;

        private readonly Dictionary<string, EnemyView> enemyViews =
            new Dictionary<string, EnemyView>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> lastKnownEnemyPositions =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> lastKnownEnemyVisualImpactPositions =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly Dictionary<string, EnemyPullVisual> enemyPullVisuals =
            new Dictionary<string, EnemyPullVisual>(StringComparer.Ordinal);
        private readonly HashSet<string> frostcrownMarkedEnemyIds =
            new HashSet<string>(StringComparer.Ordinal);
        private MatchController match;
        private EnemyRegistry registry;
        private TeamSide side;
        private float progress;
        private FixedBoardCanvasView fixedBoardCanvas;

        public RectTransform EnemyMarker => enemyMarker;
        public int WaypointCount => waypoints != null ? waypoints.Length : 0;
        public IReadOnlyList<RectTransform> Waypoints => waypoints;
        public string GoalNodeName => waypoints != null && waypoints.Length > 0
            ? waypoints[waypoints.Length - 1].name
            : string.Empty;
        public int EnemyViewCount => enemyViews.Count;

        public void SetFrostcrownMarkedEnemies(IEnumerable<string> runtimeIds)
        {
            frostcrownMarkedEnemyIds.Clear();
            if (runtimeIds != null)
            {
                foreach (var runtimeId in runtimeIds)
                {
                    if (!string.IsNullOrWhiteSpace(runtimeId))
                    {
                        frostcrownMarkedEnemyIds.Add(runtimeId);
                    }
                }
            }

            foreach (var entry in enemyViews)
            {
                if (entry.Value != null)
                {
                    entry.Value.SetFrostcrownMarked(
                        frostcrownMarkedEnemyIds.Contains(entry.Key));
                }
            }
        }

        public RectTransform RoadArtTemplate
        {
            get
            {
                foreach (var candidate in GetComponentsInChildren<RectTransform>(true))
                {
                    if (candidate != null && candidate.name.StartsWith("ART_Path", StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }

                return null;
            }
        }

        public void ConfigureFixedBoardCanvas(FixedBoardCanvasView canvasView)
        {
            fixedBoardCanvas = canvasView ?? throw new ArgumentNullException(nameof(canvasView));
        }

        public bool TryGetEnemyPosition(string runtimeId, out Vector3 position)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                enemyViews.TryGetValue(runtimeId, out var view) &&
                view != null)
            {
                position = view.RectTransform.position;
                lastKnownEnemyPositions[runtimeId] = position;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                registry != null &&
                registry.TryGet(runtimeId, out var enemy))
            {
                position = GetEnemyPosition(enemy);
                lastKnownEnemyPositions[runtimeId] = position;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                lastKnownEnemyPositions.TryGetValue(runtimeId, out position))
            {
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        public bool TryGetEnemyVisualImpactPosition(string runtimeId, out Vector3 position)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                enemyViews.TryGetValue(runtimeId, out var view) &&
                view != null)
            {
                position = view.VisualImpactPosition;
                lastKnownEnemyVisualImpactPositions[runtimeId] = position;
                return true;
            }

            // A delayed projectile can arrive after a lethal target view has gone away.
            // Prefer its last visual centre, then fall back to the retained path position.
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                lastKnownEnemyVisualImpactPositions.TryGetValue(runtimeId, out position))
            {
                return true;
            }

            return TryGetEnemyPosition(runtimeId, out position);
        }

        /// <summary>
        /// Resolves a visible, living enemy directly under a screen-space pointer.
        /// When enemy rectangles overlap, the closest visual centre wins and the
        /// runtime id provides a deterministic tie-break.
        /// </summary>
        public bool TryGetEnemyAtScreenPoint(
            Vector2 screenPoint,
            Camera eventCamera,
            out string runtimeId,
            out RectTransform targetRect)
        {
            runtimeId = null;
            targetRect = null;
            var bestDistanceSquared = float.MaxValue;
            var corners = new Vector3[4];

            foreach (var entry in enemyViews)
            {
                var view = entry.Value;
                var rect = view != null ? view.RectTransform : null;
                if (rect == null || registry == null ||
                    !registry.TryGet(entry.Key, out var enemy) ||
                    enemy.Team != side || !enemy.IsAlive ||
                    !RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, eventCamera))
                {
                    continue;
                }

                rect.GetWorldCorners(corners);
                var bottomLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
                var topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
                var centre = (bottomLeft + topRight) * 0.5f;
                var distanceSquared = (centre - screenPoint).sqrMagnitude;
                if (targetRect != null &&
                    (distanceSquared > bestDistanceSquared + 0.0001f ||
                     (Mathf.Abs(distanceSquared - bestDistanceSquared) <= 0.0001f &&
                      string.CompareOrdinal(entry.Key, runtimeId) >= 0)))
                {
                    continue;
                }

                runtimeId = entry.Key;
                targetRect = rect;
                bestDistanceSquared = distanceSquared;
            }

            return targetRect != null;
        }

        public bool TryGetEnemyPathAxis(string runtimeId, out Vector3 axis)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                registry != null &&
                registry.TryGet(runtimeId, out var enemy) &&
                TryGetPathAxis(enemy.PathProgress, out axis))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                lastKnownEnemyPositions.TryGetValue(runtimeId, out var lastPosition))
            {
                return TryGetNearestPathAxis(lastPosition, out axis);
            }

            axis = Vector3.zero;
            return false;
        }

        public bool TryGetEnemyBackwardPath(
            string runtimeId,
            float maximumDistance,
            List<Vector3> pathPoints)
        {
            if (pathPoints == null)
            {
                throw new ArgumentNullException(nameof(pathPoints));
            }

            pathPoints.Clear();
            if (maximumDistance <= 0f || waypoints == null || waypoints.Length < 3)
            {
                return false;
            }

            Vector3 targetPosition;
            int segmentIndex;
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                registry != null &&
                registry.TryGet(runtimeId, out var enemy))
            {
                var scaled = Mathf.Clamp01(enemy.PathProgress) * (waypoints.Length - 1f);
                segmentIndex = Mathf.Min(Mathf.FloorToInt(scaled), waypoints.Length - 2);
                targetPosition = GetEnemyPosition(enemy);
            }
            else if (!string.IsNullOrWhiteSpace(runtimeId) &&
                     lastKnownEnemyPositions.TryGetValue(runtimeId, out targetPosition))
            {
                segmentIndex = FindNearestPathSegment(targetPosition);
            }
            else
            {
                return false;
            }

            return BuildBackwardPath(
                targetPosition,
                segmentIndex,
                maximumDistance,
                1,
                pathPoints);
        }

        public bool TryGetBackwardPathFromPosition(
            Vector3 targetPosition,
            float maximumDistance,
            List<Vector3> pathPoints)
        {
            if (pathPoints == null)
            {
                throw new ArgumentNullException(nameof(pathPoints));
            }

            pathPoints.Clear();
            if (maximumDistance <= 0f || waypoints == null || waypoints.Length < 3)
            {
                return false;
            }

            return BuildBackwardPath(
                targetPosition,
                FindNearestPathSegment(targetPosition),
                maximumDistance,
                1,
                pathPoints);
        }

        private bool BuildBackwardPath(
            Vector3 targetPosition,
            int segmentIndex,
            float maximumDistance,
            int minimumWaypointIndex,
            List<Vector3> pathPoints)
        {
            var backwards = new List<Vector3> { targetPosition };
            var cursor = targetPosition;
            var remaining = maximumDistance;
            while (remaining > 0.01f && segmentIndex >= minimumWaypointIndex)
            {
                // Portal routes pass minimumWaypointIndex=1 to protect spawn art;
                // enemy displacement passes zero so its visual matches gameplay.
                var previous = waypoints[segmentIndex].position;
                var segmentDistance = Vector3.Distance(cursor, previous);
                if (segmentDistance > remaining && segmentDistance > 0.001f)
                {
                    backwards.Add(Vector3.MoveTowards(cursor, previous, remaining));
                    remaining = 0f;
                    break;
                }

                if (segmentDistance > 0.001f)
                {
                    backwards.Add(previous);
                    remaining -= segmentDistance;
                }

                cursor = previous;
                segmentIndex--;
            }

            for (var index = backwards.Count - 1; index >= 0; index--)
            {
                if (pathPoints.Count == 0 ||
                    Vector3.Distance(pathPoints[pathPoints.Count - 1], backwards[index]) > 0.01f)
                {
                    pathPoints.Add(backwards[index]);
                }
            }

            return pathPoints.Count >= 2;
        }

        public void PlayEnemyPathPullVisual(
            string runtimeId,
            Vector3 startPosition,
            float duration)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) ||
                registry == null ||
                !registry.TryGet(runtimeId, out var enemy))
            {
                return;
            }

            enemyPullVisuals[runtimeId] = new EnemyPullVisual(
                startPosition,
                GetEnemyPosition(enemy),
                Mathf.Max(0.05f, duration),
                false,
                null);
        }

        public void HoldEnemyPositionForPull(string runtimeId, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return;
            }

            enemyPullVisuals[runtimeId] = new EnemyPullVisual(
                position,
                position,
                1f,
                true,
                null);
            if (enemyViews.TryGetValue(runtimeId, out var view) && view != null)
            {
                view.RectTransform.position = position;
                lastKnownEnemyPositions[runtimeId] = position;
            }
        }

        public void PlayEnemyPathPullVisual(
            string runtimeId,
            Vector3 startPosition,
            Vector3 endPosition,
            float duration,
            Action onComplete)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                onComplete?.Invoke();
                return;
            }

            enemyPullVisuals[runtimeId] = new EnemyPullVisual(
                startPosition,
                endPosition,
                Mathf.Max(0.05f, duration),
                false,
                onComplete);
        }

        public bool TryGetBackwardPositionFromPosition(
            Vector3 startPosition,
            float distance,
            out Vector3 endPosition)
        {
            var points = new List<Vector3>();
            if (distance > 0f && waypoints != null && waypoints.Length >= 2 &&
                BuildBackwardPath(
                    startPosition,
                    FindNearestPathSegment(startPosition),
                    distance,
                    0,
                    points) &&
                points.Count > 0)
            {
                endPosition = points[0];
                return true;
            }

            endPosition = startPosition;
            return false;
        }

        private int FindNearestPathSegment(Vector3 position)
        {
            var nearest = 0;
            var nearestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < waypoints.Length - 1; index++)
            {
                var start = waypoints[index].position;
                var end = waypoints[index + 1].position;
                var delta = end - start;
                var progress = delta.sqrMagnitude <= 0.0001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(position - start, delta) / delta.sqrMagnitude);
                var distanceSquared = (position - (start + (delta * progress))).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = index;
                }
            }

            return nearest;
        }

        public bool TryGetEnemyPathTileIndex(string runtimeId, out int tileIndex)
        {
            tileIndex = -1;
            if (waypoints == null || waypoints.Length < 3)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                registry != null &&
                registry.TryGet(runtimeId, out var enemy))
            {
                var scaled = Mathf.Clamp01(enemy.PathProgress) * (waypoints.Length - 1f);
                tileIndex = Mathf.Clamp(
                    Mathf.RoundToInt(scaled),
                    1,
                    waypoints.Length - 2);
                return true;
            }

            if (string.IsNullOrWhiteSpace(runtimeId) ||
                !lastKnownEnemyPositions.TryGetValue(runtimeId, out var lastPosition))
            {
                return false;
            }

            var nearestDistanceSquared = float.PositiveInfinity;
            for (var index = 1; index < waypoints.Length - 1; index++)
            {
                var distanceSquared = (waypoints[index].position - lastPosition).sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                tileIndex = index;
            }

            return tileIndex >= 1;
        }

        public bool TryGetBurningGroundPathTiles(
            int targetTileIndex,
            List<Vector3> tilePositions)
        {
            if (tilePositions == null)
            {
                throw new ArgumentNullException(nameof(tilePositions));
            }

            tilePositions.Clear();
            if (waypoints == null || waypoints.Length < 3 ||
                targetTileIndex < 1 || targetTileIndex >= waypoints.Length - 1)
            {
                return false;
            }

            for (var offset = -1; offset <= 1; offset++)
            {
                var index = targetTileIndex + offset;
                // Index zero and the final index carry spawn/goal UI and never burn.
                if (index <= 0 || index >= waypoints.Length - 1)
                {
                    continue;
                }

                tilePositions.Add(waypoints[index].position);
            }

            return tilePositions.Count > 0;
        }

        public bool TryGetSafeBurningGroundTiles(
            string runtimeId,
            int maximumTileCount,
            List<Vector3> tilePositions)
        {
            if (tilePositions == null)
            {
                throw new ArgumentNullException(nameof(tilePositions));
            }

            tilePositions.Clear();
            if (maximumTileCount <= 0 || waypoints == null || waypoints.Length < 3 ||
                !TryGetEnemyPathSegment(
                    runtimeId,
                    out var targetPosition,
                    out var segmentStart,
                    out var segmentEnd))
            {
                return false;
            }

            var horizontal = Mathf.Abs(segmentEnd.x - segmentStart.x) >=
                             Mathf.Abs(segmentEnd.y - segmentStart.y);
            var perpendicularCoordinate = horizontal
                ? (segmentStart.y + segmentEnd.y) * 0.5f
                : (segmentStart.x + segmentEnd.x) * 0.5f;
            var candidates = new List<int>();
            for (var index = 1; index < waypoints.Length - 1; index++)
            {
                var position = waypoints[index].position;
                var candidatePerpendicular = horizontal ? position.y : position.x;
                if (Mathf.Abs(candidatePerpendicular - perpendicularCoordinate) <= 1f)
                {
                    candidates.Add(index);
                }
            }

            var selectedIndices = new List<int>();
            var axis = horizontal ? Vector3.right : Vector3.up;
            candidates.Sort((first, second) =>
                Vector3.Dot(waypoints[first].position, axis).CompareTo(
                    Vector3.Dot(waypoints[second].position, axis)));
            if (candidates.Count > 0)
            {
                var targetCandidate = 0;
                var targetDistanceSquared = float.PositiveInfinity;
                for (var index = 0; index < candidates.Count; index++)
                {
                    var distanceSquared =
                        (waypoints[candidates[index]].position - targetPosition).sqrMagnitude;
                    if (distanceSquared >= targetDistanceSquared)
                    {
                        continue;
                    }

                    targetDistanceSquared = distanceSquared;
                    targetCandidate = index;
                }

                // Keep the target's snapped road cell in the middle whenever possible.
                // Near a protected spawn/goal boundary, shift the full three-cell window
                // inward instead of placing fire on the endpoint UI.
                var selectionCount = Mathf.Min(maximumTileCount, candidates.Count);
                var selectionStart = Mathf.Clamp(
                    targetCandidate - (selectionCount / 2),
                    0,
                    candidates.Count - selectionCount);
                for (var index = 0; index < selectionCount; index++)
                {
                    selectedIndices.Add(candidates[selectionStart + index]);
                }
            }

            // Very short corner runs can contain fewer than three collinear road cells.
            // Fill only from other interior road cells; spawn and goal remain excluded.
            if (selectedIndices.Count < maximumTileCount)
            {
                var remaining = new List<int>();
                for (var index = 1; index < waypoints.Length - 1; index++)
                {
                    if (!selectedIndices.Contains(index))
                    {
                        remaining.Add(index);
                    }
                }
                remaining.Sort((first, second) =>
                    (waypoints[first].position - targetPosition).sqrMagnitude.CompareTo(
                        (waypoints[second].position - targetPosition).sqrMagnitude));
                for (var index = 0;
                     index < remaining.Count && selectedIndices.Count < maximumTileCount;
                     index++)
                {
                    selectedIndices.Add(remaining[index]);
                }
            }

            selectedIndices.Sort((first, second) =>
                Vector3.Dot(waypoints[first].position, axis).CompareTo(
                    Vector3.Dot(waypoints[second].position, axis)));
            foreach (var index in selectedIndices)
            {
                tilePositions.Add(waypoints[index].position);
            }

            return tilePositions.Count > 0;
        }

        private bool TryGetEnemyPathSegment(
            string runtimeId,
            out Vector3 targetPosition,
            out Vector3 segmentStart,
            out Vector3 segmentEnd)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                registry != null &&
                registry.TryGet(runtimeId, out var enemy))
            {
                targetPosition = GetEnemyPosition(enemy);
                var scaled = Mathf.Clamp01(enemy.PathProgress) * (waypoints.Length - 1f);
                var segment = Mathf.Min(Mathf.FloorToInt(scaled), waypoints.Length - 2);
                segmentStart = waypoints[segment].position;
                segmentEnd = waypoints[segment + 1].position;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                lastKnownEnemyPositions.TryGetValue(runtimeId, out targetPosition))
            {
                var bestDistanceSquared = float.PositiveInfinity;
                segmentStart = Vector3.zero;
                segmentEnd = Vector3.zero;
                for (var index = 0; index < waypoints.Length - 1; index++)
                {
                    var start = waypoints[index].position;
                    var end = waypoints[index + 1].position;
                    var delta = end - start;
                    if (delta.sqrMagnitude <= 0.0001f)
                    {
                        continue;
                    }

                    var progress = Mathf.Clamp01(
                        Vector3.Dot(targetPosition - start, delta) / delta.sqrMagnitude);
                    var distanceSquared =
                        (targetPosition - (start + (delta * progress))).sqrMagnitude;
                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    segmentStart = start;
                    segmentEnd = end;
                }

                return bestDistanceSquared < float.PositiveInfinity;
            }

            targetPosition = Vector3.zero;
            segmentStart = Vector3.zero;
            segmentEnd = Vector3.zero;
            return false;
        }

        private bool TryGetPathAxis(float pathProgress, out Vector3 axis)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                axis = Vector3.zero;
                return false;
            }

            var scaled = Mathf.Clamp01(pathProgress) * (waypoints.Length - 1f);
            var segment = Mathf.Min(Mathf.FloorToInt(scaled), waypoints.Length - 2);
            return TryResolveCardinalAxis(
                waypoints[segment].position,
                waypoints[segment + 1].position,
                out axis);
        }

        private bool TryGetNearestPathAxis(Vector3 position, out Vector3 axis)
        {
            axis = Vector3.zero;
            if (waypoints == null || waypoints.Length < 2)
            {
                return false;
            }

            var bestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < waypoints.Length - 1; index++)
            {
                var start = waypoints[index].position;
                var end = waypoints[index + 1].position;
                var segment = end - start;
                if (segment.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                var progress = Mathf.Clamp01(Vector3.Dot(position - start, segment) / segment.sqrMagnitude);
                var closest = start + (segment * progress);
                var distanceSquared = (position - closest).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared ||
                    !TryResolveCardinalAxis(start, end, out var candidateAxis))
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                axis = candidateAxis;
            }

            return axis.sqrMagnitude > 0.5f;
        }

        private static bool TryResolveCardinalAxis(Vector3 start, Vector3 end, out Vector3 axis)
        {
            var direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                axis = Vector3.zero;
                return false;
            }

            axis = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? Vector3.right
                : Vector3.up;
            return true;
        }

        public void HoldEnemyHealthVisual(string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                enemyViews.TryGetValue(runtimeId, out var view) &&
                view != null)
            {
                view.HoldHealthVisual();
            }
        }

        public void PlayEnemyHitShake(string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                enemyViews.TryGetValue(runtimeId, out var view) &&
                view != null)
            {
                view.PlayHitShake();
            }
        }

        public void ShowEnemyDeathVisual(string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                enemyViews.TryGetValue(runtimeId, out var view) &&
                view != null)
            {
                // Keep the entry until RefreshEnemyViews removes it. This preserves the
                // last position for any other combat events emitted in the same frame.
                view.ShowDeathFlash();
            }
        }

        public void ReleaseEnemyHealthVisual(string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                enemyViews.TryGetValue(runtimeId, out var view) &&
                view != null)
            {
                view.PlayHitShake();
                view.ReleaseHealthVisual();
                if (!view.IsHealthVisualHeld &&
                    registry != null &&
                    !registry.TryGet(runtimeId, out _))
                {
                    // A killing presentation may complete after MatchState stops the lane Update.
                    // Resolve the retained view here instead of waiting for RefreshEnemyViews.
                    view.ShowDeathFlash();
                    enemyViews.Remove(runtimeId);
                    lastKnownEnemyPositions.Remove(runtimeId);
                    lastKnownEnemyVisualImpactPositions.Remove(runtimeId);
                    enemyPullVisuals.Remove(runtimeId);
                }
            }
        }

        public void Configure(
            RectTransform marker,
            RectTransform[] routeWaypoints,
            float seconds,
            bool reverse)
        {
            if (seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            enemyMarker = marker;
            enemyViewTemplate = marker != null ? marker.GetComponent<EnemyView>() : null;
            waypoints = routeWaypoints ?? throw new ArgumentNullException(nameof(routeWaypoints));
            if (waypoints.Length < 2)
            {
                throw new ArgumentException("An authored lane requires an open route.", nameof(routeWaypoints));
            }

            reverseDirection = reverse;
            if (reverseDirection)
            {
                Array.Reverse(waypoints);
            }

            if (!string.Equals(waypoints[waypoints.Length - 1].name, "DragonGoal", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The last authored waypoint must be DragonGoal.");
            }

            travelSeconds = seconds;
            progress = 0f;
            ApplyPosition();
        }

        public void ConfigureEnemyPresentation(EnemyView prefab, RectTransform container = null)
        {
            enemyViewPrefab = prefab;
            enemyViewContainer = container;
        }

        public void Initialize(MatchController value)
        {
            match = value ?? throw new ArgumentNullException(nameof(value));
            progress = 0f;
            ApplyPosition();
            if (enemyViewTemplate != null)
            {
                enemyViewTemplate.gameObject.SetActive(false);
            }
        }

        public void BindEnemyRegistry(EnemyRegistry value, TeamSide teamSide)
        {
            registry = value ?? throw new ArgumentNullException(nameof(value));
            side = teamSide;
            lastKnownEnemyPositions.Clear();
            lastKnownEnemyVisualImpactPositions.Clear();
            if (enemyViewTemplate != null)
            {
                enemyViewTemplate.gameObject.SetActive(false);
            }
        }

        public void ConfigureLayout(BattlefieldLayoutDefinition layout, TeamSide layoutSide)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (layout is FixedBoardLayoutDefinition fixedLayout && fixedBoardCanvas != null)
            {
                ConfigureFixedLayout(fixedLayout, layoutSide);
                return;
            }

            var lane = layout.GetLane(layoutSide);
            if (waypoints == null || waypoints.Length < 2)
            {
                throw new InvalidOperationException("The authored lane needs spawn and goal waypoint templates.");
            }

            var sourceWaypoints = waypoints;
            var route = new RectTransform[lane.NodeNames.Count];
            for (var index = 0; index < route.Length; index++)
            {
                RectTransform waypoint;
                if (index == 0)
                {
                    waypoint = sourceWaypoints[0];
                }
                else if (index == route.Length - 1)
                {
                    waypoint = sourceWaypoints[sourceWaypoints.Length - 1];
                }
                else
                {
                    waypoint = Instantiate(sourceWaypoints[0], sourceWaypoints[0].parent);
                }

                waypoint.gameObject.SetActive(true);
                waypoint.name = lane.NodeNames[index];
                SetWaypointAnchor(waypoint, layout, lane, index);
                route[index] = waypoint;
            }

            ApplyLaneArt(lane.LaneSide);

            for (var index = 1; index < sourceWaypoints.Length - 1; index++)
            {
                if (sourceWaypoints[index] != null)
                {
                    sourceWaypoints[index].gameObject.SetActive(false);
                }
            }

            Configure(enemyMarker, route, travelSeconds, false);
        }

        private void ConfigureFixedLayout(FixedBoardLayoutDefinition layout, TeamSide layoutSide)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                throw new InvalidOperationException("The authored lane needs spawn and goal waypoint templates.");
            }

            var authoredRoadTemplate = RoadArtTemplate;
            if (authoredRoadTemplate == null)
            {
                throw new InvalidOperationException("The authored lane needs an ART_Path template.");
            }

            if (fixedBoardCanvas.IsAuthoredLayout)
            {
                fixedBoardCanvas.BindLaneArt(layout, layoutSide, authoredRoadTemplate);
                var authoredLane = layout.GetLane(layoutSide);
                if (waypoints == null || waypoints.Length != authoredLane.NodeNames.Count)
                {
                    throw new InvalidOperationException(
                        $"Authored lane waypoint count does not match {layoutSide}: " +
                        $"{waypoints?.Length ?? 0}/{authoredLane.NodeNames.Count}");
                }

                var authoredRoute = new RectTransform[waypoints.Length];
                for (var index = 0; index < authoredRoute.Length; index++)
                {
                    authoredRoute[index] = waypoints[index];
                    if (authoredRoute[index] == null ||
                        authoredRoute[index].parent != fixedBoardCanvas.LaneLayer ||
                        !string.Equals(authoredRoute[index].name, authoredLane.NodeNames[index], StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Authored lane waypoint is missing: {authoredLane.NodeNames[index]}");
                    }
                }

                Configure(enemyMarker, authoredRoute, travelSeconds, false);
                return;
            }

            HideLegacyRoadArt();
            fixedBoardCanvas.BindLaneArt(layout, layoutSide, authoredRoadTemplate);
            var positions = layoutSide == TeamSide.Player
                ? layout.PlayerLaneWaypoints
                : layout.AiLaneWaypoints;
            var lane = layout.GetLane(layoutSide);
            var sourceWaypoints = waypoints;
            var route = new RectTransform[positions.Count];
            for (var index = 0; index < route.Length; index++)
            {
                var waypoint = index < sourceWaypoints.Length
                    ? sourceWaypoints[index]
                    : Instantiate(sourceWaypoints[0], fixedBoardCanvas.LaneLayer);
                waypoint.SetParent(fixedBoardCanvas.LaneLayer, false);
                waypoint.gameObject.SetActive(true);
                waypoint.name = lane.NodeNames[index];
                fixedBoardCanvas.PositionAtCell(waypoint, positions[index]);
                route[index] = waypoint;
            }

            for (var index = route.Length; index < sourceWaypoints.Length; index++)
            {
                sourceWaypoints[index].gameObject.SetActive(false);
            }

            if (enemyMarker != null)
            {
                enemyMarker.SetParent(fixedBoardCanvas.LaneLayer, false);
                fixedBoardCanvas.PositionAtCell(enemyMarker, positions[0], true);
            }

            Configure(enemyMarker, route, travelSeconds, false);
        }

        private void HideLegacyRoadArt()
        {
            foreach (var candidate in GetComponentsInChildren<RectTransform>(true))
            {
                if (candidate != null && candidate.name.StartsWith("ART_Path", StringComparison.Ordinal))
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        public bool ValidateEnemyViewConsistency()
        {
            var valid = true;
            if (registry == null)
            {
                return true;
            }

            foreach (var entry in enemyViews)
            {
                if (!registry.TryGet(entry.Key, out _) &&
                    (entry.Value == null || !entry.Value.IsHealthVisualHeld))
                {
                    Debug.LogError(
                        $"EnemyView exists but Runtime is missing. RuntimeId={entry.Key} Team={side}",
                        entry.Value);
                    valid = false;
                }
            }

            foreach (var enemy in registry.Enemies)
            {
                if (!enemyViews.ContainsKey(enemy.RuntimeId))
                {
                    Debug.LogError(
                        $"Enemy Runtime exists but View is missing. RuntimeId={enemy.RuntimeId} Team={side}");
                    valid = false;
                }
            }

            return valid;
        }

        private void Update()
        {
            if (match == null ||
                match.State != MatchState.Running)
            {
                return;
            }

            progress = Mathf.Clamp01(progress + (Time.deltaTime / travelSeconds));
            ApplyPosition();
            RefreshEnemyViews();
            PublishFrontmostPathProgress();
        }

        private void ApplyPosition()
        {
            if (enemyMarker == null || waypoints == null || waypoints.Length < 2)
            {
                return;
            }

            var scaled = Mathf.Clamp01(progress) * (waypoints.Length - 1);
            var segment = Mathf.Min(Mathf.FloorToInt(scaled), waypoints.Length - 2);
            var segmentProgress = scaled - segment;
            enemyMarker.position = Vector3.Lerp(
                waypoints[segment].position,
                waypoints[segment + 1].position,
                segmentProgress);
        }

        private void RefreshEnemyViews()
        {
            var presentationSource = enemyViewPrefab != null ? enemyViewPrefab : enemyViewTemplate;
            if (registry == null || presentationSource == null || waypoints == null || waypoints.Length < 2)
            {
                return;
            }

            var presentationParent = enemyViewContainer != null
                ? enemyViewContainer
                : enemyViewTemplate != null
                    ? enemyViewTemplate.transform.parent
                    : transform;

            foreach (var enemy in registry.Enemies)
            {
                if (!enemyViews.TryGetValue(enemy.RuntimeId, out var view) || view == null)
                {
                    view = Instantiate(presentationSource, presentationParent, false);
                    view.gameObject.SetActive(true);
                    view.name = $"Enemy_{enemy.RuntimeId}";
                    enemyViews[enemy.RuntimeId] = view;
                }

                view.Bind(enemy);
                view.SetFrostcrownMarked(
                    frostcrownMarkedEnemyIds.Contains(enemy.RuntimeId));
                view.SetWaveAnimationMirrored(side == TeamSide.AI);
                var position = GetEnemyPosition(enemy);
                view.RectTransform.position = position;
                lastKnownEnemyPositions[enemy.RuntimeId] = position;
            }

            TickEnemyPullVisuals();

            var resolved = new List<string>();
            foreach (var entry in enemyViews)
            {
                if (!registry.TryGet(entry.Key, out _))
                {
                    if (entry.Value != null && entry.Value.IsHealthVisualHeld)
                    {
                        continue;
                    }

                    entry.Value.ShowDeathFlash();
                    resolved.Add(entry.Key);
                }
            }

            foreach (var runtimeId in resolved)
            {
                enemyViews.Remove(runtimeId);
                enemyPullVisuals.Remove(runtimeId);
                lastKnownEnemyPositions.Remove(runtimeId);
                lastKnownEnemyVisualImpactPositions.Remove(runtimeId);
            }

            ValidateEnemyViewConsistency();
        }

        private Vector3 GetEnemyPosition(EnemyRuntime enemy)
        {
            var scaled = Mathf.Clamp(
                enemy.PathProgress * (waypoints.Length - 1f),
                0f,
                waypoints.Length - 1f);
            var segment = Mathf.Min(Mathf.FloorToInt(scaled), waypoints.Length - 2);
            var segmentProgress = scaled - segment;
            return Vector3.Lerp(
                waypoints[segment].position,
                waypoints[segment + 1].position,
                segmentProgress);
        }

        private void TickEnemyPullVisuals()
        {
            if (enemyPullVisuals.Count == 0)
            {
                return;
            }

            List<string> completed = null;
            foreach (var entry in enemyPullVisuals)
            {
                var visual = entry.Value;
                var position = visual.StartPosition;
                if (!visual.IsHeld)
                {
                    visual.Elapsed += Time.deltaTime;
                    var normalized = Mathf.Clamp01(visual.Elapsed / visual.Duration);
                    position = Vector3.Lerp(
                        visual.StartPosition,
                        visual.EndPosition,
                        Mathf.SmoothStep(0f, 1f, normalized));
                    if (normalized >= 1f)
                    {
                        completed ??= new List<string>();
                        completed.Add(entry.Key);
                    }
                }

                if (enemyViews.TryGetValue(entry.Key, out var view) && view != null)
                {
                    view.RectTransform.position = position;
                    lastKnownEnemyPositions[entry.Key] = position;
                }
            }

            if (completed == null)
            {
                return;
            }

            foreach (var runtimeId in completed)
            {
                if (!enemyPullVisuals.TryGetValue(runtimeId, out var visual))
                {
                    continue;
                }

                enemyPullVisuals.Remove(runtimeId);
                visual.OnComplete?.Invoke();
            }
        }

        private sealed class EnemyPullVisual
        {
            public EnemyPullVisual(
                Vector3 startPosition,
                Vector3 endPosition,
                float duration,
                bool isHeld,
                Action onComplete)
            {
                StartPosition = startPosition;
                EndPosition = endPosition;
                Duration = duration;
                IsHeld = isHeld;
                OnComplete = onComplete;
            }

            public Vector3 StartPosition { get; }
            public Vector3 EndPosition { get; }
            public float Duration { get; }
            public bool IsHeld { get; }
            public Action OnComplete { get; }
            public float Elapsed { get; set; }
        }

        private void PublishFrontmostPathProgress()
        {
            if (fixedBoardCanvas?.DebugOverlay == null)
            {
                return;
            }

            var frontmost = 0f;
            if (registry != null)
            {
                foreach (var enemy in registry.Enemies)
                {
                    if (enemy != null && !enemy.HasResolved)
                    {
                        frontmost = Mathf.Max(frontmost, enemy.PathProgress);
                    }
                }
            }

            fixedBoardCanvas.DebugOverlay.SetPathProgress(side, frontmost);
        }

        private static void SetWaypointAnchor(
            RectTransform waypoint,
            BattlefieldLayoutDefinition layout,
            BattlefieldLaneDefinition lane,
            int index)
        {
            var point = lane.CombatPoints[index];
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var pointIndex = 0; pointIndex < lane.CombatPoints.Count; pointIndex++)
            {
                minY = Mathf.Min(minY, lane.CombatPoints[pointIndex].Y);
                maxY = Mathf.Max(maxY, lane.CombatPoints[pointIndex].Y);
            }

            var horizontalProgress = Mathf.InverseLerp(-1f, layout.Width, point.X);
            var verticalProgress = Mathf.InverseLerp(minY, maxY, point.Y);
            var x = Mathf.Lerp(0.09f, 0.91f, horizontalProgress);
            var y = lane.Side == TeamSide.Player
                ? Mathf.Lerp(0.08f, 0.92f, verticalProgress)
                : Mathf.Lerp(0.92f, 0.08f, verticalProgress);
            var anchor = new Vector2(x, y);
            waypoint.anchorMin = anchor;
            waypoint.anchorMax = anchor;
            waypoint.pivot = new Vector2(0.5f, 0.5f);
            waypoint.anchoredPosition = Vector2.zero;
            waypoint.sizeDelta = Vector2.zero;
        }

        private void ApplyLaneArt(BattlefieldLaneSide laneSide)
        {
            var usesLeftSide = laneSide == BattlefieldLaneSide.Left;
            foreach (var node in GetComponentsInChildren<Transform>(true))
            {
                if (node == transform)
                {
                    continue;
                }

                var name = node.name;
                if (name.StartsWith("ART_PathLeft", StringComparison.Ordinal))
                {
                    node.gameObject.SetActive(usesLeftSide);
                }
                else if (name.StartsWith("ART_PathRight", StringComparison.Ordinal))
                {
                    node.gameObject.SetActive(!usesLeftSide);
                }
                else if (name.StartsWith("ART_PathTop", StringComparison.Ordinal) ||
                         name.StartsWith("ART_PathBottom", StringComparison.Ordinal))
                {
                    node.gameObject.SetActive(false);
                }
            }
        }
    }
}
