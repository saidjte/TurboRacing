# TurboRacing

لعبة سباق سيارات Top-Down 2D لأندرويد — مبنية بـ Unity وتُبنى تلقائياً إلى APK عبر GitHub Actions.

## كيف يعمل البناء التلقائي؟
1. عند كل `push` إلى `main` يعمل workflow ‏`.github/workflows/build-android.yml`
2. يستخدم `game-ci/unity-builder@v4` بإصدار Unity **6000.0.84f1** (منصّة Android)
3. ينفذ `BuildCommand.PerformBuild` الذي:
   - يضبط إعدادات اللاعب لأندرويد (أفقي، ARM64+ARMv7، API 24+)
   - ينشئ المشهد برمجياً (كاميرا + GameBootstrap)
   - يبني `build/Android/TurboRacing.apk`
4. يُرفع الـ APK كـ **Artifact** باسم `TurboRacing-APK`

## البنية
```
Assets/
├── Scripts/
│   ├── CarController.cs      فيزياء السيارة (Rigidbody2D بلا جاذبية) + أزرار لمسية ذاتية الربط
│   ├── CameraFollow.cs       كاميرا تتبع سلسة
│   ├── SimpleBackground.cs   أرضية شبكية مولدة بالكود + 60 FPS
│   └── GameBootstrap.cs      يبني محتوى المشهد بالكود عند التشغيل
├── Editor/
│   └── BuildCommand.cs       أمر البناء المخصص لبيئة CI
└── Scenes/Game.unity         مشهد بسيط (كاميرا + GameBootstrap)
```

## الأسرار المطلوبة (Secrets)
| الاسم | القيمة |
|---|---|
| `UNITY_LICENSE` | محتوى ملف `Unity_lic.ulf` كاملاً |
| `UNITY_EMAIL` | بريد حساب Unity |
| `UNITY_PASSWORD` | كلمة مرور حساب Unity |

## المراحل
- [x] المرحلة 1: السيارة + التحكم اللمسي + الخلفية المؤقتة
- [ ] المرحلة 2: مسار Tilemap + جدران + عقاب العشب
- [ ] المرحلة 3: اللفات والمؤقت والحفظ
- [ ] المرحلة 4: الواجهات الكاملة
- [ ] المرحلة 5: الصوت والمؤثرات
