using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Core of the experience: UI, camera switching, tapping a cat to board the bus, and the CO2-per-cat meter.
/// Attract mode / reset, the journey (Go, compare, end) and sound live in the other partial files.
/// </summary>
public partial class ExperienceController : MonoBehaviour
{
    public enum State { Attract, Boarding, Departing, Compare, End }

    [Header("Scene (wired by Tools > De Lijn > Build Scene)")]
    public GameObject camOverview;
    public GameObject camCat;
    public GameObject camBus;
    public Transform bus;
    public Transform[] seats;
    public Cat[] cats;
    public GameObject carCloud;

    public State state { get; private set; } = State.Attract;

    // De Lijn palette
    static readonly Color Yellow = new Color32(255, 221, 0, 255);
    static readonly Color Blue = new Color32(0, 0, 153, 255);
    static readonly Color DarkGrey = new Color32(102, 102, 102, 255);
    static readonly Color LightGrey = new Color32(204, 204, 204, 255);
    static readonly Color White = new Color32(255, 255, 255, 255);

    int boardedCount;
    float lastTouch;
    Vector3 busHome;
    Camera cam;
    Font font;
    Canvas canvas;
    Text titleText, hintText, meterLabel;
    RectTransform meterRect, meterFillRect;
    GameObject currentCam;
    bool hasCinemachine;

    // Hooks implemented in the other partial files (a hook with no implementation is simply skipped).
    partial void InitAttract();
    partial void InitJourney();
    partial void InitAudio();
    partial void OnPointerDown(Vector2 position, ref bool handled);
    partial void OnBoarded();
    partial void OnResetAttract();
    partial void OnResetJourney();
    partial void OnTickAttract();
    partial void OnTickJourney();
    partial void PlaySfx(string sfxName);

    void Start()
    {
        cam = Camera.main;
        font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans" }, 48);
        busHome = bus.position;
        foreach (var c in cats)
        {
            c.homePosition = c.transform.position;
            c.homeRotation = c.transform.rotation;
            c.homeParent = c.transform.parent;
        }
        hasCinemachine = FindVcam(camOverview) != null;

        BuildUi();
        InitAudio();
        InitJourney();
        InitAttract();
        ResetAll();
    }

    void Update()
    {
        Vector2 p;
        if (Pointer.Down(out p))
        {
            lastTouch = Time.time;
            bool handled = false;
            OnPointerDown(p, ref handled);
            if (!handled) HandleBoardTap(p);
        }
        PulseRings();
        OnTickAttract();
        OnTickJourney();

        // Fallback when Cinemachine is not installed: glide the main camera to the active camera position.
        if (!hasCinemachine && currentCam != null && cam != null)
        {
            var target = currentCam.transform;
            cam.transform.position = Vector3.Lerp(cam.transform.position, target.position, 3f * Time.deltaTime);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, target.rotation, 3f * Time.deltaTime);
        }
    }

    // ------------------------------------------------------------ boarding

    void HandleBoardTap(Vector2 screenPos)
    {
        if (state != State.Attract && state != State.Boarding) return;
        if (boardedCount >= seats.Length) return;

        var ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 200f)) return;
        var cat = hit.collider.GetComponentInParent<Cat>();
        if (cat == null || cat.boarded) return;

        if (state == State.Attract)
        {
            state = State.Boarding;
            ShowCam(camOverview);
            titleText.text = "How many cats fit on one bus?";
        }
        StartCoroutine(BoardRoutine(cat));
    }

    IEnumerator BoardRoutine(Cat cat)
    {
        cat.boarded = true;
        var seat = seats[boardedCount];
        boardedCount++;
        UpdateMeter();
        hintText.text = boardedCount >= seats.Length ? "The bus is full!" : "Tap another cat";
        if (cat == cats[0] && carCloud != null) carCloud.SetActive(false);
        if (cat.ring != null) cat.ring.gameObject.SetActive(false);
        cat.Hop();
        PlaySfx("board");

        Vector3 start = cat.transform.position;
        Vector3 end = seat.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            Vector3 pos = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            pos.y += Mathf.Sin(t * Mathf.PI) * 1.5f;
            cat.transform.position = pos;
            yield return null;
        }
        cat.transform.SetParent(seat, true);
        cat.transform.position = end;
        OnBoarded();
    }

    void PulseRings()
    {
        float s = 1f + 0.15f * Mathf.Sin(Time.time * 4f);
        foreach (var c in cats)
            if (c != null && c.ring != null && !c.boarded)
                c.ring.localScale = new Vector3(c.ringSize * s, 0.02f, c.ringSize * s);
    }

    // ------------------------------------------------------------ meter

    void UpdateMeter()
    {
        if (boardedCount == 0)
        {
            meterLabel.text = "CO2 per cat: 100% (alone in a car)";
            meterFillRect.anchorMax = new Vector2(1f, 1f);
        }
        else
        {
            float share = 1f / boardedCount;
            meterLabel.text = "CO2 per cat: " + Mathf.RoundToInt(share * 100f) + "% (" + boardedCount + (boardedCount == 1 ? " cat" : " cats") + " on the bus)";
            meterFillRect.anchorMax = new Vector2(share, 1f);
        }
    }

    // ------------------------------------------------------------ reset

    void ResetAll()
    {
        StopAllCoroutines();
        boardedCount = 0;
        bus.position = busHome;
        foreach (var c in cats)
        {
            c.boarded = false;
            c.transform.SetParent(c.homeParent, true);
            c.transform.position = c.homePosition;
            c.transform.rotation = c.homeRotation;
            if (c.ring != null) c.ring.gameObject.SetActive(true);
        }
        if (carCloud != null) carCloud.SetActive(true);
        state = State.Attract;
        ShowCam(camOverview);
        titleText.text = "How many cats fit on one bus?";
        hintText.text = "Tap a cat to board the bus";
        UpdateMeter();
        OnResetAttract();
        OnResetJourney();
        lastTouch = Time.time;
    }

    // ------------------------------------------------------------ cameras

    void ShowCam(GameObject target)
    {
        currentCam = target;
        var all = new[] { camOverview, camCat, camBus };
        foreach (var g in all)
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

        titleText = Label(canvas.transform, "Title", "", 80, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(1840f, 150f), new Vector2(0f, -30f));
        hintText = Label(canvas.transform, "Hint", "", 60, DarkGrey, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(1500f, 120f), new Vector2(0f, 40f));

        var meter = Box(canvas.transform, "Meter", White, new Vector2(0f, 1f), new Vector2(940f, 170f), new Vector2(40f, -210f));
        meterRect = (RectTransform)meter.transform;
        meterLabel = Label(meter.transform, "MeterLabel", "", 42, DarkGrey, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(900f, 80f), new Vector2(20f, -10f));
        var track = Box(meter.transform, "Track", LightGrey, new Vector2(0f, 0f), new Vector2(900f, 40f), new Vector2(20f, 22f));
        var fill = Box(track.transform, "Fill", Blue, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero);
        meterFillRect = (RectTransform)fill.transform;
        meterFillRect.anchorMin = Vector2.zero;
        meterFillRect.anchorMax = Vector2.one;
        meterFillRect.offsetMin = Vector2.zero;
        meterFillRect.offsetMax = Vector2.zero;
    }

    Text Label(Transform parent, string objName, string content, int size, Color colour, TextAnchor align,
               Vector2 anchor, Vector2 sizeDelta, Vector2 position)
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
