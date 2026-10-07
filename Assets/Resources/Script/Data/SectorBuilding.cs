using UnityEngine;

[CreateAssetMenu(fileName = "\u521b\u5efa\u661f\u533a\u5efa\u7b51", menuName = "\u6570\u636e/\u661f\u533a\u5efa\u7b51", order = 0)]
public class SectorBuilding : Building
{
    [SerializeField]
    private SectorDefinition sector = null;

    [SerializeField, Min(1)]
    private int maxAmount = 1;

    public SectorDefinition Sector => sector;
    public int MaxAmount => maxAmount;

#if UNITY_EDITOR
    public void SetSectorForEditor(SectorDefinition value) => sector = value;
    public void SetMaxAmountForEditor(int value) => maxAmount = value;
#endif

    public static void Validate(SectorBuilding building)
    {
        if (building == null)
            throw new System.ArgumentNullException(nameof(building));
        if (building.sector == null)
            throw new System.InvalidOperationException(
                $"\u661f\u533a\u5efa\u7b51\\u201c{building.Id}\\u201d\\u672a\\u7ed1\\u5b9a\\u661f\\u533a\\u3002");
        if (building.maxAmount < 1)
            throw new System.InvalidOperationException(
                $"\u661f\u533a\u5efa\u7b51\\u201c{building.Id}\\u201d\\u7684\\u5efa\\u9020\\u4e0a\\u9650\\u65e0\\u6548\\u3002");
        if (building.UpgradeTo != null)
            throw new System.InvalidOperationException(
                $"\u661f\u533a\u5efa\u7b51\\u201c{building.Id}\\u201d\\u4e0d\\u80fd\\u53c2\\u4e0e\\u666e\\u901a\\u5efa\\u7b51\\u5347\\u7ea7\\u94fe\\u3002");
        if (building.SpaceCost != ExpantaNum.Zero)
            throw new System.InvalidOperationException(
                $"\u661f\u533a\u5efa\u7b51\\u201c{building.Id}\\u201d\\u4e0d\\u80fd\\u4f7f\\u7528\\u672c\\u571f\\u9886\\u571f\\u6210\\u672c\\u3002");
    }
}
