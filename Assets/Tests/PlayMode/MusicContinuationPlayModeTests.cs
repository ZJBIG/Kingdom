using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class MusicContinuationPlayModeTests
{
    private GameObject owner;
    private MusicManager manager;
    private bool hadGap;
    private float previousGap;

    [SetUp]
    public void SetUp()
    {
        owner = null;
        hadGap = PlayerPrefs.HasKey("Kingdom.Music.GapSeconds");
        previousGap = PlayerPrefs.GetFloat("Kingdom.Music.GapSeconds", 5f);
        Assert.That(Object.FindObjectOfType<MusicManager>(), Is.Null,
            "This isolated playback fixture must not replace a live music manager.");
        owner = new GameObject("MusicContinuationTest");
        manager = owner.AddComponent<MusicManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (owner != null) Object.DestroyImmediate(owner);
        if (hadGap) PlayerPrefs.SetFloat("Kingdom.Music.GapSeconds", previousGap);
        else PlayerPrefs.DeleteKey("Kingdom.Music.GapSeconds");
        PlayerPrefs.Save();
    }

    private IEnumerator WaitForPlaying()
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!manager.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(manager.IsPlaying, Is.True, "A real local Addressables track must play.");
        Assert.That(manager.Tracks.Count, Is.GreaterThan(1), "Random next-track coverage needs multiple real tracks.");
    }

    [UnityTest]
    public IEnumerator ResetAudioCursorStillContinuesAfterConfiguredGap()
    {
        yield return WaitForPlaying();
        manager.SetGapSeconds(.5f);
        MusicManager.MusicTrack previous = manager.CurrentTrack;
        // Model the cursor Unity exposes after completion: stopped and reset.
        // Manager.Stop is deliberately not used because it requests a manual stop.
        manager.AudioSource.Stop();
        Assert.That(manager.AudioSource.time, Is.LessThan(.1f));
        float stoppedAt = Time.realtimeSinceStartup;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(manager.CurrentTrack, Is.SameAs(previous), "The user-selected gap must be respected.");
        float deadline = stoppedAt + 15f;
        while ((manager.CurrentTrack == previous || !manager.IsPlaying) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(manager.CurrentTrack, Is.Not.SameAs(previous));
        Assert.That(manager.IsPlaying, Is.True);
        Assert.That(Time.realtimeSinceStartup - stoppedAt, Is.GreaterThanOrEqualTo(.5f));
        manager.Stop();
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(manager.IsPermanentlyStopped, Is.True, "A manual stop must not auto-resume.");
    }

    [UnityTest]
    public IEnumerator ManualPausePreservesTrackAndCursorUntilResume()
    {
        yield return WaitForPlaying();
        MusicManager.MusicTrack track = manager.CurrentTrack;
        manager.Pause();
        float pausedAt = manager.AudioSource.time;
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(manager.IsPaused, Is.True);
        Assert.That(manager.AudioSource.isPlaying, Is.False);
        Assert.That(manager.CurrentTrack, Is.SameAs(track));
        Assert.That(manager.AudioSource.time, Is.EqualTo(pausedAt).Within(.05f));

        manager.Resume();
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(manager.IsPlaying, Is.True);
        Assert.That(manager.CurrentTrack, Is.SameAs(track));
        Assert.That(manager.AudioSource.time, Is.GreaterThan(pausedAt));
    }

    [UnityTest]
    public IEnumerator NaturalEndAfterSeekStartsAnotherRandomTrack()
    {
        yield return WaitForPlaying();
        manager.SetGapSeconds(0f);
        MusicManager.MusicTrack previous = manager.CurrentTrack;
        manager.SeekNormalized(1f);
        float deadline = Time.realtimeSinceStartup + 15f;
        while ((manager.CurrentTrack == previous || !manager.IsPlaying) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(manager.CurrentTrack, Is.Not.SameAs(previous));
        Assert.That(manager.IsPlaying, Is.True);
    }
}

public sealed class MusicContinuationFixtureSafetyPlayModeTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void FailedSetupPreservesGapPreferenceAndExistingManager(bool hasGap)
    {
        Assert.That(Object.FindObjectOfType<MusicManager>(), Is.Null);
        const string key = "Kingdom.Music.GapSeconds";
        bool originallyPresent = PlayerPrefs.HasKey(key);
        float originalGap = PlayerPrefs.GetFloat(key, 5f);
        GameObject blocker = new GameObject("MusicContinuationSetupBlocker");
        try
        {
            if (hasGap) PlayerPrefs.SetFloat(key, .73f);
            else PlayerPrefs.DeleteKey(key);
            blocker.AddComponent<MusicManager>();
            var fixture = new MusicContinuationPlayModeTests();
            Assert.Throws<AssertionException>(() => fixture.SetUp());
            fixture.TearDown();
            Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(hasGap));
            if (hasGap)
                Assert.That(PlayerPrefs.GetFloat(key), Is.EqualTo(.73f).Within(.0001f));
            Assert.That(blocker, Is.Not.Null, "Failed setup must preserve the pre-existing music manager.");
        }
        finally
        {
            if (blocker != null) Object.DestroyImmediate(blocker);
            if (originallyPresent) PlayerPrefs.SetFloat(key, originalGap);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
