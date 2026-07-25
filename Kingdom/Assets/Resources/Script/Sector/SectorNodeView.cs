using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SectorNodeView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text enemyPower;
    [SerializeField] private Button selectButton;

    private SectorState state;
    private Action<SectorDefinition> selectAction;

    public SectorDefinition Sector => state?.Definition;
    public SectorState BoundState => state;

    public void Bind(SectorState newState, Action<SectorDefinition> onSelect)
    {
        state = newState ?? throw new ArgumentNullException(nameof(newState));
        selectAction = onSelect;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(Select);
            selectButton.onClick.AddListener(Select);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (state == null)
            return;

        if (label != null)
            label.text = string.IsNullOrWhiteSpace(state.Definition.Label)
                ? state.Definition.Id
                : state.Definition.Label;
        if (status != null)
            status.text = GetStatusText(state);
        if (enemyPower != null)
            enemyPower.text = state.Definition.EnemyPower.ToGameString();
    }

    private void Select() => selectAction?.Invoke(state?.Definition);

    private static string GetStatusText(SectorState value)
    {
        if (value.Occupied)
            return "已占领";
        if (value.Unlocked)
            return "已解锁";
        return "未解锁";
    }
}
