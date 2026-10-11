using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RelicPlayModeTests
{
    private const string DetailPath = "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport";
    private string saveRoot;
    private string tempRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private KingdomUIRoot ui;
    private GameManager game;
    private RelicManager relic;
    private ResourceManager resources;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        tempRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
        saveRoot = Path.Combine(tempRoot, "RelicPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [TearDown]
    public void TearDown()
    {
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            foreach (GameObject root in sample.GetRootGameObjects())
                Object.DestroyImmediate(root);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null && tutorial != initialTutorial)
            Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null && music != initialMusic)
            Object.DestroyImmediate(music.gameObject);
        // Keep every save writer inside the override until destruction completes.
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
        {
            string resolved = Path.GetFullPath(saveRoot);
            Assert.That(Path.GetDirectoryName(resolved), Is.EqualTo(tempRoot));
            Assert.That(Path.GetFileName(resolved), Does.StartWith("RelicPlayModeTests-"));
            Directory.Delete(resolved, true);
            Assert.That(Directory.Exists(resolved), Is.False);
        }
    }

    [UnityTest]
    public IEnumerator RepairButtons_PreviewCancelThenConfirmAndCommission()
    {
        yield return LoadRelic();
        Investigate();
        int version = relic.State.Version;
        Dictionary<Resource, ExpantaNum> before = Balances();
        Click("Repair");
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.True);
        Assert.That(relic.State.Version, Is.EqualTo(version), "Preview is presentation only.");
        AssertBalances(before);
        Click("Confirmation/Cancel");
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.False);
        Assert.That(relic.State.Route, Is.EqualTo(RelicRoute.None));
        Assert.That(relic.State.Version, Is.EqualTo(version));
        AssertBalances(before);
        Click("Repair");
        Click("Confirmation/Confirm");
        Assert.That(relic.State.Route, Is.EqualTo(RelicRoute.Repair));
        AssertStartupPayment(before, relic.Definition.Repair.StartupCosts);
        FinishWork(relic.Definition.Repair);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.Operational));
        Click("Prepare");
        Assert.That(relic.State.CommissionActive, Is.True);
        FinishWork(relic.Definition.Commission);
        Assert.That(relic.State.SupportReady, Is.True);
        Assert.That(relic.State.CompletedCommissions, Is.EqualTo(1), "Completed commissions are a discrete count.");
        Assert.That(relic.TryChooseRoute(RelicRoute.Dismantle, out _), Is.False);
        Assert.That(ui.RelicActionsForEditor.Find("Prepare").gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator DismantleButtons_ManufactureViaWorkshopAndAssignRealCampaignDiscount()
    {
        yield return LoadRelic();
        Investigate();
        Dictionary<Resource, ExpantaNum> before = Balances();
        Click("Dismantle");
        Assert.That(relic.State.Route, Is.EqualTo(RelicRoute.None));
        AssertBalances(before);
        Click("Confirmation/Confirm");
        AssertStartupPayment(before, relic.Definition.ReverseEngineering.StartupCosts);
        FinishWork(relic.Definition.ReverseEngineering);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.Operational));
        Assert.That(WorkshopManager.Instance.IsSystemUnlocked, Is.True);
        before = Balances();
        Click("Prepare");
        Assert.That(relic.State.SupportReady, Is.True);
        AssertStartupPayment(before, relic.Definition.SupportCraftCosts);
        Assert.That(WorkshopManager.Instance.TryCraftRelicSupport(out _), Is.False);
        SectorDefinition target = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        game.State.BeginCampaignForEditor(target.Id);
        ui.ShowRelicDetailsForEditor();
        SectorCampaignPreview original = game.Sectors.GetCampaignPreview(target, game.State, resources);
        Assert.That(original.FoodCostPerSecond, Is.GreaterThan(ExpantaNum.Zero));
        Click("Assign");
        SectorCampaignPreview supported = game.Sectors.GetCampaignPreview(target, game.State, resources);
        Near(supported.FoodCostPerSecond, original.FoodCostPerSecond * relic.Definition.CampaignSupplyMultiplier);
        Assert.That(supported.FoodCostPerSecond, Is.LessThan(original.FoodCostPerSecond));
        Assert.That(relic.State.SupportedSectorId, Is.EqualTo(target.Id), "Support binds a stable sector ID.");
        Assert.That(relic.State.SupportReady, Is.False);
        game.State.RestoreCampaignForEditor(false, string.Empty, ExpantaNum.Zero, ExpantaNum.Zero);
        relic.RefreshCampaignSupport();
        Assert.That(relic.State.SupportedSectorId, Is.Empty);
        Assert.That(relic.State.SupportReady, Is.False, "Retreat consumes the support.");
    }

    [UnityTest]
    public IEnumerator WorkshopSupportRow_PurchasePaysOnceAndDisablesFurtherManufacturing()
    {
        yield return LoadRelic();
        Investigate();
        Click("Dismantle");
        Click("Confirmation/Confirm");
        FinishWork(relic.Definition.ReverseEngineering);
        ui.SetPage("Workshop");
        ui.RefreshUI();
        yield return null;
        Button purchase = WorkshopSupportPurchase();
        Assert.That(purchase.gameObject.activeInHierarchy && purchase.interactable, Is.True);
        Dictionary<Resource, ExpantaNum> before = Balances();
        purchase.onClick.Invoke();
        Assert.That(relic.State.SupportReady, Is.True);
        AssertStartupPayment(before, relic.Definition.SupportCraftCosts);
        ui.RefreshUI();
        yield return null;
        purchase = WorkshopSupportPurchase();
        Assert.That(purchase.interactable, Is.False, "The prepared support occupies the single support slot.");
        before = Balances();
        int version = relic.State.Version;
        // UnityEvent invocation deliberately bypasses Selectable's input gate:
        // the Manager must also reject duplicate payment if a stale event arrives.
        purchase.onClick.Invoke();
        Assert.That(relic.State.Version, Is.EqualTo(version));
        AssertBalances(before);
        SectorDefinition target = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        game.State.BeginCampaignForEditor(target.Id);
        ui.ShowRelicDetailsForEditor();
        Click("Assign");
        Assert.That(relic.State.SupportedSectorId, Is.EqualTo(target.Id));
        game.State.RestoreCampaignForEditor(false, string.Empty, ExpantaNum.Zero, ExpantaNum.Zero);
        relic.RefreshCampaignSupport();
        // Stay on Workshop and let the actual bounded refresh loop update the
        // shared sector details after support is consumed by the retreat.
        yield return new WaitForSecondsRealtime(1.3f);
        Button prepare = ui.RelicActionsForEditor.Find("Prepare").GetComponent<Button>();
        Assert.That(prepare.gameObject.activeInHierarchy && prepare.interactable, Is.True,
            "Workshop's shared relic details must refresh after the campaign ends.");
    }

    [UnityTest]
    public IEnumerator SuspendAndResumeButtons_PreserveProgressAndDoNotRepayStartup()
    {
        yield return LoadRelic();
        Click("Investigate");
        Assert.That(relic.Tick(relic.Definition.Investigation.DurationSeconds.ToDouble() * .1d), Is.True);
        ui.ShowRelicDetailsForEditor();
        Click("Suspend");
        ExpantaNum progress = relic.State.Progress;
        Dictionary<Resource, ExpantaNum> before = Balances();
        ExpantaNum food = game.State.FoodAmount;
        Assert.That(relic.Tick(30d), Is.False);
        Near(relic.State.Progress, progress);
        Near(game.State.FoodAmount, food);
        AssertBalances(before);
        Click("Resume");
        Assert.That(relic.State.Suspended, Is.False);
        AssertBalances(before);
        Near(relic.State.Progress, progress);
        Assert.That(relic.Tick(30d), Is.True);
        Assert.That(relic.State.Progress, Is.GreaterThan(progress));
    }

    [UnityTest]
    public IEnumerator PageAndDetailSwitch_ClearConfirmationAndKeepWorkRunning()
    {
        yield return LoadRelic();
        Investigate();
        Click("Repair");
        ui.SetPage("Resources");
        yield return null;
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.False);
        ui.SetPage("Sectors");
        ui.ShowRelicDetailsForEditor();
        Click("Dismantle");
        ui.ShowDetailsForEditor("Other object", "Other details", "RelicTestOther");
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.False);
        Assert.That(ui.RelicActionsForEditor.gameObject.activeSelf, Is.False);
        ui.ShowRelicDetailsForEditor();
        Click("Repair");
        Click("Confirmation/Confirm");
        ui.SetPage("Workshop");
        yield return null;
        ExpantaNum progress = relic.State.Progress;
        Assert.That(relic.Tick(30d), Is.True);
        Assert.That(relic.State.Progress, Is.GreaterThan(progress));
        ui.SetPage("Sectors");
        ui.ShowRelicDetailsForEditor();
        Assert.That(ui.RelicActionsForEditor.gameObject.activeInHierarchy, Is.True);
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.False);
        Assert.That(relic.State.Route, Is.EqualTo(RelicRoute.Repair));
    }

    [UnityTest]
    public IEnumerator AuthoredActions_LayoutDoesNotOverlapAndConfirmationScrollsIntoReach()
    {
        yield return LoadRelic();
        Investigate();
        Click("Dismantle");
        yield return null;
        Canvas.ForceUpdateCanvases();
        RectTransform actions = ui.RelicActionsForEditor;
        RectTransform body = ui.transform.Find(DetailPath + "/DetailScrollContent/Body") as RectTransform;
        Assert.That(body, Is.Not.Null);
        Rect bodyBounds = Bounds(body);
        Rect actionsBounds = Bounds(actions);
        Assert.That(bodyBounds.width, Is.GreaterThan(0f));
        Assert.That(bodyBounds.height, Is.GreaterThan(0f));
        Assert.That(actionsBounds.width, Is.GreaterThan(0f));
        Assert.That(actionsBounds.height, Is.GreaterThan(0f));
        Assert.That(actionsBounds.yMax, Is.LessThanOrEqualTo(bodyBounds.yMin + 1f));
        RectTransform previous = null;
        foreach (Transform child in actions)
        {
            if (!child.gameObject.activeSelf)
                continue;
            RectTransform current = child as RectTransform;
            Rect bounds = Bounds(current);
            Assert.That(bounds.width, Is.GreaterThan(0f), child.name);
            if (child.name != "Result")
                Assert.That(bounds.height, Is.GreaterThan(0f), child.name);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(actionsBounds.xMin - 1f), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(actionsBounds.xMax + 1f), child.name);
            if (previous != null)
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Bounds(previous).yMin + 1f), child.name);
            previous = current;
        }
        Rect warning = Bounds(actions.Find("Confirmation/Warning") as RectTransform);
        Rect confirm = Bounds(actions.Find("Confirmation/Confirm") as RectTransform);
        Rect cancel = Bounds(actions.Find("Confirmation/Cancel") as RectTransform);
        Assert.That(confirm.yMax, Is.LessThanOrEqualTo(warning.yMin + 1f));
        Assert.That(cancel.yMax, Is.LessThanOrEqualTo(confirm.yMin + 1f));
        ScrollRect scroll = ui.transform.Find(DetailPath).GetComponent<ScrollRect>();
        Assert.That(scroll.enabled, Is.False, "Native ScrollRect owns geometry; the existing detail gesture owns scrolling.");
        Assert.That(scroll.vertical, Is.True);
        UIDetailRequirementScrollGesture gesture = scroll.GetComponent<UIDetailRequirementScrollGesture>();
        Assert.That(gesture, Is.Not.Null);
        Assert.That(gesture.isActiveAndEnabled, Is.True);
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        float positionBefore = scroll.content.anchoredPosition.y;
        gesture.SetNormalizedPosition(0f);
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(positionBefore));
        Assert.That(gesture.GetNormalizedPosition(), Is.InRange(0f, .001f));
        Rect viewport = Bounds(scroll.viewport);
        foreach (string path in new[] { "Confirmation/Confirm", "Confirmation/Cancel" })
        {
            Rect bounds = Bounds(actions.Find(path) as RectTransform);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - 1f), path);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.yMax + 1f), path);
        }
        Debug.Log("[RelicLayout] body=" + bodyBounds + " actions=" + actionsBounds +
            " viewport=" + viewport + " confirmation reachable after scrolling.");
        Click("Confirmation/Cancel");
        Assert.That(relic.State.Route, Is.EqualTo(RelicRoute.None));
    }

    private IEnumerator LoadRelic()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null; // LoadScene replaces existing roots on the next frame.
        for (int frame = 0; frame < 120; frame++)
        {
            GameBootstrap bootstrap = Object.FindObjectOfType<GameBootstrap>();
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (bootstrap != null && bootstrap.Completed && ui != null)
                break;
            yield return null;
        }
        Assert.That(Object.FindObjectOfType<GameBootstrap>().Completed, Is.True);
        Assert.That(ui, Is.Not.Null);
        Assert.That(SaveManager.Instance.LastLoadCreatedNewGame, Is.True);
        SimulationManager.Instance.SetRunning(false);
        game = GameManager.Instance;
        resources = ResourceManager.Instance;
        relic = game.Relic;
        Assert.That(relic.Definition, Is.Not.Null);
        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum(1e8d), 0L);
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        // Set up a late-game integration fixture through public typed seams.
        foreach (Research value in DataBase<Research>.All)
        {
            Dictionary<Resource, ExpantaNum> paid = new();
            foreach (Pair<Resource, ExpantaNum> cost in value.ResourceRequirements)
                if (cost.First != null && cost.Second > ExpantaNum.Zero)
                    paid[cost.First] = cost.Second;
            ResearchState state = ResearchManager.Instance.GetState(value);
            state.RestoreForEditor(state.BaseCost, true, true, paid);
        }
        ProgressionModifierManager.Rebuild(new List<ResearchState>(ResearchManager.Instance.States.Values),
            new List<WorkshopUpgradeState>(WorkshopManager.Instance.States.Values));
        foreach (Resource value in DataBase<Resource>.All)
            resources.SetAmount(value, new ExpantaNum(1e9d));
        foreach (Building value in relic.Definition.RequiredBuildings)
            BuildingManager.Instance.EnsureBuilding(value).SetAmountForEditor(ExpantaNum.One);
        game.Sectors.GetState(relic.Definition.Sector).SetUnlockedForEditor(true);
        game.Sectors.GetState(relic.Definition.Sector).SetOccupiedForEditor(true);
        game.UltraProject.State.RestoreForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Ready,
            CurrentStage = UltraProjectStage.Stabilization,
            StageProgress = "0",
            CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype },
            StateVersion = 1
        });
        ui.SetPage("Sectors");
        ui.ShowRelicDetailsForEditor();
        yield return null;
        Assert.That(ui.RelicActionsForEditor, Is.Not.Null);
        Assert.That(ui.RelicActionsForEditor.gameObject.activeInHierarchy, Is.True);
    }

    private void Investigate()
    {
        Click("Investigate");
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.Investigating));
        FinishWork(relic.Definition.Investigation);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.AwaitingChoice));
    }

    private void FinishWork(RelicWorkDefinition work)
    {
        Assert.That(relic.Tick(work.DurationSeconds.ToDouble() * 2d), Is.True);
        ui.ShowRelicDetailsForEditor();
    }

    private void Click(string path)
    {
        Button button = ui.RelicActionsForEditor.Find(path)?.GetComponent<Button>();
        Assert.That(button, Is.Not.Null, path);
        Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, path);
        button.onClick.Invoke();
    }

    private Dictionary<Resource, ExpantaNum> Balances()
    {
        Dictionary<Resource, ExpantaNum> result = new();
        foreach (Resource value in DataBase<Resource>.All)
            result[value] = resources.GetAmount(value);
        return result;
    }

    private Button WorkshopSupportPurchase()
    {
        foreach (Transform item in ui.GetComponentsInChildren<Transform>(true))
            if (item.name == "RelicSupportWorkshopRow" && item.gameObject.activeInHierarchy)
            {
                Button purchase = item.Find("PurchaseButton")?.GetComponent<Button>();
                Assert.That(purchase, Is.Not.Null, "Authored support row must bind its purchase button.");
                return purchase;
            }
        Assert.Fail("Workshop did not display its operational dismantle-route support row.");
        return null;
    }

    private void AssertBalances(Dictionary<Resource, ExpantaNum> before)
    {
        foreach (KeyValuePair<Resource, ExpantaNum> item in before)
            Near(resources.GetAmount(item.Key), item.Value);
    }

    private void AssertStartupPayment(Dictionary<Resource, ExpantaNum> before,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        Dictionary<Resource, ExpantaNum> expected = new(before);
        foreach (Pair<Resource, ExpantaNum> cost in costs)
            expected[cost.First] -= cost.Second;
        AssertBalances(expected);
    }

    private static Rect Bounds(RectTransform transform)
    {
        Vector3[] corners = new Vector3[4];
        transform.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static void Near(ExpantaNum actual, ExpantaNum expected)
    {
        ExpantaNum tolerance = ExpantaNum.Max(new ExpantaNum(1e-5d), expected.Abs() * 1e-10d);
        Assert.That((actual - expected).Abs(), Is.LessThanOrEqualTo(tolerance));
    }
}
