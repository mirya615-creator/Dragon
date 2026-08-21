# W1-W5 Survival Funnel and Player/AI Side Bias Audit V1

- Seed set: `1..1000`, exactly one original run and one Deck/Bag salt-swap run per seed.
- Both sides are driven by `BasicUnitAiController`; Player is not a human-operation sample.
- Item/Rune are disabled. W6 Boss HP is not used to judge W1-W5 survival.
- Production shared settlement remains unchanged: either side reaching zero Heart ends the match.
- Reach rates are therefore shared-settlement reach rates. They are not independent counterfactual side survival rates.

## Original salts

W1-W5 Survival Funnel and Side Bias Audit
SeedSet=1..1000 SwapDeckInputs=False SharedSettlement=Production Match ends when either side is defeated
W6BossHP=NotUsed; Items=Disabled; Runes=Disabled; PlayerAndAIControllers=BasicUnitAiController
[Player] IndependentReach W2=1000/1000(100.00 %) W3=877/1000(87.70 %) W4=839/1000(83.90 %) W5=826/1000(82.60 %) W6=762/1000(76.20 %)
[Player] SharedSettlementDeathCount=140/1000 (14.00 %) FirstDefeated=140 SameFrameDoubleDefeat=0 DeathWaves=W2=72,W3=27,W4=7,W5=34 FirstLeakWaves=W2=90,W3=23,W4=12,W5=33 FailureReasons=Heart=140
[Player] W1 n=1000 Heart=3.00 Resources=6.72 Recruit=1.97 Basic=4.93 Hero=0.17 Board=5.75 Bench=1.00 Residual=1.68
[Player] W2 n=1000 Heart=2.76 Resources=4.61 Recruit=2.85 Basic=6.79 Hero=0.31 Board=6.57 Bench=2.81 Residual=0.86
[Player] W3 n=877 Heart=2.88 Resources=3.15 Recruit=3.79 Basic=7.47 Hero=0.51 Board=7.16 Bench=3.79 Residual=0.68
[Player] W4 n=839 Heart=2.95 Resources=12.82 Recruit=3.99 Basic=7.20 Hero=0.59 Board=7.31 Bench=3.62 Residual=1.06
[Player] W5 n=826 Heart=2.85 Resources=9.45 Recruit=4.93 Basic=6.67 Hero=0.93 Board=7.69 Bench=4.12 Residual=1.44
[Player] FirstLeakRate=15.80 % RecruitStalls=0 Reasons=None Merges=3550
[Player] W6BossSpawn=762/1000 HittableBasicAvg=0.68 HittableHeroAvg=0.42 SingleTargetDPSAll=15.56 SingleTargetDPSQualified=20.42 HeroIds=HERO_STARFALL_ARCHMAGE=78,HERO_NIGHTFANG_ASSASSIN=47,HERO_SKYHUNTER_VALKYRIE=37,HERO_WINDCLAW_RANGER=69,HERO_THUNDER_JARL=71,HERO_LEVIATHAN_HUNTER=42,HERO_RUNEBOLT_MAGE=73,HERO_EMBER_SHAMAN=79,HERO_CROWN_SWORD_LEADER=80,HERO_CROWN_HUNTER_LEADER=74,HERO_STONEBINDER=76,HERO_DRAGON_RIDER=77 HeroLevels=HERO_STARFALL_ARCHMAGE=1.46,HERO_NIGHTFANG_ASSASSIN=1.43,HERO_SKYHUNTER_VALKYRIE=1.68,HERO_WINDCLAW_RANGER=1.55,HERO_THUNDER_JARL=1.52,HERO_LEVIATHAN_HUNTER=1.36,HERO_RUNEBOLT_MAGE=1.38,HERO_EMBER_SHAMAN=1.37,HERO_CROWN_SWORD_LEADER=1.39,HERO_CROWN_HUNTER_LEADER=1.55,HERO_STONEBINDER=1.22,HERO_DRAGON_RIDER=1.66

