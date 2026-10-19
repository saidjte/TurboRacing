using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية)
/// يبني سيارة سباق مجسّمة بالكود (بلا نماذج خارجية):
/// - هيكل ملوّن (Paint): جسم رئيسي + مقدمة منخفضة + مؤخرة + قمرة + سقف
///   — مادة طلاء معدنية لامعة يتحكم بلونها اللاعب من الإعدادات
/// - تفاصيل (Trim): زجاج أمامي/جانبي/خلفي داكن + مصدّات + جناح خلفي بجناحين
///   جانبيين + فاصل أمامي + مُفَرِّق خلفي + مصابيح أمام بيضاء وخلف حمراء
/// - أربع عجلات (إطار + جنط) كل واحدة بمحور مستقل: أماميتان تتوجّهان
///   مع التوجيه والجميع يدور مع السرعة (يدار من CarController)
/// المحاور المحلية: +Z أمام، +Y فوق (تحويل ذهني من القديم: up2D = forward3D).
/// </summary>
public static class CarModel3D
{
    public struct Handles
    {
        public Transform BodyPivot;     // الهيكل + التفاصيل (ميلان/انحناء بصرية)
        public MeshRenderer Paint;      // مادة الطلاء (يُلوَّن من الإعدادات)
        public Transform[] Wheels;      // محاور العجلات الأربع (FL FR RL RR)
    }

    // أبعاد السيارة (بالمتر) — مطابقة تقريباً لمساحة القيادة القديمة 1×2
    public const float ColliderWidth = 1.15f;
    public const float ColliderHeight = 0.9f;
    public const float ColliderLength = 2.55f;

