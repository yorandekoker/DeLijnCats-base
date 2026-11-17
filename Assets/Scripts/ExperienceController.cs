using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "How many cats fit on one bus?" - a 30 second round.
/// 1. Attract mode: the bus waits, the driver cat is pulsing. 2. Tap the driver: he climbs in and the bus drives off.
/// 3. The bus drives straight on. Tap the cats you pass to let them board (miss them and they are gone).
/// 4. After 30 seconds the bus stops: result screen, then back to attract mode.
/// More cats on board = less CO2 per cat.
/// </summary>
public partial class ExperienceController : MonoBehaviour
{
    public enum State { Attract, Starting, Driving, Ending, Ended }

    [Header("Scene (wired by Tools > De Lijn > Build Scene)")]
    public GameObject camOverview;
    public GameObject camDriver;
    public GameObject camFollow;
    public Transform bus;
    public Cat driver;
    public Transform driverSeat;
    public Transform[] seats;
    public Cat[] passengers;

    [Header("Game")]
    public float roundSeconds = 30f;
    public float busSpeed = 8f;
    public float pickupReach = 6f;          // a cat can be tapped until the bus is this far past it
    public float idleSeconds = 25f;         // result screen goes back to attract mode after this
    public float attractCameraSeconds = 6f;

    public State state { get; private set; } = State.Attract;

    // De Lijn palette
    static readonly Color Yellow = new Color32(255, 221, 0, 255);
    static readonly Color Blue = new Color32(0, 0, 153, 255);
    static readonly Color DarkGrey = new Color32(102, 102, 102, 255);
    static readonly Color LightGrey = new Color32(204, 204, 204, 255);
    static readonly Color White = new Color32(255, 255, 255, 255);
    static readonly Color Card = new Color(1f, 1f, 1f, 0.93f);

    int riders;
    int seatsUsed;
    float timeLeft;
    float speed;
    float lastTouch;
    float nextAttractCam;
    float nextAttractHop;
    bool attractOnDriver;
    Vector3 busHome;
    Camera cam;
    Font font;
    Canvas canvas;
    Text titleText, hintText, catsLabel, co2Label, timerLabel, endText;
    RectTransform fillRect, playAgainRect;
    GameObject hud, timerCard, endPanel;
    GameObject currentCam;
    bool hasCinemachine;

    partial void InitAudio();
    partial void PlaySfx(string sfxName);

    void Start()
    {
        cam = Camera.main;
        font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans" }, 48);
        busHome = bus.position;
        foreach (var c in AllCats())
        {
            c.homePosition = c.transform.position;
            c.homeRotation = c.transform.rotation;
            c.homeParent = c.transform.parent;
        }
        hasCinemachine = FindVcam(camOverview) != null;
        BuildUi();
        InitAudio();
        ResetAll();
    }

    System.Collections.Generic.IEnumerable<Cat> AllCats()
    {
        yield return driver;
        foreach (var c in passengers) yield return c;
    }

