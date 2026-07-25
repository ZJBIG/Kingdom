using System.Collections.Generic;
using NUnit.Framework;

public sealed class SectorMapViewerTests
{
    [Test]
    public void C703_AccessStatusRequiresOccupiedPrerequisites()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        var lowState = new SectorState(lowOrbit);
        var moonState = new SectorState(moon);
        var states = new Dictionary<SectorDefinition, SectorState>
        {
            [lowOrbit] = lowState,
            [moon] = moonState
        };

        Assert.That(SectorMapViewer.GetAccessStatus(moonState, states), Is.EqualTo(SectorAccessStatus.Locked));
        lowState.SetOccupiedForEditor(true);
        Assert.That(SectorMapViewer.GetAccessStatus(moonState, states), Is.EqualTo(SectorAccessStatus.Available));
        moonState.SetUnlockedForEditor(true);
        Assert.That(SectorMapViewer.GetAccessStatus(moonState, states), Is.EqualTo(SectorAccessStatus.Unlocked));
        moonState.SetOccupiedForEditor(true);
        Assert.That(SectorMapViewer.GetAccessStatus(moonState, states), Is.EqualTo(SectorAccessStatus.Occupied));
    }

    [Test]
    public void C703_NullAndEmptyPrerequisitesAreAvailable()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        var state = new SectorState(lowOrbit);

        Assert.That(
            SectorMapViewer.GetAccessStatus(
                state,
                new Dictionary<SectorDefinition, SectorState> { [lowOrbit] = state }),
            Is.EqualTo(SectorAccessStatus.Available));
        Assert.That(SectorMapViewer.GetAccessStatus(null, null), Is.EqualTo(SectorAccessStatus.Locked));
    }
}
