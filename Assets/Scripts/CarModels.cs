using UnityEngine;

/// <summary>
/// TurboRacing - v1.6.0 (سيارات حقيقية فقط)
/// الكراج = سيارات Racing Cars Pack 1 الثماني حصراً (نموذج FBX + نسيج الناشر)
/// بطلب المستخدم: أُزيلت كل السيارات الإجرائية (ARCADE GT + الموديلات المولّدة بالكود).
/// البناء الإجرائي يبقى احتياطياً للتطوير فقط عند غياب أصل الحزمة.
/// كل موديل = مواصفات أداء (سرعة/تسارع/توجيه/فرملة) + تطبيع مقاس 4.5م
/// + عجلات بمحاور قياسية متوافقة مع فيزياء MotionCore.
/// </summary>
public static class CarModels
{
    public struct CarSpec
    {
        public int id;
        public string name;
        public string tag;        // الأصل الملهم من Asset Store
        public int bodyType;      // 0..4 شكل الهيكل (-1 = النموذج الأصلي)
        public int packIndex;     // v1.5.0: 0..7 = سيارة حقيقية من Racing Cars Pack 1 (-1 = إجرائية)
        public Color paint;       // الطلاء الافتراضي (اللاعب يغيّره من الإعدادات)
        public Color accent;      // لون التمييز (خطوط/جناح/عناصر)
        public float topSpeed;    // كم/س (قبل معامل الضبط STOCK/SPORT/TURBO)
        public int accel;         // معامل عزم المحرك (يحوَّل إلى نيوتن.متر في MotionCore)
        public float steerAngle;  // أقصى زاوية توجيه
        public int brakeForce;
    }

    /// <summary>معامل تحويل أرقام المواصفات الأركيد إلى سرعات فيزياء MotionCore الحقيقية.</summary>
    public const float MotionCoreSpeedScale = 2.6f;

