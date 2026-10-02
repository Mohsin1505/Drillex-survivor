using UnityEngine;
using System.Collections;

public class WormWarningUI : MonoBehaviour
{
    public CanvasGroup canvasGroup;

    public float blinkSpeed = 2f;



    private void OnEnable()
    {
        StartCoroutine(Blink());
    }





    IEnumerator Blink()
    {
        while (true)
        {
            // Fade IN

            while (canvasGroup.alpha < 1)
            {
                canvasGroup.alpha +=
                    Time.deltaTime *
                    blinkSpeed;


                yield return null;
            }






            // Fade OUT

            while (canvasGroup.alpha > 0)
            {
                canvasGroup.alpha -=
                    Time.deltaTime *
                    blinkSpeed;


                yield return null;
            }

        }
    }
}