    public static Handles Build(Transform carRoot)
    {
        Handles h = new Handles();
        h.Wheels = new Transform[4];

        // ================= الهيكل الملوّن =================
        Transform bodyPivot = new GameObject("BodyPivot").transform;
        bodyPivot.SetParent(carRoot, false);

        var paint = new MeshAccumulator();
        Color white = Color.white; // الطلاء يأتي لون المادة (وليس لون الرأس)

        paint.AddBox(new Vector3(0f, 0.40f, 0.02f), new Vector3(1.05f, 0.36f, 2.25f), 0.5f, white, white);        // الجسم الرئيسي
        paint.AddBox(new Vector3(0f, 0.29f, 1.30f), new Vector3(0.80f, 0.22f, 0.62f), 0.5f, white, white);        // مقدمة منخفضة
        paint.AddBox(new Vector3(0f, 0.50f, -1.10f), new Vector3(0.98f, 0.26f, 0.30f), 0.5f, white, white);       // مؤخرة مرتفعة
        paint.AddBox(new Vector3(0f, 0.66f, -0.12f), new Vector3(0.78f, 0.26f, 1.05f), 0.5f, white, white);       // قمرة
        paint.AddBox(new Vector3(0f, 0.80f, -0.22f), new Vector3(0.70f, 0.05f, 0.62f), 0.5f, white, white);       // سقف

        h.Paint = Emit(carRoot, bodyPivot, paint, "Paint", MakeMat(0.60f, 0.40f));

        // ================= التفاصيل الثابتة =================
        var trim = new MeshAccumulator();
        Color glass  = new Color(0.10f, 0.13f, 0.17f);   // زجاج داكن مزرق
        Color dark   = new Color(0.10f, 0.10f, 0.11f);   // بلاستيك أسود
        Color chrome = new Color(0.55f, 0.56f, 0.58f);
        Color head   = new Color(1.00f, 0.97f, 0.85f);   // مصابيح أمام
        Color tail   = new Color(1.00f, 0.10f, 0.06f);   // مصابيح خلف

        // زجاج: لوح أمامي/خلفي رقيق + شريط جانبي عريض يبرز 1سم عن القمرة
        trim.AddBox(new Vector3(0f, 0.66f, 0.41f),  new Vector3(0.74f, 0.22f, 0.07f), 0.5f, glass, glass);
        trim.AddBox(new Vector3(0f, 0.66f, -0.65f), new Vector3(0.74f, 0.20f, 0.07f), 0.5f, glass, glass);
        trim.AddBox(new Vector3(0f, 0.66f, -0.14f), new Vector3(0.80f, 0.16f, 0.60f), 0.5f, glass, glass);

        // مصدّات + فاصل أمامي + مفرّق خلفي
        trim.AddBox(new Vector3(0f, 0.28f, 1.56f),  new Vector3(0.92f, 0.20f, 0.18f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(0f, 0.34f, -1.29f), new Vector3(0.95f, 0.22f, 0.16f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(0f, 0.13f, 1.42f),  new Vector3(0.95f, 0.06f, 0.42f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(0f, 0.15f, -1.31f), new Vector3(0.88f, 0.08f, 0.26f), 0.5f, dark, dark);

        // جناح خلفي: جناح + جناحان جانبيان + قائمان
        trim.AddBox(new Vector3(0f, 0.88f, -1.20f),   new Vector3(1.00f, 0.05f, 0.30f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(-0.50f, 0.88f, -1.20f), new Vector3(0.03f, 0.14f, 0.34f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(0.50f, 0.88f, -1.20f),  new Vector3(0.03f, 0.14f, 0.34f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(-0.30f, 0.74f, -1.20f), new Vector3(0.05f, 0.22f, 0.10f), 0.5f, dark, dark);
        trim.AddBox(new Vector3(0.30f, 0.74f, -1.20f),  new Vector3(0.05f, 0.22f, 0.10f), 0.5f, dark, dark);

        // عوادم
        trim.AddBox(new Vector3(-0.18f, 0.30f, -1.35f), new Vector3(0.10f, 0.09f, 0.07f), 0.5f, chrome, chrome);
        trim.AddBox(new Vector3(0.18f, 0.30f, -1.35f),  new Vector3(0.10f, 0.09f, 0.07f), 0.5f, chrome, chrome);

        // مصابيح
        trim.AddBox(new Vector3(-0.30f, 0.40f, 1.575f), new Vector3(0.22f, 0.10f, 0.05f), 0.5f, head, head);
        trim.AddBox(new Vector3(0.30f, 0.40f, 1.575f),  new Vector3(0.22f, 0.10f, 0.05f), 0.5f, head, head);
        trim.AddBox(new Vector3(-0.32f, 0.50f, -1.315f), new Vector3(0.24f, 0.09f, 0.04f), 0.5f, tail, tail);
        trim.AddBox(new Vector3(0.32f, 0.50f, -1.315f),  new Vector3(0.24f, 0.09f, 0.04f), 0.5f, tail, tail);

        Emit(carRoot, bodyPivot, trim, "Trim", MakeMat(0.10f, 0.35f));

        // ================= العجلات =================
        // شبكة عجلة واحدة (إطار + جنط بألوان رأسية) يشاركها المحاور الأربعة
        Mesh wheelMesh = BuildWheelMesh();
        Material wheelMat = MakeMat(0.05f, 0.30f);

        Vector3[] wheelPos =
        {
            new Vector3(-0.52f, 0.30f,  0.80f),  // FL
            new Vector3( 0.52f, 0.30f,  0.80f),  // FR
            new Vector3(-0.52f, 0.30f, -0.82f),  // RL
            new Vector3( 0.52f, 0.30f, -0.82f)   // RR
        };

        for (int i = 0; i < 4; i++)
        {
            Transform pivot = new GameObject(i < 2 ? $"Wheel{(i == 0 ? "FL" : "FR")}" : $"Wheel{(i == 2 ? "RL" : "RR")}").transform;
            pivot.SetParent(carRoot, false);
            pivot.localPosition = wheelPos[i];

            // الشبكة داخل المحور مُدارة 90° حول Z: محور الأسطوانة (Y) يصبح X = محور الدوران
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

    /// <summary>إطار داكن + جنط فضي أعرض قليلاً (يظهر من الجانبين).</summary>
    static Mesh BuildWheelMesh()
    {
        var acc = new MeshAccumulator();
        Color tire = new Color(0.06f, 0.06f, 0.07f);
        Color rim  = new Color(0.72f, 0.73f, 0.76f);

        acc.AddCylinder(Vector3.zero, 0.30f, 0.24f, 10, 0.5f, tire);
        acc.AddCylinder(Vector3.zero, 0.17f, 0.26f, 8, 0.5f, rim);

        Mesh m = acc.Build("CarWheel_Mesh");
        return m;
    }

    static MeshRenderer Emit(Transform carRoot, Transform parent, MeshAccumulator acc,
                             string name, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = acc.Build("Car" + name + "_Mesh");

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        return mr;
    }

    /// <summary>
    /// مادة السيارة: TR/Paint المخصص (ألوان رؤوس + BlinnPhong) أو Standard بديلاً.
    /// specular = شدة اللمعان، shininess = حدة الضوء.
    /// </summary>
    public static Material MakeMat(float specular, float shininess)
    {
        Shader sh = Shader.Find("TR/Paint");
        if (sh == null) sh = Shader.Find("Standard");

        Material m = new Material(sh);
        m.name = "CarMat";

        if (m.HasProperty("_SpecColor"))
            m.SetColor("_SpecColor", new Color(specular, specular, specular, 1f));
        if (m.HasProperty("_Shininess"))
            m.SetFloat("_Shininess", Mathf.Clamp(shininess, 0.01f, 1f));
        if (m.HasProperty("_Glossiness"))
        {
            m.SetFloat("_Glossiness", shininess);
            m.SetFloat("_Metallic", specular);
        }
        if (m.HasProperty("_GlossyReflections"))
            m.SetFloat("_GlossyReflections", 0f);
        return m;
    }
}
