using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private int timeCounter;
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<T>();
                if (instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    instance = obj.AddComponent<T>();
                }
            }
            return instance;
        }
    }
    public virtual void Awake()
    {
        if (instance == null)
            instance = this as T;
        else
            Destroy(gameObject);
        Initialize();
    }
    public virtual void Update()
    {
        timeCounter++;
        Tick();
        if (timeCounter % 60 == 0)
            TickLong();
        if (timeCounter % 1800 == 0)
            TickRare();
    }
    protected virtual void Initialize() { }
    /// <summary>
    /// Tick/ 1 Frame
    /// </summary>
    protected virtual void Tick() { }
    /// <summary>
    /// Tick/ 60 Frame
    /// </summary>
    protected virtual void TickLong() { }
    /// <summary>
    /// Tick/ 1800 Frame
    /// </summary>
    protected virtual void TickRare() { }
}