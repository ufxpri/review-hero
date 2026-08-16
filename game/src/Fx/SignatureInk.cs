// 서명 — 카드를 낼 때 서명란에 획이 그어진다. 그어지는 것 자체가 「리뷰 게시」다 (worldview §1.1-2).
//
// 웹판(combat.html .sig)은 ui/signature.html 에서 등록한 실제 획을 SVG path 로 그렸다.
// Godot 에는 아직 **서명 등록 화면이 없다** — 그래서 기본 필체(웹판 DEF_LN/DEF_FL 를 그대로
// 옮긴 베지어)를 쓰되, 나중에 실제 서명을 꽂을 자리는 여기 <see cref="SignatureStore"/> 하나다:
//
//   SignatureStore.Strokes = 저장된 획 목록;   // 점 좌표계는 아무거나 — 여기서 정규화한다
//
// 서명 등록 씬이 생기면 그 씬이 RunStore 에 획을 저장하고, 전투 진입 시 위 한 줄만 채우면 된다.
// (웹판 계약: RH.sig() → { v, box:[660,236], strokes:[[[x,y],…],…] })

using Godot;
using ReviewHero.Game.Combat;

namespace ReviewHero.Game.Fx;

/// <summary>
/// 등록된 서명 획을 담는 자리. 비어 있으면 기본 필체로 대체한다.
/// 좌표계는 자유 — <see cref="SignatureInk"/> 가 서명란(140×38)에 맞춰 정규화한다.
/// </summary>
public static class SignatureStore
{
    public static IReadOnlyList<Vector2[]>? Strokes { get; set; }

    /// <summary>실제 서명이 등록되어 있는가 (없으면 기본 필체)</summary>
    public static bool HasCustom => Strokes is { Count: > 0 };
}

/// <summary>서명란 한 칸. <see cref="Progress"/> 0→1 로 획이 순서대로 그어진다.</summary>
public partial class SignatureInk : Control
{
    /// <summary>웹판 .sig 의 좌표계 (viewBox 0 0 140 38)</summary>
    private const float BoxW = 140f;
    private const float BoxH = 38f;

    private readonly List<Vector2[]> _strokes = new();
    private readonly List<float> _lengths = new();
    private float _total;
    private float _progress;

    /// <summary>마지막 획이 끝난 자리 — 잉크 방울이 떨어지는 곳</summary>
    private Vector2 _blot;

