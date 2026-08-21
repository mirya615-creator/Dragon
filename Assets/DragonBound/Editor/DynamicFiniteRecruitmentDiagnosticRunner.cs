using DragonBound.Recruitment;
using DragonBound.AI;
using DragonBound.Core;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class DynamicFiniteRecruitmentDiagnosticRunner
    {
        public static void RunOneHundredThousandSeeds()
        {
            var report = FiniteComponentRecruitmentDiagnostics.SampleDynamicCatchup(
                GreyboxRecruitmentCatalog.Create(),
                1,
                100000);
            Debug.Log("DynamicFiniteRecruitmentDiagnosticRunner\n" + report.FormatReport());
        }

        public static void RunAiSurvivalOneThousandSeeds()
        {
            var wasEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            AiSurvivalSampleReport report = null;
            try
            {
                report = AiSurvivalSimulation.Run(1, 1000, 6);
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasEnabled;
            }

            Debug.Log(report.CreateReport());
        }

        public static void RunGlobalPressureOneThousandSeeds()
        {
            var wasEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            GlobalPressureDiagnosticsReport report = null;
            try
            {
                report = GlobalPressureDiagnostics.Run(1, 1000);
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasEnabled;
            }

            Debug.Log(report.FormatReport());
        }
    }
}
