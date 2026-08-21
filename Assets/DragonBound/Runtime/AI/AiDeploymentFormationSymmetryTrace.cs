using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;

namespace DragonBound.AI
{
    /// <summary>
    /// Deterministic same-input replay for Player/AI deployment decisions. This is diagnostics
    /// only and never changes the production controller policy.
    /// </summary>
    public sealed class AiDeploymentFormationSymmetryTrace
    {
        public const int DefaultCycles = 8;

        public AiDeploymentFormationSymmetryTraceResult Run(int runSeed = 1701, int cycles = DefaultCycles)
        {
            if (cycles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cycles));
            }

            var layout = BattlefieldLayoutDefinitions.Fixed8x10HorizontalStart;
            var catalog = GreyboxRecruitmentCatalog.Create();
            var player = CreateSide(layout, catalog, TeamSide.Player, runSeed);
            var ai = CreateSide(layout, catalog, TeamSide.AI, runSeed);
            var result = new AiDeploymentFormationSymmetryTraceResult(runSeed, cycles);
            for (var cycle = 1; cycle <= cycles; cycle++)
            {
                player.Controller.Tick(cycle);
                ai.Controller.Tick(cycle);
                var playerSnapshot = Capture(player, layout, cycle);
                var aiSnapshot = Capture(ai, layout, cycle);
                var difference = Compare(playerSnapshot, aiSnapshot);
                result.Add(new AiDeploymentDecisionTrace(
                    cycle,
                    playerSnapshot,
                    aiSnapshot,
                    difference,
                    player.Recruitment,
                    ai.Recruitment,
                    player.Controller,
                    ai.Controller));
                if (result.FirstDivergenceCycle < 0 && !string.IsNullOrEmpty(difference))
                {
                    result.FirstDivergenceCycle = cycle;
                    result.FirstDivergenceReason = difference;
                }
            }

