using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameElements : MonoBehaviour
{
    public Transform player;
    private bool reloadScene = false;
    public AudioSource[] musics;
    public GameObject panel;
    public Transform sun;
    private int currentInd = 0;
    [SerializeField] private bool isMuted = true;


    private void Start()
    {
        GameAudio.InitializeMusicDefault(isMuted);
        if(musics!=null)
        {
            foreach(var source in musics)GameAudio.Route(source,true);
            if(musics.Length>0){currentInd=Random.Range(0,musics.Length);if(musics[currentInd]!=null)musics[currentInd].Play();}
        }
        if(sun!=null)StartCoroutine(RotateSun());
    }
    IEnumerator RotateSun()
    {
        while (true)
        {
            
            while (sun.rotation.x < 180)
            {
                sun.Rotate(1 * Time.deltaTime, 0, 0);
                yield return null;
            }
            
            while (sun.rotation.x > 0)
            {
                sun.Rotate(-1 * Time.deltaTime, 0, 0);
                yield return null;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // check if the song is finished playing
        if (musics!=null && musics.Length>0 && musics[currentInd]!=null && (!musics[currentInd].isPlaying || Input.GetKeyDown(KeyCode.T)))
        {
            // swithc to a new song, not the same one
            int nextInd = Random.Range(0, musics.Length);
            while(musics.Length>1 && nextInd == currentInd)
            {
                // swithc it
                nextInd = Random.Range(0, musics.Length);
            }
            // stop the current one
            musics[currentInd].Stop();
            currentInd = nextInd;
            // start the song
            if(musics[currentInd]!=null)musics[currentInd].Play();
        }
        if(player!=null && player.transform.position.y <= -25f && reloadScene == false)
        {
            Scene scene = SceneManager.GetActiveScene(); SceneManager.LoadScene(scene.name);

            reloadScene = true;
        }
        if (panel!=null && Input.GetKeyDown(KeyCode.J))
        {
            if(panel.activeSelf == true)
            {
                panel.SetActive(false);
                GameAudio.Save();
                Cursor.lockState = CursorLockMode.Locked;
                Time.timeScale = 1;
            }
            else
            {
                panel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Time.timeScale = 0;
            }
        }

    }
    void OnGUI(){if(panel!=null && panel.activeInHierarchy)GameAudio.DrawMenu();}
}