    /// <summary>
    /// 0→1 로 획이 그어진다. <b>[Export] 가 붙어 있어야 한다</b> — Tween 은 ClassDB 에 등록된
    /// 속성만 건드릴 수 있어서, 없으면 「tweened property does not exist」로 서명 연출이 통째로 죽는다.
    /// </summary>
    [Export]
    public float Progress
    {
        get => _progress;
        set { _progress = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); }
    }

    /// <summary>날아가는 복제본용 — 이미 다 그어진 상태로 굳혀 둔다</summary>
    public bool Signed { set { if (value) Progress = 1f; } }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Build();
    }

    private void Build()
    {
        _strokes.Clear();
        _lengths.Clear();
        var src = SignatureStore.HasCustom ? Normalize(SignatureStore.Strokes!) : DefaultStrokes();
        foreach (var s in src)
        {
            if (s.Length < 2) continue;
            _strokes.Add(s);
            float len = 0f;
            for (int i = 1; i < s.Length; i++) len += s[i].DistanceTo(s[i - 1]);
            _lengths.Add(len);
            _total += len;
        }
        if (_total <= 0f) _total = 1f;
        _blot = _strokes.Count > 0 ? _strokes[^1][^1] : new Vector2(BoxW - 6, BoxH - 8);
    }

    public override void _Draw()
    {
        if (_progress <= 0f || _strokes.Count == 0) return;
        var scale = new Vector2(Size.X / BoxW, Size.Y / BoxH);
        float budget = _progress * _total;

        for (int s = 0; s < _strokes.Count; s++)
        {
            if (budget <= 0f) break;
            var pts = _strokes[s];
            // 첫 획은 두껍게(.ln 2.3), 기본 필체의 마지막 밑줄(.fl)은 얇게 — 웹판 그대로
            bool flourish = !SignatureStore.HasCustom && s == _strokes.Count - 1 && _strokes.Count > 1;
            float w = flourish ? 1.4f : 2.3f;
            var col = CombatArt.Inkc with { A = flourish ? 0.65f : 1f };

            var drawn = new List<Vector2> { pts[0] * scale };
            for (int i = 1; i < pts.Length && budget > 0f; i++)
            {
                float seg = pts[i].DistanceTo(pts[i - 1]);
                if (seg <= 0f) continue;
                float t = Mathf.Min(1f, budget / seg);
                drawn.Add(pts[i - 1].Lerp(pts[i], t) * scale);
                budget -= seg;
            }
            if (drawn.Count >= 2) DrawPolyline(drawn.ToArray(), col, w, antialiased: true);
        }

        // 잉크 방울 — 다 그은 뒤에 톡 떨어진다
        if (_progress > 0.97f)
        {
            DrawCircle(_blot * scale, 3.4f, CombatArt.Inkc with { A = 0.85f });
        }
    }

    // ── 기본 필체 (combat.html DEF_LN / DEF_FL 의 3차 베지어를 표본화) ──

    private static List<Vector2[]> DefaultStrokes()
    {
        // M3,25 C…  — 손글씨 획 하나 + 밑줄 플러리시 하나
        var main = new[]
        {
            new[] { V(3, 25), V(11, 6), V(19, 3), V(23, 13) },
            new[] { V(23, 13), V(27, 23), V(21, 30), V(17, 24) },
            new[] { V(17, 24), V(13, 17), V(23, 7), V(35, 9) },
            new[] { V(35, 9), V(47, 11), V(41, 28), V(35, 25) },
            new[] { V(35, 25), V(29, 22), V(37, 9), V(51, 11) },
            new[] { V(51, 11), V(65, 13), V(59, 30), V(67, 25) },
            new[] { V(67, 25), V(75, 20), V(71, 9), V(83, 11) },
            new[] { V(83, 11), V(95, 13), V(89, 26), V(97, 23) },
            new[] { V(97, 23), V(105, 20), V(101, 11), V(113, 15) },
            new[] { V(113, 15), V(125, 19), V(119, 26), V(133, 18) },
        };
        var flourish = new[] { new[] { V(7, 31), V(42, 36), V(92, 33), V(131, 27) } };
        return new List<Vector2[]> { Sample(main), Sample(flourish) };
    }

    private static Vector2 V(float x, float y) => new(x, y);

    /// <summary>이어진 3차 베지어 조각들을 폴리라인 한 줄로</summary>
    private static Vector2[] Sample(Vector2[][] segments, int steps = 8)
    {
        var pts = new List<Vector2> { segments[0][0] };
        foreach (var s in segments)
        {
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                pts.Add(Cubic(s[0], s[1], s[2], s[3], t));
            }
        }
        return pts.ToArray();
    }

    private static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    /// <summary>등록 서명을 서명란(140×38)에 꽉 차게 맞춘다 — 웹판 buildSignature() 와 같은 계산</summary>
    private static List<Vector2[]> Normalize(IReadOnlyList<Vector2[]> src)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var s in src)
            foreach (var p in s)
            {
                minX = Mathf.Min(minX, p.X); minY = Mathf.Min(minY, p.Y);
                maxX = Mathf.Max(maxX, p.X); maxY = Mathf.Max(maxY, p.Y);
            }
        float sw = Mathf.Max(1f, maxX - minX), sh = Mathf.Max(1f, maxY - minY);
        float k = Mathf.Min(BoxW / sw, BoxH / sh) * 0.94f;
        float ox = (BoxW - sw * k) / 2f - minX * k, oy = (BoxH - sh * k) / 2f - minY * k;
        var outp = new List<Vector2[]>();
        foreach (var s in src)
        {
            var a = new Vector2[s.Length];
            for (int i = 0; i < s.Length; i++) a[i] = new Vector2(s[i].X * k + ox, s[i].Y * k + oy);
            outp.Add(a);
        }
        return outp;
    }
}
