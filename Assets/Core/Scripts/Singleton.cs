using UnityEngine;

public abstract class Singleton<T> where T : class, new()
{
    private static readonly System.Lazy<T> _lazy = new System.Lazy<T>(() => new T());
    public static T Instance => _lazy.Value;
}

public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static bool _isQuitting;

    public static T Instance
    {
        get
        {
            if (_isQuitting)
            {
                return null;
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] duplicate instance on '{name}', destroying it.");
            Destroy(gameObject);
            return;
        }

        _instance = (T)this;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}
