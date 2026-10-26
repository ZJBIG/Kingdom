using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class SoundPalettePlayModeTests
{
    private GameObject owner;
    private bool hadVolume;
    private bool hadMute;
    private float previousVolume;
    private int previousMute;
    private bool preferencesCaptured;

    [SetUp]
    public void SetUp()
    {
        owner = null;
        preferencesCaptured = false;
        Assert.That(Object.FindObjectOfType<UIButtonSoundManager>(), Is.Null);
        hadVolume = PlayerPrefs.HasKey("Kingdom.Sfx.Volume");
        hadMute = PlayerPrefs.HasKey("Kingdom.Sfx.Mute");
        previousVolume = PlayerPrefs.GetFloat("Kingdom.Sfx.Volume", 1f);
        previousMute = PlayerPrefs.GetInt("Kingdom.Sfx.Mute", 0);
        preferencesCaptured = true;
        owner = new GameObject("SoundPaletteTest");
        owner.AddComponent<AudioListener>();
        owner.AddComponent<UIButtonSoundManager>();
        UIButtonSoundManager.SetVolume(1f);
        UIButtonSoundManager.SetMuted(false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            if (owner != null) Object.DestroyImmediate(owner);
            owner = null;
        }
        finally
        {
            // SetUp can fail before a preference snapshot exists.
            if (preferencesCaptured)
            {
                if (hadVolume) PlayerPrefs.SetFloat("Kingdom.Sfx.Volume", previousVolume);
                else PlayerPrefs.DeleteKey("Kingdom.Sfx.Volume");
                if (hadMute) PlayerPrefs.SetInt("Kingdom.Sfx.Mute", previousMute);
                else PlayerPrefs.DeleteKey("Kingdom.Sfx.Mute");
                PlayerPrefs.Save();
                preferencesCaptured = false;
            }
        }
        yield return null;
    }

    private AudioSource Output(UIButtonSoundManager.Sound sound)
    {
        int priority = sound == UIButtonSoundManager.Sound.ResearchComplete ||
            sound == UIButtonSoundManager.Sound.EraBreakthrough || sound == UIButtonSoundManager.Sound.StrategicCommit ? 64 :
            sound == UIButtonSoundManager.Sound.Click || sound == UIButtonSoundManager.Sound.Detail ? 160 : 128;
        foreach (AudioSource output in owner.GetComponents<AudioSource>())
            if (output.priority == priority) return output;
        throw new InvalidOperationException("Missing sound output");
    }

    [Test]
    public void EveryEventHasAudibleBoundedDistinctVariantsAndExportsPreview()
    {
        var preview = new List<float>();
        var names = new List<string>();
        // Enum values and three variations are discrete protocol counts.
        for (int eventIndex = 0; eventIndex <= (int)UIButtonSoundManager.Sound.Click; eventIndex++)
        {
            var sound = (UIButtonSoundManager.Sound)eventIndex;
            var selected = new HashSet<AudioClip>();
            float[] previous = null;
            for (int variant = 0; variant < 3; variant++)
            {
                Output(sound).Stop();
                UIButtonSoundManager.Play(sound);
                AudioClip clip = Output(sound).clip;
                Assert.That(clip.name, Does.StartWith("SFX_" + sound + "_"));
                Assert.That(selected.Add(clip), Is.True, "Repeated operations need variation.");
                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True);
                float peak = 0f;
                double energy = 0;
                double difference = 0;
                bool finite = true;
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = samples[i];
                    finite &= !float.IsNaN(sample) && !float.IsInfinity(sample);
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                    energy += sample * sample;
                    if (previous != null) difference += Math.Abs(sample - previous[i]);
                }
                Assert.That(finite, Is.True, clip.name);
                if (previous != null)
                    Assert.That(difference / samples.Length, Is.GreaterThan(.0001), "Variants must sound different, not just have different names.");
                previous = samples;
                Assert.That(peak, Is.InRange(.01f, .85f), clip.name);
                Assert.That(energy / samples.Length, Is.GreaterThan(.00001), clip.name);
                Assert.That(Mathf.Abs(samples[0]), Is.LessThan(.0001f));
                Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(.0001f));
                if (variant == 0)
                {
                    names.Add(sound.ToString());
                    foreach (float sample in samples) preview.Add(sample * Output(sound).volume);
                    preview.AddRange(new float[11025]);
                }
            }
        }
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../data/player-qa-fixes-20261025/audio"));
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "preview-order.txt"), names);
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "sound-palette.wav"))))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + preview.Count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(22050); writer.Write(44100); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(preview.Count * 2);
            foreach (float sample in preview) writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767));
        }
    }

    [Test]
    public void RapidActionsStayWithinThreeVoicesAndDoNotInterruptEra()
    {
        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.EraBreakthrough);
        AudioSource milestone = Output(UIButtonSoundManager.Sound.EraBreakthrough);
        AudioClip era = milestone.clip;
        for (int i = 0; i < 30; i++)
        {
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Click);
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Build);
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.ResearchComplete);
        }
        Assert.That(milestone.clip, Is.SameAs(era));
        Assert.That(owner.GetComponents<AudioSource>().Length, Is.EqualTo(3), "The voice budget is a discrete limit.");
        Assert.That(Output(UIButtonSoundManager.Sound.Click).clip.name, Does.StartWith("SFX_Click_"));
        Assert.That(Output(UIButtonSoundManager.Sound.Build).clip.name, Does.StartWith("SFX_Build_"));
    }

    [Test]
    public void VolumeAndMuteApplyToAllVoicesWithoutLosingRequestFeedback()
    {
        UIButtonSoundManager.SetVolume(.5f);
        foreach (AudioSource output in owner.GetComponents<AudioSource>())
            Assert.That(output.volume, Is.EqualTo(.21f).Within(.0001f));
        UIButtonSoundManager.SetMuted(true);
        int requests = 0;
        Action<UIButtonSoundManager.Sound> observer = sound => requests++;
        UIButtonSoundManager.PlayRequested += observer;
        try { UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Build); }
        finally { UIButtonSoundManager.PlayRequested -= observer; }
        Assert.That(requests, Is.EqualTo(1), "One input emits one request, including while muted.");
        foreach (AudioSource output in owner.GetComponents<AudioSource>())
        {
            Assert.That(output.volume, Is.LessThan(.0001f));
            Assert.That(output.clip, Is.Null, "Muted requests must not start a new clip.");
        }
        UIButtonSoundManager.SetMuted(false);
        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Build);
        Assert.That(Output(UIButtonSoundManager.Sound.Build).clip, Is.Not.Null);
    }
}

