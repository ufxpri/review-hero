// 앰비언트 불씨 — 무대 뒤에서 떠도는 파티클. 액션감만 얹고 조작을 방해하지 않는다.
//
// 웹판(combat.html 의 #pfx canvas + PFX_PRESET.ember)을 GpuParticles2D 두 개로 옮긴 것이다:
//   ① 상시 불씨 — 화면 전체에서 위로 흘러오른다. Preprocess 로 첫 프레임부터 화면이 차 있다.
//   ② 적중 버스트 — 원산지·팩트가 꽂힌 자리에서 확 일었다 잦아든다 (OneShot).
//
// 설정의 「화면 흔들림」이 꺼져 있으면(RunStore.Settings.Shake == false) 아예 방출하지 않는다 —
// 웹판이 prefers-reduced-motion 과 같은 스위치로 묶어 둔 계약을 그대로 승계한다.

using Godot;
using ReviewHero.Game.Combat;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Fx;

public partial class Embers : Node2D
{
    private GpuParticles2D _ambient = null!;
    private GpuParticles2D _burst = null!;

    /// <summary>설정이 꺼져 있으면 false — 이 경우 파티클을 만들되 방출하지 않는다</summary>
    public bool Enabled { get; private set; } = true;

    public override void _Ready()
    {
        Enabled = MotionAllowed();

        var dot = DotTexture();

        _ambient = new GpuParticles2D
        {
            Texture = dot,
            Amount = 56,
            Lifetime = 6.0,
            Preprocess = 6.0f,          // 첫 프레임부터 화면이 비지 않게
            Explosiveness = 0f,
            Randomness = 1f,
            ProcessMaterial = AmbientMaterial(),
            Position = new Vector2(CombatArt.ScreenW / 2f, CombatArt.ScreenH / 2f),
            // 기본 가시 영역은 노드 주변 200px 뿐이다 — 화면 전체에 뿌리므로 넓혀 주지 않으면 통째로 잘린다
            VisibilityRect = new Rect2(-CombatArt.ScreenW / 2f - 40, -CombatArt.ScreenH / 2f - 40,
                CombatArt.ScreenW + 80, CombatArt.ScreenH + 80),
            Emitting = Enabled,
        };
        AddChild(_ambient);

        _burst = new GpuParticles2D
        {
            Texture = dot,
            Amount = 30,
            Lifetime = 1.1,
            OneShot = true,
            Explosiveness = 1f,
            Randomness = 1f,
            ProcessMaterial = BurstMaterial(),
            VisibilityRect = new Rect2(-320, -320, 640, 640),
            Emitting = false,
        };
        AddChild(_burst);
    }

    /// <summary>원산지·팩트 적중 — 그 자리에서 불씨가 확 인다</summary>
    public void Burst(Vector2 at, int amount = 26)
    {
        if (!Enabled || _burst is null) return;
        _burst.Position = at;
        _burst.Amount = Mathf.Clamp(amount, 6, 48);
        _burst.Restart();
        _burst.Emitting = true;
    }

    public static bool MotionAllowed()
    {
        try { return RunStore.Settings.Shake; }
        catch (System.Exception) { return true; }   // 세이브를 못 읽어도 화면은 살아야 한다
    }

    // ── 재료 ──────────────────────────────────────────

    private static ParticleProcessMaterial AmbientMaterial()
    {
        var m = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(CombatArt.ScreenW / 2f, CombatArt.ScreenH / 2f, 1f),
            Direction = new Vector3(0, -1, 0),
            Spread = 22f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.18f,
            ScaleMax = 0.46f,
            Color = new Color(1f, 0.80f, 0.42f),
        };
        m.SetParamMin(ParticleProcessMaterial.Parameter.InitialLinearVelocity, 8f);
        m.SetParamMax(ParticleProcessMaterial.Parameter.InitialLinearVelocity, 26f);
        m.SetParamMin(ParticleProcessMaterial.Parameter.Angle, 0f);
        m.SetParamMax(ParticleProcessMaterial.Parameter.Angle, 360f);
        m.ColorRamp = FadeRamp(0.85f);
        return m;
    }

    private static ParticleProcessMaterial BurstMaterial()
    {
        var m = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 22f,
            Direction = new Vector3(0, -1, 0),
            Spread = 180f,
            Gravity = new Vector3(0, -40f, 0),
            ScaleMin = 0.18f,
            ScaleMax = 0.42f,
            Color = new Color(1f, 0.86f, 0.55f),
        };
        m.SetParamMin(ParticleProcessMaterial.Parameter.InitialLinearVelocity, 70f);
        m.SetParamMax(ParticleProcessMaterial.Parameter.InitialLinearVelocity, 190f);
        m.SetParamMin(ParticleProcessMaterial.Parameter.Damping, 40f);
        m.SetParamMax(ParticleProcessMaterial.Parameter.Damping, 90f);
        m.ColorRamp = FadeRamp(1f);
        return m;
    }

    private static GradientTexture1D FadeRamp(float peak)
    {
        var g = new Gradient();
        g.SetOffset(0, 0f);
        g.SetColor(0, new Color(1, 1, 1, 0));
        g.AddPoint(0.25f, new Color(1, 1, 1, peak));
        g.AddPoint(0.7f, new Color(1, 0.78f, 0.42f, peak * 0.75f));
        g.SetOffset(g.GetPointCount() - 1, 1f);
        g.SetColor(g.GetPointCount() - 1, new Color(1, 0.55f, 0.2f, 0));
        return new GradientTexture1D { Gradient = g };
    }

    /// <summary>불씨 한 톨 — 가운데가 밝은 원. 텍스처 파일을 늘리지 않으려고 코드로 굽는다</summary>
    private static Texture2D DotTexture()
    {
        var g = new Gradient();
        g.SetOffset(0, 0f);
        g.SetColor(0, new Color(1, 1, 1, 1));
        g.SetOffset(1, 1f);
        g.SetColor(1, new Color(1, 1, 1, 0));
        return new GradientTexture2D
        {
            Gradient = g,
            Width = 24,
            Height = 24,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }
}
