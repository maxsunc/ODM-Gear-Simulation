using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class Zoom : MonoBehaviour
{
    [SerializeField]
    private float _zoom;

   // public Cinemachine vcam;

    // Update is called once per frame
    void Update()
    {
      //  if (vcam.m_Lens.OrthographicSize != _zoom)
        {
     //       vcam.m_Lens.OrthographicSize = _zoom;
        }

        float _zoomChangeAmount = 20f;
        if (Input.GetKey(KeyCode.O))
        {

            _zoom -= _zoomChangeAmount * Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.P))
        {

            _zoom += _zoomChangeAmount * Time.deltaTime;
        }

        // mouse wheel
        if (Input.mouseScrollDelta.y > 0)
        {
            _zoom -= _zoomChangeAmount * Time.deltaTime * 10f;
        }
        else if (Input.mouseScrollDelta.y < 0)
        {
            _zoom += _zoomChangeAmount * Time.deltaTime * 10f;
        }

        
    }
}
