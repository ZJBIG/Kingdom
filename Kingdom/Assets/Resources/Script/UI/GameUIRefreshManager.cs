using System.Collections.Generic;
using UnityEngine;

public interface IGameUIRefreshable
{
    void RefreshUI();
}

public sealed class GameUIRefreshManager : MonoBehaviour
{
    private static GameUIRefreshManager instance;

    [SerializeField] private float refreshIntervalSeconds = 0.1f;

    private readonly List<IGameUIRefreshable> viewers = new();
    private float refreshTimer;

    public static GameUIRefreshManager Instance => instance;
    public int RegisteredViewerCount => viewers.Count;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < Mathf.Max(0.01f, refreshIntervalSeconds))
            return;

        refreshTimer = 0f;
        for (int i = viewers.Count - 1; i >= 0; i--)
        {
            if (viewers[i] == null)
            {
                viewers.RemoveAt(i);
                continue;
            }

            viewers[i].RefreshUI();
        }
    }

    public void Register(IGameUIRefreshable viewer)
    {
        if (viewer != null && !viewers.Contains(viewer))
            viewers.Add(viewer);
    }

    public void Unregister(IGameUIRefreshable viewer)
    {
        if (viewer != null)
            viewers.Remove(viewer);
    }
}
