using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Single-player confirmed receipts. Session survives player-only respawn; lifetime totals use separate PlayerPrefs keys.</summary>
public sealed class GameStats : MonoBehaviour
{
    const string Prefix = "ODM.Stats.";
    static GameStats instance;
    public static GameStats Current { get { Ensure(); return instance; } }
    public int RunKills { get; private set; }
    public int RunDeaths { get; private set; }
    public int LifeKills { get; private set; }
    public int TotalKills { get; private set; }
    public int TotalDeaths { get; private set; }
    public int BestStreak { get; private set; }
    public float RunSeconds { get; private set; }
    public float LifeSeconds { get; private set; }
    public float DamageDealt { get; private set; }
    public int LimbsSevered { get; private set; }
    public int EyeStuns { get; private set; }
    public bool OverlayVisible { get { return overlay != null && overlay.gameObject.activeSelf; } }
    PlayerHealth player;
    int runScene = int.MinValue;
    bool deathRecorded, dirty, applicationPaused, focused = true;
    float saveAt, nextDisplay;
    ConditionalWeakTable<ModularTitanBody, object> killed = new ConditionalWeakTable<ModularTitanBody, object>();
    RectTransform overlay, design;
    Text killsValue, totalKillsValue, damageValue, limbsValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("Field Record [persistent stats]"); instance = go.AddComponent<GameStats>();
    }
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        TotalKills = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "Kills.v1", 0));
        TotalDeaths = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "Deaths.v1", 0));
        BestStreak = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "BestStreak.v1", 0));
        SceneManager.sceneLoaded += SceneLoaded;
    }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) player = null;
        if (overlay != null) overlay.gameObject.SetActive(false);
    }
    public static void Bind(PlayerHealth health)
    {
        if (!IsPlayer(health)) return;
        var s = Current;
        if (s.runScene != health.gameObject.scene.handle)
        {
            s.Save(); s.runScene = health.gameObject.scene.handle;
            s.RunKills = s.RunDeaths = s.LifeKills = s.LimbsSevered = s.EyeStuns = 0;
            s.RunSeconds = s.LifeSeconds = s.DamageDealt = 0; s.deathRecorded = false;
            s.killed = new ConditionalWeakTable<ModularTitanBody, object>();
        }
        s.player = health;
    }
    static bool IsPlayer(PlayerHealth health)
    {
        return Application.isPlaying && health != null && health.gameObject.scene.IsValid() && health.gameObject.scene.name != "Menu"
            && (health.GetComponent<Movement>() != null || health.GetComponent<TitanODMMotor>() != null);
    }
    static int Increment(int value) { return value < int.MaxValue ? value + 1 : int.MaxValue; }
    public static void RecordHit(DamageDealer dealer, TitanDamageResult hit)
    {
        if (dealer == null || !hit.Accepted || hit.Body == null) return;
        var health = dealer.GetComponentInParent<PlayerHealth>();
        if (!IsPlayer(health) || health.isDead || hit.Body.gameObject.scene != health.gameObject.scene) return;
        Bind(health); var s = instance;
        float damage = hit.AppliedDamage;
        if (!float.IsNaN(damage) && !float.IsInfinity(damage) && damage > 0) s.DamageDealt = Mathf.Min(1e12f, s.DamageDealt + damage);
        if (hit.Outcome == TitanDamageOutcome.Severed) s.LimbsSevered = Increment(s.LimbsSevered);
        if (hit.Outcome == TitanDamageOutcome.Blinded) s.EyeStuns = Increment(s.EyeStuns);
        if (hit.Outcome != TitanDamageOutcome.Killed || !hit.Body.IsDead) return;
        object receipt; if (s.killed.TryGetValue(hit.Body, out receipt)) return;
        s.killed.Add(hit.Body, new object()); // Weak identity: no retained corpses or recycled-instance-ID collisions.
        s.RunKills = Increment(s.RunKills); s.LifeKills = Increment(s.LifeKills); s.TotalKills = Increment(s.TotalKills);
        s.BestStreak = Mathf.Max(s.BestStreak, s.LifeKills); s.Changed();
    }
    public static void RecordDeath(PlayerHealth health)
    {
        if (!IsPlayer(health) || !health.isDead) return;
        Bind(health); var s = instance; if (s.deathRecorded) return;
        s.deathRecorded = true; s.RunDeaths = Increment(s.RunDeaths); s.TotalDeaths = Increment(s.TotalDeaths); s.Changed();
    }
    public static void BeginLife(PlayerHealth health)
    {
        if (!IsPlayer(health) || health.isDead) return;
        Bind(health); instance.deathRecorded = false; instance.LifeKills = 0; instance.LifeSeconds = 0;
    }
    void Changed() { dirty = true; saveAt = Time.unscaledTime + .4f; }
    public static void SaveCurrent() { if (instance != null) instance.Save(); }
    public void Save()
    {
        if (!dirty) return;
        PlayerPrefs.SetInt(Prefix + "Kills.v1", TotalKills); PlayerPrefs.SetInt(Prefix + "Deaths.v1", TotalDeaths);
        PlayerPrefs.SetInt(Prefix + "BestStreak.v1", BestStreak); PlayerPrefs.Save(); dirty = false;
    }
    void Update()
    {
        GameOptions.TickSave(); if (dirty && Time.unscaledTime >= saveAt) Save();
        if (player != null && player.isActiveAndEnabled)
        {
            RunSeconds += Time.deltaTime; if (!player.isDead) LifeSeconds += Time.deltaTime;
        }
        bool visible = focused && !applicationPaused && Time.timeScale > 0 && player != null && player.gameObject.activeInHierarchy && Input.GetKey(KeyCode.Tab);
        if (visible && overlay == null) BuildOverlay();
        if (overlay == null) return;
        if (overlay.gameObject.activeSelf != visible) { overlay.gameObject.SetActive(visible); nextDisplay = 0; }
        if (!visible) return;
        FrontendUI.Fit(design);
        if (Time.unscaledTime < nextDisplay) return; nextDisplay = Time.unscaledTime + .1f;
        killsValue.text = RunKills.ToString("N0"); totalKillsValue.text = TotalKills.ToString("N0");
        damageValue.text = DamageDealt.ToString("N0"); limbsValue.text = LimbsSevered.ToString("N0");
    }
    public static string Clock(float seconds)
    { int n = Mathf.FloorToInt(Mathf.Clamp(seconds, 0, 1e8f)); return (n / 3600).ToString("00") + ":" + (n / 60 % 60).ToString("00") + ":" + (n % 60).ToString("00"); }
    void BuildOverlay()
    {
        overlay = FrontendUI.Canvas(transform, "Hold TAB - Stats", 200, false);
        design = FrontendUI.Design(overlay, "Stats design");
        var panel = FrontendUI.Image(design, "Stats panel", 580, 260, 760, 560, FrontendUI.MinimalPanel);
        FrontendUI.Image(panel, "Accent", 48, 44, 4, 40, FrontendUI.MinimalAccent);
        FrontendUI.Text(panel, "Title", "Stats", 72, 34, 620, 62, 40, FrontendUI.Paper, FontStyle.Bold);
        killsValue = StatRow(panel, "Kills", 128);
        totalKillsValue = StatRow(panel, "Total kills", 224);
        damageValue = StatRow(panel, "Damage done", 320);
        limbsValue = StatRow(panel, "Limbs severed", 416);
        overlay.gameObject.SetActive(false);
    }
    static Text StatRow(Transform panel, string label, float y)
    {
        FrontendUI.Image(panel, label + " divider", 48, y - 10, 664, 1, FrontendUI.MinimalLine);
        FrontendUI.Text(panel, label + " label", label, 48, y, 380, 72, 24, FrontendUI.MinimalMuted);
        var value = FrontendUI.Text(panel, label + " value", "0", 412, y, 300, 72, 36, FrontendUI.Paper, FontStyle.Bold);
        value.alignment = TextAnchor.MiddleRight; return value;
    }
    void OnApplicationFocus(bool value) { focused = value; if (!value && overlay != null) overlay.gameObject.SetActive(false); if (!value) SaveAll(); }
    void OnApplicationPause(bool value) { applicationPaused = value; if (value) { if (overlay != null) overlay.gameObject.SetActive(false); SaveAll(); } }
    void SaveAll() { Save(); GameOptions.Save(); GameAudio.Save(); }
    void OnApplicationQuit() { SaveAll(); }
    void OnDestroy() { SceneManager.sceneLoaded -= SceneLoaded; if (instance == this) { SaveAll(); instance = null; } }
}
