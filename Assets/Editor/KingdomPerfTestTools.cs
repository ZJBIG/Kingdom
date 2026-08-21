using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only runtime helpers for exercising late-game UI without changing
/// definitions or the normal save format. These operations affect the live
/// Play Mode state only; do not save while a test multiplier is active.
/// </summary>
public sealed class KingdomPerfTestTools : EditorWindow
{
    private const string TestAmount = "1e1000";
    private const string TestResearchMultiplier = "1e1000";
    private const int QueueTestTargetCount = 64;
    private static bool researchGraphLinesVisible = true;

    [MenuItem("Tools/Kingdom/Performance Test Tools")]
    private static void Open()
    {
        GetWindow<KingdomPerfTestTools>("Kingdom Perf Tests");
    }

    [MenuItem("Tools/Kingdom/Prepare Research Queue Test")]
    private static void PrepareResearchQueueTestFromMenu()
    {
        PrepareResearchQueueTest();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "仅 Play Mode 有效。注入/加速只作用于当前运行时状态；测试期间不要保存。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("注入大量资源（全部资源）"))
                InjectAllResources();
            if (GUILayout.Button("研究加速：1e1000"))
                SetResearchAcceleration(true);
            if (GUILayout.Button("恢复正常研究速度"))
                SetResearchAcceleration(false);
            if (GUILayout.Button("Reset Research State (Play Mode Test Only)"))
                ResetResearchStateForTest();
            if (GUILayout.Button("准备研究队列测试（重置 + 注入资源 + 排队）"))
                PrepareResearchQueueTest();
            if (GUILayout.Button("快速完成可达研究（最多 512 次 tick）"))
                CompleteReachableResearch();
            if (GUILayout.Button("记录内存快照并执行 GC（检测专用）"))
                CollectAndLogMemory();
            if (GUILayout.Button(researchGraphLinesVisible
                ? "Research graph lines: OFF (drag baseline)"
                : "Research graph lines: ON"))
                ToggleResearchGraphLines();
        }
    }

    private static bool TryGetManagers(out ResourceManager resources, out ResearchManager researches,
        out SimulationManager simulation)
    {
        resources = ResourceManager.Instance;
        researches = ResearchManager.Instance;
        simulation = SimulationManager.Instance;
        if (resources != null && researches != null && simulation != null)
            return true;

        Debug.LogWarning("[KingdomPerfTest] Managers are not ready; enter Play Mode after bootstrap.");
        return false;
    }

    private static void InjectAllResources()
    {
        if (!TryGetManagers(out ResourceManager resources, out _, out _))
            return;

        ExpantaNum amount = new(TestAmount);
        int count = 0;
        foreach (Resource resource in DataBase<Resource>.All)
        {
            if (resource == null)
                continue;
            resources.SetAmount(resource, amount);
            count++;
        }
        Debug.Log($"[KingdomPerfTest] InjectResources resources={count} amount={TestAmount}");
        KingdomEditorPerfLog.Write($"[KingdomPerf] TestInjectResources resources={count} amount={TestAmount}");
    }

    private static void SetResearchAcceleration(bool enabled)
    {
        if (!TryGetManagers(out _, out ResearchManager researches, out _))
            return;

        researches.GlobalEfficiencyFactor = enabled
            ? new ExpantaNum(TestResearchMultiplier)
            : ExpantaNum.One;
        Debug.Log($"[KingdomPerfTest] ResearchAcceleration enabled={enabled} multiplier={(enabled ? TestResearchMultiplier : "1")}");
        KingdomEditorPerfLog.Write($"[KingdomPerf] TestResearchAcceleration enabled={enabled} multiplier={(enabled ? TestResearchMultiplier : "1")}");
    }

    private static void ResetResearchStateForTest()
    {
        if (!TryGetManagers(out _, out ResearchManager researches, out _))
            return;

        researches.ResetForPerformanceTest();
        Debug.Log("[KingdomPerfTest] Research state reset for Play Mode test; do not save.");
        KingdomEditorPerfLog.Write("[KingdomPerf] TestResearchStateReset completed=True");
    }

    private static void PrepareResearchQueueTest()
    {
        if (!TryGetManagers(out _, out ResearchManager researches, out _))
            return;

        researches.ResetForPerformanceTest();
        InjectAllResources();
        SetResearchAcceleration(false);
        int queued = 0;
        var candidates = new List<Research>(DataBase<Research>.All);
        candidates.Sort((left, right) =>
        {
            int leftCount = left?.Prerequisites?.Count ?? 0;
            int rightCount = right?.Prerequisites?.Count ?? 0;
            return rightCount.CompareTo(leftCount);
        });
        // EnqueueResearch intentionally accepts only a research whose
        // prerequisites are already completed.  Use the same action path as
        // the player so prerequisite batches are added when needed.
        for (int pass = 0; pass < candidates.Count && researches.ResearchQueue.Count < QueueTestTargetCount; pass++)
        {
            foreach (Research research in candidates)
            {
                if (researches.ResearchQueue.Count >= QueueTestTargetCount)
                    break;
                if (research == null)
                    continue;
                // HandleResearchAction toggles an already queued item. The
                // stress tool must preserve earlier entries while making
                // additional passes for prerequisite batches.
                if (researches.IsQueued(research))
                    continue;
                ResearchActionResult result = researches.HandleResearchAction(research);
                if (result == ResearchActionResult.Started ||
                    result == ResearchActionResult.Queued ||
                    result == ResearchActionResult.QueuedWaitingResources)
                    queued++;
            }
        }
        IReadOnlyList<ResearchState> queueSnapshot = researches.ResearchQueue;
        var queueIds = new StringBuilder();
        for (int i = 0; i < queueSnapshot.Count; i++)
        {
            if (i > 0)
                queueIds.Append(',');
            queueIds.Append(queueSnapshot[i]?.Definition?.Id ?? "<null>");
        }
        string activeId = researches.ActiveResearch?.Definition?.Id ?? "<none>";
        Debug.Log($"[KingdomPerfTest] QueuePrepared reset=True added={queued} active={activeId} queueCount={queueSnapshot.Count} queueIds={queueIds}");
        KingdomEditorPerfLog.Write($"[KingdomPerf] TestQueuePrepared reset=True added={queued} active={activeId} queueCount={queueSnapshot.Count} queueIds={queueIds}");
    }

    private static void ToggleResearchGraphLines()
    {
        KingdomUIRoot ui = UnityEngine.Object.FindObjectOfType<KingdomUIRoot>();
        if (ui == null || !ui.SetResearchGraphLinesVisibleForPerfTest(!researchGraphLinesVisible))
        {
            Debug.LogWarning("[KingdomPerfTest] Research graph is not built; enter the Research page first.");
            return;
        }

        researchGraphLinesVisible = !researchGraphLinesVisible;
        Debug.Log($"[KingdomPerfTest] Research graph line visibility={researchGraphLinesVisible}");
    }

    private static void CompleteReachableResearch()
    {
        if (!TryGetManagers(out _, out ResearchManager researches, out SimulationManager simulation))
            return;

        InjectAllResources();
        SetResearchAcceleration(true);
        int completedBefore = researches.TotalFinishedResearchCount;
        int ticks = 0;
        for (; ticks < 512 && researches.TotalFinishedResearchCount < researches.TotalResearchCount; ticks++)
        {
            for (int i = 0; i < DataBase<Research>.All.Count; i++)
            {
                Research research = DataBase<Research>.All[i];
                if (research != null)
                    researches.EnqueueResearch(research);
            }
            simulation.ManualTick(0.1d);
        }
        SetResearchAcceleration(false);
        Debug.Log($"[KingdomPerfTest] FastResearch completedDelta={researches.TotalFinishedResearchCount - completedBefore} " +
                  $"completed={researches.TotalFinishedResearchCount}/{researches.TotalResearchCount} ticks={ticks} " +
                  $"queueCount={researches.ResearchQueue.Count}");
        KingdomEditorPerfLog.Write($"[KingdomPerf] TestFastResearch completedDelta={researches.TotalFinishedResearchCount - completedBefore} " +
                                   $"completed={researches.TotalFinishedResearchCount}/{researches.TotalResearchCount} ticks={ticks} " +
                                   $"queueCount={researches.ResearchQueue.Count}");
    }

    private static void CollectAndLogMemory()
    {
        LogMemorySnapshot("before");
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        LogMemorySnapshot("after");
        KingdomEditorPerfLog.Write("[KingdomPerf] TestMemoryCollect completed=True");
    }

    private static void LogMemorySnapshot(string phase)
    {
        long managedBytes = System.GC.GetTotalMemory(false);
        long allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        long monoBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] TestMemorySnapshot phase={phase} " +
            $"managed={managedBytes / (1024f * 1024f):F1}MB " +
            $"allocated={allocatedBytes / (1024f * 1024f):F1}MB " +
            $"mono={monoBytes / (1024f * 1024f):F1}MB");
    }
}
