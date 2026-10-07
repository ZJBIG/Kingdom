using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class AudioFeedbackRegressionTests
{
    private const string VolumeKey = "Kingdom.Sfx.Volume";
    private const string MuteKey = "Kingdom.Sfx.Mute";
    private float previousVolume;
    private int previousMute;
    private GameObject managerObject;

    [SetUp]
    public void SetUp()
    {
        previousVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        previousMute = PlayerPrefs.GetInt(MuteKey, 0);
        PlayerPrefs.DeleteKey(VolumeKey);
        PlayerPrefs.DeleteKey(MuteKey);
        PlayerPrefs.Save();
    }

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
            Object.DestroyImmediate(managerObject);
        PlayerPrefs.SetFloat(VolumeKey, previousVolume);
        PlayerPrefs.SetInt(MuteKey, previousMute);
        PlayerPrefs.Save();
    }

    [Test]
    public void SfxVolumeAndMutePersistAcrossManagerRecreation()
    {
        managerObject = new GameObject("Sfx-Settings-Test");
        managerObject.AddComponent<UIButtonSoundManager>();
        UIButtonSoundManager.SetVolume(.27f);
        UIButtonSoundManager.SetMuted(true);

        Object.DestroyImmediate(managerObject);
        managerObject = null;

        Assert.That(UIButtonSoundManager.SfxVolume, Is.EqualTo(.27f).Within(.0001f));
        Assert.That(UIButtonSoundManager.SfxMuted, Is.True);
    }

    [Test]
    public void AuthoredMusicSurfaceContainsIndependentSfxControls()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Assert.That(prefab, Is.Not.Null);
        Transform surface = null;
        foreach (Transform candidate in prefab.GetComponentsInChildren<Transform>(true))
            if (candidate.name == "MusicSurface")
            {
                surface = candidate;
                break;
            }
        Transform controls = surface?.Find("Controls");
        Assert.That(controls, Is.Not.Null);
        Slider sfxVolume = controls.Find("SfxVolume")?.GetComponent<Slider>();
        Button sfxMute = controls.Find("SfxMute")?.GetComponent<Button>();
        Assert.That(sfxVolume, Is.Not.Null);
        Assert.That(sfxVolume.fillRect, Is.Not.Null);
        Assert.That(sfxVolume.handleRect, Is.Not.Null);
        Assert.That(sfxMute, Is.Not.Null);
        Assert.That(sfxMute.targetGraphic, Is.Not.Null);
        Assert.That(controls.Find("SfxVolumeValue")?.GetComponent<TMP_Text>(), Is.Not.Null);
    }

    [Test]
    public void TypedPlayRequests_ReportOneRequestPerSuccessfulIntent()
    {
        managerObject = new GameObject("Sfx-Request-Test");
        managerObject.AddComponent<UIButtonSoundManager>();
        var requests = new List<UIButtonSoundManager.Sound>();
        Action<UIButtonSoundManager.Sound> observer = requests.Add;
        UIButtonSoundManager.PlayRequested += observer;
        try
        {
            // A successful single operation emits its typed result sound once.
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Build);
            Assert.That(requests, Is.EqualTo(new[] { UIButtonSoundManager.Sound.Build }));

            // A batch operation is represented by one successful UI intent,
            // therefore one caller request, rather than one request per item.
            requests.Clear();
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Upgrade);
            Assert.That(requests, Is.EqualTo(new[] { UIButtonSoundManager.Sound.Upgrade }));
        }
        finally
        {
            UIButtonSoundManager.PlayRequested -= observer;
        }
    }

    [Test]
    public void MutingChangesOutputVolumeWhileTypedRequestsRemainObservable()
    {
        managerObject = new GameObject("Sfx-Mute-Test");
        managerObject.AddComponent<UIButtonSoundManager>();
        UIButtonSoundManager.EnsureInitialized();
        AudioSource source = managerObject.GetComponent<AudioSource>();
        Assert.That(source, Is.Not.Null);

        UIButtonSoundManager.SetVolume(.5f);
        Assert.That(source.volume, Is.EqualTo(.21f).Within(.0001f));
        UIButtonSoundManager.SetMuted(true);
        Assert.That(source.volume, Is.EqualTo(0f).Within(.0001f));
        var requests = new List<UIButtonSoundManager.Sound>();
        Action<UIButtonSoundManager.Sound> observer = requests.Add;
        UIButtonSoundManager.PlayRequested += observer;
        try
        {
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
            Assert.That(requests, Is.EqualTo(new[] { UIButtonSoundManager.Sound.Detail }));
        }
        finally
        {
            UIButtonSoundManager.PlayRequested -= observer;
        }
        UIButtonSoundManager.SetMuted(false);
        Assert.That(source.volume, Is.EqualTo(.21f).Within(.0001f));
    }
}
