using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the bundled music small and prevents Unity from preloading every track.
/// </summary>
public sealed class PMusicAudioImporter : AssetPostprocessor
{
    private const string MusicPath = "Assets/Resources/Musics/PMusic/";

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
    }
}
