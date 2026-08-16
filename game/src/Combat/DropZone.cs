// 드롭 존 — 「여기선 전부 상품이다」.
//
// 판매자 본체 / 그 판매자의 구성품 각각 / 내가 산 상품 각각 / 초고 폐기함이 전부 별개의 존이다
// (worldview §1.1 · card-system-v2 §3). 카드를 끌면 점선으로 드러나고, 유효한 곳 위에서
// 금색으로 강조된다. 카드의 target 과 안 맞는 존은 흐려지며 「대상 아님」을 달고 드롭을 거부한다.
//
// **이 파일은 규칙을 모른다.** 「받는가」 판단은 CombatScene 이 카드의 Target 을 보고 정해
// <see cref="Accepts"/> 에 꽂아 준다 (폐기함만 예외적으로 아무 카드나 받는다).

using Godot;

namespace ReviewHero.Game.Combat;

public enum ZoneKind
{
    Enemy,
    EnemyEquipment,
    MyEquipment,
    Trash,
}

public partial class DropZone : Control
{
    public ZoneKind Kind { get; init; }
    public int Index { get; init; }

    /// <summary>품절(파괴)된 구성품 — 지목도 드롭도 받지 않는다</summary>
    public bool Dead { get; set; }

    /// <summary>지금 지목 중인 대상인가 (카드 뱃지 미리보기의 기준)</summary>
    public bool Aimed { get; set; }

    private bool _marking, _accepts, _hot;
    private ZoneOverlay? _ov;

    /// <summary>끌기 시작 — 존을 점선으로 드러낸다</summary>
    public void Mark(bool accepts)
    {
        _marking = true;
        _accepts = accepts && !Dead;
        _hot = false;
        _ov?.QueueRedraw();
    }

    public void Unmark()
    {
        _marking = false;
        _hot = false;
        _ov?.QueueRedraw();
    }

    public bool Hot
    {
        get => _hot;
        set { if (_hot == value) return; _hot = value; _ov?.QueueRedraw(); }
    }

    public bool Accepts => _accepts;

    public bool Marking => _marking;

    /// <summary>시각 요소를 다 붙인 뒤 한 번 부른다 — 점선·강조가 항상 맨 위에 그려지게 한다</summary>
    public void Seal()
    {
        _ov = new ZoneOverlay(this);
        AddChild(_ov);
        _ov.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void Repaint() => _ov?.QueueRedraw();
}

/// <summary>존의 점선·강조를 시각 요소 위에 그리는 얇은 판 (부모 _Draw 는 자식 아래에 깔린다)</summary>
internal partial class ZoneOverlay : Control
{
    private readonly DropZone _z;

    public ZoneOverlay(DropZone z)
    {
        _z = z;
        MouseFilter = MouseFilterEnum.Ignore;
    }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);

            // 지목 중인 구성품·내 장비 — 끌지 않을 때도 은은하게 표시한다
            // (손패 뱃지·예상 좋아요가 이 대상 기준으로 계산되므로 어디를 겨누는지 보여야 한다)
            if (!_z.Marking && _z.Aimed
                && _z.Kind is ZoneKind.EnemyEquipment or ZoneKind.MyEquipment)
            {
                DrawRect(r.Grow(2f), CombatArt.EdgeHi with { A = 0.45f }, filled: false, width: 1f);
            }

            if (!_z.Marking) return;

            if (_z.Hot && _z.Accepts)
            {
                for (int i = 3; i >= 1; i--)
                    DrawRect(r.Grow(3f + i * 3f), CombatArt.Gold with { A = 0.10f * i }, filled: false, width: 3f);
                DrawRect(r.Grow(3f), CombatArt.Gold, filled: false, width: 2.5f);
                return;
            }

            var col = _z.Accepts ? CombatArt.Gold with { A = 0.55f } : new Color("96503e") with { A = 0.4f };
            Dashed(r.Grow(3f), col);

            if (!_z.Accepts) Chip("대상 아님", r.Size / 2f);
        }

        private void Dashed(Rect2 r, Color c)
        {
            const float dash = 7f, gap = 5f;
            Run(r.Position, new Vector2(r.End.X, r.Position.Y), c, dash, gap);
            Run(new Vector2(r.End.X, r.Position.Y), r.End, c, dash, gap);
            Run(r.End, new Vector2(r.Position.X, r.End.Y), c, dash, gap);
            Run(new Vector2(r.Position.X, r.End.Y), r.Position, c, dash, gap);
        }

        private void Run(Vector2 a, Vector2 b, Color c, float dash, float gap)
        {
            float len = a.DistanceTo(b);
            if (len <= 0f) return;
            var dir = (b - a) / len;
            for (float t = 0; t < len; t += dash + gap)
                DrawLine(a + dir * t, a + dir * Mathf.Min(len, t + dash), c, 2f);
        }

        private void Chip(string text, Vector2 center)
        {
            var font = CombatArt.Font();
            const int size = 12;
            var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            var box = new Rect2(center - new Vector2(ts.X / 2f + 8f, ts.Y / 2f + 4f),
                new Vector2(ts.X + 16f, ts.Y + 8f));
            DrawRect(box, new Color(9f / 255f, 8f / 255f, 7f / 255f, 0.92f));
            DrawRect(box, new Color("7a3a2c"), filled: false, width: 1f);
            DrawString(font, new Vector2(box.Position.X, center.Y + size * 0.36f), text,
                HorizontalAlignment.Center, box.Size.X, size, new Color("e08b72"));
    }
}