public sealed class SoundPaletteFixtureSafetyPlayModeTests
{
    [UnityTest]
    public IEnumerator FailedSetupBeforeSnapshotLeavesExistingPreferencesUntouched()
    {
        Assert.That(Object.FindObjectOfType<UIButtonSoundManager>(), Is.Null);
        bool hadVolume = PlayerPrefs.HasKey("Kingdom.Sfx.Volume");
        bool hadMute = PlayerPrefs.HasKey("Kingdom.Sfx.Mute");
        float volume = PlayerPrefs.GetFloat("Kingdom.Sfx.Volume", 1f);
        int mute = PlayerPrefs.GetInt("Kingdom.Sfx.Mute", 0);
        GameObject blocker = new GameObject("SoundPaletteSetupBlocker");
        try
        {
            blocker.AddComponent<UIButtonSoundManager>();
            PlayerPrefs.SetFloat("Kingdom.Sfx.Volume", .37f);
            PlayerPrefs.SetInt("Kingdom.Sfx.Mute", 1);
            var fixture = new SoundPalettePlayModeTests();
            Assert.Throws<AssertionException>(() => fixture.SetUp());
            IEnumerator cleanup = fixture.TearDown();
            while (cleanup.MoveNext()) yield return cleanup.Current;
            Assert.That(PlayerPrefs.HasKey("Kingdom.Sfx.Volume"), Is.True);
            Assert.That(PlayerPrefs.HasKey("Kingdom.Sfx.Mute"), Is.True);
            Assert.That(PlayerPrefs.GetFloat("Kingdom.Sfx.Volume"), Is.EqualTo(.37f).Within(.0001f));
            Assert.That(PlayerPrefs.GetInt("Kingdom.Sfx.Mute"), Is.EqualTo(1), "Mute is a stored boolean protocol value.");
            Assert.That(blocker, Is.Not.Null, "Failed setup must preserve the pre-existing sound manager.");
        }
        finally
        {
            if (blocker != null) Object.DestroyImmediate(blocker);
            if (hadVolume) PlayerPrefs.SetFloat("Kingdom.Sfx.Volume", volume);
            else PlayerPrefs.DeleteKey("Kingdom.Sfx.Volume");
            if (hadMute) PlayerPrefs.SetInt("Kingdom.Sfx.Mute", mute);
            else PlayerPrefs.DeleteKey("Kingdom.Sfx.Mute");
            PlayerPrefs.Save();
        }
    }
}
