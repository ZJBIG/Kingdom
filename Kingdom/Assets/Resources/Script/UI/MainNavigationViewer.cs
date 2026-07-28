using System;
using UnityEngine;

public enum MainTab
{
    Resource,
    Building,
    Research,
    Workshop,
}

public sealed class MainNavigationViewer : MonoBehaviour
{
    [SerializeField] private GameHudViewer HudViewer;
    [SerializeField] private RectTransform ResourceViewer;
    [SerializeField] private RectTransform BuildingViewer;
    [SerializeField] private RectTransform ResearchViewer;
    [SerializeField] private RectTransform WorkshopViewer;
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
            MainTab.Research => IsWorkshopAvailable() ? MainTab.Workshop : MainTab.Resource,
            MainTab.Workshop => MainTab.Resource,
            _ => MainTab.Resource
        });
    }

    public void SetMainTab(MainTab tab)
    {
        if (tab == MainTab.Workshop && !IsWorkshopAvailable())
            tab = MainTab.Research;
        bool changed = currentTab != tab;
        currentTab = tab;
        SetActive(ResourceViewer, tab == MainTab.Resource);
        SetActive(BuildingViewer, tab == MainTab.Building);
        SetActive(ResearchViewer, tab == MainTab.Research);
        SetActive(WorkshopViewer, tab == MainTab.Workshop);
        SetActive(SettingViewer, false);
        HudViewer?.SetMainTab(currentTab);
        if (changed)
            MainTabChanged?.Invoke(currentTab);
    }

    private static bool IsWorkshopAvailable()
    {
        WorkshopManager manager = FindObjectOfType<WorkshopManager>();
        return manager != null && manager.IsSystemUnlocked;
    }

    private static void SetActive(RectTransform viewer, bool active)
    {
        if (viewer != null && viewer.gameObject.activeSelf != active)
            viewer.gameObject.SetActive(active);
    }
}
