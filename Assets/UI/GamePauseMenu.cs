using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared J-menu presentation for both gameplay scenes. Replaces only the
/// legacy panel at runtime; no saved scene, player tuning, motor or camera edits.</summary>
[DisallowMultipleComponent]
public sealed class GamePauseMenu : MonoBehaviour
{
    const string MenuScene="Assets/Scenes/Menu.unity";
    RectTransform canvasRoot,design,confirmation,confirmationDesign;
    Text masterValue,sfxValue,musicValue,status,question,explanation;
    Slider master,sfx,music;
    Button resume,mainMenu,quit,cancel,confirm;
    bool opened,leaving,confirmQuit;
    float previousTimeScale=1;CursorLockMode previousLock;bool previousCursorVisible;
    public bool IsOpen {get{return opened;}}

    public static GamePauseMenu Install(GameElements game)
    {
        if(!Application.isPlaying || game==null || game.player==null)return null;
        var result=game.GetComponent<GamePauseMenu>();if(result!=null)return result;
        if(game.panel!=null)game.panel.SetActive(false);
        result=game.gameObject.AddComponent<GamePauseMenu>();
        result.CreateVisuals(MapName(game.gameObject.scene));game.panel=result.canvasRoot.gameObject;
        if(EventSystem.current==null)
        {var events=new GameObject("Gameplay menu EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(game.transform,false);}
        return result;
    }
    static string MapName(Scene scene)
    {
        if(scene.path==GameMenu.ForestScenePath)return "Giant Forest";
        if(scene.path==GameMenu.TownScenePath)return "Shiganshina";
        return scene.name;
    }
    public void Toggle(){if(opened)Resume();else Show();}
    void Show()
    {
        if(leaving || canvasRoot==null)return;
        previousTimeScale=Time.timeScale;previousLock=Cursor.lockState;previousCursorVisible=Cursor.visible;
        opened=true;confirmation.gameObject.SetActive(false);status.text="";
        canvasRoot.gameObject.SetActive(true);SyncAudio();FrontendUI.Fit(design);
        Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;resume.Select();
    }
    public void Resume()
    {
        if(!opened || leaving)return;
        Save();RestorePauseState();canvasRoot.gameObject.SetActive(false);
    }
    void RestorePauseState()
    {
        if(!opened)return;opened=false;Time.timeScale=previousTimeScale;Cursor.lockState=previousLock;Cursor.visible=previousCursorVisible;
    }
    void Update()
    {
        if(!opened)return;
        FrontendUI.Fit(design);FrontendUI.Fit(confirmationDesign);SyncAudio();
        if(!leaving && Input.GetKeyDown(KeyCode.Escape))
        {if(confirmation.gameObject.activeSelf)CancelConfirmation();else Resume();}
    }
    void OnDisable(){if(opened){Save();RestorePauseState();if(canvasRoot!=null)canvasRoot.gameObject.SetActive(false);}}
    static void Save(){GameStats.SaveCurrent();GameOptions.Save();GameAudio.Save();}
    void Ask(bool quitting)
    {
        if(leaving)return;confirmQuit=quitting;
        question.text=quitting?"Quit game?":"Return to menu?";
        explanation.text="Your total kills and settings are saved. This ends the current run.";
        confirm.GetComponentInChildren<Text>().text=quitting?"Quit game":"Return to menu";
        confirmation.gameObject.SetActive(true);cancel.Select();
    }
    void CancelConfirmation(){confirmation.gameObject.SetActive(false);resume.Select();}
    void Confirm()
    {
        Save();
        if(confirmQuit)
        {
            if(Application.isEditor){CancelConfirmation();status.text="Quit is available in the standalone game.";return;}
#if UNITY_WEBGL
            CancelConfirmation();status.text="Close the browser tab to leave the game.";
#else
            Application.Quit();
#endif
            return;
        }
        if(!Application.CanStreamedLevelBeLoaded(MenuScene))
        {CancelConfirmation();status.text="Menu is not enabled in Build Settings.";return;}
        confirmation.gameObject.SetActive(false);status.text="Returning to menu...";
        leaving=true;resume.interactable=mainMenu.interactable=quit.interactable=false;
        try
        {
            if(SceneManager.LoadSceneAsync(MenuScene,LoadSceneMode.Single)==null)throw new InvalidOperationException("Scene could not be loaded");
        }
        catch(Exception)
        {leaving=false;resume.interactable=mainMenu.interactable=quit.interactable=true;status.text="Could not load Menu. You can resume safely.";}
    }
    void SyncAudio()
    {
        master.SetValueWithoutNotify(GameAudio.MasterVolume);sfx.SetValueWithoutNotify(GameAudio.SfxVolume);music.SetValueWithoutNotify(GameAudio.MusicVolume);
        masterValue.text=Mathf.RoundToInt(master.value*100)+"%";sfxValue.text=Mathf.RoundToInt(sfx.value*100)+"%";musicValue.text=Mathf.RoundToInt(music.value*100)+"%";
    }
    static Button MenuButton(Transform panel,string label,float y,bool primary=false)
    {
        var button=FrontendUI.Button(panel,label,label,56,y,360,64,primary);
        button.GetComponent<Image>().color=primary?FrontendUI.MinimalAccent:new Color(.095f,.115f,.135f,1);
        button.GetComponentInChildren<Text>().color=primary?FrontendUI.MinimalPanel:FrontendUI.Paper;
        return button;
    }
    static Slider Volume(Transform panel,string label,float y,out Text value)
    {
        var root=FrontendUI.Rect(panel,label,520,y,464,80);
        FrontendUI.Text(root,"Label",label,0,0,364,30,20,FrontendUI.Paper);
        value=FrontendUI.Text(root,"Value","",364,0,100,30,19,FrontendUI.MinimalMuted);value.alignment=TextAnchor.MiddleRight;
        var track=FrontendUI.Image(root,"Track",0,49,464,5,FrontendUI.MinimalLine);
        var area=FrontendUI.Full(track,"Fill area");
        var fill=FrontendUI.Image(area,"Fill",0,0,464,5,FrontendUI.MinimalAccent);FrontendUI.Stretch(fill);
        // A handle-free bar stays visually quiet; its larger invisible target
        // still supports pointer dragging and normal Slider keyboard navigation.
        FrontendUI.Image(track,"Hit area",0,-15,464,36,Color.clear,true).SetAsFirstSibling();
        var slider=root.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
        slider.fillRect=fill;slider.targetGraphic=fill.GetComponent<Image>();slider.direction=Slider.Direction.LeftToRight;
        return slider;
    }
    /// <summary>Static native art capture can build these visuals without changing
    /// pause/cursor state, loading a scene, reading stats or writing preferences.</summary>
    public void CreateVisuals(string mapName)
    {
        canvasRoot=FrontendUI.Canvas(transform,"J - Pause menu",300,true);
        FrontendUI.Shade(canvasRoot,"Dimmed game",new Color(.005f,.01f,.02f,.48f),true);
        design=FrontendUI.Design(canvasRoot,"Pause menu design");
        var panel=FrontendUI.Image(design,"Pause card",440,220,1040,640,FrontendUI.MinimalPanel,true);
        FrontendUI.Text(panel,"Title","Paused",56,42,820,62,42,FrontendUI.Paper,FontStyle.Bold);
        FrontendUI.Text(panel,"Map",mapName,56,113,820,32,21,FrontendUI.MinimalMuted);
        FrontendUI.Image(panel,"Top divider",56,174,928,1,FrontendUI.MinimalLine);
        resume=MenuButton(panel,"Resume",214,true);resume.onClick.AddListener(Resume);
        mainMenu=MenuButton(panel,"Main menu",300);mainMenu.onClick.AddListener(()=>Ask(false));
        quit=MenuButton(panel,"Quit game",386);quit.onClick.AddListener(()=>Ask(true));
#if UNITY_WEBGL && !UNITY_EDITOR
        quit.interactable=false;
#endif
        status=FrontendUI.Text(panel,"Status","",56,466,370,70,18,FrontendUI.MinimalMuted);
        FrontendUI.Text(panel,"Audio heading","Audio",520,211,464,34,24,FrontendUI.Paper,FontStyle.Bold);
        master=Volume(panel,"Master",264,out masterValue);sfx=Volume(panel,"SFX",346,out sfxValue);music=Volume(panel,"Music",428,out musicValue);
        masterValue.text=sfxValue.text=musicValue.text="100%";
        master.SetValueWithoutNotify(1);sfx.SetValueWithoutNotify(1);music.SetValueWithoutNotify(1);
        master.onValueChanged.AddListener(GameAudio.SetMasterVolume);sfx.onValueChanged.AddListener(GameAudio.SetSfxVolume);music.onValueChanged.AddListener(GameAudio.SetMusicVolume);
        FrontendUI.Image(panel,"Bottom divider",56,552,928,1,FrontendUI.MinimalLine);
        FrontendUI.Text(panel,"Resume hint","J or Esc to resume",56,577,928,30,18,FrontendUI.MinimalMuted);
        confirmation=FrontendUI.Shade(canvasRoot,"Confirmation",new Color(0,0,0,.65f),true);
        confirmationDesign=FrontendUI.Design(confirmation,"Confirmation design");
        var dialog=FrontendUI.Image(confirmationDesign,"Dialog",560,350,800,380,FrontendUI.MinimalPanel,true);
        question=FrontendUI.Text(dialog,"Question","Return to menu?",48,40,704,60,36,FrontendUI.Paper,FontStyle.Bold);
        explanation=FrontendUI.Text(dialog,"Explanation","",48,119,704,88,22,FrontendUI.MinimalMuted);
        cancel=FrontendUI.Button(dialog,"Cancel","Cancel",48,258,320,66);
        confirm=FrontendUI.Button(dialog,"Confirm","Return to menu",392,258,360,66,true);
        cancel.GetComponent<Image>().color=new Color(.095f,.115f,.135f,1);confirm.GetComponent<Image>().color=FrontendUI.MinimalAccent;
        cancel.onClick.AddListener(CancelConfirmation);confirm.onClick.AddListener(Confirm);
        confirmation.gameObject.SetActive(false);canvasRoot.gameObject.SetActive(false);
    }
}
