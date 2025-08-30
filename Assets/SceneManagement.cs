using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagement : MonoBehaviour
{

    public GameObject scenePickPanel;
    public TextMeshProUGUI apology;

    private Coroutine _sorry;

    public void GoOpenWorld(bool option)
    {
        scenePickPanel.SetActive(option);
    }
    public void LoadScene(int sceneNum)
    {
        apology.text = "Loading...";
        SceneManager.LoadScene(sceneNum);
    }

    public void Apologize()
    {
        if(_sorry != null)
            StopCoroutine(_sorry);
        _sorry = StartCoroutine(Sorry());
    }

    IEnumerator Sorry()
    {
        apology.text = "Haven't made it yet sorry \n ㄟ( ▔, ▔ )ㄏ";
        yield return new WaitForSeconds(3);
        apology.text = string.Empty;
    }

    IEnumerator Loading()
    {

        while (true)
        {
            apology.text = "Loading";
            yield return new WaitForSeconds(0.7f);
            apology.text = "Loading.";
            yield return new WaitForSeconds(0.7f);
            apology.text = "Loading..";
            yield return new WaitForSeconds(0.7f);
            apology.text = "Loading...";
            yield return new WaitForSeconds(0.7f);

        }

    }
    
}
