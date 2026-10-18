/// <summary>
/// TurboRacing - مصدر الحقيقة الوحيد لرقم الإصدار.
/// القاعدة: كل إصلاح أو ترقية يرفع هذا الرقم وجوباً.
/// - Number: يظهر في الواجهة + في اسم ملف الـ APK المبني + في bundleVersion.
/// - Code: رقم متزايد لأندرويد (versionCode) لتحديثات المتجر.
/// v0.6.8: إصلاح دوران السيارة الذاتي (سيطرة كاملة على السرعة الزاوية)
/// + تحصين مدخلات اللمس (تتبع الأصابع + قسر التحرير) + مادة هيكل بلا احتكاك.
/// </summary>
public static class GameVersion
{
    public const string Number = "0.6.8";
    public const int Code = 15;
}
