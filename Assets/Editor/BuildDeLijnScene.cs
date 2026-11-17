#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Menu: Tools > De Lijn > Build Scene
/// Builds the whole world in one click: a long straight road with bus stops, buildings and trees,
/// a yellow De Lijn bus with seats, a driver cat, nine waiting cats, three Cinemachine cameras and the game object.
/// Run it again at any time to rebuild the scene from scratch.
/// </summary>
public static class BuildDeLijnScene
{
    static readonly Color Yellow = new Color32(255, 221, 0, 255);
    static readonly Color Blue = new Color32(0, 0, 153, 255);
    static readonly Color Dark = new Color32(102, 102, 102, 255);
    static readonly Color Mid = new Color32(153, 153, 153, 255);
    static readonly Color Light = new Color32(204, 204, 204, 255);
    static readonly Color White = new Color32(255, 255, 255, 255);
    static readonly Color Black = new Color32(34, 34, 34, 255);
    static readonly Color Sky = new Color32(205, 228, 246, 255);

    const int PassengerCount = 9;
    const int SeatCount = 9;   // one seat per waiting cat

    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
    static bool cinemachineMissing;

    [MenuItem("Tools/De Lijn/Build Scene")]
    public static void Build()
    {
        cache.Clear();
        cinemachineMissing = false;
        EnsureFolder("Assets/Generated");
        EnsureFolder("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ---- Camera and light ----
        var mainCam = Camera.main;
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = Sky;
        mainCam.farClipPlane = 400f;
        var brainType = FindType("Unity.Cinemachine.CinemachineBrain", "Cinemachine.CinemachineBrain");
        if (brainType != null) { if (mainCam.GetComponent(brainType) == null) mainCam.gameObject.AddComponent(brainType); }
        else cinemachineMissing = true;

        var lightObj = GameObject.Find("Directional Light");
        if (lightObj != null)
        {
            lightObj.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var l = lightObj.GetComponent<Light>();
            if (l != null) { l.intensity = 1.15f; l.shadows = LightShadows.Soft; l.shadowBias = 0.05f; l.shadowNormalBias = 1f; }
        }

        BuildWorld();

        // ---- Bus ----
        var bus = BuildBus(out var driverSeat, out var seats);

        // ---- Cats ----
        var controller = BuildCatAnimator();
        var driverCat = BuildCat("Driver", new Vector3(7.5f, 0f, -4.4f), new Color32(255, 255, 255, 255), true, controller);
        var cols = new[]
        {
            new Color32(232, 162, 74, 255), new Color32(255, 255, 255, 255), new Color32(70, 70, 70, 255),
            new Color32(204, 204, 204, 255), new Color32(232, 162, 74, 255), new Color32(245, 232, 210, 255),
            new Color32(70, 70, 70, 255), new Color32(255, 255, 255, 255), new Color32(232, 162, 74, 255),
        };
        var passengers = new Cat[PassengerCount];
        for (int i = 0; i < PassengerCount; i++)
        {
            float x = StopX(i);
            float z = (i % 2 == 0) ? -4.5f : -3.9f;
            passengers[i] = BuildCat("Cat_" + (i + 1), new Vector3(x, 0f, z), cols[i % cols.Length], false, controller).GetComponent<Cat>();
        }

        // ---- Three Cinemachine cameras ----
        var camOverview = MakeVcam("Cam_Overview", new Vector3(11f, 6.5f, -21f), new Vector3(11f, 1.8f, 0f), null);
        var camDriver = MakeVcam("Cam_Driver", new Vector3(7.5f, 2.4f, -9f), new Vector3(7.5f, 1.4f, 0f), null);
        // This one rides along with the bus, so the road scrolls past and the cats come into view.
        var camFollow = MakeVcam("Cam_Follow", new Vector3(8.5f, 3.9f, -16.5f), new Vector3(4.5f, 1.6f, 0f), bus.transform);

        // ---- The game ----
        var exp = new GameObject("Experience").AddComponent<ExperienceController>();
        exp.camOverview = camOverview;
        exp.camDriver = camDriver;
        exp.camFollow = camFollow;
        exp.bus = bus.transform;
        exp.driver = driverCat.GetComponent<Cat>();
        exp.driverSeat = driverSeat;
        exp.seats = seats;
        exp.passengers = passengers;
        var mixer = BuildMixer();
        if (mixer != null)
        {
            exp.ambientGroup = FindGroup(mixer, "Ambient");
            exp.musicGroup = FindGroup(mixer, "Music");
            exp.sfxGroup = FindGroup(mixer, "SFX");
            exp.uiGroup = FindGroup(mixer, "UI");
        }

        // ---- Save ----
        const string scenePath = "Assets/Scenes/Museum.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool inList = false;
        foreach (var bs in buildScenes) if (bs.path == scenePath) inList = true;
        if (!inList) { buildScenes.Add(new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = buildScenes.ToArray(); }
        AssetDatabase.SaveAssets();

        if (cinemachineMissing)
            Debug.LogWarning("De Lijn: Cinemachine is not installed. Open Window > Package Manager, install 'Cinemachine', then run Tools > De Lijn > Build Scene again.");
        else
            Debug.Log("De Lijn: scene built and saved to " + scenePath + ". Press Play.");
    }

    static AudioMixerGroup FindGroup(AudioMixer m, string n)
    {
        var g = m.FindMatchingGroups(n);
        return (g != null && g.Length > 0) ? g[0] : null;
    }

    // Creates Assets/Audio/MainMixer.mixer with Ambient, Music, SFX and UI groups under Master (once).
    static AudioMixer BuildMixer()
    {
        const string path = "Assets/Audio/MainMixer.mixer";
        EnsureFolder("Assets/Audio");
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        if (existing != null) return existing;
        try
        {
            var ctrlType = FindType("UnityEditor.Audio.AudioMixerController");
            var create = ctrlType.GetMethod("CreateMixerControllerAtPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var ctrl = create.Invoke(null, new object[] { path });
            var master = ctrlType.GetProperty("masterGroup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ctrl, null);
            var newGroup = ctrlType.GetMethod("CreateNewGroup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var addChild = ctrlType.GetMethod("AddChildToParent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var n in new[] { "Ambient", "Music", "SFX", "UI" })
            {
                var g = newGroup.Invoke(ctrl, new object[] { n, true });
                addChild.Invoke(ctrl, new object[] { g, master });
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("De Lijn: could not create the Audio Mixer automatically (" + e.Message + "). Create one via Assets > Create > Audio > Audio Mixer with groups Ambient, Music, SFX, UI.");
        }
        return AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
    }

    static float StopX(int i) { return 24f + i * 22f; }

    // ---------------------------------------------------------------- world

    static void BuildWorld()
    {
        var world = new GameObject("World").transform;

        Prim(PrimitiveType.Cube, "Ground", world, new Vector3(100f, -0.1f, 0f), new Vector3(420f, 0.2f, 80f), new Color32(236, 238, 232, 255));
        Prim(PrimitiveType.Cube, "Road", world, new Vector3(100f, 0.02f, 0f), new Vector3(420f, 0.06f, 6.4f), Dark);
        Prim(PrimitiveType.Cube, "SidewalkNear", world, new Vector3(100f, 0.03f, -5.4f), new Vector3(420f, 0.06f, 3.6f), Light);
        Prim(PrimitiveType.Cube, "SidewalkFar", world, new Vector3(100f, 0.03f, 5.4f), new Vector3(420f, 0.06f, 3.6f), Light);
        Prim(PrimitiveType.Cube, "KerbNear", world, new Vector3(100f, 0.06f, -3.4f), new Vector3(420f, 0.12f, 0.25f), White);
        Prim(PrimitiveType.Cube, "KerbFar", world, new Vector3(100f, 0.06f, 3.4f), new Vector3(420f, 0.12f, 0.25f), White);

        for (float x = -30f; x < 290f; x += 6f)
            Prim(PrimitiveType.Cube, "Dash", world, new Vector3(x, 0.06f, 0f), new Vector3(2.6f, 0.02f, 0.2f), White);

        // Buildings (soft greys and whites, one yellow one now and then).
        var rng = new System.Random(11);
        var palette = new[] { new Color32(244, 246, 248, 255), new Color32(222, 228, 236, 255), new Color32(200, 210, 224, 255), new Color32(255, 255, 255, 255) };
        float bx = -30f;
        while (bx < 290f)
        {
            float w = 8f + (float)rng.NextDouble() * 6f;
            float h = 4f + (float)rng.NextDouble() * 7f;
            Color c = rng.Next(0, 9) == 0 ? (Color)Yellow : (Color)palette[rng.Next(0, palette.Length)];
            Prim(PrimitiveType.Cube, "Building", world, new Vector3(bx + w / 2f, h / 2f, 15f), new Vector3(w, h, 8f), c);
            // a blue roof edge for a bit of De Lijn colour
            Prim(PrimitiveType.Cube, "RoofEdge", world, new Vector3(bx + w / 2f, h + 0.1f, 15f), new Vector3(w + 0.2f, 0.2f, 8.2f), Blue);
            bx += w + 1.2f;
        }

        // Trees along the far sidewalk.
        for (float x = -26f; x < 290f; x += 9f)
        {
            float jitter = (float)rng.NextDouble() * 3f;
            Prim(PrimitiveType.Cylinder, "TreeTrunk", world, new Vector3(x + jitter, 1f, 7.6f), new Vector3(0.35f, 1f, 0.35f), new Color32(120, 90, 60, 255));
            Prim(PrimitiveType.Sphere, "TreeCrown", world, new Vector3(x + jitter, 3.0f, 7.6f), new Vector3(2.8f, 2.8f, 2.8f), (rng.Next(0, 2) == 0) ? new Color32(127, 176, 105, 255) : new Color32(156, 203, 134, 255));
        }

        // Bus stops: a pole with a yellow sign and a blue square, behind each waiting cat.
        for (int i = 0; i < PassengerCount; i++)
        {
            float x = StopX(i) + 1.4f;
            Prim(PrimitiveType.Cylinder, "StopPole", world, new Vector3(x, 1.3f, -6.2f), new Vector3(0.12f, 1.3f, 0.12f), Dark);
            Prim(PrimitiveType.Cube, "StopSign", world, new Vector3(x, 2.9f, -6.2f), new Vector3(0.9f, 0.9f, 0.08f), Yellow);
            Prim(PrimitiveType.Cube, "StopSignBlue", world, new Vector3(x, 2.9f, -6.25f), new Vector3(0.45f, 0.45f, 0.06f), Blue);
        }
        // A stop at the start too, where the driver waits.
        Prim(PrimitiveType.Cylinder, "StopPole", world, new Vector3(9.2f, 1.3f, -6.2f), new Vector3(0.12f, 1.3f, 0.12f), Dark);
        Prim(PrimitiveType.Cube, "StopSign", world, new Vector3(9.2f, 2.9f, -6.2f), new Vector3(0.9f, 0.9f, 0.08f), Yellow);
        Prim(PrimitiveType.Cube, "StopSignBlue", world, new Vector3(9.2f, 2.9f, -6.25f), new Vector3(0.45f, 0.45f, 0.06f), Blue);
    }

    // ---------------------------------------------------------------- bus

    static GameObject BuildBus(out Transform driverSeat, out Transform[] seats)
    {
        var bus = new GameObject("Bus");
        var t = bus.transform;
        Prim(PrimitiveType.Cube, "BusBase", t, new Vector3(0f, 0.75f, 0f), new Vector3(9f, 0.9f, 2.4f), Yellow);
        Prim(PrimitiveType.Cube, "BusStripe", t, new Vector3(0f, 0.55f, 0f), new Vector3(9.02f, 0.18f, 2.42f), Blue);
        Prim(PrimitiveType.Cube, "BusRoof", t, new Vector3(0f, 3.3f, 0f), new Vector3(9.3f, 0.22f, 2.6f), Yellow);
        Prim(PrimitiveType.Cube, "BusRoofEdge", t, new Vector3(0f, 3.15f, 0f), new Vector3(9.32f, 0.08f, 2.62f), Blue);
        Prim(PrimitiveType.Cube, "Windscreen", t, new Vector3(4.5f, 2.3f, 0f), new Vector3(0.12f, 1.9f, 2.3f), new Color32(190, 215, 240, 255));
        Prim(PrimitiveType.Cube, "RearPanel", t, new Vector3(-4.5f, 2.3f, 0f), new Vector3(0.12f, 1.9f, 2.3f), Yellow);
        Prim(PrimitiveType.Cube, "Destination", t, new Vector3(4.56f, 3.0f, 0f), new Vector3(0.06f, 0.3f, 1.5f), Black);
        foreach (var z in new[] { -0.8f, 0.8f })
            Prim(PrimitiveType.Sphere, "Headlight", t, new Vector3(4.55f, 0.85f, z), new Vector3(0.3f, 0.3f, 0.3f), White);
        foreach (var px in new[] { -4.4f, -1.5f, 1.5f, 4.4f })
            Prim(PrimitiveType.Cube, "PillarBack", t, new Vector3(px, 2.25f, 1.15f), new Vector3(0.15f, 2.1f, 0.15f), Blue);
        foreach (var px in new[] { -4.4f, 4.4f })
            Prim(PrimitiveType.Cube, "PillarFront", t, new Vector3(px, 2.25f, -1.15f), new Vector3(0.15f, 2.1f, 0.15f), Blue);
        foreach (var x in new[] { -3f, 3f })
            foreach (var z in new[] { -1.25f, 1.25f })
            {
                var w = Prim(PrimitiveType.Cylinder, "Wheel", t, new Vector3(x, 0.5f, z), new Vector3(1f, 0.2f, 1f), Black);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        // Seats, a driver seat at the front, with a little seat block under each.
        seats = new Transform[SeatCount];
        for (int i = 0; i < SeatCount; i++)
        {
            var s = new GameObject("Seat_" + i).transform;
            s.SetParent(t, false);
            s.localPosition = new Vector3(-3.2f + i * 0.8f, 1.2f, 0f);
            seats[i] = s;
        }
        var ds = new GameObject("DriverSeat").transform;
        ds.SetParent(t, false);
        ds.localPosition = new Vector3(3.7f, 1.2f, 0.2f);
        driverSeat = ds;
        return bus;
    }

    // ---------------------------------------------------------------- cats

    static GameObject BuildCat(string name, Vector3 position, Color colour, bool isDriver, RuntimeAnimatorController controller)
    {
        var cat = new GameObject(name);
        cat.transform.position = position;

        // The Animator moves the "Model" child up and down; the root is moved by the game.
        var model = new GameObject("Model").transform;
        model.SetParent(cat.transform, false);
        model.localScale = Vector3.one * 0.85f;

        Prim(PrimitiveType.Sphere, "Body", model, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.8f, 1.0f), colour);
        Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 1.0f, -0.25f), new Vector3(0.75f, 0.65f, 0.65f), colour);
        var earL = Prim(PrimitiveType.Cube, "EarL", model, new Vector3(-0.24f, 1.38f, -0.25f), new Vector3(0.24f, 0.24f, 0.1f), colour);
        earL.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var earR = Prim(PrimitiveType.Cube, "EarR", model, new Vector3(0.24f, 1.38f, -0.25f), new Vector3(0.24f, 0.24f, 0.1f), colour);
        earR.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Prim(PrimitiveType.Sphere, "EyeL", model, new Vector3(-0.16f, 1.05f, -0.55f), new Vector3(0.11f, 0.13f, 0.08f), Black);
        Prim(PrimitiveType.Sphere, "EyeR", model, new Vector3(0.16f, 1.05f, -0.55f), new Vector3(0.11f, 0.13f, 0.08f), Black);
        Prim(PrimitiveType.Sphere, "Nose", model, new Vector3(0f, 0.93f, -0.58f), new Vector3(0.08f, 0.06f, 0.06f), new Color32(236, 130, 150, 255));
        var tail = Prim(PrimitiveType.Capsule, "Tail", model, new Vector3(0.3f, 0.6f, 0.55f), new Vector3(0.14f, 0.4f, 0.14f), colour);
        tail.transform.localRotation = Quaternion.Euler(50f, 0f, -25f);

        if (isDriver)
        {
            // Yellow cap and a blue scarf.
            Prim(PrimitiveType.Cylinder, "Cap", model, new Vector3(0f, 1.34f, -0.25f), new Vector3(0.62f, 0.07f, 0.62f), Yellow);
            Prim(PrimitiveType.Sphere, "CapTop", model, new Vector3(0f, 1.36f, -0.25f), new Vector3(0.58f, 0.34f, 0.58f), Yellow);
            Prim(PrimitiveType.Cube, "CapBrim", model, new Vector3(0f, 1.3f, -0.62f), new Vector3(0.5f, 0.04f, 0.3f), Blue);
            Prim(PrimitiveType.Sphere, "Scarf", model, new Vector3(0f, 0.72f, -0.2f), new Vector3(0.72f, 0.22f, 0.72f), Blue);
        }

        // Blue ring on the ground: the "tap me" signifier. It pulses at runtime.
        var ring = Prim(PrimitiveType.Cylinder, "Ring", cat.transform, new Vector3(0f, 0.1f, 0f), new Vector3(1.5f, 0.02f, 1.5f), Blue);

        var anim = model.gameObject.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        var script = cat.AddComponent<Cat>();
        script.animator = anim;
        script.ring = ring.transform;

        // Generous invisible hit area so a finger can hit it easily.
        var sc = cat.AddComponent<SphereCollider>();
        sc.radius = 1.1f;
        sc.center = new Vector3(0f, 0.7f, 0f);
        return cat;
    }

    static RuntimeAnimatorController BuildCatAnimator()
    {
        const string path = "Assets/Generated/CatAnimator.controller";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.DeleteAsset("Assets/Generated/Cat_Idle.anim");
        AssetDatabase.DeleteAsset("Assets/Generated/Cat_Hop.anim");

        var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.y");

        var idle = new AnimationClip { name = "Cat_Idle" };
        AnimationUtility.SetEditorCurve(idle, binding,
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.04f), new Keyframe(2f, 0f)));
        var settings = AnimationUtility.GetAnimationClipSettings(idle);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(idle, settings);

        var hop = new AnimationClip { name = "Cat_Hop" };
        AnimationUtility.SetEditorCurve(hop, binding,
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 0.5f), new Keyframe(0.45f, 0f)));

