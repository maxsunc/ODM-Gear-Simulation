using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
  
    public Text[] text;


    private ODM_Movement odm;

    void Start()
    {
        odm = GameObject.Find("Player").GetComponent<ODM_Movement>();
        text[0].text = "Acceleration: " + odm.acceleration.ToString();
        text[1].text = "Max Speed: " + odm.maxSpeed.ToString();
        text[2].text = "Strafe Speed: " + odm.strafeSpeed.ToString();
    }


  


    public void ChangeAccel(bool add)
    {
        int amount = (add == true) ? 5 : -5;

            odm.acceleration += amount;
            float accel = odm.AddAccel(amount);
            text[0].text = "Acceleration: " + accel.ToString();
        
    }

    public void ChangeMax(bool add)
    {
        int amount = (add == true) ? 5 : -5;

        odm.maxSpeed += amount;
        float maxSpeed = odm.maxSpeed;
        text[1].text = "Max Speed: " + maxSpeed.ToString();

    }
    public void ChangeStrafe(bool add)
    {
        int amount = (add == true) ? 5 : -5;

        odm.strafeSpeed += amount;
        float strafe = odm.strafeSpeed;
        text[2].text = "Strafe Speed: " + strafe.ToString();

    }

}
