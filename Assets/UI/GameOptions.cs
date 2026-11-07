using UnityEngine;

/// <summary>User presentation preferences. Never changes movement, combat, physics or authored camera baselines.</summary>
public static class GameOptions
{
    const string Prefix = "ODM.Options.";
    static bool loaded, dirty;
    static float sensitivity = 1, saveAt;
    static bool invert, hasInvertOverride;
    public static float LookSensitivity { get { Load(); return sensitivity; } }
    public static bool InvertY { get { Load(); return invert; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { loaded = dirty = false; sensitivity = 1; invert = hasInvertOverride = false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        Load();
        if (PlayerPrefs.HasKey(Prefix + "Fullscreen.v1")) ApplyFullscreen(PlayerPrefs.GetInt(Prefix + "Fullscreen.v1") != 0);
        if (PlayerPrefs.HasKey(Prefix + "VSync.v1")) QualitySettings.vSyncCount = PlayerPrefs.GetInt(Prefix + "VSync.v1") != 0 ? 1 : 0;
    }
    static void Load()
    {
        if (loaded) return; loaded = true;
        float saved = PlayerPrefs.GetFloat(Prefix + "LookSensitivity.v1", 1);
        sensitivity = float.IsNaN(saved) || float.IsInfinity(saved) ? 1 : Mathf.Clamp(saved, .25f, 3);
        hasInvertOverride = PlayerPrefs.HasKey(Prefix + "InvertY.v1");
        invert = PlayerPrefs.GetInt(Prefix + "InvertY.v1", 0) != 0;
    }
    public static void SetLookSensitivity(float value)
    {
        Load(); if (float.IsNaN(value) || float.IsInfinity(value)) return;
        sensitivity = Mathf.Clamp(value, .25f, 3); PlayerPrefs.SetFloat(Prefix + "LookSensitivity.v1", sensitivity); Changed();
    }
    public static bool ResolveInvertY(bool authored) { Load(); return hasInvertOverride ? invert : authored; }
    public static void SetInvertY(bool value) { Load(); invert = value; hasInvertOverride = true; PlayerPrefs.SetInt(Prefix + "InvertY.v1", value ? 1 : 0); Changed(); }
    public static void SetFullscreen(bool value) { ApplyFullscreen(value); PlayerPrefs.SetInt(Prefix + "Fullscreen.v1", value ? 1 : 0); Changed(); }
    static void ApplyFullscreen(bool value)
    {
        // Do not resize or take over an Editor Game view while previewing a menu.
        if (!Application.isEditor) Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
    public static void SetVSync(bool value) { QualitySettings.vSyncCount = value ? 1 : 0; PlayerPrefs.SetInt(Prefix + "VSync.v1", value ? 1 : 0); Changed(); }
    static void Changed() { dirty = true; saveAt = Time.unscaledTime + .4f; }
    public static void TickSave() { if (dirty && Time.unscaledTime >= saveAt) Save(); }
    public static void Save() { if (!dirty) return; PlayerPrefs.Save(); dirty = false; }
}