        AssetDatabase.CreateAsset(idle, "Assets/Generated/Cat_Idle.anim");
        AssetDatabase.CreateAsset(hop, "Assets/Generated/Cat_Hop.anim");

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Hop", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        var sHop = sm.AddState("Hop");
        sHop.motion = hop;
        sm.defaultState = sIdle;

        var toHop = sIdle.AddTransition(sHop);
        toHop.AddCondition(AnimatorConditionMode.If, 0f, "Hop");
        toHop.hasExitTime = false;
        toHop.duration = 0.05f;

        var toIdle = sHop.AddTransition(sIdle);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 1f;
        toIdle.duration = 0.1f;

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ---------------------------------------------------------------- helpers

    static GameObject MakeVcam(string name, Vector3 position, Vector3 lookAt, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(lookAt - position);
        if (parent != null) go.transform.SetParent(parent, true);
        var t = FindType("Unity.Cinemachine.CinemachineCamera", "Cinemachine.CinemachineVirtualCamera");
        if (t != null) go.AddComponent(t);
        else cinemachineMissing = true;
        return go;
    }

    static Type FindType(params string[] names)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var n in names)
            {
                var t = asm.GetType(n);
                if (t != null) return t;
            }
        return null;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color colour)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Mat(colour);
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        return go;
    }

    static Material Mat(Color colour)
    {
        string key = ColorUtility.ToHtmlStringRGB(colour);
        Material m;
        if (cache.TryGetValue(key, out m) && m != null) return m;
        string path = "Assets/Generated/Mat_" + key + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(PickShader());
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = PickShader();
        m.color = colour;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        cache[key] = m;
        return m;
    }

    static Shader PickShader()
    {
        // Lit shading gives the shapes some depth; De Lijn colours stay recognisable.
        var s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        return s;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
