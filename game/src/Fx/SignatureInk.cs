// 서명 — 카드를 낼 때 서명란에 획이 그어진다. 그어지는 것 자체가 「리뷰 게시」다 (worldview §1.1-2).
//
// 웹판(combat.html .sig)은 ui/signature.html 에서 등록한 실제 획을 SVG path 로 그렸다.
// Godot 도 같다 — 서명 등록 씬(scenes/Signature.tscn)이 세이브에 획을 넣고,
// <see cref="SignatureStore"/> 가 **처음 필요할 때 그것을 읽어 온다**.
//
// 지연 로드로 만든 이유: 전투 쪽 코드가 「서명을 불러온다」를 따로 부르지 않아도 되게 하기 위해서다.
// 카드가 서명란을 만드는 순간 저장본이 알아서 딸려 온다. 등록이 없으면 기본 필체
// (웹판 DEF_LN/DEF_FL 를 그대로 옮긴 베지어)로 대체된다 — 서명란이 비는 일은 없다.
// (저장 형식: user://save.json 의 meta.signature = { v, box:[660,236], strokes:[[[x,y],…],…] })

using Godot;
using ReviewHero.Game.Combat;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Fx;

/// <summary>
/// 등록된 서명 획을 담는 자리. 비어 있으면 기본 필체로 대체한다.
/// 좌표계는 자유 — <see cref="SignatureInk"/> 가 서명란(140×38)에 맞춰 정규화한다.
/// </summary>
public static class SignatureStore
{
    private static IReadOnlyList<Vector2[]>? _strokes;
    private static bool _loaded;

    /// <summary>
    /// 등록된 획. **처음 읽을 때 세이브에서 지연 로드한다** — 부르는 쪽이 로드를 신경 쓰지 않는다.
    /// 직접 대입하면 그 값이 우선한다(등록 직후 이 프로세스에 즉시 반영하는 경로).
    /// </summary>
    public static IReadOnlyList<Vector2[]>? Strokes
    {
        get
        {
            if (_loaded) return _strokes;
            _loaded = true;
            try
            {
                var saved = RunStore.Signature;
                _strokes = saved is { HasStrokes: true } ? saved.ToVectors() : null;
            }
            catch (System.Exception e)
            {
                // 서명을 못 읽었다고 카드가 안 나오면 안 된다 — 기본 필체로 떨어진다
                GD.PushWarning($"[SignatureStore] 등록 서명 로드 실패 — 기본 필체로 간다: {e.Message}");
                _strokes = null;
            }
            return _strokes;
        }
        set { _strokes = value; _loaded = true; }
    }

    /// <summary>실제 서명이 등록되어 있는가 (없으면 기본 필체)</summary>
    public static bool HasCustom => Strokes is { Count: > 0 };

    /// <summary>다음 읽기에서 세이브를 다시 보게 한다 (세이브를 갈아엎었을 때)</summary>
    public static void Invalidate()
    {
        _strokes = null;
        _loaded = false;
    }
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

    /// <summary>등록 화면이 그은 획으로 대신 그린다 — 저장 전이라 전역 저장본을 건드리면 안 된다</summary>
    public IReadOnlyList<Vector2[]>? Preview { get; set; }

    /// <summary>지금 그리고 있는 것이 실제 서명인가 (기본 필체의 밑줄 플러리시 판정에 쓴다)</summary>
    private bool _custom;

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

    /// <summary>획이 바뀌었을 때 다시 만든다 (등록 화면 미리보기가 매 획마다 부른다)</summary>
    public void Rebuild()
    {
        Build();
        Progress = 0f;
    }

    private void Build()
    {
        _strokes.Clear();
        _lengths.Clear();
        _total = 0f;
        _custom = Preview is { Count: > 0 } || SignatureStore.HasCustom;
        var src = Preview is { Count: > 0 } p ? Normalize(p)
            : SignatureStore.HasCustom ? Normalize(SignatureStore.Strokes!)
            : DefaultStrokes();
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
            bool flourish = !_custom && s == _strokes.Count - 1 && _strokes.Count > 1;
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