[AI] IndependentReach W2=1000/1000(100.00 %) W3=877/1000(87.70 %) W4=839/1000(83.90 %) W5=826/1000(82.60 %) W6=762/1000(76.20 %)
[AI] SharedSettlementDeathCount=98/1000 (9.80 %) FirstDefeated=98 SameFrameDoubleDefeat=0 DeathWaves=W2=51,W3=11,W4=6,W5=30 FirstLeakWaves=W2=59,W3=12,W4=8,W5=30 FailureReasons=Heart=98
[AI] W1 n=1000 Heart=3.00 Resources=6.13 Recruit=1.98 Basic=5.07 Hero=0.16 Board=5.77 Bench=1.06 Residual=2.09
[AI] W2 n=1000 Heart=2.84 Resources=4.11 Recruit=2.86 Basic=6.80 Hero=0.34 Board=6.55 Bench=2.85 Residual=1.28
[AI] W3 n=877 Heart=2.95 Resources=5.98 Recruit=3.58 Basic=7.34 Hero=0.48 Board=7.04 Bench=3.55 Residual=1.30
[AI] W4 n=839 Heart=2.96 Resources=12.26 Recruit=4.00 Basic=7.13 Hero=0.63 Board=7.28 Bench=3.64 Residual=1.61
[AI] W5 n=826 Heart=2.87 Resources=9.21 Recruit=4.93 Basic=6.63 Hero=0.99 Board=7.64 Bench=4.19 Residual=1.64
[AI] FirstLeakRate=10.90 % RecruitStalls=0 Reasons=None Merges=3548
[AI] W6BossSpawn=762/1000 HittableBasicAvg=0.18 HittableHeroAvg=0.08 SingleTargetDPSAll=2.93 SingleTargetDPSQualified=3.85 HeroIds=HERO_NIGHTFANG_ASSASSIN=44,HERO_CROWN_SWORD_LEADER=72,HERO_WINDCLAW_RANGER=64,HERO_DRAGON_RIDER=99,HERO_STARFALL_ARCHMAGE=85,HERO_EMBER_SHAMAN=83,HERO_STONEBINDER=86,HERO_CROWN_HUNTER_LEADER=82,HERO_RUNEBOLT_MAGE=100,HERO_SKYHUNTER_VALKYRIE=34,HERO_THUNDER_JARL=75,HERO_LEVIATHAN_HUNTER=36 HeroLevels=HERO_NIGHTFANG_ASSASSIN=1.41,HERO_CROWN_SWORD_LEADER=1.24,HERO_WINDCLAW_RANGER=1.58,HERO_DRAGON_RIDER=1.35,HERO_STARFALL_ARCHMAGE=1.52,HERO_EMBER_SHAMAN=1.24,HERO_STONEBINDER=1.35,HERO_CROWN_HUNTER_LEADER=1.43,HERO_RUNEBOLT_MAGE=1.32,HERO_SKYHUNTER_VALKYRIE=1.56,HERO_THUNDER_JARL=1.33,HERO_LEVIATHAN_HUNTER=1.56


## Swapped salts

W1-W5 Survival Funnel and Side Bias Audit
SeedSet=1..1000 SwapDeckInputs=True SharedSettlement=Production Match ends when either side is defeated
W6BossHP=NotUsed; Items=Disabled; Runes=Disabled; PlayerAndAIControllers=BasicUnitAiController
[Player] IndependentReach W2=1000/1000(100.00 %) W3=862/1000(86.20 %) W4=825/1000(82.50 %) W5=802/1000(80.20 %) W6=740/1000(74.00 %)
[Player] SharedSettlementDeathCount=124/1000 (12.40 %) FirstDefeated=124 SameFrameDoubleDefeat=0 DeathWaves=W2=55,W3=19,W4=16,W5=34 FirstLeakWaves=W2=75,W3=30,W4=15,W5=29 FailureReasons=Heart=124
[Player] W1 n=1000 Heart=3.00 Resources=6.71 Recruit=1.97 Basic=4.99 Hero=0.15 Board=5.73 Bench=0.96 Residual=1.64
[Player] W2 n=1000 Heart=2.80 Resources=4.79 Recruit=2.84 Basic=6.68 Hero=0.31 Board=6.54 Bench=2.68 Residual=0.85
[Player] W3 n=862 Heart=2.88 Resources=3.36 Recruit=3.78 Basic=7.46 Hero=0.52 Board=7.18 Bench=3.71 Residual=0.71
[Player] W4 n=825 Heart=2.89 Resources=12.80 Recruit=3.98 Basic=7.19 Hero=0.59 Board=7.30 Bench=3.55 Residual=0.99
[Player] W5 n=802 Heart=2.83 Resources=9.74 Recruit=4.93 Basic=6.72 Hero=0.96 Board=7.67 Bench=4.16 Residual=1.14
[Player] FirstLeakRate=14.90 % RecruitStalls=0 Reasons=None Merges=3697
[Player] W6BossSpawn=740/1000 HittableBasicAvg=0.64 HittableHeroAvg=0.43 SingleTargetDPSAll=15.17 SingleTargetDPSQualified=20.50 HeroIds=HERO_NIGHTFANG_ASSASSIN=35,HERO_EMBER_SHAMAN=82,HERO_DRAGON_RIDER=81,HERO_RUNEBOLT_MAGE=93,HERO_STARFALL_ARCHMAGE=80,HERO_THUNDER_JARL=71,HERO_CROWN_SWORD_LEADER=71,HERO_CROWN_HUNTER_LEADER=79,HERO_STONEBINDER=74,HERO_WINDCLAW_RANGER=73,HERO_LEVIATHAN_HUNTER=34,HERO_SKYHUNTER_VALKYRIE=31 HeroLevels=HERO_NIGHTFANG_ASSASSIN=1.37,HERO_EMBER_SHAMAN=1.43,HERO_DRAGON_RIDER=1.65,HERO_RUNEBOLT_MAGE=1.26,HERO_STARFALL_ARCHMAGE=1.40,HERO_THUNDER_JARL=1.41,HERO_CROWN_SWORD_LEADER=1.21,HERO_CROWN_HUNTER_LEADER=1.61,HERO_STONEBINDER=1.32,HERO_WINDCLAW_RANGER=1.55,HERO_LEVIATHAN_HUNTER=1.38,HERO_SKYHUNTER_VALKYRIE=1.35

