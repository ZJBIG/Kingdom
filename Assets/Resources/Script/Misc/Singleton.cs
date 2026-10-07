using System;
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<T>();

            if (instance == null)
                throw new InvalidOperationException($"场景中缺少{typeof(T).Name}对象。");

            return instance;
        }
    }

    public static bool TryGetInstance(out T value)
    {
        if (instance == null)
            instance = FindObjectOfType<T>();
        value = instance;
        return value != null;
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
            return;
        }

        instance = (T)this;
        Initialize();
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    protected virtual void Initialize() { }
    public virtual void Save() { }
    public virtual void Load() { }   
}
