using System;

[Serializable]
public sealed class WorkshopUpgradeState
{
    public WorkshopUpgradeDefinition Definition { get; }
    public bool Purchased { get; private set; }
    public int Version { get; private set; }

    public WorkshopUpgradeState(WorkshopUpgradeDefinition definition)
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
}