[AI] IndependentReach W2=1000/1000(100.00 %) W3=862/1000(86.20 %) W4=825/1000(82.50 %) W5=802/1000(80.20 %) W6=740/1000(74.00 %)
[AI] SharedSettlementDeathCount=136/1000 (13.60 %) FirstDefeated=136 SameFrameDoubleDefeat=0 DeathWaves=W2=83,W3=18,W4=7,W5=28 FirstLeakWaves=W2=99,W3=13,W4=3,W5=28 FailureReasons=Heart=136
[AI] W1 n=1000 Heart=3.00 Resources=6.15 Recruit=1.96 Basic=4.96 Hero=0.14 Board=5.70 Bench=1.03 Residual=2.32
[AI] W2 n=1000 Heart=2.73 Resources=4.18 Recruit=2.83 Basic=6.77 Hero=0.28 Board=6.52 Bench=2.78 Residual=1.26
[AI] W3 n=862 Heart=2.92 Resources=6.01 Recruit=3.58 Basic=7.45 Hero=0.44 Board=7.05 Bench=3.58 Residual=1.27
[AI] W4 n=825 Heart=2.97 Resources=12.20 Recruit=3.99 Basic=7.19 Hero=0.59 Board=7.28 Bench=3.64 Residual=1.57
[AI] W5 n=802 Heart=2.89 Resources=9.25 Recruit=4.93 Basic=6.63 Hero=0.98 Board=7.66 Bench=4.17 Residual=1.61
[AI] FirstLeakRate=14.30 % RecruitStalls=0 Reasons=None Merges=3536
[AI] W6BossSpawn=740/1000 HittableBasicAvg=0.15 HittableHeroAvg=0.08 SingleTargetDPSAll=2.80 SingleTargetDPSQualified=3.79 HeroIds=HERO_WINDCLAW_RANGER=69,HERO_THUNDER_JARL=72,HERO_RUNEBOLT_MAGE=95,HERO_DRAGON_RIDER=74,HERO_LEVIATHAN_HUNTER=40,HERO_STARFALL_ARCHMAGE=77,HERO_CROWN_SWORD_LEADER=77,HERO_SKYHUNTER_VALKYRIE=47,HERO_CROWN_HUNTER_LEADER=72,HERO_EMBER_SHAMAN=83,HERO_NIGHTFANG_ASSASSIN=44,HERO_STONEBINDER=59 HeroLevels=HERO_WINDCLAW_RANGER=1.43,HERO_THUNDER_JARL=1.42,HERO_RUNEBOLT_MAGE=1.28,HERO_DRAGON_RIDER=1.39,HERO_LEVIATHAN_HUNTER=1.60,HERO_STARFALL_ARCHMAGE=1.40,HERO_CROWN_SWORD_LEADER=1.19,HERO_SKYHUNTER_VALKYRIE=1.43,HERO_CROWN_HUNTER_LEADER=1.40,HERO_EMBER_SHAMAN=1.34,HERO_NIGHTFANG_ASSASSIN=1.50,HERO_STONEBINDER=1.29


## Original vs swapped deltas

- Player:
  - W2 reach delta: 0 samples (swapped 1000, original 1000).
  - W3 reach delta: -15 samples (swapped 862, original 877).
  - W4 reach delta: -14 samples (swapped 825, original 839).
  - W5 reach delta: -24 samples (swapped 802, original 826).
  - W6 reach delta: -22 samples (swapped 740, original 762).
- AI:
  - W2 reach delta: 0 samples (swapped 1000, original 1000).
  - W3 reach delta: -15 samples (swapped 862, original 877).
  - W4 reach delta: -14 samples (swapped 825, original 839).
  - W5 reach delta: -24 samples (swapped 802, original 826).
  - W6 reach delta: -22 samples (swapped 740, original 762).

## Root-cause conclusion

The W6 figure above is the proportion of shared matches that remain alive until the synchronized W6 generation node. Since both sides enter each wave together, observed per-side W6 reach is the same shared-settlement event; the useful asymmetry is which side first depletes Heart and whether that difference follows the side or the Deck/Bag input after swapping.
First-defeated counts: original Player=140, AI=98, same-frame double=0; swapped Player=124, AI=136, same-frame double=0. The side gap changes after the Deck/Bag inputs are exchanged, so the dominant asymmetry follows the random Deck/Bag input rather than a fixed Player/AI branch.
Largest death-wave bucket: original Player=2, AI=2; swapped Player=2, AI=2. W1 deaths original Player=0, AI=0; swapped Player=0, AI=0. Recruit Stall is zero in both experiments, so the observed early collapse is not the repaired W6 recruit-stall path.
Priority recommendation: audit the largest observed early death-wave pressure next. Do not change W6 Boss HP or call this an AI-vs-human win rate; this remains a shared-settlement survival funnel.
No Production behavior or numerical value was changed. Do not freeze W6 Boss HP until this funnel/side-bias decision is resolved.

Raw rows: `Logs/W1ToW5SurvivalFunnel-OriginalAndSwapped.csv`.
