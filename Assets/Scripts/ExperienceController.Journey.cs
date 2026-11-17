using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rest of the journey: tap "Go" (the bus drives off, camera rides along),
/// tap the CO2 meter (comparison), then the end message and an automatic reset.
/// Interactions: 1 = tap a cat, 2 = tap Go, 3 = tap the CO2 meter.
/// </summary>
public partial class ExperienceController
{
    const int CatsNeededToLeave = 3;

    GameObject goButton, comparePanel, endPanel;
    Text compareText;
    bool meterTappable;

    partial void InitJourney()
    {
        // Go button (yellow with blue text: the strongest contrast in the palette).
        goButton = Box(canvas.transform, "GoButton", Yellow, new Vector2(1f, 0f), new Vector2(440f, 180f), new Vector2(-60f, 220f));
        Label(goButton.transform, "Text", "Go  >", 96, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(440f, 180f), Vector2.zero);

        // Comparison panel.
        comparePanel = Box(canvas.transform, "ComparePanel", White, new Vector2(0.5f, 0.5f), new Vector2(1400f, 720f), Vector2.zero);
        compareText = Label(comparePanel.transform, "Text", "", 64, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(1300f, 560f), new Vector2(0f, 30f));
        Label(comparePanel.transform, "Note", "Simplified example: one vehicle's CO2 shared between its passengers.", 34, DarkGrey,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(1300f, 60f), new Vector2(0f, 20f));

        // End panel.
        endPanel = Box(canvas.transform, "EndPanel", Blue, new Vector2(0.5f, 0.5f), new Vector2(1500f, 600f), Vector2.zero);
        Label(endPanel.transform, "Text", "Travel together =\nless CO2 per person", 100, Yellow, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(1400f, 500f), Vector2.zero);

        goButton.SetActive(false);
        comparePanel.SetActive(false);
        endPanel.SetActive(false);
    }

    partial void OnResetJourney()
    {
        meterTappable = false;
        if (goButton != null) goButton.SetActive(false);
        if (comparePanel != null) comparePanel.SetActive(false);
        if (endPanel != null) endPanel.SetActive(false);
        if (meterRect != null) meterRect.localScale = Vector3.one;
    }

    partial void OnBoarded()
    {
        if (state != State.Boarding) return;
        if (boardedCount >= CatsNeededToLeave)
        {
            goButton.SetActive(true);
            hintText.text = boardedCount >= seats.Length ? "The bus is full! Tap Go" : "Tap another cat, or tap Go";
        }
    }

    partial void OnPointerDown(Vector2 position, ref bool handled)
    {
        // Interaction 2: Go
        if (state == State.Boarding && Hit((RectTransform)goButton.transform, position))
        {
            handled = true;
            StartCoroutine(Depart());
            return;
        }
        // Interaction 3: the meter
        if (state == State.Compare && meterTappable && Hit(meterRect, position))
        {
            handled = true;
            StartCoroutine(ShowCompare());
        }
    }

    partial void OnTickJourney()
    {
        // The meter pulses when it is waiting to be tapped.
        if (state == State.Compare && meterTappable)
        {
            float s = 1f + 0.04f * Mathf.Sin(Time.time * 5f);
            meterRect.localScale = new Vector3(s, s, 1f);
        }
    }

    IEnumerator Depart()
    {
        state = State.Departing;
        PlaySfx("go");
        goButton.SetActive(false);
        hintText.text = "";
        ShowCam(camBus);
        yield return new WaitForSeconds(1.2f);   // let the camera blend first

        Vector3 start = bus.position;
        Vector3 end = start + Vector3.right * 18f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 4f)
        {
            bus.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        state = State.Compare;
        meterTappable = true;
        hintText.text = "Tap the CO2 meter to compare";
        titleText.text = "Off we go!";
    }

    IEnumerator ShowCompare()
    {
        meterTappable = false;
        meterRect.localScale = Vector3.one;
        PlaySfx("ui");
        int n = Mathf.Max(1, boardedCount);
        compareText.text = "1 cat alone in a car:\n100% of the CO2 per cat\n\n" +
                           n + (n == 1 ? " cat" : " cats") + " on one bus:\n" +
                           Mathf.RoundToInt(100f / n) + "% of the CO2 per cat";
        comparePanel.SetActive(true);
        hintText.text = "";
        yield return new WaitForSeconds(7f);

        comparePanel.SetActive(false);
        state = State.End;
        endPanel.SetActive(true);
        PlaySfx("ui");
        yield return new WaitForSeconds(8f);

        ResetAll();
    }
}