            return result;
        }

        private static SideContext CreateSide(
            FixedBoardLayoutDefinition layout,
            RecruitmentCatalog catalog,
            TeamSide side,
            int runSeed)
        {
            var board = DragonBoundBoardLayout.Create(layout, side);
            var destination = new BoardRecruitDestination(board);
            var team = new TeamState(side);
            team.AddResources(1000);
            var bag = LimitedComponentBag.CreateBag(
                runSeed,
                LimitedComponentBag.DefaultContentVersion,
                catalog);
            var deck = new RecruitDeck(
                catalog,
                runSeed,
                "symmetry.replay",
                bag,
                shovelState: new ShovelRecruitmentState(() => board.GetPositions(CellType.Locked).Count),
                componentPolicy: RecruitComponentPolicy.V3,
                currentWaveProvider: () => 1);
            var recruitment = new RecruitmentService(team, deck, destination);
            var controller = new BasicUnitAiController(board, destination, recruitment, null, team);
            if (controller.Diagnostics != null)
            {
                controller.Diagnostics.EmitLogs = false;
            }

            return new SideContext(board, destination, team, recruitment, controller);
        }

        private static AiDeploymentSnapshot Capture(SideContext context, FixedBoardLayoutDefinition layout, int cycle)
        {
            var entries = new List<string>();
            foreach (var occupant in context.Board.GetOccupants())
            {
                var position = ToPlayerLocalPosition(layout, context.Board.Side, occupant.Position);
                context.Destination.TryGetCard(occupant.UnitId, out var card);
                var pair = context.Destination.TryGetPairLinkForComponent(occupant.UnitId, out var pairLink)
                    ? pairLink.HeroId
                    : string.Empty;
                entries.Add(
                    position.X + ":" + position.Y + "|" +
                    (card == null ? "?" : card.Kind + ":" + card.ConfigId + ":" + card.Level) +
                    "|Pair=" + pair);
            }

            entries.Sort(StringComparer.Ordinal);
            var boss = new EnemyRuntime(
                "trace.boss." + context.Board.Side,
                context.Board.Side,
                500f,
                EnemyArchetype.Boss,
                0);
            var lane = layout.GetLane(context.Board.Side);
            var path = new EnemyPath(lane.NodeNames, lane.CombatPoints);
            path.PlaceAtSpawn(boss);
            var targeting = new TargetingSystem();
            var hittableBasic = 0;
            var hittableHero = 0;
            var predictedDps = 0f;
            foreach (var unit in context.Destination.GetDeployedUnits())
            {
                var stats = BasicUnitCatalog.GetStats(unit.Card.ConfigId, unit.Card.Level);
                if (!targeting.IsWithinRange(unit.CombatPosition, boss, stats.RangeCells)) continue;
                hittableBasic++;
                predictedDps += stats.Attack * stats.AttackSpeed;
            }

            foreach (var hero in context.Destination.GetActiveHeroPairs())
            {
                var combat = hero.PairLink.CombatProxy;
                if (!targeting.IsWithinRange(hero.CombatPosition, boss, combat.RangeCells)) continue;
                hittableHero++;
                predictedDps += combat.Attack * combat.AttackSpeed;
            }

            return new AiDeploymentSnapshot(
                cycle,
                string.Join(";", entries.ToArray()),
                context.Team.Resources,
                context.Recruitment.CompletedRecruitments,
                context.Destination.DeployedCount,
                context.Destination.CampCount,
                CountKind(context.Destination, RecruitItemKind.BasicUnit),
                CountKind(context.Destination, RecruitItemKind.HeroComponent),
                CountUnpairedComponents(context.Destination),
                context.Destination.ActivePairLinkCount,
                hittableBasic,
                hittableHero,
                predictedDps,
                context.Recruitment.HasLastAttempt ? context.Recruitment.LastAttempt.ResultSummary : "NONE");
        }

        private static string Compare(AiDeploymentSnapshot player, AiDeploymentSnapshot ai)
        {
            var differences = new List<string>();
            CompareValue(differences, "Resources", player.Resources, ai.Resources);
            CompareValue(differences, "Recruitments", player.Recruitments, ai.Recruitments);
            CompareValue(differences, "Board", player.BoardState, ai.BoardState);
            CompareValue(differences, "HittableBasic", player.HittableBasic, ai.HittableBasic);
            CompareValue(differences, "HittableHero", player.HittableHero, ai.HittableHero);
            CompareValue(differences, "PredictedDps", player.PredictedDps, ai.PredictedDps, 0.0001f);
            return string.Join(";", differences.ToArray());
        }

        private static void CompareValue(List<string> differences, string label, object player, object ai)
        {
            if (!Equals(player, ai)) differences.Add(label + " Player=" + player + " AI=" + ai);
        }

        private static void CompareValue(List<string> differences, string label, float player, float ai, float epsilon)
        {
            if (Math.Abs(player - ai) > epsilon)
            {
                differences.Add(label + " Player=" + player.ToString("0.000", CultureInfo.InvariantCulture) +
                                " AI=" + ai.ToString("0.000", CultureInfo.InvariantCulture));
            }
        }

        private static GridPosition ToPlayerLocalPosition(
            FixedBoardLayoutDefinition layout,
            TeamSide side,
            GridPosition position)
        {
            if (side == TeamSide.Player)
            {
                return position;
            }

            if (layout.IsOwnedDeploymentCell(position, TeamSide.AI))
            {
                return layout.GetFairCounterpart(position, TeamSide.AI);
            }

            return position;
        }

        private static int CountKind(BoardRecruitDestination destination, RecruitItemKind kind)
        {
            return destination.GetBoardCards().Count(card => card.Kind == kind);
        }

        private static int CountUnpairedComponents(BoardRecruitDestination destination)
        {
            return destination.GetBoardCards().Count(card =>
                card.Kind == RecruitItemKind.HeroComponent &&
                !destination.TryGetPairLinkForComponent(card.RuntimeId, out _));
        }

        private sealed class SideContext
        {
            public SideContext(
                BoardGrid board,
                BoardRecruitDestination destination,
                TeamState team,
                RecruitmentService recruitment,
                BasicUnitAiController controller)
            {
                Board = board;
                Destination = destination;
                Team = team;
                Recruitment = recruitment;
                Controller = controller;
            }

            public BoardGrid Board { get; }
            public BoardRecruitDestination Destination { get; }
            public TeamState Team { get; }
            public RecruitmentService Recruitment { get; }
            public BasicUnitAiController Controller { get; }
        }
    }

    public sealed class AiDeploymentFormationSymmetryTraceResult
    {
        private readonly List<AiDeploymentDecisionTrace> traces = new List<AiDeploymentDecisionTrace>();

        internal AiDeploymentFormationSymmetryTraceResult(int runSeed, int cycleCount)
        {
            RunSeed = runSeed;
            CycleCount = cycleCount;
            FirstDivergenceCycle = -1;
        }

        public int RunSeed { get; }
        public int CycleCount { get; }
        public int FirstDivergenceCycle { get; internal set; }
        public string FirstDivergenceReason { get; internal set; } = string.Empty;
        public IReadOnlyList<AiDeploymentDecisionTrace> Cycles => traces;

        internal void Add(AiDeploymentDecisionTrace trace)
        {
            traces.Add(trace);
        }

        public string ToCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("cycle,symmetric,difference,playerResources,aiResources,playerRecruitments,aiRecruitments,playerBoard,aiBoard,playerHittableBasic,aiHittableBasic,playerHittableHero,aiHittableHero,playerPredictedDps,aiPredictedDps");
            foreach (var trace in traces)
            {
                builder.Append(trace.Cycle).Append(',')
                    .Append(trace.IsSymmetric ? 1 : 0).Append(',')
                    .Append(Escape(trace.Difference)).Append(',')
                    .Append(trace.Player.Resources).Append(',').Append(trace.AI.Resources).Append(',')
                    .Append(trace.Player.Recruitments).Append(',').Append(trace.AI.Recruitments).Append(',')
                    .Append(Escape(trace.Player.BoardState)).Append(',').Append(Escape(trace.AI.BoardState)).Append(',')
                    .Append(trace.Player.HittableBasic).Append(',').Append(trace.AI.HittableBasic).Append(',')
                    .Append(trace.Player.HittableHero).Append(',').Append(trace.AI.HittableHero).Append(',')
                    .Append(trace.Player.PredictedDps.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(trace.AI.PredictedDps.ToString("0.000", CultureInfo.InvariantCulture)).AppendLine();
            }

            return builder.ToString();
        }

        public string FormatReport()
        {
            return "SameInputReplay RunSeed=" + RunSeed +
                   " Cycles=" + CycleCount +
                   " FirstDivergenceCycle=" + FirstDivergenceCycle +
                   " Reason=" + FirstDivergenceReason;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public sealed class AiDeploymentDecisionTrace
    {
        internal AiDeploymentDecisionTrace(
            int cycle,
            AiDeploymentSnapshot player,
            AiDeploymentSnapshot ai,
            string difference,
            RecruitmentService playerRecruitment,
            RecruitmentService aiRecruitment,
            BasicUnitAiController playerController,
            BasicUnitAiController aiController)
        {
            Cycle = cycle;
            Player = player;
            AI = ai;
            Difference = difference;
            PlayerRecipeAttempts = playerController.RecipeFormationAttempted;
            AiRecipeAttempts = aiController.RecipeFormationAttempted;
            PlayerRecipeSuccesses = playerController.RecipeFormationSucceeded;
            AiRecipeSuccesses = aiController.RecipeFormationSucceeded;
            PlayerLastBatch = playerRecruitment.HasLastAttempt ? playerRecruitment.LastAttempt.ResultSummary : "NONE";
            AiLastBatch = aiRecruitment.HasLastAttempt ? aiRecruitment.LastAttempt.ResultSummary : "NONE";
        }

        public int Cycle { get; }
        public AiDeploymentSnapshot Player { get; }
        public AiDeploymentSnapshot AI { get; }
        public string Difference { get; }
        public bool IsSymmetric => string.IsNullOrEmpty(Difference);
        public int PlayerRecipeAttempts { get; }
        public int AiRecipeAttempts { get; }
        public int PlayerRecipeSuccesses { get; }
        public int AiRecipeSuccesses { get; }
        public string PlayerLastBatch { get; }
        public string AiLastBatch { get; }
    }

    public sealed class AiDeploymentSnapshot
    {
        internal AiDeploymentSnapshot(
            int cycle,
            string boardState,
            int resources,
            int recruitments,
            int deployed,
            int camp,
            int basicCount,
            int componentCount,
            int unpairedComponents,
            int pairLinks,
            int hittableBasic,
            int hittableHero,
            float predictedDps,
            string lastBatch)
        {
            Cycle = cycle;
            BoardState = boardState;
            Resources = resources;
            Recruitments = recruitments;
            Deployed = deployed;
            Camp = camp;
            BasicCount = basicCount;
            ComponentCount = componentCount;
            UnpairedComponents = unpairedComponents;
            PairLinks = pairLinks;
            HittableBasic = hittableBasic;
            HittableHero = hittableHero;
            PredictedDps = predictedDps;
            LastBatch = lastBatch;
        }

        public int Cycle { get; }
        public string BoardState { get; }
        public int Resources { get; }
        public int Recruitments { get; }
        public int Deployed { get; }
        public int Camp { get; }
        public int BasicCount { get; }
        public int ComponentCount { get; }
        public int UnpairedComponents { get; }
        public int PairLinks { get; }
        public int HittableBasic { get; }
        public int HittableHero { get; }
        public float PredictedDps { get; }
        public string LastBatch { get; }
    }
}
