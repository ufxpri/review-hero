// 양피지 서명 패드 — 마우스로 획을 긋는 곳 (이관 원본: ui/signature.html .pad).
//
// 저장 좌표계(660×236)와 화면 크기를 분리해 둔다. 지금은 1:1 이지만, 좌표를 화면 픽셀로
// 저장해 버리면 패드 크기를 손대는 순간 옛 서명이 전부 어긋난다.
//
// 점은 2.5(저장 좌표) 이상 움직였을 때만 쌓는다 — 웹판과 같은 과밀 제거다. 그리기는 쌓인
// 점을 그대로 잇지 않고 **중점을 지나는 이차 베지어**로 부드럽게 만든다(손떨림 완화).

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 (읽기만 한다)

namespace ReviewHero.Game.Signature;

public partial class SignaturePad : Control
{
    /// <summary>저장 좌표계</summary>
    public const float BoxW = SignatureData.BoxW;

    public const float BoxH = SignatureData.BoxH;

    /// <summary>점을 새로 쌓는 최소 이동 거리 (저장 좌표 기준)</summary>
    private const float MinStep = 2.5f;

    private readonly List<List<Vector2>> _strokes = new();
    private List<Vector2>? _cur;
    private ulong _strokeStart;   // 획 시작 시각 — 긁는 소리 길이를 정한다

    /// <summary>획이 늘거나 줄었다 — 미리보기가 다시 그어진다</summary>
    public event System.Action? Changed;

    public IReadOnlyList<List<Vector2>> Strokes => _strokes;

    public bool HasInk => _strokes.Count > 0;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Cross;
        CustomMinimumSize = new Vector2(BoxW, BoxH);
    }

    // ── 편집 ─────────────────────────────────────────

    public void Clear()
    {
        if (_strokes.Count == 0) return;
        _strokes.Clear();
        _cur = null;
        Touched();
    }

    public void Undo()
    {
        if (_strokes.Count == 0) return;
        _strokes.RemoveAt(_strokes.Count - 1);
        _cur = null;
        Touched();
    }

    /// <summary>저장된 서명을 패드에 얹는다 (재등록 — 지우고 다시 쓸 수 있게)</summary>
    public void Load(IEnumerable<Vector2[]> strokes)
    {
        _strokes.Clear();
        foreach (var s in strokes) _strokes.Add(new List<Vector2>(s));
        _cur = null;
        Touched();
    }

    private void Touched()
    {
        QueueRedraw();
        Changed?.Invoke();
    }

    // ── 입력 ─────────────────────────────────────────

    /// <summary>화면 좌표 → 저장 좌표(660×236)</summary>
    private Vector2 ToBox(Vector2 local)
    {
        float w = Mathf.Max(1f, Size.X), h = Mathf.Max(1f, Size.Y);
        return new Vector2(
            Mathf.Clamp(local.X / w * BoxW, 0f, BoxW),
            Mathf.Clamp(local.Y / h * BoxH, 0f, BoxH));
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed)
                {
                    _cur = new List<Vector2> { ToBox(mb.Position) };
                    _strokes.Add(_cur);
                    _strokeStart = Time.GetTicksMsec();
                    Touched();
                }
                else
                {
                    // 점 하나로 끝난 획은 선이 되지 않는다 — 남기면 저장 형식만 지저분해진다
                    if (_cur is { Count: < 2 }) _strokes.Remove(_cur);
                    // 펜이 종이를 긁는 소리 — 그은 시간만큼 지속된다
                    else Audio.Sfx.Stroke((Time.GetTicksMsec() - _strokeStart) / 1000.0);
                    _cur = null;
                    Touched();
                }
                AcceptEvent();
                break;

            case InputEventMouseMotion mm when _cur is not null:
                var p = ToBox(mm.Position);
                if (p.DistanceTo(_cur[^1]) < MinStep) return;
                _cur.Add(p);
                Touched();
                AcceptEvent();
                break;
        }
    }

    /// <summary>패드 밖에서 손을 뗐을 때도 획을 닫아 준다 (드래그가 화면 밖으로 나간 경우)</summary>
    public override void _Input(InputEvent @event)
    {
        if (_cur is null) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            if (_cur.Count < 2) _strokes.Remove(_cur);
            _cur = null;
            Touched();
        }
    }

    // ── 그리기 ───────────────────────────────────────

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);

        // 그림자 — 웹판 box-shadow 6px 6px 0
        DrawStyleBox(CombatArt.Box(new Color(0f, 0f, 0f, 0.5f), null, 10), new Rect2(new Vector2(6, 6), Size));
        DrawStyleBox(CombatArt.Box(CombatArt.Parch, CombatArt.Inkc, 10, 2), rect);

        var scale = new Vector2(Size.X / BoxW, Size.Y / BoxH);
        var font = CombatArt.Font();

        // 서명선 — 「× ______」. 대장의 서식이지 안내문이 아니다
        float ruleY = (BoxH - 56f) * scale.Y;
        DrawLine(new Vector2(44 * scale.X, ruleY), new Vector2(Size.X - 44 * scale.X, ruleY), CombatArt.ParchD, 1f);
        DrawString(font, new Vector2(24 * scale.X, ruleY - 4f), "×", HorizontalAlignment.Left, -1, 17,
            CombatArt.ParchD);

        if (_strokes.Count == 0)
        {
            DrawString(font, new Vector2(0, Size.Y - 20f), "여기에 서명을 그으십시오 — 획을 나눠 그어도 됩니다",
                HorizontalAlignment.Center, Size.X, 14, new Color("a08f68"));
        }

        foreach (var s in _strokes)
        {
            if (s.Count < 2)
            {
                if (s.Count == 1) DrawCircle(s[0] * scale, 1.6f, CombatArt.Inkc);
                continue;
            }
            var pts = SignatureGeometry.Smooth(s);
            for (int i = 0; i < pts.Length; i++) pts[i] *= scale;
            DrawPolyline(pts, CombatArt.Inkc, 3f, antialiased: true);
        }
    }
}

/// <summary>획을 부드럽게 잇는 계산 한 곳 — 패드와 미리보기가 같은 모양을 그려야 한다</summary>
public static class SignatureGeometry
{
    /// <summary>
    /// 웹판 toPath() 와 같은 계산 — 점 사이의 **중점을 지나는 이차 베지어**로 잇는다.
    /// 원래 점은 제어점이 되어 모서리가 둥글어지고, 손떨림이 눌린다.
    /// </summary>
    public static Vector2[] Smooth(IReadOnlyList<Vector2> pts, int steps = 4)
    {
        if (pts.Count < 3) return pts.ToArray();
        var outp = new List<Vector2> { pts[0] };
        var from = pts[0];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var ctrl = pts[i];
            var to = (pts[i] + pts[i + 1]) / 2f;
            for (int k = 1; k <= steps; k++)
            {
                float t = (float)k / steps;
                outp.Add(Quad(from, ctrl, to, t));
            }
            from = to;
        }
        outp.Add(pts[^1]);
        return outp.ToArray();
    }

    private static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
