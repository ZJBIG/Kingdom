using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MusicCatalogEntry
{
    [SerializeField] private string id;
    [SerializeField] private string label;
    [SerializeField] private string category;
    [SerializeField] private string address;
    [SerializeField] private float durationSeconds;

    public string Id => id;
    public string Label => label;
    public string Category => category;
    public string Address => address;
    public float DurationSeconds => durationSeconds;

    public MusicCatalogEntry(string id, string label, string category, string address,
        float durationSeconds)
    {
        this.id = id;
        this.label = label;
        this.category = category;
        this.address = address;
        this.durationSeconds = durationSeconds;
    }
}

[CreateAssetMenu(fileName = "MusicCatalog", menuName = "Kingdom/Music Catalog")]
public sealed class MusicCatalog : ScriptableObject
{
    public const string AddressableAddress = "music/catalog";

    [SerializeField] private List<MusicCatalogEntry> entries = new();

    public IReadOnlyList<MusicCatalogEntry> Entries => entries;

    public void ReplaceEntries(List<MusicCatalogEntry> replacement)
    {
        entries = replacement ?? new List<MusicCatalogEntry>();
    }
}
