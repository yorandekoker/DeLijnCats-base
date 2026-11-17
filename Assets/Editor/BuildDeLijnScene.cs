#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > De Lijn > Build Scene
/// Builds the whole museum-entrance scene in one click: floor, two zones (car / bus) in De Lijn colours,
/// a car, an open bus with 5 seats, 5 cats (with Animator), and three Cinemachine cameras.
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

        // ---- Main camera (Cinemachine Brain drives it) ----
        var mainCam = Camera.main;
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = Light;
        var brainType = FindType("Unity.Cinemachine.CinemachineBrain", "Cinemachine.CinemachineBrain");
        if (brainType != null) { if (mainCam.GetComponent(brainType) == null) mainCam.gameObject.AddComponent(brainType); }
        else cinemachineMissing = true;

        // ---- Hall: floor, zones, back wall ----
        Prim(PrimitiveType.Plane, "Floor", null, Vector3.zero, new Vector3(3f, 1f, 2f), White);
        Prim(PrimitiveType.Cube, "Zone_Car", null, new Vector3(-6f, 0.02f, 0f), new Vector3(12f, 0.04f, 10f), Light);
        Prim(PrimitiveType.Cube, "Zone_Bus", null, new Vector3(6f, 0.02f, 0f), new Vector3(12f, 0.04f, 10f), Yellow);
        Prim(PrimitiveType.Cube, "BackWall", null, new Vector3(0f, 3f, 7f), new Vector3(30f, 6f, 0.2f), White);
        Prim(PrimitiveType.Cube, "WallStripe", null, new Vector3(0f, 0.6f, 6.85f), new Vector3(30f, 0.3f, 0.1f), Blue);

        // ---- Car (left zone) ----
        var car = new GameObject("Car");
        car.transform.position = new Vector3(-6f, 0f, 0f);
        Prim(PrimitiveType.Cube, "CarBody", car.transform, new Vector3(0f, 0.7f, 0f), new Vector3(4f, 0.9f, 2f), Dark);
        Prim(PrimitiveType.Cube, "Windshield", car.transform, new Vector3(0.9f, 1.45f, 0f), new Vector3(0.1f, 0.7f, 1.8f), Mid);
        foreach (var x in new[] { -1.3f, 1.3f })
            foreach (var z in new[] { -1.05f, 1.05f })
            {
                var w = Prim(PrimitiveType.Cylinder, "Wheel", car.transform, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.12f, 0.7f), Black);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        var cloud = Prim(PrimitiveType.Sphere, "CarCloud", null, new Vector3(-6f, 3.9f, 0f), new Vector3(2.8f, 1.5f, 1.5f), Mid);

        // ---- Bus (right zone): open sides so you can see the cats ----
        var bus = new GameObject("Bus");
        bus.transform.position = new Vector3(6f, 0f, 0f);
        Prim(PrimitiveType.Cube, "BusBase", bus.transform, new Vector3(0f, 0.75f, 0f), new Vector3(9f, 0.9f, 2.4f), Blue);
        Prim(PrimitiveType.Cube, "BusStripe", bus.transform, new Vector3(0f, 0.95f, 0f), new Vector3(9.02f, 0.25f, 2.42f), Yellow);
        Prim(PrimitiveType.Cube, "BusRoof", bus.transform, new Vector3(0f, 3.3f, 0f), new Vector3(9.2f, 0.2f, 2.6f), Blue);
        foreach (var px in new[] { -4.4f, 0f, 4.4f })
        {
            Prim(PrimitiveType.Cube, "Pillar", bus.transform, new Vector3(px, 2.25f, 1.15f), new Vector3(0.15f, 2.1f, 0.15f), Blue);
            if (Mathf.Abs(px) > 1f)
                Prim(PrimitiveType.Cube, "Pillar", bus.transform, new Vector3(px, 2.25f, -1.15f), new Vector3(0.15f, 2.1f, 0.15f), Blue);
        }
        foreach (var x in new[] { -3f, 3f })
            foreach (var z in new[] { -1.25f, 1.25f })
            {
                var w = Prim(PrimitiveType.Cylinder, "Wheel", bus.transform, new Vector3(x, 0.45f, z), new Vector3(0.9f, 0.2f, 0.9f), Black);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        var seats = new Transform[5];
        for (int i = 0; i < seats.Length; i++)
        {
            var s = new GameObject("Seat_" + i).transform;
            s.SetParent(bus.transform, false);
            s.localPosition = new Vector3(-3.2f + i * 1.6f, 1.2f, 0f);
            seats[i] = s;
        }

        // ---- Cats ----
        var controller = BuildCatAnimator();
        var catColours = new[] { White, White, Light, Dark, White };
        var catPositions = new[]
        {
            new Vector3(-6.4f, 1.15f, 0f),   // the lone cat in the car
            new Vector3(2.5f, 0f, -3.2f),
            new Vector3(4.5f, 0f, -3.2f),
            new Vector3(6.5f, 0f, -3.2f),
            new Vector3(8.5f, 0f, -3.2f),
        };
        var cats = new GameObject[5];
        for (int i = 0; i < cats.Length; i++)
            cats[i] = BuildCat(i, catPositions[i], catColours[i], controller);

        // ---- Three Cinemachine cameras ----
        var camOverview = MakeVcam("Cam_Overview", new Vector3(0f, 7.5f, -19f), new Vector3(0f, 1.5f, 0f), null);
        var camCat = MakeVcam("Cam_Cat", new Vector3(-6.4f, 3.2f, -6.5f), new Vector3(-6.4f, 1.8f, 0f), null);
        // This one rides along with the bus, so it keeps the bus in frame when it drives away.
        var camBus = MakeVcam("Cam_Bus", new Vector3(6f, 9.5f, -6.5f), new Vector3(6f, 1.5f, 0f), bus.transform);

        // ---- The experience: one object that runs everything ----
        var catComponents = new Cat[cats.Length];
        for (int i = 0; i < cats.Length; i++) catComponents[i] = cats[i].GetComponent<Cat>();
        var experience = new GameObject("Experience").AddComponent<ExperienceController>();
        experience.camOverview = camOverview;
        experience.camCat = camCat;
        experience.camBus = camBus;
        experience.bus = bus.transform;
        experience.seats = seats;
        experience.cats = catComponents;
        experience.carCloud = cloud;

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

    // ---------------------------------------------------------------- helpers

    static GameObject BuildCat(int index, Vector3 position, Color colour, RuntimeAnimatorController controller)
    {
        var cat = new GameObject("Cat_" + index);
        cat.transform.position = position;
        Prim(PrimitiveType.Sphere, "Body", cat.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.8f, 1.1f), colour);
        Prim(PrimitiveType.Sphere, "Head", cat.transform, new Vector3(0f, 0.95f, -0.3f), new Vector3(0.6f, 0.55f, 0.55f), colour);
        var earL = Prim(PrimitiveType.Cube, "EarL", cat.transform, new Vector3(-0.2f, 1.3f, -0.3f), new Vector3(0.18f, 0.22f, 0.08f), colour);
        earL.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        var earR = Prim(PrimitiveType.Cube, "EarR", cat.transform, new Vector3(0.2f, 1.3f, -0.3f), new Vector3(0.18f, 0.22f, 0.08f), colour);
        earR.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
        Prim(PrimitiveType.Sphere, "EyeL", cat.transform, new Vector3(-0.12f, 1.0f, -0.55f), new Vector3(0.09f, 0.09f, 0.09f), Black);
        Prim(PrimitiveType.Sphere, "EyeR", cat.transform, new Vector3(0.12f, 1.0f, -0.55f), new Vector3(0.09f, 0.09f, 0.09f), Black);
        var tail = Prim(PrimitiveType.Capsule, "Tail", cat.transform, new Vector3(0.0f, 0.65f, 0.65f), new Vector3(0.12f, 0.35f, 0.12f), colour);
        tail.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
        // Blue ring under the cat = "tap me" signifier. It pulses at runtime.
        var ring = Prim(PrimitiveType.Cylinder, "Ring", cat.transform, new Vector3(0f, 0.03f, 0f), new Vector3(1.6f, 0.02f, 1.6f), Blue);

        var anim = cat.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        var catScript = cat.AddComponent<Cat>();
        catScript.animator = anim;
        catScript.ring = ring.transform;

        // Generous invisible hit area so a finger can hit it easily.
        var sc = cat.AddComponent<SphereCollider>();
        sc.radius = 0.9f;
        sc.center = new Vector3(0f, 0.5f, 0f);
        return cat;
    }

    static RuntimeAnimatorController BuildCatAnimator()
    {
        const string path = "Assets/Generated/CatAnimator.controller";
        AssetDatabase.DeleteAsset(path);

        var idle = new AnimationClip { name = "Cat_Idle" };
        var idleBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y");
        AnimationUtility.SetEditorCurve(idle, idleBinding,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1.05f), new Keyframe(2f, 1f)));
        var settings = AnimationUtility.GetAnimationClipSettings(idle);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(idle, settings);

        var hop = new AnimationClip { name = "Cat_Hop" };
        AnimationUtility.SetEditorCurve(hop, idleBinding,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.15f, 0.75f), new Keyframe(0.3f, 1.2f), new Keyframe(0.5f, 1f)));

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
        m.color = colour;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(m);
        cache[key] = m;
        return m;
    }

    static Shader PickShader()
    {
        // Flat, unlit colours give a clean, professional look that matches the De Lijn palette exactly.
        var s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Standard");
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
