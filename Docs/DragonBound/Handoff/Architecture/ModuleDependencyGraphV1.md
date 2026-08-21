# Module Dependency Graph V1

## Current graph

The graph is inferred from actual `using DragonBound.*` references and constructor calls in the current `DragonBound.Runtime` assembly.

```mermaid
flowchart LR
  GS[GameShared.Runtime]
  R[DragonBound.Runtime monolith]
  H[DragonBound.HandoffUi]
  E[DragonBound.Editor]
  TE[Tests.EditMode]
  TP[Tests.PlayMode]
  GS --> R
  R --> H
  E --> R
  E --> H
  TE --> R
  TE --> H
  TP --> R
  TP --> H
  subgraph RInternal[Inside DragonBound.Runtime: namespace edges, not assemblies]
    Core[Core]
    Grid[Grid]
    Combat[Combat]
    Recruit[Recruitment]
    AI[AI]
    Items[Items]
    Runes[Runes]
    Pres[Presentation]
    Boot[Bootstrap]
    Analytics[Analytics]
    Core --> Grid
    Core --> Combat
    Core --> Recruit
    Core --> AI
    Core --> Items
    Core --> Runes
    Grid --> Core
    Grid --> Combat
    Combat --> Core
    Combat --> Grid
    Combat --> Recruit
    Combat --> Runes
    Recruit --> Core
    Recruit --> Grid
    Recruit --> Combat
    Recruit --> Runes
    Runes --> Core
    Runes --> Recruit
    Runes --> Combat
    AI --> Core
    AI --> Grid
    AI --> Combat
    AI --> Recruit
    Pres --> Core
    Pres --> Grid
    Pres --> Combat
    Pres --> Recruit
    Pres --> Items
    Pres --> Runes
    Boot --> Core
    Boot --> Grid
    Boot --> Combat
    Boot --> Recruit
    Boot --> AI
    Boot --> Items
    Boot --> Runes
    Boot --> Pres
  end
```

## Target DAG

```mermaid
flowchart TD
  GS[GameShared.Runtime]
  FC[Foundation.Contracts]
  BC[Board.Contracts]
  EC[Enemies.Contracts]
  CC[Combat.Contracts]
  HC[Heroes.Contracts]
  RC[Recruitment.Runtime]
  ER[Enemies.Runtime]
  CR[Combat.Runtime]
  IR[Items.Runtime]
  RR[Runes.Runtime]
  MC[Match.Runtime]
  AIR[AI.Runtime]
  TC[Telemetry.Contracts]
  AR[Analytics.Runtime]
  PR[Presentation.Runtime]
  HU[HandoffUi.Runtime]
  INT[Integration.Runtime]
  QA[QA assemblies]
  GS --> FC
  FC --> BC
  FC --> EC
  FC --> CC
  FC --> HC
  BC --> RC
  HC --> RC
  EC --> ER
  CC --> CR
  BC --> CR
  HC --> CR
  FC --> IR
  FC --> RR
  HC --> RR
  FC --> MC
  BC --> MC
  EC --> MC
  CC --> MC
  RC --> MC
  IR --> MC
  RR --> MC
  MC --> AIR
  BC --> AIR
  RC --> AIR
  CR --> AIR
  FC --> TC
  TC --> AR
  MC --> PR
  BC --> PR
  CC --> PR
  HC --> PR
  IR --> PR
  RR --> PR
  HU --> INT
  MC --> INT
  AIR --> INT
  PR --> INT
  IR --> INT
  RR --> INT
  AR --> INT
  FC --> QA
  BC --> QA
  EC --> QA
  CC --> QA
  HC --> QA
  RC --> QA
  MC --> QA
  INT --> QA
```

## DAG constraints

- Contracts contain no concrete reference to a higher layer and no Scene/Prefab dependency.
- Match owns orchestration, not AI policy, UI, or persistence authority.
- Combat consumes modifier ports; it does not reference Rune or Item implementations.
- Items and Runes consume Match/Combat contracts through ports and never reference Presentation.
- Presentation consumes immutable snapshots and emits commands; Integration wires commands to services.
- Analytics consumes telemetry contracts; gameplay does not depend on analytics sinks.
- Integration is the only assembly allowed to reference every runtime module and serialized product asset.
