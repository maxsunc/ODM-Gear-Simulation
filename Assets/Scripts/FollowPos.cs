using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPos : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public float lagTime;

    private void Awake()
    {
        if(lagTime > 0)
        {
            StartCoroutine(LagFollow());
        }
    }

    IEnumerator LagFollow()
    {
        while(true)
        {
            transform.position = target.position + offset;
            yield return new WaitForSeconds( lagTime );
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (lagTime == 0)
        {
            transform.position = target.position + offset;
        }
    }
}
