using UnityEngine;

/// <summary>
/// Attract mode and reset.
/// Attract: the hint pulses, the lone cat hops, and the camera swings between the hall and the cat
/// so passers-by notice the screen. Reset: after a while without a touch, everything goes back to attract mode.
/// </summary>
public partial class ExperienceController
{
    [Header("Attract / reset")]
    public float idleSeconds = 25f;
    public float attractCameraSeconds = 6f;
    public float attractHopSeconds = 3f;

    float nextAttractCam;
    float nextAttractHop;
    bool attractOnCat;

    partial void OnResetAttract()
    {
        attractOnCat = false;
        nextAttractCam = Time.time + attractCameraSeconds;
        nextAttractHop = Time.time + 1.5f;
    }

    partial void OnTickAttract()
    {
        // Reset to attract mode after a time without touch.
        if (state != State.Attract && Time.time - lastTouch > idleSeconds)
        {
            ResetAll();
            return;
        }

        if (state != State.Attract)
        {
            hintText.rectTransform.localScale = Vector3.one;
            return;
        }

        // Pulsing invitation.
        float s = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
        hintText.rectTransform.localScale = new Vector3(s, s, 1f);

        // Swing the camera between the hall and the lone cat.
        if (Time.time > nextAttractCam)
        {
            attractOnCat = !attractOnCat;
            ShowCam(attractOnCat ? camCat : camOverview);
            nextAttractCam = Time.time + attractCameraSeconds;
        }

        // The lone cat waves for attention.
        if (Time.time > nextAttractHop)
        {
            cats[0].Hop();
            nextAttractHop = Time.time + attractHopSeconds;
        }
    }
}
