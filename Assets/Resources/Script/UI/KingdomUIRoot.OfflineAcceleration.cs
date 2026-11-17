using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private RectTransform offlineAccelerationToolbar;
    private TMP_Text remainingOfflineTime;
    private readonly Button[] accelerationButtons = new Button[4];
    private readonly Outline[] accelerationSelectionOutlines = new Outline[4];
    private static readonly int[] AccelerationChoices = { 1, 5, 20, 100 };
    private long displayedOfflineSeconds = -1;
    private int displayedAcceleration;

    private void BindOfflineAccelerationToolbar()
    {
        offlineAccelerationToolbar = safeArea.Find("NotificationBar/OfflineAccelerationToolbar") as RectTransform;
        if (offlineAccelerationToolbar == null)
        {
            Debug.LogError("[王国界面] Missing authored OfflineAccelerationToolbar.");
            return;
        }
        remainingOfflineTime = offlineAccelerationToolbar.Find("RemainingTime").GetComponent<TMP_Text>();
        string[] names = { "Normal", "Speed5", "Speed20", "Speed100" };
        for (int i = 0; i < names.Length; i++)
        {
            int multiplier = AccelerationChoices[i];
            accelerationButtons[i] = offlineAccelerationToolbar.Find(names[i]).GetComponent<Button>();
            accelerationSelectionOutlines[i] = accelerationButtons[i].GetComponent<Outline>();
            accelerationButtons[i].onClick.AddListener(() =>
            {
                SimulationManager.Instance.SetAccelerationMultiplier(multiplier);
                RefreshOfflineAccelerationToolbar();
            });
        }
        RefreshOfflineAccelerationToolbar();
    }

    private void RefreshOfflineAccelerationToolbar()
    {
        if (offlineAccelerationToolbar == null) return;
        if (!SaveManager.TryGetInstance(out SaveManager saves) ||
            !SimulationManager.TryGetInstance(out SimulationManager simulation)) return;
        long seconds = (long)Math.Ceiling(saves.RemainingOfflineSeconds);
        int multiplier = simulation.AccelerationMultiplier;
        if (seconds == displayedOfflineSeconds && multiplier == displayedAcceleration) return;
        displayedOfflineSeconds = seconds;
        displayedAcceleration = multiplier;
        remainingOfflineTime.text = $"离线时间\n<mspace=0.6em>{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}</mspace>";
        for (int i = 0; i < accelerationButtons.Length; i++)
        {
            Button button = accelerationButtons[i];
            button.interactable = AccelerationChoices[i] == 1 || seconds > 0;
            button.image.color = AccelerationChoices[i] == multiplier ? BuildableActionColor : PanelRaised;
            accelerationSelectionOutlines[i].enabled = AccelerationChoices[i] == multiplier;
        }
    }
}
