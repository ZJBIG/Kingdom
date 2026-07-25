using System;
using UnityEngine;

public enum MainTab
{
    Resource,
    Building,
    Research
}

public sealed class MainNavigationViewer : MonoBehaviour
{
    [SerializeField] private GameHudViewer HudViewer;
    [SerializeField] private RectTransform ResourceViewer;
    [SerializeField] private RectTransform BuildingViewer;
    [SerializeField] private RectTransform ResearchViewer;
    [SerializeField] private RectTransform SpecialViewer;
    [SerializeField] private RectTransform SettingViewer;

    private MainTab currentTab = MainTab.Resource;

    public MainTab CurrentTab => currentTab;
    public event Action<MainTab> MainTabChanged;

    private void Awake()
    {
        if (HudViewer == null)
            HudViewer = GetComponent<GameHudViewer>();

        SetMainTab(MainTab.Resource);
    }

    public void SwitchTop()
    {
        SetMainTab(currentTab switch
        {
            MainTab.Resource => MainTab.Building,
            MainTab.Building => MainTab.Research,
            _ => MainTab.Resource
        });
    }

    public void SetMainTab(MainTab tab)
    {
        bool changed = currentTab != tab;
        currentTab = tab;
        SetActive(ResourceViewer, tab == MainTab.Resource);
        SetActive(BuildingViewer, tab == MainTab.Building);
        SetActive(ResearchViewer, tab == MainTab.Research);
        SetActive(SpecialViewer, false);
        SetActive(SettingViewer, false);
        HudViewer?.SetMainTab(currentTab);
        if (changed)
            MainTabChanged?.Invoke(currentTab);
    }

    private static void SetActive(RectTransform viewer, bool active)
    {
        if (viewer != null && viewer.gameObject.activeSelf != active)
            viewer.gameObject.SetActive(active);
    }
}
