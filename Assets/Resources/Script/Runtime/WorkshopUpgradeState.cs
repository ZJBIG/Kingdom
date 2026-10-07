using System;

[Serializable]
public sealed class WorkshopUpgradeState
{
    public WorkshopUpgrade Definition { get; }
    public bool Purchased { get; private set; }
    public int Version { get; private set; }

    public WorkshopUpgradeState(WorkshopUpgrade definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    internal void SetPurchased(bool value)
    {
        if (Purchased == value)
            return;
        Purchased = value;
        Version++;
    }

#if UNITY_EDITOR
    public void SetPurchasedForEditor(bool value) => SetPurchased(value);
#endif
}
