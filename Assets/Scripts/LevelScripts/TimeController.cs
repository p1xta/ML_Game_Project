using UnityEngine;

public class TimeController : MonoBehaviour
{
    [Header("Time Control Settings")]
    public bool forceTimeScale = true;
    public float targetTimeScale = 0.1f;
    
    [Header("Debug Info")]
    public float currentTimeScale;
    public float realTimeSinceStartup;
    
    private float lastLogTime = 0f;
    private float logInterval = 5f; // Логируем каждые 5 секунд реального времени

    void Start()
    {
        Debug.Log($"[TIME CONTROLLER] Started - Initial Time.timeScale: {Time.timeScale}");
        
        if (forceTimeScale)
        {
            Time.timeScale = targetTimeScale;
            Debug.Log($"[TIME CONTROLLER] Set Time.timeScale to: {targetTimeScale}");
        }
    }

    void Update()
    {
        // Обновляем информацию для отладки
        currentTimeScale = Time.timeScale;
        realTimeSinceStartup = Time.realtimeSinceStartup;
        
        // Принудительно устанавливаем timeScale если нужно
        if (forceTimeScale && Time.timeScale != targetTimeScale)
        {
            Time.timeScale = targetTimeScale;
            Debug.LogWarning($"[TIME CONTROLLER] Time.timeScale was {currentTimeScale}, corrected to {targetTimeScale}");
        }
        
        // Периодически логируем состояние времени
        if (Time.realtimeSinceStartup - lastLogTime > logInterval)
        {
            Debug.Log($"[TIME CONTROLLER] Status - TimeScale: {Time.timeScale}, RealTime: {Time.realtimeSinceStartup:F1}s, UnscaledTime: {Time.unscaledTime:F1}s");
            lastLogTime = Time.realtimeSinceStartup;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && forceTimeScale)
        {
            Time.timeScale = targetTimeScale;
            Debug.Log($"[TIME CONTROLLER] Application focused - restored Time.timeScale to {targetTimeScale}");
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && forceTimeScale)
        {
            Time.timeScale = targetTimeScale;
            Debug.Log($"[TIME CONTROLLER] Application unpaused - restored Time.timeScale to {targetTimeScale}");
        }
    }
}
