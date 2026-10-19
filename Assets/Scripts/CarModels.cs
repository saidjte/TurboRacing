using UnityEngine;

/// <summary>
/// TurboRacing - v1.4.0 (الكراج وتعدد السيارات)
/// مكافئات إجرائية بأسلوب أصول المتجر المختارة (نمط المشهد المعتمد في المشروع —
/// بناء Low-Poly بالكود بدل استيراد الحزم):
/// - GT-5 SPORT   ≈ Sport Car Free 5 (360208): كوبيه منخفض بجناح خلفي كبير
/// - MUSCLE RS-4  ≈ Sport Car Free 4 (360150): عضلية بخطوط سباق وسقيفة الشفط
/// - FORMULA T    ≈ Race Car package (141690): عجلات مكشوفة وجناحان أمامي/خلفي
/// - MINI EVO     ≈ Low Poly Car Vehicle Pack (259182): هاتشباك رشيقة قمرة عالية
/// - PICKUP 4X4   ≈ Low Poly Car Vehicle Pack (259182): بيك أب بصندوق خلفي
/// - ARCADE GT    = النموذج الأصلي (ARCADE: FREE Racing Car) يبقى الخيار الافتراضي
/// كل موديل = مواصفات أداء (سرعة/تسارع/توجيه/فرملة) + هيكل مدموج في شبكتين
/// (طلاء + تفاصيل) + أربع عجلات بمحاور قياسية متوافقة مع فيزياء Prometeo.
/// </summary>
public static class CarModels
{
    public struct CarSpec
    {
        public int id;
        public string name;
        public string tag;        // الأصل الملهم من Asset Store
        public int bodyType;      // 0..4 شكل الهيكل (-1 = النموذج الأصلي)
        public Color paint;       // الطلاء الافتراضي (اللاعب يغيّره من الإعدادات)
        public Color accent;      // لون التمييز (خطوط/جناح/عناصر)
        public float topSpeed;    // كم/س (قبل معامل الضبط STOCK/SPORT/TURBO)
        public int accel;         // معامل عزم Prometeo 1..10
        public float steerAngle;  // أقصى زاوية توجيه
        public int brakeForce;
    }

    public static readonly CarSpec[] All =
    {
        new CarSpec { id = 0, name = "ARCADE GT",   tag = "ARCADE FREE RACING CAR", bodyType = -1,
            paint = new Color(0.85f, 0.20f, 0.20f), accent = new Color(1f, 0.72f, 0.15f),
            topSpeed = 75f, accel = 9,  steerAngle = 36f, brakeForce = 540 },

        new CarSpec { id = 1, name = "GT-5 SPORT",  tag = "SPORT CAR FREE 5", bodyType = 0,
            paint = new Color(0.86f, 0.12f, 0.16f), accent = new Color(0.94f, 0.94f, 0.92f),
            topSpeed = 84f, accel = 10, steerAngle = 38f, brakeForce = 560 },

        new CarSpec { id = 2, name = "MUSCLE RS-4", tag = "SPORT CAR FREE 4", bodyType = 1,
            paint = new Color(0.16f, 0.36f, 0.86f), accent = new Color(0.95f, 0.78f, 0.20f),
            topSpeed = 86f, accel = 9,  steerAngle = 34f, brakeForce = 540 },

        new CarSpec { id = 3, name = "FORMULA T",   tag = "RACE CAR PACKAGE", bodyType = 2,
            paint = new Color(0.95f, 0.45f, 0.12f), accent = new Color(0.94f, 0.94f, 0.92f),
            topSpeed = 88f, accel = 10, steerAngle = 40f, brakeForce = 520 },

        new CarSpec { id = 4, name = "MINI EVO",    tag = "LOW POLY VEHICLE PACK", bodyType = 3,
            paint = new Color(0.20f, 0.72f, 0.35f), accent = new Color(0.10f, 0.10f, 0.11f),
            topSpeed = 78f, accel = 9,  steerAngle = 42f, brakeForce = 560 },

        new CarSpec { id = 5, name = "PICKUP 4X4",  tag = "LOW POLY VEHICLE PACK", bodyType = 4,
            paint = new Color(0.95f, 0.78f, 0.20f), accent = new Color(0.12f, 0.12f, 0.13f),
            topSpeed = 76f, accel = 8,  steerAngle = 34f, brakeForce = 580 },
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
