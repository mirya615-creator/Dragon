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
        [SerializeField] private float travelSeconds = 12f;
        [SerializeField] private bool reverseDirection;

        private readonly Dictionary<string, EnemyView> enemyViews =
            new Dictionary<string, EnemyView>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> lastKnownEnemyPositions =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
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
                if (!registry.TryGet(entry.Key, out _))
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
            if (registry == null || enemyViewTemplate == null || waypoints == null || waypoints.Length < 2)
            {
                return;
            }

            foreach (var enemy in registry.Enemies)
            {
                if (!enemyViews.TryGetValue(enemy.RuntimeId, out var view) || view == null)
                {
                    view = Instantiate(enemyViewTemplate, enemyViewTemplate.transform.parent);
                    view.gameObject.SetActive(true);
                    view.name = $"Enemy_{enemy.RuntimeId}";
                    enemyViews[enemy.RuntimeId] = view;
                }

                view.Bind(enemy);
                var position = GetEnemyPosition(enemy);
                view.RectTransform.position = position;
                lastKnownEnemyPositions[enemy.RuntimeId] = position;
            }

            var resolved = new List<string>();
            foreach (var entry in enemyViews)
            {
                if (!registry.TryGet(entry.Key, out _))
                {
                    entry.Value.ShowDeathFlash();
                    Destroy(entry.Value.gameObject, 0.16f);
                    resolved.Add(entry.Key);
                }
            }

            foreach (var runtimeId in resolved)
            {
                enemyViews.Remove(runtimeId);
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
