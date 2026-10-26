using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIBuildingActionIcon : MonoBehaviour
{
    [SerializeField] private GameObject buildIcon;
    [SerializeField] private GameObject upgradeIcon;
    [SerializeField] private GameObject pauseIcon;
    [SerializeField] private GameObject resumeIcon;

    public void SetUpgrade(bool upgrade)
    {
        if (buildIcon != null && buildIcon.activeSelf == upgrade) buildIcon.SetActive(!upgrade);
        if (upgradeIcon != null && upgradeIcon.activeSelf != upgrade) upgradeIcon.SetActive(upgrade);
    }

    public void SetPaused(bool paused)
    {
        if (pauseIcon != null && pauseIcon.activeSelf == paused) pauseIcon.SetActive(!paused);
        if (resumeIcon != null && resumeIcon.activeSelf != paused) resumeIcon.SetActive(paused);
    }
}
