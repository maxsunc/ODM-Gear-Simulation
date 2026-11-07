using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameElements : MonoBehaviour
{
    public Transform player;
    private bool warnedMissingHealth;
    public AudioSource[] musics;
    public GameObject panel;
    public Transform sun;
    private int currentInd = 0;
    [SerializeField] private bool isMuted = true;
    private GamePauseMenu pauseMenu;


    private void Start()
    {
        pauseMenu=GamePauseMenu.Install(this);
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
        if(player!=null && player.transform.position.y <= -25f)
        {
            var health=player.GetComponent<PlayerHealth>();
            if(health!=null){if(!health.isDead)health.Die();}
            else if(!warnedMissingHealth){warnedMissingHealth=true;Debug.LogWarning("Void death needs PlayerHealth; refusing to reset the whole scene",this);}
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            if(pauseMenu==null)pauseMenu=GamePauseMenu.Install(this);
            if(pauseMenu!=null)pauseMenu.Toggle();
        }

    }
}
