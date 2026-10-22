using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the bundled music small and prevents Unity from preloading every track.
/// </summary>
public sealed class PMusicAudioImporter : AssetPostprocessor
{
    private const string MusicPath = "Assets/Musics/PMusic/";
    internal const float AndroidQuality = 0.35f;
    internal const uint AndroidSampleRate = 32000;

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(MusicPath, System.StringComparison.OrdinalIgnoreCase))
            return;

        AudioImporter importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        AudioImporterSampleSettings android = settings;
        android.quality = AndroidQuality;
        android.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        android.sampleRateOverride = AndroidSampleRate;
        if (!importer.SetOverrideSampleSettings("Android", android))
            throw new System.InvalidOperationException("Could not apply Android music settings: " + assetPath);
        importer.loadInBackground = true;
    }
}
