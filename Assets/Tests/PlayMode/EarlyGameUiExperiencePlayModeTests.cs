using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class EarlyGameUiExperiencePlayModeTests
{
    private string saveRoot;
    private KingdomUIRoot ui;
    [SetUp] public void SetUp()
    {
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "EarlyGameUi-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }
    [UnityTearDown] public IEnumerator TearDown()
    {
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        if (scene.IsValid() && scene.isLoaded)
            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null) Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null) Object.DestroyImmediate(music.gameObject);
        yield return null;
        SaveManager.ClearSaveRootOverrideForTests();
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"))));
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }
    [UnityTest] public IEnumerator HudEraAndPurchaseDetails_UseLiveSelectionAndAuthoredIcons()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 160; i++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(ui, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        yield return DismissIntroductions();
        ui.RefreshTopInfoForEditor();
        Transform grid = ui.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid");
        TMP_Text date = ui.transform.Find("SafeAreaRoot/TopStatusBar/Title/Date").GetComponent<TMP_Text>();
        Assert.That(date.text, Is.EqualTo("????/??/??"), "A settlement has no readable date before Calendar.");
        Assert.That(grid.Find("CurrentResearch/Value").GetComponent<TMP_Text>().text,
            Is.EqualTo(BuildingManager.Instance.UsedProductivity.ToGameString() + "/" + BuildingManager.Instance.TotalProductivity.ToGameString()));
        Assert.That(grid.Find("Power").gameObject.activeSelf, Is.False);
        Assert.That(grid.Find("Logistics").gameObject.activeSelf, Is.False);
        foreach (Transform slot in grid)
            Assert.That(slot.Find("Icon").GetComponent<Image>().sprite, Is.Not.Null, slot.name);
        ui.SetPage("Overview"); yield return null;
        AssertHudGeometry(grid, false);
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        Transform primary = ui.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        Assert.That(primary.Find("Text").GetComponent<TMP_Text>().text, Does.Contain(house.Label),
            "Skipping the introduction must leave a concrete first-housing goal.");
        Button next = primary.Find("NavigationButton").GetComponent<Button>();
        Assert.That(next.gameObject.activeInHierarchy && next.interactable, Is.True,
            "The housing goal must provide an actionable building destination.");
        Capture("overview-hud");
        UITouchTooltip tooltip = grid.Find("ResearchPower").GetComponent<UITouchTooltip>();
        var pointer = new PointerEventData(EventSystem.current);
        tooltip.OnPointerDown(pointer);
        yield return new WaitForSecondsRealtime(.65f);
        tooltip.OnPointerUp(pointer);
        Transform help = ui.transform.Find("SafeAreaRoot/Tooltip");
        Assert.That(help.gameObject.activeInHierarchy, Is.True, "Holding the metric still opens its explanation.");
        Assert.That(help.Find("Text").GetComponent<TMP_Text>().text, Is.Not.Empty);
        Capture("tooltip-help");
        help.gameObject.SetActive(false);

        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(house), ExpantaNum.One);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            if (research.TechLevel == TechLevel.Animal && !research.AdvancesTechLevel)
                ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshAutomaticUIForEditor();
        ui.RefreshTopInfoForEditor();
        Assert.That(date.text, Is.Not.EqualTo("????/??/??"));
        yield return DismissIntroductions();
        ui.SetPage("Era"); yield return null;
        Transform era = ui.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows/EraTransitionSection/Content/EraTransitionTarget");
        Assert.That(era, Is.Not.Null);
        era.GetComponent<Button>().onClick.Invoke();
        Assert.That(era.gameObject.activeInHierarchy, Is.True, "Selecting a transition keeps the era page visible.");
        Assert.That(ui.DetailActionButtonForEditor.gameObject.activeInHierarchy, Is.True);
        TMP_Text eraBody = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        Assert.That(eraBody.text, Does.Contain(TechLevel.StoneAge.GetDescription()), "The transition's effect must identify its target era.");
        ResearchDef transition = EraGoalEvaluator.FindTransition(TechLevel.StoneAge);
        Assert.That(transition, Is.Not.Null);
        ui.ShowResearchDetailsForEditor(transition);
        foreach (TMP_Text text in ui.transform.Find("SafeAreaRoot/DetailPanel").GetComponentsInChildren<TMP_Text>())
            if (text.transform.parent.name.StartsWith("Requirement_", StringComparison.Ordinal) && text.name == "Amount")
                Assert.That(text.text, Does.Not.Contain(" / "), "Research requires a single full payment.");
        yield return null; Capture("era-selected-detail");

        ui.SetPage("Resources"); yield return null;
        Capture("resource-pins");
        ui.SetPage("Buildings");
        foreach (var cost in house.ResourceRequirements) ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        ui.ShowBuildingDetailsForEditor(house); yield return null;
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        Assert.That(body.text, Does.Contain("index=5"), "Material shortage belongs in the detail panel.");
        Transform entry = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/ResourceProductionTree");
        Assert.That(entry.gameObject.activeSelf, Is.False, "Production entry stays hidden before its industrial unlock.");
        Capture("building-materials-detail");
        ResearchManager.Instance.GetState(DataBase<ResearchDef>.Find("FactoryOrganization")).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshAutomaticUIForEditor();
        ui.ShowBuildingDetailsForEditor(house);
        Assert.That(entry.gameObject.activeSelf, Is.True, "The same detail selection gains its entry after the real research unlock.");
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Industrial);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            if (research.TechLevel <= TechLevel.Industrial)
                ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshTopInfoForEditor();
        Canvas.ForceUpdateCanvases();
        Assert.That(grid.Find("Power").gameObject.activeSelf, Is.True);
        Assert.That(grid.Find("Logistics").gameObject.activeSelf, Is.True);
        AssertHudGeometry(grid, true);
        Capture("hud-infrastructure-unlocked");
    }
    [UnityTest]
    public IEnumerator BuildingRecommendations_FollowResourceDeficitsAndBuildFactoryWithoutWorkshopGate()
    {
        yield return PrepareIndustrialRecommendationScene();
        ResourceManager resources = ResourceManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        BuildingDef lumberyard = DataBase<BuildingDef>.Find("MechanizedLumberyard");
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        resources.AdjustConsumptionRate(wood, new ExpantaNum(100));
        Assert.That(resources.GetNetRate(resources.GetState(wood)), Is.LessThan(ExpantaNum.Zero));
        ui.SetPage("Buildings");
        yield return null;
        ui.RefreshLiveCardValuesForEditor();
        Image star = GetRecommendationStar(lumberyard);
        BuildingDef reserveLumberyard = DataBase<BuildingDef>.Find("Lumberyard");
        Image reserveStar = GetRecommendationStar(reserveLumberyard);
        Assert.That(star.gameObject.activeSelf, Is.True);
        Assert.That(reserveStar.gameObject.activeSelf, Is.True);
        Assert.That(star.raycastTarget, Is.False);
        ExpantaNum count = buildings.GetState(lumberyard).Amount;
        ExpantaNum stock = resources.GetAmount(wood);
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(buildings.GetState(lumberyard).Amount, Is.EqualTo(count)); // Discrete ownership is unchanged by a hint.
        Assert.That(resources.GetAmount(wood).ToDouble(), Is.EqualTo(stock.ToDouble()).Within(1e-6));

        RectTransform row = (RectTransform)star.transform.parent;
        Canvas.ForceUpdateCanvases();
        Bounds starBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(row, star.rectTransform);
        Assert.That(starBounds.min.y, Is.GreaterThanOrEqualTo(row.rect.yMin));
        Assert.That(starBounds.max.y, Is.LessThanOrEqualTo(row.rect.yMax));
        foreach (string name in new[] { "PinButton", "BuildButton", "DeconstructButton", "Label" })
        {
            Bounds other = RectTransformUtility.CalculateRelativeRectTransformBounds(row, row.Find(name));
            Assert.That(starBounds.Intersects(other), Is.False, name + " must remain separate from the star.");
        }

        BuildingDef factory = DataBase<BuildingDef>.Find("MachineFactory");
        Button factoryDeconstruct = ui.GetDeconstructButtonForEditor(factory);
        Assert.That(factoryDeconstruct, Is.Not.Null, "The researched factory must be visible without its production upgrade.");
        Button factoryBuild = factoryDeconstruct.transform.parent.Find("BuildButton").GetComponent<Button>();
        Assert.That(factoryBuild.interactable, Is.True);
        ui.ShowBuildingDetailsForEditor(factory);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        Assert.That(body.text, Does.Not.Contain("workshop:PrecisionTooling"));
        WorkshopManager workshop = WorkshopManager.Instance;
        Assert.That(workshop.IsPurchased(DataBase<WorkshopUpgradeDef>.Find("PrecisionTooling")), Is.False);
        ExpantaNum factoryCount = buildings.GetState(factory).Amount;
        factoryBuild.onClick.Invoke();
        Assert.That(buildings.GetState(factory).Amount, Is.EqualTo(factoryCount + ExpantaNum.One)); // One build command without the workshop.
        Assert.That(workshop.TryPurchase(DataBase<WorkshopUpgradeDef>.Find("PrecisionTooling"), out WorkshopPurchaseFailure failure),
            Is.True, failure.ToString());
        ui.RefreshUI();
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(factoryBuild.interactable, Is.True);
        Assert.That(buildings.States.ContainsKey(reserveLumberyard), Is.False,
            "Workshop completion must exercise cleanup of the zero-count fallback source.");
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(body.text, Does.Not.Contain("workshop:PrecisionTooling"));
        Assert.That(body.text, Does.Contain("每座建筑属性"));

        ScrollRect scroll = row.GetComponentInParent<ScrollRect>();
        float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
        if (overflow > 0f)
        {
            Bounds location = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, row);
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((scroll.content.rect.yMax - location.max.y) / overflow);
        }
        Canvas.ForceUpdateCanvases();
        Capture("building-recommendation-factory-unlocked", "industrial-feedback-20261110");

        resources.AdjustConsumptionRate(wood, -resources.GetState(wood).ConsumptionRate);
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(star.gameObject.activeSelf, Is.False, "The hint must clear when the shortage clears.");
        Assert.That(reserveStar.gameObject.activeSelf, Is.False,
            "A visible fallback source must refresh even after its zero-count State was removed.");
        ui.SetPage("Overview");
        resources.AdjustConsumptionRate(wood, new ExpantaNum(100));
        ui.SetPage("Buildings");
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(GetRecommendationStar(lumberyard).gameObject.activeSelf, Is.True, "Reopening must use current rates.");
        Assert.That(GetRecommendationStar(reserveLumberyard).gameObject.activeSelf, Is.True);

        BuildingDef chemical = DataBase<BuildingDef>.Find("ChemicalPlant");
        ResourceDef fuel = DataBase<ResourceDef>.Find("RocketFuel");
        resources.AdjustConsumptionRate(fuel, new ExpantaNum(10));
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(GetRecommendationStar(chemical).gameObject.activeSelf, Is.False,
            "A locked rocket-fuel recipe cannot justify recommending a chemical plant.");
        ResearchManager research = ResearchManager.Instance;
        research.GetState(DataBase<ResearchDef>.Find("OrbitalEngineering")).SetStatusForEditor(ResearchStatus.Completed);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values),
            new List<WorkshopUpgradeState>(workshop.States.Values));
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(GetRecommendationStar(chemical).gameObject.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator BuildingRecommendations_TrackFoodPowerLogisticsAndHousingShortfalls()
    {
        yield return PrepareIndustrialRecommendationScene();
        GameManager game = GameManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        BuildingDef factory = DataBase<BuildingDef>.Find("MachineFactory");
        BuildingDef oldHousing = DataBase<BuildingDef>.Find("TownHouse");
        buildings.SetAmountAndRatesForEditor(buildings.GetState(factory), ExpantaNum.One);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(oldHousing), ExpantaNum.One);
        ui.SetPage("Buildings");
        game.AdjustFoodRates(ExpantaNum.Zero, game.State.FoodProductionRate + new ExpantaNum(100));
        ui.RefreshLiveCardValuesForEditor();
        Image power = GetRecommendationStar(DataBase<BuildingDef>.Find("CentralPowerStation"));
        Image logistics = GetRecommendationStar(DataBase<BuildingDef>.Find("RailHub"));
        Image food = GetRecommendationStar(DataBase<BuildingDef>.Find("PlantingField"));
        Assert.That(power.gameObject.activeSelf, Is.True);
        Assert.That(logistics.gameObject.activeSelf, Is.True);
        Assert.That(game.State.FoodNetRate, Is.LessThan(ExpantaNum.Zero));
        Assert.That(food.gameObject.activeSelf, Is.True);
        game.AdjustPowerRates(new ExpantaNum(1000), ExpantaNum.Zero);
        game.AdjustLogisticsRates(new ExpantaNum(1000), ExpantaNum.Zero);
        game.AdjustFoodRates(-game.State.FoodNetRate + ExpantaNum.One, ExpantaNum.Zero);
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(power.gameObject.activeSelf, Is.False);
        Assert.That(logistics.gameObject.activeSelf, Is.False);
        Assert.That(food.gameObject.activeSelf, Is.False);

        game.State.RestorePopulationForEditor(game.State.Population.PopulationCapacity);
        ExpantaNum factoryCount = (buildings.TotalProductivity / factory.ProductivityConsumption).Floor() + ExpantaNum.One;
        buildings.SetAmountAndRatesForEditor(buildings.GetState(factory), factoryCount);
        Assert.That(buildings.AvailableProductivity, Is.LessThan(ExpantaNum.Zero));
        ui.RefreshLiveCardValuesForEditor();
        Image housing = GetRecommendationStar(DataBase<BuildingDef>.Find("IndustrialHabitationComplex"));
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(oldHousing, out BuildingDef housingTarget), Is.True);
        Assert.That(housingTarget.Id, Is.EqualTo("IndustrialHabitationComplex"));
        Image housingUpgrade = GetRecommendationStar(oldHousing);
        Assert.That(housing.gameObject.activeSelf, Is.True);
        Assert.That(housingUpgrade.gameObject.activeSelf, Is.True, "The existing house card must recommend its useful upgrade.");
        ExpantaNum perPerson = PopulationState.ProductivityGrantedPerPerson *
            ProgressionModifierManager.Current.PopulationProductivityMultiplier;
        ExpantaNum population = game.State.Population.Population +
            (-buildings.AvailableProductivity / perPerson).Ceiling() + ExpantaNum.One;
        game.State.RestorePopulationCapacityExactForEditor(population, ExpantaNum.Zero);
        game.State.RestorePopulationForEditor(population);
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(housing.gameObject.activeSelf, Is.False);
        Assert.That(housingUpgrade.gameObject.activeSelf, Is.False);
    }

    private IEnumerator PrepareIndustrialRecommendationScene()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 160; i++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(Object.FindObjectOfType<GameBootstrap>()?.Completed, Is.True);
        SimulationManager.Instance.SetRunning(false);
        yield return DismissIntroductions();
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Industrial);
        GameManager.Instance.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(100), ExpantaNum.Zero);
        GameManager.Instance.State.RestorePopulationForEditor(new ExpantaNum(100));
        ResearchManager research = ResearchManager.Instance;
        WorkshopManager workshop = WorkshopManager.Instance;
        foreach (ResearchDef definition in DataBase<ResearchDef>.All)
            if (definition.TechLevel <= TechLevel.Industrial)
                research.GetState(definition).SetStatusForEditor(ResearchStatus.Completed);
        foreach (WorkshopUpgradeDef definition in DataBase<WorkshopUpgradeDef>.All)
            if (definition.TechLevel <= TechLevel.Industrial && definition.Id != "PrecisionTooling")
                workshop.States[definition].SetPurchasedForEditor(true);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values),
            new List<WorkshopUpgradeState>(workshop.States.Values));
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum("1e9"));
    }

    private Image GetRecommendationStar(BuildingDef building)
    {
        Button action = ui.GetDeconstructButtonForEditor(building);
        Assert.That(action, Is.Not.Null, building.Id + " must have a visible building card.");
        Image star = action.transform.parent.Find("RecommendationStar")?.GetComponent<Image>();
        Assert.That(star, Is.Not.Null);
        return star;
    }

    private static void AssertHudGeometry(Transform grid, bool infrastructureVisible)
    {
        Canvas.ForceUpdateCanvases();
        var layout = grid.GetComponent<GridLayoutGroup>();
        string[] order = { "Food", "Population", "ResearchPower", "Happiness", "Territory", "CurrentResearch", "Power", "Logistics" };
        int count = infrastructureVisible ? order.Length : 6;
        var rectangles = new Rect[count];
        var corners = new Vector3[4];
        for (int i = 0; i < count; i++)
        {
            RectTransform slot = grid.Find(order[i]) as RectTransform;
            slot.GetWorldCorners(corners);
            Vector3 lower = grid.InverseTransformPoint(corners[0]);
            Vector3 upper = grid.InverseTransformPoint(corners[2]);
            rectangles[i] = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            Assert.That(rectangles[i].width, Is.GreaterThan(0));
            Assert.That(rectangles[i].height, Is.GreaterThan(0));
            if (i % 2 == 1)
                Assert.That(Mathf.Abs(rectangles[i].xMin - rectangles[i - 1].xMin), Is.LessThan(.5f), "Each pair forms one column.");
            if (i >= 2 && i % 2 == 0)
                Assert.That(rectangles[i].xMin - rectangles[i - 2].xMax, Is.EqualTo(layout.spacing.x).Within(.5f), "Visible columns are contiguous without a hidden infrastructure gap.");
            for (int j = 0; j < i; j++) Assert.That(rectangles[i].Overlaps(rectangles[j]), Is.False, "HUD entries must not overlap.");
        }
        if (infrastructureVisible)
            Assert.That(rectangles[6].xMin, Is.GreaterThan(rectangles[5].xMax), "Power and logistics occupy the rightmost column.");
    }

    private IEnumerator DismissIntroductions()
    {
        for (int i = 0; i < 16; i++)
        {
            ui.RefreshOnboardingForEditor();
            TutorialManager.Current?.SkipCurrentStage();
            yield return null;
        }
        ui.RefreshOnboardingForEditor();
    }
    private void Capture(string name, string evidenceDirectory = "experience-20261023")
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", evidenceDirectory));
        Directory.CreateDirectory(directory);
        Canvas canvas = ui.GetComponent<Canvas>();
        RenderMode mode = canvas.renderMode; Camera previousCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("EarlyGameEvidenceCamera"); Camera camera = host.AddComponent<Camera>();
        var target = new RenderTexture(2640, 1200, 24); var pixels = new Texture2D(2640, 1200, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            target.Create(); camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance;
            camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(host);
        }
    }
}

