using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Instantiates authored list-row prefabs and supplies only definition data
/// and interaction callbacks. Geometry and visual hierarchy belong to the
/// corresponding prefab assets.
/// </summary>
public sealed partial class KingdomUIRoot
{
    // Keep the alternating list treatment in one place so Resource, Workshop,
    // Building and Music rows remain visually consistent.
    private static readonly Color ListRowEven = new Color(1f, 1f, 1f, .05f);
    private static readonly Color ListRowOdd = new Color(1f, 1f, 1f, .09f);
    private readonly System.Text.StringBuilder buildingDisplaySignatureBuilder = new(1024);
    private readonly Dictionary<WorkshopUpgradeDef, Button> workshopPurchaseButtons = new();

    private static void ApplyListRowStyle(GameObject row, int index)
    {
        if (row == null)
            return;
        Image surface = row.GetComponent<Image>();
        if (surface == null)
        {
            Debug.LogError("[王国界面] Authored list row is missing its root Image: " + row.name);
            return;
        }
        surface.color = index % 2 == 0 ? ListRowEven : ListRowOdd;
        Button button = row.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = surface;
            // Keep the alternating normal color while restoring a visible
            // hover/pressed state for touch and mouse feedback.
            button.transition = Selectable.Transition.ColorTint;
            ApplyButtonColors(button, surface.color);
        }
    }

    private static void ApplyButtonColors(Button button, Color normal)
    {
        if (button == null)
            return;
        ColorBlock colors = button.colors;
        // Image.color owns the alternating row palette. A white normal tint
        // prevents Unity from multiplying that palette a second time.
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(.66f, .66f, .66f, 1f);
        colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.disabledColor = new Color(.55f, .57f, .57f, .90f);
        colors.fadeDuration = .08f;
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private void BuildAuthoredResourceRows(RectTransform parent)
    {
        int visible = 0;
        ResourceManager resourceManager = ResourceManager.Instance;
        IReadOnlyList<ResourceDef> definitions = DataBase<ResourceDef>.All;
        CaptureVisibleResourceIds(definitions);
        var orderedDefinitions = new List<ResourceDef>();
        for (int i = 0; i < definitions.Count; i++)
        {
            ResourceDef resource = definitions[i];
            if (resource == null || !visibleResourceIds.Contains(resource.Id))
                continue;
            orderedDefinitions.Add(resource);
        }

        orderedDefinitions.Sort((left, right) =>
            ComparePinnedAuthoredRows(UIPinPreferences.Resource,
                left.TechLevel, left.Id, right.TechLevel, right.Id));
        for (int i = 0; i < orderedDefinitions.Count; i++)
        {
            ResourceDef resource = orderedDefinitions[i];
            ResourceState state = null;
            resourceManager?.States.TryGetValue(resource, out state);
            string amount = state == null ? "0" : state.Amount.ToGameString();
            ExpantaNum net = state == null
                ? ExpantaNum.Zero
                : NormalizeDisplayedNetRate(
                    ResourceManager.ApplyCurrentProductionReward(state.ProductionRate),
                    state.ConsumptionRate);
            string change = (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s";
            GameObject row = InstantiateAuthoredRow(GetEraCollectionPrefabName(KingdomUIPrefabLibrary.ResourceCard,
                resource.TechLevel), parent, visible++);
            if (row == null)
                continue;
            ApplyEraRowStyle(row, resource.TechLevel, visible - 1);
            BindRowPin(row, UIPinPreferences.Resource, resource.Id);
            if (!SetRowText(row, "Label", resource.Label) ||
                !SetRowText(row, "Amount", amount) ||
                !SetRowText(row, "ChangeRate", change, net >= ExpantaNum.Zero ? Positive : Error))
                continue;
            Transform iconTransform = row.transform.Find("Icon");
            Image icon = iconTransform == null ? null : iconTransform.GetComponent<Image>();
            if (icon == null)
            {
                Debug.LogError("[王国界面] ResourceCard prefab is missing authored Icon Image.");
                continue;
            }
            icon.sprite = resource.Sprite;
            icon.color = resource.Color;
            icon.preserveAspect = true;
            Button button = RequireRowButton(row);
            if (button == null)
                continue;
            button.onClick.AddListener(() =>
            {
                ShowResourceDetails(resource);
            });
            resourceAmountLabels[resource] = row.transform.Find("Amount")?.GetComponent<TMP_Text>();
            resourceChangeLabels[resource] = row.transform.Find("ChangeRate")?.GetComponent<TMP_Text>();
        }
        Debug.Log($"[王国界面] Authored resource rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private bool CaptureVisibleResourceIds(IReadOnlyList<ResourceDef> definitions)
    {
        bool changed = false;
        for (int i = 0; i < definitions.Count; i++)
        {
            ResourceDef resource = definitions[i];
            if (resource == null || visibleResourceIds.Contains(resource.Id) ||
                !IsResourceVisible(resource))
                continue;
            visibleResourceIds.Add(resource.Id);
            changed = true;
        }
        return changed;
    }

    private static int CompareAuthoredRows(
        TechLevel leftTechLevel, string leftId, TechLevel rightTechLevel, string rightId)
    {
        int techComparison = rightTechLevel.CompareTo(leftTechLevel);
        return techComparison != 0
            ? techComparison
            : string.CompareOrdinal(leftId, rightId);
    }

    private static int ComparePinnedAuthoredRows(string kind,
        TechLevel leftTechLevel, string leftId, TechLevel rightTechLevel, string rightId)
    {
        int pin = UIPinPreferences.Compare(kind, leftId, rightId);
        return pin != 0 ? pin : CompareAuthoredRows(leftTechLevel, leftId, rightTechLevel, rightId);
    }

    private void BuildAuthoredBuildingRows(RectTransform parent)
    {
        int visible = 0;
        BuildingManager buildingManager = BuildingManager.Instance;
        IReadOnlyList<BuildingDef> definitions = DataBase<BuildingDef>.All;
        var orderedDefinitions = new List<BuildingDef>();
        for (int i = 0; i < definitions.Count; i++)
        {
            BuildingDef building = definitions[i];
            if (building == null || building is SectorBuildingDef || !ShouldDisplayBuilding(building))
                continue;
            orderedDefinitions.Add(building);
        }
        orderedDefinitions.Sort((left, right) =>
            ComparePinnedAuthoredRows(UIPinPreferences.Building,
                left.TechLevel, left.Id, right.TechLevel, right.Id));
        for (int i = 0; i < orderedDefinitions.Count; i++)
        {
            BuildingDef building = orderedDefinitions[i];
            string amount = "0";
            BuildingState state = null;
            if (buildingManager != null && buildingManager.States.TryGetValue(building, out state))
                amount = state.Amount.ToGameString();
            GameObject row = InstantiateAuthoredRow(GetEraCollectionPrefabName(KingdomUIPrefabLibrary.BuildingCard,
                building.TechLevel), parent, visible++);
            if (row == null)
                continue;
            ApplyEraRowStyle(row, building.TechLevel, visible - 1);
            BindRowPin(row, UIPinPreferences.Building, building.Id);
            Image buildingIcon = row.transform.Find("BuildingIcon")?.GetComponent<Image>();
            BindArtworkIcon(buildingIcon, building.Icon, building.Label);
            if (!SetRowText(row, "Label", building.Label) ||
                !SetRowText(row, "TechLevel", building.TechLevel.GetDescription()) ||
                !SetRowText(row, "Amount", amount) ||
                !SetRowText(row, "Effect", FormatBuildingAvailability(state), TextSecondary))
                continue;
            Button cardButton = RequireRowButton(row);
            if (cardButton == null)
                continue;
            cardButton.onClick.AddListener(() =>
            {
                ShowBuildingDetails(building);
            });

            bool canUpgrade = buildingManager != null &&
                buildingManager.TryGetUnlockedUpgradeTarget(building, out _);
            Button buildButton = RequireChildButton(row, "BuildButton");
            Button deconstructButton = RequireChildButton(row, "DeconstructButton");
            if (buildButton == null || deconstructButton == null)
                continue;
            ExpantaNum buildQuantity = GetSelectedBuildingQuantity(building, canUpgrade, false);
            buildingActionIconUpgradeModes[buildButton] = canUpgrade;
            SetBuildingActionQuantity(buildButton, buildQuantity, canUpgrade ? "升级" : "建造");
            buildButton.onClick.RemoveAllListeners();
            buildButton.onClick.AddListener(() =>
            {
                PerformBuildingAction(building, canUpgrade);
            });
            buildButton.interactable = CanPerformBuildingAction(building, canUpgrade, buildQuantity);
            SetBuildingActionButtonState(buildButton, buildButton.interactable);
            buildingActionButtons[building] = buildButton;
            buildingActionUpgradeModes[building] = canUpgrade;

            bool hasAmount = buildingManager != null &&
                buildingManager.States.TryGetValue(building, out BuildingState buildingState) &&
                buildingState.Amount > ExpantaNum.Zero;
            ExpantaNum deconstructQuantity = GetSelectedBuildingQuantity(building, false, true);
            SetBuildingActionQuantity(deconstructButton, deconstructQuantity, "拆除");
            deconstructButton.onClick.RemoveAllListeners();
            deconstructButton.onClick.AddListener(() =>
            {
                DeconstructBuilding(building);
            });
            deconstructButton.interactable = hasAmount;
            SetBuildingActionButtonState(deconstructButton, hasAmount);
            buildingDeconstructSurfaces[building] = deconstructButton.targetGraphic as Image;
            buildingDeconstructButtons[building] = deconstructButton;
            buildingAmountLabels[building] = row.transform.Find("Amount")?.GetComponent<TMP_Text>();
            buildingEffectLabels[building] = row.transform.Find("Effect")?.GetComponent<TMP_Text>();
        }
        lastBuildingDisplaySignature = BuildBuildingDisplaySignature().ToString();
        Debug.Log($"[王国界面] Authored building rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private const string LandIcon = "<sprite=\"HudTerritory\" index=0>";
    private const string MaterialsIcon = "<sprite=\"BuildingStatusIcons\" index=2>";

    private const string LandShortageIcon = "<sprite=\"BuildingStatusIcons\" index=3>";
    private const string MaterialsShortageIcon = "<sprite=\"BuildingStatusIcons\" index=5>";

    private readonly List<BuildFailure> buildingAvailabilityFailures = new List<BuildFailure>();

    private string FormatBuildingAvailability(BuildingState state)
    {
        if (state == null) return string.Empty;
        BuildingDef building = state.Definition;
        bool upgrade = BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
        ExpantaNum amount = GetSelectedBuildingQuantity(building, upgrade, false);
        BuildingManager.Instance.GetConstructionFailures(building, amount, upgrade, buildingAvailabilityFailures);
        string icons = string.Empty;
        if (buildingAvailabilityFailures.Contains(BuildFailure.SpaceInsufficient)) icons += LandShortageIcon;
        if (icons.Length > 0) return icons;
        if (buildingAvailabilityFailures.Count > 0) return buildingAvailabilityFailures[0] switch
        {
            BuildFailure.ResourceInsufficient => string.Empty,
            BuildFailure.TechnologyInsufficient => "时代未到",
            BuildFailure.ResearchPrerequisiteIncomplete => "待研究", BuildFailure.WorkshopPrerequisiteIncomplete => "待工坊升级",
            BuildFailure.SectorNotOccupied => "待占领", BuildFailure.BuildingTierSuperseded => string.Empty,
            BuildFailure.BuildingLimitReached => "已满额", _ => "不可建"
        };
        return string.Empty;
    }

    private System.Text.StringBuilder BuildBuildingDisplaySignature()
    {
        System.Text.StringBuilder signature = buildingDisplaySignatureBuilder;
        signature.Clear();
        IReadOnlyList<BuildingDef> definitions = DataBase<BuildingDef>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            BuildingDef building = definitions[i];
            if (building != null && !(building is SectorBuildingDef) &&
                ShouldDisplayBuilding(building))
            {
                signature.Append(building.Id).Append(':');
                if (BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out BuildingDef target))
                    signature.Append(target.Id);
                signature.Append(';');
            }
        }
        return signature;
    }

    private void BuildAuthoredWorkshopRows(RectTransform parent)
    {
        int visible = 0;
        workshopPurchaseButtons.Clear();
        IReadOnlyList<WorkshopUpgradeDef> definitions = DataBase<WorkshopUpgradeDef>.All;
        var orderedDefinitions = new List<WorkshopUpgradeDef>();
        for (int i = 0; i < definitions.Count; i++)
        {
            WorkshopUpgradeDef definition = definitions[i];
            if (definition == null || WorkshopManager.Instance == null ||
                !ShouldRevealWorkshop(definition))
                continue;
            orderedDefinitions.Add(definition);
        }
        orderedDefinitions.Sort((left, right) =>
            CompareAuthoredRows(left.TechLevel, left.Id, right.TechLevel, right.Id));
        bool supportInserted = false;
        for (int i = 0; i < orderedDefinitions.Count; i++)
        {
            WorkshopUpgradeDef definition = orderedDefinitions[i];
            if (!supportInserted && CompareAuthoredRows(TechLevel.Spacer, "relic-support", definition.TechLevel, definition.Id) < 0)
            {
                AppendRelicWorkshopRow(parent, ref visible);
                supportInserted = true;
            }
            int index = visible;
            GameObject reusable = index < workshopRows.Count
                ? workshopRows[index]
                : null;
            GameObject row = InstantiateAuthoredRow(
                KingdomUIPrefabLibrary.WorkshopCard, parent, index, reusable);
            if (row == null)
                continue;
            if (index < workshopRows.Count)
                workshopRows[index] = row;
            else
                workshopRows.Add(row);
            visible++;
            ApplyEraRowStyle(row, definition.TechLevel, index);
            Image workshopIcon = row.transform.Find("WorkshopIcon")?.GetComponent<Image>();
            if (workshopIcon == null)
            {
                Debug.LogError("[王国界面] WorkshopCard prefab is missing authored WorkshopIcon Image.");
                continue;
            }
            BindArtworkIcon(workshopIcon, definition.Icon, definition.Label);
            bool purchased = WorkshopManager.Instance.IsPurchased(definition);
            string availability = GetWorkshopAvailability(
                definition, out bool canPurchase);
            if (!SetRowText(row, "Label", definition.Label) ||
                !SetRowText(row, "TechLevel", definition.TechLevel.GetDescription()) ||
                !SetRowText(row, "Effect", availability,
                    purchased ? Positive : canPurchase ? BasicMetalIngot : TextSecondary))
                continue;
            Button cardButton = RequireRowButton(row);
            Button purchaseButton = RequireChildButton(row, "PurchaseButton");
            if (cardButton == null || purchaseButton == null)
                continue;
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() =>
            {
                ShowWorkshopDetails(definition);
            });
            SetBuildingActionButtonText(
                purchaseButton,
                purchased ? "已拥有" : "购买");
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(() =>
            {
                PurchaseWorkshopFromRow(definition);
            });
            purchaseButton.interactable = canPurchase;
            SetBuildingActionButtonState(purchaseButton, purchaseButton.interactable);
            workshopPurchaseButtons[definition] = purchaseButton;
        }
        if (!supportInserted) AppendRelicWorkshopRow(parent, ref visible);
        for (int i = visible; i < workshopRows.Count; i++)
            if (workshopRows[i] != null)
                workshopRows[i].SetActive(false);
        CaptureWorkshopFilterMembershipSignature();
        Debug.Log($"[王国界面] Authored workshop rows: visible={visible}, pooled={workshopRows.Count}, rowsRect={parent.rect.size}");
    }

    private bool ShouldRevealWorkshop(WorkshopUpgradeDef definition)
    {
        WorkshopManager manager = WorkshopManager.Instance;
        GameManager game = GameManager.Instance;
        if (definition == null || manager == null || game == null)
            return false;
        if (manager.IsPurchased(definition))
            return showOwned && !affordableOnly;
        bool unlocked = manager.IsSystemUnlocked &&
            definition.TechLevel <= game.State.TechLevel &&
            manager.ArePrerequisitesMet(definition);
        if (!unlocked || !affordableOnly)
            return unlocked;
        GetWorkshopAvailability(definition, out bool canPurchase);
        return canPurchase;
    }


    private void RefreshWorkshopPurchaseButtonStates()
    {
        WorkshopManager manager = WorkshopManager.Instance;
        if (manager == null)
            return;
        foreach (KeyValuePair<WorkshopUpgradeDef, Button> pair in workshopPurchaseButtons)
        {
            WorkshopUpgradeDef definition = pair.Key;
            Button purchaseButton = pair.Value;
            if (definition == null || purchaseButton == null ||
                !purchaseButton.gameObject.activeInHierarchy)
                continue;
            bool purchased = manager.IsPurchased(definition);
            string availability = GetWorkshopAvailability(definition, out bool canPurchase);
            SetRowText(purchaseButton.transform.parent.gameObject, "Effect", availability,
                purchased ? Positive : canPurchase ? BasicMetalIngot : TextSecondary);
            SetBuildingActionButtonText(
                purchaseButton,
                purchased ? "已拥有" : "购买");
            purchaseButton.interactable = canPurchase;
            SetBuildingActionButtonState(purchaseButton, canPurchase);
        }
    }

    private GameObject InstantiateAuthoredRow(string prefab, RectTransform parent, int index,
        GameObject reusable = null)
    {
        GameObject row = reusable == null
            ? KingdomUIPrefabLibrary.Instantiate(prefab, parent)
            : reusable;
        if (row == null)
        {
            Debug.LogError("[王国界面] Required authored row prefab is unavailable: " + prefab);
            return null;
        }
        RectTransform rect = row.GetComponent<RectTransform>();
        LayoutElement layout = row.GetComponent<LayoutElement>();
        if (rect == null || layout == null || layout.preferredHeight <= 1f)
        {
            Debug.LogError("[王国界面] Authored row prefab LayoutElement is incomplete: " + prefab);
            Destroy(row);
            return null;
        }
        float top = -nextRowTop;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, layout.preferredHeight);
        rect.anchoredPosition = new Vector2(0f, top);
        nextRowTop += layout.preferredHeight;
        row.name = prefab.Substring(prefab.LastIndexOf('/') + 1) + "_" + index;
        // Row prefabs own the drag-forwarder component.  Do not add it here:
        // page scrolling must remain authored consistently across Resources,
        // Buildings and Era, rather than being repaired differently at runtime.
        row.SetActive(true);
        return row;
    }

    private static bool SetRowText(GameObject row, string childName, string value, Color? color = null)
    {
        Transform child = row.transform.Find(childName);
        TMP_Text text = child == null ? null : child.GetComponent<TMP_Text>();
        if (text == null)
        {
            Debug.LogError("[王国界面] Authored row is missing text child: " + childName);
            return false;
        }
        text.text = value ?? string.Empty;
        if (color.HasValue)
            text.color = color.Value;
        text.enabled = true;
        text.gameObject.SetActive(true);
        text.raycastTarget = false;
        return true;
    }

    private static Button RequireRowButton(GameObject row)
    {
        Button button = row.GetComponent<Button>();
        if (button == null)
            Debug.LogError("[王国界面] Authored row is missing its root Button: " + row.name);
        else if (!button.TryGetComponent<UIButtonClickFeedback>(out _))
            button.gameObject.AddComponent<UIButtonClickFeedback>();
        return button;
    }

    private static Button RequireChildButton(GameObject row, string childName)
    {
        Transform child = row.transform.Find(childName);
        Button button = child == null ? null : child.GetComponent<Button>();
        if (button == null)
            Debug.LogError("[王国界面] Authored building card is missing Button: " + childName);
        else if (!button.TryGetComponent<UIButtonClickFeedback>(out _))
            button.gameObject.AddComponent<UIButtonClickFeedback>();
        return button;
    }
}