    void Update()
    {
        Vector2 p;
        if (Pointer.Down(out p))
        {
            lastTouch = Time.time;
            HandleTap(p);
        }
        PulseRings();

        switch (state)
        {
            case State.Attract: TickAttract(); break;
            case State.Driving: TickDriving(); break;
            case State.Ending: TickEnding(); break;
            case State.Ended:
                if (Time.time - lastTouch > idleSeconds) ResetAll();
                break;
        }

        // Fallback when Cinemachine is not installed: glide the main camera to the active camera position.
        if (!hasCinemachine && currentCam != null && cam != null)
        {
            var target = currentCam.transform;
            cam.transform.position = Vector3.Lerp(cam.transform.position, target.position, 3f * Time.deltaTime);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, target.rotation, 3f * Time.deltaTime);
        }
    }

    // ------------------------------------------------------------ input

    void HandleTap(Vector2 pos)
    {
        if (state == State.Ended)
        {
            if (Hit(playAgainRect, pos)) ResetAll();     // interaction 3
            return;
        }
        if (state != State.Attract && state != State.Driving) return;

        RaycastHit hit;
        if (!Physics.Raycast(cam.ScreenPointToRay(pos), out hit, 300f)) return;
        var cat = hit.collider.GetComponentInParent<Cat>();
        if (cat == null || cat.boarded || cat.missed) return;

        if (state == State.Attract && cat == driver)
            StartCoroutine(StartRound());                // interaction 1: tap the driver
        else if (state == State.Driving && cat != driver && seatsUsed < seats.Length)
            StartCoroutine(Board(cat));                  // interaction 2: tap the cats you pass
    }

    // ------------------------------------------------------------ the round

    IEnumerator StartRound()
    {
        state = State.Starting;
        titleText.text = "";
        hintText.text = "";
        hintText.rectTransform.localScale = Vector3.one;
        HideRing(driver);
        driver.boarded = true;
        driver.Hop();
        PlaySfx("board");
        ShowCam(camFollow);
        yield return FlyTo(driver.transform, driverSeat, 0.8f);

        riders = 1;
        seatsUsed = 0;
        timeLeft = roundSeconds;
        speed = 0f;
        hud.SetActive(true);
        timerCard.SetActive(true);
        UpdateHud();
        hintText.text = "Tap the cats you pass!";
        PlaySfx("go");
        state = State.Driving;
    }

    IEnumerator Board(Cat cat)
    {
        cat.boarded = true;
        var seat = seats[seatsUsed];
        seatsUsed++;
        riders++;
        UpdateHud();
        HideRing(cat);
        cat.Hop();
        PlaySfx("board");
        yield return FlyTo(cat.transform, seat, 0.6f);
    }

    IEnumerator FlyTo(Transform t, Transform seat, float seconds)
    {
        Vector3 start = t.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            Vector3 pos = Vector3.Lerp(start, seat.position, Mathf.SmoothStep(0f, 1f, k));
            pos.y += Mathf.Sin(k * Mathf.PI) * 1.5f;
            t.position = pos;
            yield return null;
        }
        t.SetParent(seat, true);
        t.position = seat.position;
    }

    void TickDriving()
    {
        speed = Mathf.MoveTowards(speed, busSpeed, 5f * Time.deltaTime);
        bus.position += Vector3.right * speed * Time.deltaTime;

        timeLeft -= Time.deltaTime;
        timerLabel.text = "0:" + Mathf.Clamp(Mathf.CeilToInt(timeLeft), 0, 99).ToString("00");

        foreach (var c in passengers)
        {
            if (c.boarded || c.missed) continue;
            if (bus.position.x - c.transform.position.x > pickupReach)
            {
                c.missed = true;
                HideRing(c);
            }
        }

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            timerLabel.text = "0:00";
            hintText.text = "";
            state = State.Ending;
        }
    }

    void TickEnding()
    {
        speed = Mathf.MoveTowards(speed, 0f, 6f * Time.deltaTime);
        bus.position += Vector3.right * speed * Time.deltaTime;
        if (speed <= 0.01f) ShowEnd();
    }

    void ShowEnd()
    {
        state = State.Ended;
        lastTouch = Time.time;
        int n = Mathf.Max(1, riders);
        endText.text = n + (n == 1 ? " cat" : " cats") + " on one bus!\nCO2 per cat: " + Mathf.RoundToInt(100f / n) + "%\n(alone in a car: 100%)";
        hud.SetActive(false);
        timerCard.SetActive(false);
        endPanel.SetActive(true);
        PlaySfx("ui");
    }

    // ------------------------------------------------------------ attract mode and reset

    void TickAttract()
    {
        float s = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
        hintText.rectTransform.localScale = new Vector3(s, s, 1f);

        if (Time.time > nextAttractCam)
        {
            attractOnDriver = !attractOnDriver;
            ShowCam(attractOnDriver ? camDriver : camOverview);
            nextAttractCam = Time.time + attractCameraSeconds;
        }
        if (Time.time > nextAttractHop)
        {
            driver.Hop();
            nextAttractHop = Time.time + 3f;
        }
    }

    void ResetAll()
    {
        StopAllCoroutines();
        state = State.Attract;
        riders = 0;
        seatsUsed = 0;
        speed = 0f;
        bus.position = busHome;
        foreach (var c in AllCats())
        {
            c.boarded = false;
            c.missed = false;
            c.transform.SetParent(c.homeParent, true);
            c.transform.position = c.homePosition;
            c.transform.rotation = c.homeRotation;
            if (c.ring != null) c.ring.gameObject.SetActive(true);
        }
        hud.SetActive(false);
        timerCard.SetActive(false);
        endPanel.SetActive(false);
        titleText.text = "How many cats fit on one bus?";
        hintText.text = "Tap the driver cat to start the bus";
        attractOnDriver = false;
        nextAttractCam = Time.time + attractCameraSeconds;
        nextAttractHop = Time.time + 1.5f;
        ShowCam(camOverview);
        lastTouch = Time.time;
    }

    // ------------------------------------------------------------ small helpers

    void UpdateHud()
    {
        float share = 1f / Mathf.Max(1, riders);
        catsLabel.text = "Cats on board: " + riders;
        co2Label.text = "CO2 per cat: " + Mathf.RoundToInt(share * 100f) + "%";
        fillRect.anchorMax = new Vector2(share, 1f);
    }

    void PulseRings()
    {
        float s = 1f + 0.15f * Mathf.Sin(Time.time * 4f);
        foreach (var c in AllCats())
            if (c != null && c.ring != null && !c.boarded && !c.missed)
                c.ring.localScale = new Vector3(c.ringSize * s, 0.02f, c.ringSize * s);
    }

    static void HideRing(Cat c)
    {
        if (c.ring != null) c.ring.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ cameras

    void ShowCam(GameObject target)
    {
        currentCam = target;
        foreach (var g in new[] { camOverview, camDriver, camFollow })
        {
            if (g == null) continue;
            var v = FindVcam(g);
            if (v != null) SetPriority(v, g == target ? 20 : 10);
        }
    }

    static Component FindVcam(GameObject go)
    {
        if (go == null) return null;
        foreach (var c in go.GetComponents<Component>())
        {
            if (c == null) continue;
            var n = c.GetType().Name;
            if (n == "CinemachineCamera" || n == "CinemachineVirtualCamera") return c;
        }
        return null;
    }

    // Cinemachine 2 stores Priority as an int, Cinemachine 3 as a small struct. This handles both.
    static void SetPriority(Component vcam, int value)
    {
        var t = vcam.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance;
        var field = t.GetField("Priority", flags);
        var prop = t.GetProperty("Priority", flags);
        object current = field != null ? field.GetValue(vcam) : (prop != null ? prop.GetValue(vcam, null) : null);

        if (current == null)
        {
            var old = t.GetField("m_Priority", flags);
            if (old != null) old.SetValue(vcam, value);
            return;
        }
        if (current is int)
        {
            if (field != null) field.SetValue(vcam, value);
            else prop.SetValue(vcam, value, null);
            return;
        }
        var ct = current.GetType();
        var vf = ct.GetField("Value", flags);
        var vp = ct.GetProperty("Value", flags);
        if (vf != null) vf.SetValue(current, value);
        else if (vp != null) vp.SetValue(current, value, null);
        var ef = ct.GetField("Enabled", flags);
        var ep = ct.GetProperty("Enabled", flags);
        if (ef != null) ef.SetValue(current, true);
        else if (ep != null) ep.SetValue(current, true, null);
        if (field != null) field.SetValue(vcam, current);
        else prop.SetValue(vcam, current, null);
    }

    // ------------------------------------------------------------ UI (scales with the screen, fingers only)

    void BuildUi()
    {
        var root = new GameObject("UI");
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        titleText = Label(canvas.transform, "Title", "", 92, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(1840f, 170f), new Vector2(0f, -40f), true);
        hintText = Label(canvas.transform, "Hint", "", 64, DarkGrey, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(1700f, 120f), new Vector2(0f, 50f), true);

        // Top-left: cats on board, CO2 per cat and a bar.
        hud = Box(canvas.transform, "Hud", Card, new Vector2(0f, 1f), new Vector2(860f, 250f), new Vector2(40f, -40f));
        catsLabel = Label(hud.transform, "Cats", "", 60, Blue, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(800f, 80f), new Vector2(30f, -20f), false);
        catsLabel.fontStyle = FontStyle.Bold;
        co2Label = Label(hud.transform, "Co2", "", 52, DarkGrey, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(800f, 70f), new Vector2(30f, -100f), false);
        var track = Box(hud.transform, "Track", LightGrey, new Vector2(0f, 0f), new Vector2(800f, 36f), new Vector2(30f, 28f));
        var fill = Box(track.transform, "Fill", Blue, new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Top-right: timer.
        timerCard = Box(canvas.transform, "TimerCard", Yellow, new Vector2(1f, 1f), new Vector2(330f, 170f), new Vector2(-40f, -40f));
        timerLabel = Label(timerCard.transform, "Timer", "0:30", 110, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(330f, 170f), Vector2.zero, false);
        timerLabel.fontStyle = FontStyle.Bold;

        // Result panel.
        endPanel = Box(canvas.transform, "EndPanel", Blue, new Vector2(0.5f, 0.5f), new Vector2(1500f, 780f), Vector2.zero);
        endText = Label(endPanel.transform, "Result", "", 88, Yellow, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(1400f, 380f), new Vector2(0f, -40f), false);
        endText.fontStyle = FontStyle.Bold;
        Label(endPanel.transform, "Message", "Travel together = less CO2 per person", 56, White, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(1400f, 90f), new Vector2(0f, -70f), false);
        var again = Box(endPanel.transform, "PlayAgain", Yellow, new Vector2(0.5f, 0f), new Vector2(560f, 150f), new Vector2(0f, 50f));
        playAgainRect = (RectTransform)again.transform;
        var againText = Label(again.transform, "Text", "Play again", 76, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(560f, 150f), Vector2.zero, false);
        againText.fontStyle = FontStyle.Bold;
    }

    Text Label(Transform parent, string objName, string content, int size, Color colour, TextAnchor align,
               Vector2 anchor, Vector2 sizeDelta, Vector2 position, bool outline)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = position;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.text = content;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(1f, 1f, 1f, 0.95f);
            o.effectDistance = new Vector2(4f, -4f);
        }
        return t;
    }

    GameObject Box(Transform parent, string objName, Color colour, Vector2 anchor, Vector2 sizeDelta, Vector2 position)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = position;
        var img = go.AddComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        return go;
    }

    static bool Hit(RectTransform rect, Vector2 screenPos)
    {
        return rect != null && rect.gameObject.activeInHierarchy &&
               RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
    }
}
