using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SectorBuildingPlayModeTests
{
    [SetUp]
    public void SetUp()
    {
        KingdomPlayModeSaveScope.Begin();
    }

    [TearDown]
    public void TearDown()
    {
        KingdomPlayModeSaveScope.Clear();
    }

    [UnityTest]
    public IEnumerator SectorNavigationUnlocksAfterRequiredResearchAndOpensPage()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        GameManager game = Object.FindObjectOfType<GameManager>();
        Assert.That(game, Is.Not.Null);
        ResearchManager researchManager = ResearchManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;

        typeof(GameManager).GetMethod(
            "InitializeNewGame", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(game, null);
        researchManager.ResetForPerformanceTest();
        GrantResearchTestResources(resourceManager);
        MethodInfo refreshNavigation = typeof(KingdomUIRoot).GetMethod(
            "RefreshNavigationVisibility", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(refreshNavigation, Is.Not.Null);
        refreshNavigation.Invoke(root, null);

        Button sectorsButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Sectors")?.GetComponent<Button>();
        Assert.That(sectorsButton, Is.Not.Null);
        Assert.That(sectorsButton.gameObject.activeSelf, Is.False,
            "The Sectors navigation entry must remain hidden before its research gates complete.");

        CompleteResearchAndPrerequisites(game, researchManager, resourceManager,
            DataBase<Research>.Find("HomeSystemSurvey"), new HashSet<string>());
        refreshNavigation.Invoke(root, null);
        yield return null;

        Assert.That(researchManager.IsResearchCompleted("HomeSystemSurvey"), Is.True);
        Assert.That(sectorsButton.gameObject.activeSelf, Is.True,
            "Completing HomeSystemSurvey must reveal the home-system Sectors entry.");

        CompleteResearchAndPrerequisites(game, researchManager, resourceManager,
            DataBase<Research>.Find("InterstellarNavigation"), new HashSet<string>());
        refreshNavigation.Invoke(root, null);
        yield return null;

        Assert.That(researchManager.IsResearchCompleted("HomeSystemSurvey"), Is.True);
        Assert.That(researchManager.IsResearchCompleted("InterstellarNavigation"), Is.True);
        Assert.That(sectorsButton.gameObject.activeSelf, Is.True,
            "Completing the sector research gates must reveal the Sectors navigation entry.");
        Assert.That(sectorsButton.interactable, Is.True);

        sectorsButton.onClick.Invoke();
        yield return null;
        Transform sectorsPage = root.transform.Find("SafeAreaRoot/Content/PageHost/Sectors");
        Assert.That(sectorsPage, Is.Not.Null);
        Assert.That(sectorsPage.gameObject.activeSelf, Is.True,
            "The enabled Sectors button must navigate to the Sectors page.");
    }

    [UnityTest]
    public IEnumerator SectorBuildingMenuIsGatedAndReflowsRows()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        GameManager game = Object.FindObjectOfType<GameManager>();
        Assert.That(game, Is.Not.Null);
        typeof(GameManager).GetMethod(
            "InitializeNewGame", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(game, null);
        ResearchManager.Instance.ResetForPerformanceTest();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorState moonState = game.Sectors.GetState(moon);
        SectorBuilding hub = DataBase<SectorBuilding>.Find("EarthMoonLogisticsHub");
        Assert.That(hub, Is.Not.Null);
        foreach (BuildingState state in BuildingManager.Instance.States.Values)
            state.SetAmountForEditor(ExpantaNum.Zero);
        foreach (Resource resource in DataBase<Resource>.All)
            ResourceManager.Instance.SetAmount(resource, ExpantaNum.Zero);
        moonState.SetOccupiedForEditor(false);

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Sectors" });
        yield return null;

        Transform rows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Sectors/DataRows");
        Assert.That(rows, Is.Not.Null);
        Transform moonRow = rows.Find("SectorRow_AzurePool");
        Assert.That(moonRow, Is.Not.Null);
        Assert.That(moonRow.Find("Buildings"), Is.Null);

        moonState.SetOccupiedForEditor(true);
        MethodInfo refresh = typeof(KingdomUIRoot).GetMethod(
            "RefreshSectorRowSummaries", BindingFlags.Instance | BindingFlags.NonPublic);
        refresh.Invoke(root, null);
        yield return null;
        Transform buildingButton = moonRow.Find("Buildings");
        Assert.That(buildingButton, Is.Not.Null);
        Assert.That(buildingButton.gameObject.activeSelf, Is.True);

        float before = moonRow.GetComponent<RectTransform>().rect.height;
        RectTransform nextRow = moonRow.GetSiblingIndex() + 1 < rows.childCount
            ? rows.GetChild(moonRow.GetSiblingIndex() + 1).GetComponent<RectTransform>()
            : null;
        float nextRowBefore = nextRow == null ? 0f : nextRow.anchoredPosition.y;
        buildingButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        float expandedHeight = moonRow.GetComponent<RectTransform>().rect.height;
        Assert.That(expandedHeight, Is.GreaterThan(before));
        Assert.That(expandedHeight, Is.GreaterThan(400f));
        Assert.That(before, Is.LessThan(104f));
        if (nextRow != null)
            Assert.That(nextRow.anchoredPosition.y, Is.LessThan(nextRowBefore - 400f));

        Transform menu = moonRow.Find("SectorBuildingMenu");
        Assert.That(menu, Is.Not.Null);
        Transform build = menu.Find("EarthMoonLogisticsHub/Build");
        Assert.That(build, Is.Not.Null);
        Assert.That(build.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);

        PrepareBuildableSectorBuilding(game, hub);
        refresh.Invoke(root, null);
        yield return null;
        Assert.That(build.GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
        build.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;

        menu = moonRow.Find("SectorBuildingMenu");
        Transform card = menu.Find("EarthMoonLogisticsHub");
        Assert.That(card.Find("Amount").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("1/1"));
        Assert.That(card.Find("Build").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
        Assert.That(card.Find("Deconstruct").GetComponent<UnityEngine.UI.Button>().interactable, Is.True);

        buildingButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Assert.That(moonRow.GetComponent<RectTransform>().rect.height, Is.LessThan(expandedHeight));
        Assert.That(moonRow.GetComponent<RectTransform>().rect.height, Is.LessThan(104f));

        setPage.Invoke(root, new object[] { "Buildings" });
        yield return null;
        Transform buildingRows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Buildings/DataRows");
        Assert.That(buildingRows.Find("EarthMoonLogisticsHub"), Is.Null);

        moonState.SetOccupiedForEditor(false);
        setPage.Invoke(root, new object[] { "Sectors" });
        refresh.Invoke(root, null);
        yield return null;
        Assert.That(moonRow.Find("Buildings"), Is.Null);
        Assert.That(moonRow.Find("SectorBuildingMenu"), Is.Null);
    }

    private static void PrepareBuildableSectorBuilding(GameManager game, SectorBuilding building)
    {
        typeof(GameManager).GetMethod(
            "AdvanceTechLevel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(game, new object[] { building.TechLevel });
        typeof(GameState).GetMethod(
            "RestorePopulation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(game.State, new object[] { new ExpantaNum(1000d) });

        MethodInfo setStatus = typeof(ResearchState).GetMethod(
            "SetStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setStatus, Is.Not.Null);
        for (int i = 0; i < building.RequiredResearch.Count; i++)
        {
            Research required = building.RequiredResearch[i];
            setStatus.Invoke(ResearchManager.Instance.GetState(required),
                new object[] { ResearchStatus.Completed });
        }

        MethodInfo getConstructionCostMultiplier = typeof(BuildingManager).GetMethod(
            "GetConstructionCostMultiplier", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(getConstructionCostMultiplier, Is.Not.Null);
        ExpantaNum constructionCostMultiplier = (ExpantaNum)getConstructionCostMultiplier.Invoke(
            null, new object[] { building });
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = building.ResourceRequirements[i];
            // Derive the fixture from the live construction multiplier. The test
            // must not depend on an exact inventory total or hard-coded costs.
            ResourceManager.Instance.AddAmount(
                requirement.First,
                requirement.Second * constructionCostMultiplier * new ExpantaNum(2d));
        }
    }

    private static void GrantResearchTestResources(ResourceManager resourceManager)
    {
        var totals = new Dictionary<Resource, ExpantaNum>();
        foreach (Research research in DataBase<Research>.All)
        {
            if (research == null)
                continue;
            foreach (Pair<Resource, ExpantaNum> requirement in research.ResourceRequirements)
            {
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                totals[requirement.First] = totals.TryGetValue(
                    requirement.First, out ExpantaNum total)
                    ? total + requirement.Second
                    : requirement.Second;
            }
        }
        foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
        {
            if (upgrade == null)
                continue;
            foreach (Pair<Resource, ExpantaNum> requirement in upgrade.ResourceRequirements)
            {
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                totals[requirement.First] = totals.TryGetValue(
                    requirement.First, out ExpantaNum total)
                    ? total + requirement.Second
                    : requirement.Second;
            }
        }
        foreach (Resource resource in DataBase<Resource>.All)
            resourceManager.SetAmount(resource, totals.TryGetValue(
                resource, out ExpantaNum total) ? total : ExpantaNum.Zero);
    }

    private static void CompleteResearchAndPrerequisites(
        GameManager game,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Research target,
        HashSet<string> visiting)
    {
        if (target == null || researchManager.IsResearchCompleted(target.Id))
            return;
        Assert.That(visiting.Add(target.Id), Is.True,
            "Research prerequisite cycle detected at " + target.Id);
        for (int i = 0; i < target.Prerequisites.Count; i++)
            CompleteResearchAndPrerequisites(
                game, researchManager, resourceManager, target.Prerequisites[i], visiting);
        visiting.Remove(target.Id);

        ResearchActionResult action = researchManager.HandleResearchAction(target);
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Invalid));
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Blocked),
            "Research was blocked before completion: " + target.Id);
        for (int guard = 0; guard < 10000 && !researchManager.IsResearchCompleted(target.Id); guard++)
        {
            if (researchManager.ActiveResearch == null && researchManager.ResearchQueue.Count > 0)
            {
                researchManager.TryStartNextQueuedResearch();
            }
            if (researchManager.ActiveResearch != null)
                researchManager.Tick(1000000d);
            else if (researchManager.ResearchQueue.Count == 0)
                break;
        }
        Assert.That(researchManager.IsResearchCompleted(target.Id), Is.True,
            "Research did not complete through the runtime clock: " + target.Id);
        Assert.That(game.State.TechLevel, Is.GreaterThanOrEqualTo(target.TechLevel));
    }
}
