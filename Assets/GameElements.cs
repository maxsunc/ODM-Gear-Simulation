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
        currentInd = Random.Range(0, musics.Length);
        // start the current song
        musics[currentInd].Play();
        StartCoroutine(RotateSun());
        if(isMuted)
        {
            for (int i = 0; i < musics.Length; i++)
            {
                musics[i].volume = (musics[i].volume == 0.05f) ? 0f : 0.05f;
            }
        }
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
        if (!musics[currentInd].isPlaying || Input.GetKeyDown(KeyCode.T))
        {
            // swithc to a new song, not the same one
            int nextInd = Random.Range(0, musics.Length);
            while(nextInd == currentInd)
            {
                // swithc it
                nextInd = Random.Range(0, musics.Length);
            }
            // stop the current one
            musics[currentInd].Stop();
            currentInd = nextInd;
            // start the song
            musics[currentInd].Play();
        }
        if(player.transform.position.y <= -25f && reloadScene == false)
        {
            Scene scene = SceneManager.GetActiveScene(); SceneManager.LoadScene(scene.name);

            reloadScene = true;
        }
        if(Input.GetKeyDown(KeyCode.M))
        {
            for (int i = 0; i<musics.Length; i++){
                musics[i].volume = (musics[i].volume == 0.05f) ? 0f : 0.05f;
            }
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            if(panel.activeSelf == true)
            {
                panel.SetActive(false);
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
}
