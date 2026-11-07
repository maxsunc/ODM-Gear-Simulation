using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>Menu frontend with explicit forest/town destinations. Legacy scenes remain intact.</summary>
public sealed class GameMenu : MonoBehaviour
{
    public const string ForestScenePath = "Assets/Scenes/GiantForest2.unity";
    public const string TownScenePath = "Assets/Scenes/ShiganshinaDistrict.unity";
    [SerializeField] RectTransform canvasRoot, design, playPage, optionsPage, quitPage, loadingPage;
    [SerializeField] Button playNavigation, optionsNavigation, quitNavigation, deploy, back, quitYes, quitNo;
    [SerializeField] Slider master, sfx, music, sensitivity;
    [SerializeField] Toggle invert, fullscreen, vsync;
    [SerializeField] Text masterValue, sfxValue, musicValue, sensitivityValue, status, career, loadingText;
    [SerializeField] Button forestSelection, townSelection;
    [SerializeField] Text destinationTitle, destinationIntro, locationLabel, destinationDetails, destinationDescription, respawnNote, loadingTitle;
    [SerializeField] Image destinationPhoto, backdropPhoto;
    [SerializeField] Sprite forestImage, townImage;
    bool loading, selectedTown;
    string DestinationPath { get { return selectedTown ? TownScenePath : ForestScenePath; } }
    string DestinationName { get { return selectedTown ? "Shiganshina" : "Giant Forest"; } }