    public static readonly CarSpec[] All =
    {
        // ============ v1.5.0: سيارات Racing Cars Pack 1 الحقيقية (8) ============
        new CarSpec { id = 0,  name = "RCP RED",     tag = "RACING CARS PACK 1 #1", packIndex = 0, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(1f, 0.72f, 0.15f),
            topSpeed = 88f, accel = 10, steerAngle = 36f, brakeForce = 560 },
        new CarSpec { id = 1,  name = "RCP BLUE",    tag = "RACING CARS PACK 1 #2", packIndex = 1, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.20f, 0.45f, 0.90f),
            topSpeed = 87f, accel = 10, steerAngle = 36f, brakeForce = 550 },
        new CarSpec { id = 2,  name = "RCP GREEN",   tag = "RACING CARS PACK 1 #3", packIndex = 2, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.20f, 0.72f, 0.35f),
            topSpeed = 86f, accel = 9,  steerAngle = 37f, brakeForce = 545 },
        new CarSpec { id = 3,  name = "RCP GOLD",    tag = "RACING CARS PACK 1 #4", packIndex = 3, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.95f, 0.78f, 0.20f),
            topSpeed = 89f, accel = 10, steerAngle = 35f, brakeForce = 560 },
        new CarSpec { id = 4,  name = "RCP ORANGE",  tag = "RACING CARS PACK 1 #5", packIndex = 4, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.95f, 0.50f, 0.15f),
            topSpeed = 88f, accel = 10, steerAngle = 36f, brakeForce = 550 },
        new CarSpec { id = 5,  name = "RCP PURPLE",  tag = "RACING CARS PACK 1 #6", packIndex = 5, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.60f, 0.30f, 0.85f),
            topSpeed = 87f, accel = 9,  steerAngle = 37f, brakeForce = 545 },
        new CarSpec { id = 6,  name = "RCP CYAN",    tag = "RACING CARS PACK 1 #7", packIndex = 6, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.15f, 0.70f, 0.70f),
            topSpeed = 90f, accel = 10, steerAngle = 36f, brakeForce = 565 },
        new CarSpec { id = 7,  name = "RCP WHITE",   tag = "RACING CARS PACK 1 #8", packIndex = 7, bodyType = -1,
            paint = new Color(1f, 1f, 1f), accent = new Color(0.92f, 0.92f, 0.94f),
            topSpeed = 89f, accel = 10, steerAngle = 35f, brakeForce = 555 },

        // v1.6.0: أُزيلت السيارات الإجرائية (ARCADE GT + GT-5/MUSCLE/FORMULA/MINI/PICKUP)
        // بطلب المستخدم — الكراج سيارات Racing Cars Pack 1 الحقيقية حصراً
    };

    public static int Count => All.Length;

    public static CarSpec Get(int index) => All[Mathf.Clamp(index, 0, All.Length - 1)];

    public struct Handles
    {
        public Transform Root;       // جذر النموذج الكامل
        public MeshRenderer Paint;   // مادة الطلاء (تُلوَّن لاحقاً)
        public Transform[] Wheels;   // محاور العجلات FL FR RL RR
    }

    // مواضع العجلات القياسية (مطابقة للسيارة الإجرائية الكلاسيكية = فيزياء موحّدة لكل الموديلات)
    static readonly Vector3[] WheelPos =
    {
        new Vector3(-0.52f, 0.30f,  0.80f),
        new Vector3( 0.52f, 0.30f,  0.80f),
        new Vector3(-0.52f, 0.30f, -0.82f),
        new Vector3( 0.52f, 0.30f, -0.82f),
    };

    public static Handles Build(Transform parent, CarSpec spec)
    {
        // v1.5.0: سيارة حقيقية من Racing Cars Pack 1 (نموذج FBX + نسيج الناشر)
        if (spec.packIndex >= 0)
        {
            Handles real = TryBuildPackCar(parent, spec);
            if (real.Root != null) return real;
            // الأصل غائب (بيئة تطوير بلا مستودع الأصول): نشترط السقوط للإجرائي
        }

        return BuildProcedural(parent, spec);
    }

    /// <summary>بناء سيارة حقيقية من Racing Cars Pack 1 (v1.5.0): FBX + النسيج الأصلي
    /// + تطبيع المقاس (4.5م) والاتجاه (الأمام +Z) وتوسيط مراكز العجلات على عقد.</summary>
    static Handles TryBuildPackCar(Transform parent, CarSpec spec)
    {
        Handles h = new Handles();
        h.Wheels = new Transform[4];

        GameObject prefab = Resources.Load<GameObject>($"GameAssets/RacingCarsPack1/Car{spec.packIndex + 1}");
        if (prefab == null) return h;

        GameObject go = Object.Instantiate(prefab, parent);
        go.name = "CarModel_" + spec.name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        h.Root = go.transform;

        // اتجاه السيارة: عجلات الأمام يجب أن تكون عند +Z — وإلا ندير النموذج 180°
        Transform fl = FindDeep(go.transform, "Front_Left_Wheel");
        Transform rl = FindDeep(go.transform, "Rear_Left_Wheel");
        if (fl != null && rl != null && parent != null &&
            parent.InverseTransformPoint(fl.position).z < parent.InverseTransformPoint(rl.position).z)
        {
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // نسيج السيارة الأصلي من الحزمة (خامة Standard واحدة على كل الرندررات)
        Texture2D tex = Resources.Load<Texture2D>($"GameAssets/RacingCarsPack1/Car{spec.packIndex + 1}Texture");
        if (tex != null)
        {
            Material body = new Material(Shader.Find("Standard"));
            body.name = "CarPaint_" + spec.name;
            body.mainTexture = tex;
            body.color = Color.white; // لون النسيج الأصلي (التظليل من الإعدادات فوقه)
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int s = 0; s < mats.Length; s++) mats[s] = body;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                if (r.name == "CarBody") h.Paint = r as MeshRenderer;
            }
            if (h.Paint == null)
            {
                Renderer first = go.GetComponentInChildren<Renderer>();
                if (first != null) h.Paint = first as MeshRenderer;
            }
        }

        // تطبيع المقاس: طول مستهدف 4.5 متراً
        Bounds b = BoundsOf(go.transform);
        float length = Mathf.Max(b.size.x, b.size.z);
        if (length > 0.01f)
        {
            float s = 4.5f / length;
            go.transform.localScale = new Vector3(s, s, s);
        }

        // محاذاة مراكز العجلات: عقد العجلات في هذه الحزمة عند حافة العجلة العليا
        CenterWheelMeshes(go.transform);

        // v1.6.0 (إصلاح الغرق): رفع النموذج حتى تلمس العجلات أرض **جذر السيارة**
        // (المحلي y=0) لا أرض العالم! التثبيت العالمي على y=0 كان يغرق السيارات
        // المستوردة تحت سطح حلبة Cartoon Oval (سطحها أعلى من صفر العالم) فتفقد
        // العجلات تماسها → لا بنزين ولا توجيه (التحكم=0) والهيكل غارق في الأرض.
        b = BoundsOf(go.transform);
        Vector3 bottomWorld = new Vector3(go.transform.position.x, b.min.y, go.transform.position.z);
        float localBottom = parent != null
            ? parent.InverseTransformPoint(bottomWorld).y
            : b.min.y;
        go.transform.localPosition += new Vector3(0f, -localBottom, 0f);

        h.Wheels[0] = FindDeep(go.transform, "Front_Left_Wheel");
        h.Wheels[1] = FindDeep(go.transform, "Front_Right_Wheel");
        h.Wheels[2] = FindDeep(go.transform, "Rear_Left_Wheel");
        h.Wheels[3] = FindDeep(go.transform, "Rear_Right_Wheel");
        return h;
    }

    static void CenterWheelMeshes(Transform model)
    {
        string[] wheelNames = { "Front_Left_Wheel", "Front_Right_Wheel", "Rear_Left_Wheel", "Rear_Right_Wheel" };
        foreach (string wn in wheelNames)
        {
            Transform node = FindDeep(model, wn);
            if (node == null) continue;

            Renderer r = node.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            Vector3 localCenter = node.InverseTransformPoint(r.bounds.center);

            if (r.transform == node)
            {
                GameObject meshGo = new GameObject("Mesh");
                meshGo.transform.SetParent(node, false);
                MeshFilter mf = node.GetComponent<MeshFilter>();
                MeshRenderer mr = node.GetComponent<MeshRenderer>();
                if (mf != null)
                {
                    meshGo.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    Object.Destroy(mf);
                }
                if (mr != null)
                {
                    MeshRenderer newMr = meshGo.AddComponent<MeshRenderer>();
                    newMr.sharedMaterials = mr.sharedMaterials;
                    newMr.shadowCastingMode = mr.shadowCastingMode;
                    Object.Destroy(mr);
                }
                meshGo.transform.localPosition = -localCenter;
            }
            else
            {
                r.transform.localPosition -= localCenter;
            }
        }
    }

    static Bounds BoundsOf(Transform t)
    {
        Bounds b = new Bounds(t.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static Handles BuildProcedural(Transform parent, CarSpec spec)
    {
        Handles h = new Handles();
        h.Wheels = new Transform[4];

        Transform root = new GameObject("CarModel_" + spec.name).transform;
        root.SetParent(parent, false);
        h.Root = root;

        Transform bodyPivot = new GameObject("BodyPivot").transform;
        bodyPivot.SetParent(root, false);

        // ============ الهيكل الملوّن (لون الرؤوس أبيض؛ اللون من مادة الطلاء) ============
        var paint = new MeshAccumulator();
        Color white = Color.white;

        switch (Mathf.Clamp(spec.bodyType, 0, 4))
        {
            case 0: BuildGtSport(paint);  break;
            case 1: BuildMuscle(paint);   break;
            case 2: BuildFormula(paint);  break;
            case 3: BuildMini(paint);     break;
            case 4: BuildPickup(paint);   break;
        }

        h.Paint = Emit(root, bodyPivot, paint, "Paint", CarModel3D.MakeMat(0.60f, 0.40f));
        h.Paint.material.color = spec.paint;

        // ============ التفاصيل الثابتة (ألوان رأسية) ============
        var trim = new MeshAccumulator();
        Color glass  = new Color(0.10f, 0.13f, 0.17f);
        Color dark   = new Color(0.10f, 0.10f, 0.11f);
        Color chrome = new Color(0.55f, 0.56f, 0.58f);
        Color head   = new Color(1.00f, 0.97f, 0.85f);
        Color tail   = new Color(1.00f, 0.10f, 0.06f);
        Color acc    = spec.accent;

        switch (Mathf.Clamp(spec.bodyType, 0, 4))
        {
            case 0: TrimGtSport(trim, dark, glass, head, tail, chrome, acc);   break;
            case 1: TrimMuscle(trim, dark, glass, head, tail, chrome, acc);    break;
            case 2: TrimFormula(trim, dark, glass, head, tail, chrome, acc);   break;
            case 3: TrimMini(trim, dark, glass, head, tail, chrome, acc);      break;
            case 4: TrimPickup(trim, dark, glass, head, tail, chrome, acc);    break;
        }

        Emit(root, bodyPivot, trim, "Trim", CarModel3D.MakeMat(0.10f, 0.35f));

        // ============ العجلات (شبكة مشتركة) ============
        Mesh wheelMesh = CarModel3D.BuildWheelMesh();
        Material wheelMat = CarModel3D.MakeMat(0.05f, 0.30f);

        string[] names = { "WheelFL", "WheelFR", "WheelRL", "WheelRR" };
        for (int i = 0; i < 4; i++)
        {
            Transform pivot = new GameObject(names[i]).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = WheelPos[i];

            // الشبكة داخل المحور مدارة 90° حول Z: محور الإطار يصبح X = محور الدوران
            Transform meshT = new GameObject("Mesh").transform;
            meshT.SetParent(pivot, false);
            meshT.localRotation = Quaternion.Euler(0f, 0f, 90f);

            MeshFilter mf = meshT.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = wheelMesh;

            MeshRenderer mr = meshT.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = wheelMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            h.Wheels[i] = pivot;
        }

        return h;
    }

    static MeshRenderer Emit(Transform root, Transform parent, MeshAccumulator acc,
                             string name, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = acc.Build("CarModels_" + name + "_Mesh");

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        return mr;
    }

    // ================= أشكال الهياكل (كل واحدة بمشيتها المميزة) =================

    /// <summary>GT-5 SPORT: كوبيه منخفض انسيابي بذيل عريض (أسلوب Sport Car Free 5).</summary>
    static void BuildGtSport(MeshAccumulator p)
    {
        p.AddBox(new Vector3(0f, 0.38f, 0.02f),   new Vector3(1.00f, 0.30f, 2.20f), 0.5f, Color.white, Color.white); // الجسم
        p.AddBox(new Vector3(0f, 0.30f, 1.28f),   new Vector3(0.76f, 0.16f, 0.55f), 0.5f, Color.white, Color.white); // مقدمة منخفضة
        p.AddBox(new Vector3(0f, 0.50f, -1.02f),  new Vector3(0.94f, 0.20f, 0.28f), 0.5f, Color.white, Color.white); // سطح خلفي
        p.AddBox(new Vector3(0f, 0.62f, -0.05f),  new Vector3(0.70f, 0.24f, 0.95f), 0.5f, Color.white, Color.white); // قمرة منخفضة
        p.AddBox(new Vector3(0f, 0.755f, -0.20f), new Vector3(0.60f, 0.05f, 0.55f), 0.5f, Color.white, Color.white); // سقف
    }

    static void TrimGtSport(MeshAccumulator t, Color dark, Color glass, Color head, Color tail, Color chrome, Color acc)
    {
        t.AddBox(new Vector3(0f, 0.13f, 1.48f),  new Vector3(0.92f, 0.06f, 0.30f), 0.5f, dark, dark);      // فاصل أمامي
        t.AddBox(new Vector3(0f, 0.15f, -1.30f), new Vector3(0.86f, 0.08f, 0.22f), 0.5f, dark, dark);      // مفرّق خلفي
        t.AddBox(new Vector3(0f, 0.90f, -1.16f), new Vector3(1.00f, 0.05f, 0.32f), 0.5f, acc, acc);        // جناح كبير
        t.AddBox(new Vector3(-0.30f, 0.76f, -1.16f), new Vector3(0.05f, 0.22f, 0.10f), 0.5f, dark, dark);  // قائما جناح
        t.AddBox(new Vector3(0.30f, 0.76f, -1.16f),  new Vector3(0.05f, 0.22f, 0.10f), 0.5f, dark, dark);
        t.AddBox(new Vector3(0f, 0.62f, 0.42f),  new Vector3(0.66f, 0.20f, 0.07f), 0.5f, glass, glass);    // زجاج أمامي
        t.AddBox(new Vector3(0f, 0.62f, -0.56f), new Vector3(0.66f, 0.18f, 0.07f), 0.5f, glass, glass);    // زجاج خلفي
        t.AddBox(new Vector3(0f, 0.62f, -0.07f), new Vector3(0.74f, 0.15f, 0.62f), 0.5f, glass, glass);    // زجاج جانبي
        t.AddBox(new Vector3(-0.28f, 0.34f, 1.56f), new Vector3(0.22f, 0.09f, 0.05f), 0.5f, head, head);   // مصابيح أمامية
        t.AddBox(new Vector3(0.28f, 0.34f, 1.56f),  new Vector3(0.22f, 0.09f, 0.05f), 0.5f, head, head);
        t.AddBox(new Vector3(-0.30f, 0.52f, -1.17f), new Vector3(0.24f, 0.08f, 0.04f), 0.5f, tail, tail);  // مصابيح خلفية
        t.AddBox(new Vector3(0.30f, 0.52f, -1.17f),  new Vector3(0.24f, 0.08f, 0.04f), 0.5f, tail, tail);
        t.AddBox(new Vector3(-0.18f, 0.28f, -1.33f), new Vector3(0.10f, 0.09f, 0.07f), 0.5f, chrome, chrome); // عوادم
        t.AddBox(new Vector3(0.18f, 0.28f, -1.33f),  new Vector3(0.10f, 0.09f, 0.07f), 0.5f, chrome, chrome);
    }

    /// <summary>MUSCLE RS-4: عضلية عريضة بخطوط سباق وسقيفة شفط (أسلوب Sport Car Free 4).</summary>
    static void BuildMuscle(MeshAccumulator p)
    {
        p.AddBox(new Vector3(0f, 0.42f, 0.00f),   new Vector3(1.06f, 0.40f, 2.30f), 0.5f, Color.white, Color.white); // الجسم الضخم
        p.AddBox(new Vector3(0f, 0.33f, 1.30f),   new Vector3(0.90f, 0.24f, 0.50f), 0.5f, Color.white, Color.white); // مقدمة عالية
        p.AddBox(new Vector3(0f, 0.55f, -1.10f),  new Vector3(1.00f, 0.28f, 0.30f), 0.5f, Color.white, Color.white); // مؤخرة مرتفعة
        p.AddBox(new Vector3(0f, 0.72f, -0.15f),  new Vector3(0.80f, 0.30f, 1.00f), 0.5f, Color.white, Color.white); // قمرة
        p.AddBox(new Vector3(0f, 0.885f, -0.22f), new Vector3(0.70f, 0.05f, 0.62f), 0.5f, Color.white, Color.white); // سقف
    }

    static void TrimMuscle(MeshAccumulator t, Color dark, Color glass, Color head, Color tail, Color chrome, Color acc)
    {
        t.AddBox(new Vector3(0f, 0.66f, 0.62f),  new Vector3(0.40f, 0.10f, 0.50f), 0.5f, dark, dark);      // سقيفة شفط
        t.AddBox(new Vector3(-0.11f, 0.645f, 0.70f), new Vector3(0.14f, 0.015f, 0.90f), 0.5f, acc, acc);   // خطا سباق
        t.AddBox(new Vector3(0.11f, 0.645f, 0.70f),  new Vector3(0.14f, 0.015f, 0.90f), 0.5f, acc, acc);
        t.AddBox(new Vector3(-0.11f, 0.935f, -0.22f), new Vector3(0.14f, 0.015f, 0.60f), 0.5f, acc, acc);
        t.AddBox(new Vector3(0.11f, 0.935f, -0.22f),  new Vector3(0.14f, 0.015f, 0.60f), 0.5f, acc, acc);
        t.AddBox(new Vector3(-0.11f, 0.715f, -1.10f), new Vector3(0.14f, 0.015f, 0.28f), 0.5f, acc, acc);
        t.AddBox(new Vector3(0.11f, 0.715f, -1.10f),  new Vector3(0.14f, 0.015f, 0.28f), 0.5f, acc, acc);
        t.AddBox(new Vector3(0f, 0.78f, -1.20f), new Vector3(0.94f, 0.05f, 0.22f), 0.5f, acc, acc);        // جناح صغير
        t.AddBox(new Vector3(0f, 0.30f, 1.60f),  new Vector3(0.94f, 0.20f, 0.16f), 0.5f, dark, dark);      // مصدّ أمامي
        t.AddBox(new Vector3(0f, 0.34f, -1.32f), new Vector3(0.96f, 0.22f, 0.16f), 0.5f, dark, dark);      // مصدّ خلفي
        t.AddBox(new Vector3(0f, 0.72f, 0.48f),  new Vector3(0.74f, 0.24f, 0.07f), 0.5f, glass, glass);    // زجاج
        t.AddBox(new Vector3(0f, 0.72f, -0.68f), new Vector3(0.74f, 0.20f, 0.07f), 0.5f, glass, glass);
        t.AddBox(new Vector3(0f, 0.72f, -0.15f), new Vector3(0.82f, 0.18f, 0.66f), 0.5f, glass, glass);
        t.AddBox(new Vector3(-0.32f, 0.38f, 1.57f), new Vector3(0.22f, 0.10f, 0.05f), 0.5f, head, head);
        t.AddBox(new Vector3(0.32f, 0.38f, 1.57f),  new Vector3(0.22f, 0.10f, 0.05f), 0.5f, head, head);
        t.AddBox(new Vector3(-0.32f, 0.60f, -1.27f), new Vector3(0.26f, 0.09f, 0.04f), 0.5f, tail, tail);
        t.AddBox(new Vector3(0.32f, 0.60f, -1.27f),  new Vector3(0.26f, 0.09f, 0.04f), 0.5f, tail, tail);
        t.AddBox(new Vector3(-0.20f, 0.30f, -1.35f), new Vector3(0.11f, 0.09f, 0.07f), 0.5f, chrome, chrome);
        t.AddBox(new Vector3(0.20f, 0.30f, -1.35f),  new Vector3(0.11f, 0.09f, 0.07f), 0.5f, chrome, chrome);
    }

    /// <summary>FORMULA T: عجلات مكشوفة وجسم ضيق وجناحان (أسلوب Race Car package).</summary>
    static void BuildFormula(MeshAccumulator p)
    {
        p.AddBox(new Vector3(0f, 0.36f, 0.05f),   new Vector3(0.46f, 0.30f, 2.30f), 0.5f, Color.white, Color.white); // الجسم المركزي
        p.AddBox(new Vector3(0f, 0.28f, 1.35f),   new Vector3(0.30f, 0.16f, 0.60f), 0.5f, Color.white, Color.white); // أنف رفيع
        p.AddBox(new Vector3(-0.42f, 0.36f, 0.15f), new Vector3(0.34f, 0.26f, 1.30f), 0.5f, Color.white, Color.white); // بودّة يسار
        p.AddBox(new Vector3(0.42f, 0.36f, 0.15f),  new Vector3(0.34f, 0.26f, 1.30f), 0.5f, Color.white, Color.white); // بودّة يمين
        p.AddBox(new Vector3(0f, 0.60f, -0.45f),  new Vector3(0.30f, 0.36f, 0.90f), 0.5f, Color.white, Color.white); // غطاء محرك
    }

    static void TrimFormula(MeshAccumulator t, Color dark, Color glass, Color head, Color tail, Color chrome, Color acc)
    {
        t.AddBox(new Vector3(0f, 0.84f, -0.60f), new Vector3(0.22f, 0.14f, 0.40f), 0.5f, dark, dark);      // مدخل هواء
        t.AddSphere(new Vector3(0f, 0.68f, 0.10f), 0.13f, 8, 5, acc, 0.95f);                               // خوذة السائق
        t.AddBox(new Vector3(0f, 0.52f, 0.05f),  new Vector3(0.34f, 0.06f, 0.60f), 0.5f, dark, dark);      // فتحة قمرة
        t.AddBox(new Vector3(0f, 0.12f, 1.72f),  new Vector3(1.30f, 0.05f, 0.28f), 0.5f, acc, acc);        // جناح أمامي
        t.AddBox(new Vector3(-0.63f, 0.18f, 1.72f), new Vector3(0.04f, 0.14f, 0.30f), 0.5f, dark, dark);   // لوحا جناح أمامي
        t.AddBox(new Vector3(0.63f, 0.18f, 1.72f),  new Vector3(0.04f, 0.14f, 0.30f), 0.5f, dark, dark);
        t.AddBox(new Vector3(0f, 0.98f, -1.05f), new Vector3(1.20f, 0.05f, 0.30f), 0.5f, acc, acc);        // جناح خلفي عالٍ
        t.AddBox(new Vector3(-0.20f, 0.80f, -1.05f), new Vector3(0.05f, 0.30f, 0.10f), 0.5f, dark, dark);  // قائمان
        t.AddBox(new Vector3(0.20f, 0.80f, -1.05f),  new Vector3(0.05f, 0.30f, 0.10f), 0.5f, dark, dark);
        t.AddBox(new Vector3(-0.58f, 0.98f, -1.05f), new Vector3(0.04f, 0.16f, 0.32f), 0.5f, dark, dark);  // لوحا جناح خلفي
        t.AddBox(new Vector3(0.58f, 0.98f, -1.05f),  new Vector3(0.04f, 0.16f, 0.32f), 0.5f, dark, dark);
        t.AddBox(new Vector3(0f, 0.14f, -1.22f), new Vector3(0.44f, 0.10f, 0.30f), 0.5f, dark, dark);      // مفرّق
        t.AddBox(new Vector3(-0.16f, 0.34f, 1.62f), new Vector3(0.12f, 0.08f, 0.04f), 0.5f, head, head);   // مصباح أنف
        t.AddBox(new Vector3(0.16f, 0.34f, 1.62f),  new Vector3(0.12f, 0.08f, 0.04f), 0.5f, head, head);
    }

    /// <summary>MINI EVO: هاتشباك قصيرة بقمرة عالية ورَف سقف (أسلوب Low Poly Vehicle Pack).</summary>
    static void BuildMini(MeshAccumulator p)
    {
        p.AddBox(new Vector3(0f, 0.42f, -0.05f),  new Vector3(0.98f, 0.38f, 1.95f), 0.5f, Color.white, Color.white); // جسم قصير
        p.AddBox(new Vector3(0f, 0.32f, 1.05f),   new Vector3(0.84f, 0.20f, 0.30f), 0.5f, Color.white, Color.white); // مقدمة
        p.AddBox(new Vector3(0f, 0.74f, -0.10f),  new Vector3(0.84f, 0.34f, 1.05f), 0.5f, Color.white, Color.white); // قمرة عالية
        p.AddBox(new Vector3(0f, 0.925f, -0.12f), new Vector3(0.78f, 0.05f, 0.75f), 0.5f, Color.white, Color.white); // سقف
    }

    static void TrimMini(MeshAccumulator t, Color dark, Color glass, Color head, Color tail, Color chrome, Color acc)
    {
        t.AddBox(new Vector3(0f, 0.975f, -0.12f), new Vector3(0.58f, 0.05f, 0.55f), 0.5f, acc, acc);       // رَف سقف
        t.AddBox(new Vector3(0f, 0.74f, 0.46f),  new Vector3(0.72f, 0.26f, 0.07f), 0.5f, glass, glass);    // زجاج أمامي
        t.AddBox(new Vector3(0f, 0.74f, -0.64f), new Vector3(0.72f, 0.22f, 0.07f), 0.5f, glass, glass);    // زجاج خلفي
        t.AddBox(new Vector3(0f, 0.74f, -0.09f), new Vector3(0.86f, 0.20f, 0.70f), 0.5f, glass, glass);    // زجاج جانبي
        t.AddBox(new Vector3(0f, 0.28f, 1.24f),  new Vector3(0.90f, 0.18f, 0.14f), 0.5f, dark, dark);      // مصدّان
        t.AddBox(new Vector3(0f, 0.30f, -1.08f), new Vector3(0.90f, 0.20f, 0.14f), 0.5f, dark, dark);
        t.AddBox(new Vector3(-0.30f, 0.36f, 1.22f), new Vector3(0.16f, 0.12f, 0.05f), 0.5f, head, head);   // مصابيح
        t.AddBox(new Vector3(0.30f, 0.36f, 1.22f),  new Vector3(0.16f, 0.12f, 0.05f), 0.5f, head, head);
        t.AddBox(new Vector3(-0.30f, 0.42f, -1.14f), new Vector3(0.18f, 0.10f, 0.04f), 0.5f, tail, tail);
        t.AddBox(new Vector3(0.30f, 0.42f, -1.14f),  new Vector3(0.18f, 0.10f, 0.04f), 0.5f, tail, tail);
    }

    /// <summary>PICKUP 4X4: بيك أب بشاسيه مرتفع وصندوق خلفي (أسلوب Low Poly Vehicle Pack).</summary>
    static void BuildPickup(MeshAccumulator p)
    {
        p.AddBox(new Vector3(0f, 0.34f, 0.00f),   new Vector3(0.96f, 0.28f, 2.35f), 0.5f, Color.white, Color.white); // شاسيه عالٍ
        p.AddBox(new Vector3(0f, 0.64f, 0.42f),   new Vector3(0.92f, 0.44f, 0.90f), 0.5f, Color.white, Color.white); // كابينة
        p.AddBox(new Vector3(0f, 0.875f, 0.40f),  new Vector3(0.84f, 0.05f, 0.72f), 0.5f, Color.white, Color.white); // سقف
        p.AddBox(new Vector3(0f, 0.50f, -0.72f),  new Vector3(0.92f, 0.08f, 1.05f), 0.5f, Color.white, Color.white); // أرضية الصندوق
        p.AddBox(new Vector3(-0.475f, 0.66f, -0.72f), new Vector3(0.05f, 0.36f, 1.05f), 0.5f, Color.white, Color.white); // جدار أيسر
        p.AddBox(new Vector3(0.475f, 0.66f, -0.72f),  new Vector3(0.05f, 0.36f, 1.05f), 0.5f, Color.white, Color.white); // جدار أيمن
        p.AddBox(new Vector3(0f, 0.66f, -1.28f),  new Vector3(0.92f, 0.36f, 0.06f), 0.5f, Color.white, Color.white); // بوابة خلفية
    }

    static void TrimPickup(MeshAccumulator t, Color dark, Color glass, Color head, Color tail, Color chrome, Color acc)
    {
        t.AddBox(new Vector3(0f, 0.36f, 1.62f),  new Vector3(0.94f, 0.16f, 0.12f), 0.5f, dark, dark);      // حاجز أمامي
        t.AddBox(new Vector3(0f, 0.52f, 1.62f),  new Vector3(0.94f, 0.10f, 0.12f), 0.5f, dark, dark);
        t.AddBox(new Vector3(0f, 0.94f, 0.40f),  new Vector3(0.70f, 0.09f, 0.12f), 0.5f, dark, dark);      // شريط أضواء سقف
        t.AddBox(new Vector3(-0.28f, 0.94f, 0.46f), new Vector3(0.08f, 0.06f, 0.04f), 0.5f, head, head);   // أضواء
        t.AddBox(new Vector3(-0.10f, 0.94f, 0.46f), new Vector3(0.08f, 0.06f, 0.04f), 0.5f, head, head);
        t.AddBox(new Vector3(0.10f, 0.94f, 0.46f),  new Vector3(0.08f, 0.06f, 0.04f), 0.5f, head, head);
        t.AddBox(new Vector3(0.28f, 0.94f, 0.46f),  new Vector3(0.08f, 0.06f, 0.04f), 0.5f, head, head);
        t.AddBox(new Vector3(0f, 0.66f, -1.315f), new Vector3(0.70f, 0.08f, 0.02f), 0.5f, acc, acc);       // شريط البوابة
        t.AddBox(new Vector3(0f, 0.66f, 0.86f),  new Vector3(0.78f, 0.30f, 0.06f), 0.5f, glass, glass);    // زجاج
        t.AddBox(new Vector3(0f, 0.66f, 0.05f),  new Vector3(0.94f, 0.26f, 0.60f), 0.5f, glass, glass);
        t.AddBox(new Vector3(-0.32f, 0.42f, 1.56f), new Vector3(0.20f, 0.12f, 0.05f), 0.5f, head, head);
        t.AddBox(new Vector3(0.32f, 0.42f, 1.56f),  new Vector3(0.20f, 0.12f, 0.05f), 0.5f, head, head);
        t.AddBox(new Vector3(-0.32f, 0.72f, -1.32f), new Vector3(0.22f, 0.10f, 0.04f), 0.5f, tail, tail);
        t.AddBox(new Vector3(0.32f, 0.72f, -1.32f),  new Vector3(0.22f, 0.10f, 0.04f), 0.5f, tail, tail);
        t.AddBox(new Vector3(-0.20f, 0.26f, -1.34f), new Vector3(0.10f, 0.09f, 0.07f), 0.5f, chrome, chrome);
        t.AddBox(new Vector3(0.20f, 0.26f, -1.34f),  new Vector3(0.10f, 0.09f, 0.07f), 0.5f, chrome, chrome);
    }
}
