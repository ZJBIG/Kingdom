using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private int lastRelicWorkshopSignature = -1;

    private int GetRelicWorkshopSignature()
    {
        if (!GameManager.TryGetInstance(out GameManager game) ||
            !WorkshopManager.TryGetInstance(out WorkshopManager workshop))
            return -1;
        game.Relic.CanCraftSupport(out RelicOperationFailure failure);
        return unchecked(game.Relic.State.Version * 31 + (int)failure * 2 +
            (workshop.IsSystemUnlocked ? 1 : 0));
    }

    private void AppendRelicWorkshopRow(RectTransform parent, ref int visible)
    {
        GameManager game = GameManager.Instance;
        RelicState state = game.Relic.State;
        lastRelicWorkshopSignature = GetRelicWorkshopSignature();
        if (state.Route != RelicRoute.Dismantle || state.Status != RelicStatus.Operational)
            return;
        bool canCraft = WorkshopManager.Instance.IsSystemUnlocked &&
            game.Relic.CanCraftSupport(out _);
        if (affordableOnly && !canCraft)
            return;
        int index = visible;
        GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.WorkshopCard, parent,
            index, index < workshopRows.Count ? workshopRows[index] : null);
        if (row == null)
            return;
        if (index < workshopRows.Count) workshopRows[index] = row;
        else workshopRows.Add(row);
        visible++;
        row.name = "RelicSupportWorkshopRow";
        ApplyEraRowStyle(row, TechLevel.Spacer, index);
        Image icon = row.transform.Find("WorkshopIcon")?.GetComponent<Image>();
        if (icon != null) BindArtworkIcon(icon, game.Relic.Definition.Sector.Icon, "远征支援组件");
        SetRowText(row, "Label", "远征支援组件 · 自主制造");
        SetRowText(row, "TechLevel", TechLevel.Spacer.GetDescription());
        SetRowText(row, "Effect", state.SupportReady || !string.IsNullOrEmpty(state.SupportedSectorId)
            ? "已有待用或服役支援" : "单次制造 · 降低一场远星战役补给", TextSecondary);
        Button card = RequireRowButton(row);
        Button purchase = RequireChildButton(row, "PurchaseButton");
        if (card == null || purchase == null)
            return;
        SetBuildingActionButtonText(purchase, "制造支援");
        purchase.interactable = canCraft;
        SetBuildingActionButtonState(purchase, canCraft);
        card.onClick.RemoveAllListeners();
        card.onClick.AddListener(() => ShowSectorDetails(game.Relic.Definition.Sector,
            game.Sectors, game.State, ResourceManager.Instance));
        purchase.onClick.RemoveAllListeners();
        purchase.onClick.AddListener(() =>
        {
            if (WorkshopManager.Instance.TryCraftRelicSupport(out _))
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.WorkshopPurchase);
            workshopRowsUiDirty = true;
            ShowSectorDetails(game.Relic.Definition.Sector, game.Sectors, game.State, ResourceManager.Instance);
        });
    }
}
