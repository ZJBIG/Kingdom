using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceDynamicData
{
    public BigNumber Amount;
    public BigNumber GrowthRate;
    public ResourceDynamicData(BigNumber Amount, BigNumber GrowthRate)
    {
        this.Amount = Amount;
        this.GrowthRate = GrowthRate;
    }
}
