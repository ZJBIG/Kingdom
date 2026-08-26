using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MusicManagerPlayModeTests
{
    private GameObject managerObject;
    private MusicManager manager;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        managerObject = new GameObject("MusicManagerTest");
        managerObject.AddComponent<AudioSource>();
        manager = managerObject.AddComponent<MusicManager>();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (managerObject != null)
            Object.Destroy(managerObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MusicCatalogUsesPMusicAndStartsPlaying()
    {
        Assert.That(manager.Tracks.Count, Is.GreaterThan(0));
        for (int i = 0; i < manager.Tracks.Count; i++)
        {
            MusicManager.MusicTrack track = manager.Tracks[i];
            Assert.That(new[] { "Tense", "Day", "Night", "AllTime" },
                Does.Contain(track.Category));
            Assert.That(track.ResourcePath, Does.StartWith("Musics/PMusic/" + track.Category + "/"));
        }
        Assert.That(manager.State, Is.EqualTo(MusicManager.PlaybackState.Playing));
        Assert.That(manager.CurrentTrack, Is.Not.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PauseResumeAndStopHaveDistinctStates()
    {
        manager.Pause();
        Assert.That(manager.State, Is.EqualTo(MusicManager.PlaybackState.Paused));
        manager.Resume();
        Assert.That(manager.State, Is.EqualTo(MusicManager.PlaybackState.Playing));
        manager.Stop();
        Assert.That(manager.State, Is.EqualTo(MusicManager.PlaybackState.Stopped));
        Assert.That(manager.AudioSource.clip, Is.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NextAndPreviousUsePlaybackHistory()
    {
        string first = manager.CurrentTrack.Id;
        Assert.That(manager.NextTrack(), Is.True);
        string second = manager.CurrentTrack.Id;
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(manager.PreviousTrack(), Is.True);
        Assert.That(manager.CurrentTrack.Id, Is.EqualTo(first));
        yield return null;
    }
}