    void Awake()
    {
        if (gameObject.scene.name != "Menu") { enabled = false; return; }
        if (canvasRoot == null) CreateVisuals(null);
        if (EventSystem.current == null) new GameObject("Menu EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        foreach (var source in FindObjectsOfType<AudioSource>(true)) if (source.gameObject.scene == gameObject.scene) GameAudio.Route(source, true);
        playNavigation.onClick.AddListener(ShowPlay); optionsNavigation.onClick.AddListener(ShowOptions);
        quitNavigation.onClick.AddListener(ShowQuit); back.onClick.AddListener(ShowPlay);
        quitNo.onClick.AddListener(CancelQuit); quitYes.onClick.AddListener(Quit); deploy.onClick.AddListener(Deploy);
        master.onValueChanged.AddListener(GameAudio.SetMasterVolume); sfx.onValueChanged.AddListener(GameAudio.SetSfxVolume); music.onValueChanged.AddListener(GameAudio.SetMusicVolume);
        sensitivity.onValueChanged.AddListener(GameOptions.SetLookSensitivity); invert.onValueChanged.AddListener(GameOptions.SetInvertY);
        fullscreen.onValueChanged.AddListener(GameOptions.SetFullscreen); vsync.onValueChanged.AddListener(GameOptions.SetVSync);
        if (forestSelection != null) forestSelection.onClick.AddListener(() => SelectDestination(false));
        if (townSelection != null) townSelection.onClick.AddListener(() => SelectDestination(true));
        SelectDestination(false); SyncOptions(); ShowPlay();
        if (Application.platform == RuntimePlatform.WebGLPlayer) { quitNavigation.interactable = false; }
    }
    void Start()
    {
        if (!enabled) return;
        // This belongs to the actual menu, never the hold-Tab overlay.
        Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        playNavigation.Select();
    }
    void Update()
    {
        FrontendUI.Fit(design); FrontendUI.Fit(quitPage.GetChild(0) as RectTransform); FrontendUI.Fit(loadingPage.GetChild(0) as RectTransform);
        if (!loading && Input.GetKeyDown(KeyCode.Escape)) { if (quitPage.gameObject.activeSelf) CancelQuit(); else ShowPlay(); }
        masterValue.text = Percent(GameAudio.MasterVolume); sfxValue.text = Percent(GameAudio.SfxVolume); musicValue.text = Percent(GameAudio.MusicVolume);
        sensitivityValue.text = GameOptions.LookSensitivity.ToString("0.00") + "x";
        // M can mute music through the existing mixer service while Options is open.
        music.SetValueWithoutNotify(GameAudio.MusicVolume);
    }
    static string Percent(float value) { return Mathf.RoundToInt(value * 100) + "%"; }
    void SyncOptions()
    {
        master.SetValueWithoutNotify(GameAudio.MasterVolume); sfx.SetValueWithoutNotify(GameAudio.SfxVolume); music.SetValueWithoutNotify(GameAudio.MusicVolume);
        sensitivity.SetValueWithoutNotify(GameOptions.LookSensitivity); invert.SetIsOnWithoutNotify(GameOptions.InvertY);
        fullscreen.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ODM.Options.Fullscreen.v1", Screen.fullScreen ? 1 : 0) != 0);
        vsync.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
        masterValue.text = Percent(GameAudio.MasterVolume); sfxValue.text = Percent(GameAudio.SfxVolume); musicValue.text = Percent(GameAudio.MusicVolume);
        sensitivityValue.text = GameOptions.LookSensitivity.ToString("0.00") + "x";
    }
    public void ShowPlay()
    {
        if (loading) return; Save(); playPage.gameObject.SetActive(true); optionsPage.gameObject.SetActive(false); quitPage.gameObject.SetActive(false);
        SetNavigation(false);
        var stats = GameStats.Current; career.text = stats.TotalKills.ToString("N0") + " TOTAL KILLS   /   " + stats.TotalDeaths.ToString("N0") + " DEATHS";
        playNavigation.Select();
    }
    public void ShowOptions()
    {
        if (loading) return; SyncOptions(); playPage.gameObject.SetActive(false); optionsPage.gameObject.SetActive(true); quitPage.gameObject.SetActive(false); SetNavigation(true); optionsNavigation.Select();
    }
    void SetNavigation(bool options)
    {
        SetNavigationColor(playNavigation, !options); SetNavigationColor(optionsNavigation, options);
    }
    static void SetNavigationColor(Button button, bool active)
    {
        button.GetComponent<Image>().color = active ? FrontendUI.Gold : FrontendUI.PanelColor;
        button.GetComponentInChildren<Text>().color = active ? FrontendUI.Ink : FrontendUI.Paper;
    }
    void ShowQuit() { if (loading) return; quitPage.gameObject.SetActive(true); quitNo.Select(); }
    void CancelQuit() { quitPage.gameObject.SetActive(false); quitNavigation.Select(); }
    void Quit()
    {
        Save();
        if (Application.isEditor) { status.text = "Quit is available in the standalone game."; CancelQuit(); return; }
        Application.Quit();
    }
    public void SelectDestination(bool town)
    {
        if (loading) return; selectedTown = town;
        if (destinationTitle == null) return; // Compatibility during source reload of the previous saved frontend.
        string label = DestinationName.ToUpperInvariant();
        destinationTitle.text = label;
        destinationIntro.text = town ? "Above the streets. Between the walls. Take back the district." : "The walls are behind you. Take to the trees.";
        locationLabel.text = town ? "WALLED DISTRICT  /  SHIGANSHINA" : "PRIMARY FIELD  /  GIANT FOREST";
        destinationDetails.text = town ? "SINGLE PLAYER     /     ROOFTOP ROUTES     /     TITAN PATROLS" : "SINGLE PLAYER     /     TITAN PATROLS     /     OPEN EXPLORATION";
        destinationDescription.text = town ? "Chain rooftops, cross the canal and use the towers to keep your height." : "Keep your momentum. Strike the nape. Your field record stays with you.";
        respawnNote.text = "Player-only respawn\n" + (town ? "The district keeps moving." : "The forest keeps moving.");
        deploy.GetComponentInChildren<Text>().text = "DEPLOY TO " + label + "    >";
        loadingTitle.text = label; loadingText.text = "DEPLOYING TO " + label;
        var sprite = town && townImage != null ? townImage : forestImage;
        foreach (var photo in new[] { destinationPhoto, backdropPhoto })
        {
            photo.sprite = sprite; var fit = photo.GetComponent<AspectRatioFitter>();
            if (fit != null && sprite != null) fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
        SetNavigationColor(forestSelection, !town); SetNavigationColor(townSelection, town);
    }
    void Deploy() { if (!loading) StartCoroutine(LoadDestination()); }
    IEnumerator LoadDestination()
    {
        string scenePath = DestinationPath, label = DestinationName;
        if (!Application.CanStreamedLevelBeLoaded(scenePath)) { status.text = label + " is missing from Build Settings."; yield break; }
        Save(); loading = true; loadingPage.gameObject.SetActive(true); deploy.interactable = false;
        Time.timeScale = 1;
        // Named path, not a fragile build index. Existing world scene and baked navigation are untouched.
        var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        if (operation == null) { loading = false; loadingPage.gameObject.SetActive(false); deploy.interactable = true; status.text = "Could not load " + label + "."; yield break; }
        while (!operation.isDone) { loadingText.text = "DEPLOYING TO " + label.ToUpperInvariant() + "   " + Mathf.RoundToInt(Mathf.Clamp01(operation.progress / .9f) * 100) + "%"; yield return null; }
    }
    static void Save() { GameAudio.Save(); GameOptions.Save(); GameStats.SaveCurrent(); }
    void OnDisable() { if (Application.isPlaying) Save(); }

    /// <summary>Used by the explicitly owned Editor builder; static uGUI remains editable in Menu.unity.</summary>
    public void CreateVisuals(Sprite forest, Sprite town = null)
    {
        forestImage = forest; townImage = town;
        canvasRoot = FrontendUI.Canvas(transform, "ODM Menu Canvas", 100, true);
        FrontendUI.Shade(canvasRoot, "Letterbox", FrontendUI.Ink, true);
        FrontendUI.Photo(canvasRoot, "Forest backdrop", forest, new Color(.5f, .62f, .55f, 1));
        backdropPhoto = canvasRoot.Find("Forest backdrop/Image").GetComponent<Image>();
        FrontendUI.Shade(canvasRoot, "Backdrop veil", new Color(.015f, .035f, .032f, .76f));
        design = FrontendUI.Design(canvasRoot, "1920x1080 safe layout");
        FrontendUI.Image(design, "Sidebar", 0, 0, 532, 1080, new Color(.02f, .04f, .038f, .92f));
        FrontendUI.Image(design, "Sidebar rule", 531, 70, 1, 940, FrontendUI.Line);
        FrontendUI.Image(design, "Brand accent", 96, 100, 64, 4, FrontendUI.Gold);
        FrontendUI.Text(design, "Brand", "ODM", 90, 124, 360, 120, 104, FrontendUI.Paper, FontStyle.Bold);
        FrontendUI.Text(design, "Subtitle", "GEAR SIMULATION", 96, 249, 350, 38, 24, FrontendUI.Gold, FontStyle.Bold);
        FrontendUI.Text(design, "Edition", "FIELD OPERATIONS  /  01", 96, 304, 350, 30, 16, FrontendUI.Muted);
        playNavigation = FrontendUI.Button(design, "Free Roam", "01   FREE ROAM", 96, 412, 340, 76, true);
        optionsNavigation = FrontendUI.Button(design, "Options", "02   OPTIONS", 96, 508, 340, 76);
        quitNavigation = FrontendUI.Button(design, "Quit", "03   QUIT GAME", 96, 604, 340, 76);
        FrontendUI.Text(design, "Motto", "Steel. Momentum.\nOne more expedition.", 96, 788, 350, 100, 27, FrontendUI.Paper);
        career = FrontendUI.Text(design, "Career summary", "0 TOTAL KILLS   /   0 DEATHS", 96, 927, 350, 46, 16, FrontendUI.Muted);
        FrontendUI.Text(design, "Footer", "TAB  /  FIELD RECORD IN GAME                 M  /  MUSIC MUTE", 632, 1008, 1150, 32, 16, FrontendUI.Muted);
        status = FrontendUI.Text(design, "Status", "", 632, 956, 1150, 34, 18, FrontendUI.Gold);

        playPage = FrontendUI.Rect(design, "Free Roam page", 632, 92, 1160, 840);
        FrontendUI.Text(playPage, "Kicker", "FREE ROAM   /   SELECT YOUR EXPEDITION", 0, 0, 1100, 36, 19, FrontendUI.Gold, FontStyle.Bold);
        destinationTitle = FrontendUI.Text(playPage, "Title", "GIANT FOREST", 0, 56, 1160, 110, 78, FrontendUI.Paper, FontStyle.Bold);
        destinationIntro = FrontendUI.Text(playPage, "Intro", "The walls are behind you. Take to the trees.", 0, 176, 1120, 44, 25, FrontendUI.Muted);
        forestSelection = FrontendUI.Button(playPage, "Choose Giant Forest", "GIANT FOREST", 0, 236, 548, 56, true);
        townSelection = FrontendUI.Button(playPage, "Choose Shiganshina", "SHIGANSHINA", 572, 236, 548, 56);
        var card = FrontendUI.Image(playPage, "Destination card", 0, 312, 1120, 294, FrontendUI.PanelColor);
        FrontendUI.Photo(card, "Forest destination", forest, new Color(.83f, .90f, .81f));
        destinationPhoto = card.Find("Forest destination/Image").GetComponent<Image>();
        FrontendUI.Image(card, "Card bottom", 0, 218, 1120, 76, new Color(.02f, .05f, .04f, .94f));
        locationLabel = FrontendUI.Text(card, "Location", "PRIMARY FIELD  /  GIANT FOREST", 28, 233, 1050, 44, 21, FrontendUI.Paper, FontStyle.Bold);
        destinationDetails = FrontendUI.Text(playPage, "Details", "SINGLE PLAYER     /     TITAN PATROLS     /     OPEN EXPLORATION", 0, 638, 1120, 36, 18, FrontendUI.Gold);
        destinationDescription = FrontendUI.Text(playPage, "Description", "Keep your momentum. Strike the nape. Your field record stays with you.", 0, 691, 1120, 42, 23, FrontendUI.Muted);
        deploy = FrontendUI.Button(playPage, "Deploy Giant Forest", "DEPLOY TO GIANT FOREST    >", 0, 772, 570, 76, true);
        respawnNote = FrontendUI.Text(playPage, "Respawn note", "Player-only respawn\nThe forest keeps moving.", 620, 774, 480, 70, 19, FrontendUI.Muted);

        optionsPage = FrontendUI.Rect(design, "Options page", 632, 92, 1160, 840);
        FrontendUI.Text(optionsPage, "Kicker", "PREFERENCES   /   SAVED ON THIS DEVICE", 0, 0, 1120, 36, 19, FrontendUI.Gold, FontStyle.Bold);
        FrontendUI.Text(optionsPage, "Title", "OPTIONS", 0, 56, 1120, 110, 78, FrontendUI.Paper, FontStyle.Bold);
        FrontendUI.Text(optionsPage, "Audio heading", "AUDIO", 0, 225, 480, 40, 23, FrontendUI.Gold, FontStyle.Bold);
        FrontendUI.Text(optionsPage, "Controls heading", "LOOK & DISPLAY", 610, 225, 480, 40, 23, FrontendUI.Gold, FontStyle.Bold);
        master = FrontendUI.Slider(optionsPage, "Master volume", 0, 302, 480, 0, 1, out masterValue);
        sfx = FrontendUI.Slider(optionsPage, "Sound effects", 0, 412, 480, 0, 1, out sfxValue);
        music = FrontendUI.Slider(optionsPage, "Music volume", 0, 522, 480, 0, 1, out musicValue);
        sensitivity = FrontendUI.Slider(optionsPage, "Look sensitivity", 610, 302, 480, .25f, 3, out sensitivityValue);
        invert = FrontendUI.Toggle(optionsPage, "Invert vertical look", 610, 412, 480);
        fullscreen = FrontendUI.Toggle(optionsPage, "Borderless fullscreen", 610, 492, 480);
        vsync = FrontendUI.Toggle(optionsPage, "Vertical sync", 610, 572, 480);
        FrontendUI.Text(optionsPage, "Notes", "Changes apply immediately and save automatically.\nAudio uses the existing Master / SFX / Music mixer.\nFullscreen is applied in standalone builds, not the Editor.", 0, 662, 1120, 90, 19, FrontendUI.Muted);
        back = FrontendUI.Button(optionsPage, "Back", "<    BACK TO FREE ROAM", 0, 788, 440, 60);
        optionsPage.gameObject.SetActive(false);

        quitPage = FrontendUI.Shade(canvasRoot, "Quit confirmation", new Color(0, 0, 0, .8f), true);
        var quitDesign = FrontendUI.Design(quitPage, "Quit design"); var dialog = FrontendUI.Image(quitDesign, "Dialog", 580, 350, 760, 360, FrontendUI.Ink, true);
        FrontendUI.Image(dialog, "Accent", 0, 0, 760, 4, FrontendUI.Gold);
        FrontendUI.Text(dialog, "Title", "END THE EXPEDITION?", 44, 42, 680, 66, 34, FrontendUI.Paper, FontStyle.Bold);
        FrontendUI.Text(dialog, "Body", "Your career totals and options are saved on this device.", 44, 134, 670, 70, 22, FrontendUI.Muted);
        quitNo = FrontendUI.Button(dialog, "Cancel", "STAY", 44, 254, 302, 62, true);
        quitYes = FrontendUI.Button(dialog, "Confirm quit", "QUIT GAME", 382, 254, 334, 62); quitPage.gameObject.SetActive(false);

        loadingPage = FrontendUI.Shade(canvasRoot, "Loading", FrontendUI.Ink, true);
        var loadingDesign = FrontendUI.Design(loadingPage, "Loading design");
        loadingTitle = FrontendUI.Text(loadingDesign, "Loading title", "GIANT FOREST", 300, 380, 1400, 120, 80, FrontendUI.Paper, FontStyle.Bold);
        loadingText = FrontendUI.Text(loadingDesign, "Progress", "DEPLOYING TO GIANT FOREST", 306, 535, 1300, 60, 28, FrontendUI.Gold);
        FrontendUI.Text(loadingDesign, "Loading hint", "Hold TAB in the field to check your record. The world does not pause.", 306, 650, 1300, 60, 23, FrontendUI.Muted);
        loadingPage.gameObject.SetActive(false);
    }
}
