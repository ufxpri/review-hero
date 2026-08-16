// 전투 화면 (ADR-029 3차) — 웹판 ui/game/combat.html 의 이관본.
//
// ── 이 화면이 지키는 것 ─────────────────────────────────
// ① **규칙을 재구현하지 않는다** (ADR-025). 판정·좋아요·게이지·회복·방어는 전부
//    Battle.PreviewSubmit / SubmitReview 가 준다. 웹판에서 화면이 판정을 따로 계산하다가
//    밸런스를 고칠 때마다 표시값만 조용히 틀려지는 「미리보기 드리프트」를 겪었고,
//    previewSubmit 은 그걸 없애려고 만든 것이다. 여기에 수식을 쓰기 시작하면 그 버그가 돌아온다.
// ② **끌어다 놓기가 주 조작이다.** 만물마켓에선 무엇이든 상품이다 — 판매자도, 그가 든 칼도,
//    내가 산 물건도 각각 별개의 상품이고 각각 따로 리뷰가 달린다 (worldview §1.1).
//    카드를 끌면 드롭 존이 점선으로 드러나고, 유효한 대상 위에서 그 대상 기준 판정 말풍선이 뜬다.
// ③ **클릭 폴백**을 같이 산다 — 카드 클릭 → 상품 클릭. 접근성과 자동 검증 양쪽에 필요하다.
// ④ **연출이 흐름을 막지 않는다.** 헤드리스 완주(AutoPlay)는 이 파일을 통과하지 않고
//    CombatSession 만 두드린다 — 연출은 전부 여기에 갇혀 있다.
//
// 좌표는 웹판 CSS 를 1:1 로 옮긴 절대 배치다 (뷰포트가 같은 1344×768). 컨테이너로 다시 짜면
// 픽셀이 어긋나 「어디에 무엇이 있었는지」를 대조할 수 없어, 이관 단계에서는 좌표를 그대로 둔다.

using Godot;
using ReviewHero.Data;
using ReviewHero.Engine;
using ReviewHero.Game.Fx;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Combat;

public partial class CombatScene : Control
{
    private LoadedData _data = null!;
    private RunBridge? _run;
    private CombatSession _s = null!;

    // ── 항구 레이어 (다시 그려도 살아남는다) ──
    private Control _shaker = null!;
    private TextureRect _bg = null!;
    private Embers _pfx = null!;
    private Control _stage = null!;
    private Control _hand = null!;
    private CombatFx _fx = null!;
    private Control _dragLayer = null!;
    private VerdictBubble _verdict = null!;
    private ColorRect _desat = null!;
    private Control _overlay = null!;
    private Label _toast = null!;

    // ── 조작 상태 ──
    private readonly List<DropZone> _zones = new();
    private readonly List<CardView> _cards = new();
    private int? _held;
    private bool _dragging, _pressed, _toggleOff;
    private Vector2 _pressPos;
    private CardView? _proxy;
    private DropZone? _hot;
    private bool _busy, _resultHandled, _retreatArmed;
    private int _prevEnemyWill;
    private Rect2 _ctrlRect;
    private string _shot = string.Empty;

    // 적 의지 게이지의 잔상(ghost) — 깎인 만큼이 늦게 따라온다
    private ColorRect? _eGhost;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var args = Godot.OS.GetCmdlineUserArgs();
        _shot = CombatEntry.ArgValue(args, "shot") ?? string.Empty;

        _data = GameData.All;

        // 디버그 캡처용 — 런이 없으면 하나 만들어 첫 전투 노드에 세운다 (화면을 찍기 위한 발판)
        if (CombatEntry.HasFlag(args, "newrun") && RunStore.Current is null)
        {
            RunStore.NewRun(42);
            foreach (var id in RunStore.Reachable())
            {
                RunStore.EnterNode(id, navigate: false);
                break;
            }
        }

        // --rh-norun 은 저장된 런을 무시하고 디버그 단독 전투로 연다 (임의의 적을 화면에 세워 보기 위한 스위치)
        string why = "런 연결 생략(--rh-norun)";
        _run = CombatEntry.HasFlag(args, "norun") ? null : RunBridge.TryAttach(out why);
        var ctx = CombatEntry.Build(_data, _run, args, out string note);
        _s = new CombatSession(_data, ctx);
        _s.Log($"전투 개시: {_s.Enemy.Name} ({CombatSession.TierLabel(_s.Enemy.Tier)}) · {note} · {why}");
        _prevEnemyWill = Mathf.Max(0, _s.St.Enemy.Will);

        // 「전투 중」 표시 — 여기서 이탈하면 이 노드로 강제 복귀하고 전투는 처음부터다 (CONTRACT)
        _run?.BeginCombat(_run.NodeId);

        BuildLayers();
        Paint();
        ApplyShot();
    }

    // ══ 레이어 ═══════════════════════════════════════════

    private void BuildLayers()
    {
        _shaker = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _shaker.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        AddChild(_shaker);

        var back = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        back.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        _shaker.AddChild(back);

        _bg = new TextureRect
        {
            Texture = CombatArt.SceneTexture(_s.Enemy),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bg.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        _shaker.AddChild(_bg);

        // 비네트 — 무대 가운데만 남기고 가장자리를 어둠으로 눌러 카드 글자가 뜨게 한다
        var vig = new TextureRect
        {
            Texture = Vignette(),
            StretchMode = TextureRect.StretchModeEnum.Scale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        vig.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        _shaker.AddChild(vig);

        _pfx = new Embers();          // 앰비언트 불씨 — 배경 위, 인물·UI 아래 (웹판 z-index 9)
        _shaker.AddChild(_pfx);

        _stage = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _stage.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH - CombatArt.HandH);
        _shaker.AddChild(_stage);

        _hand = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _hand.At(0, CombatArt.ScreenH - CombatArt.HandH, CombatArt.ScreenW, CombatArt.HandH);
        _shaker.AddChild(_hand);

        _verdict = new VerdictBubble();
        _shaker.AddChild(_verdict);

        _fx = new CombatFx();
        _shaker.AddChild(_fx);

        _dragLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _dragLayer.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        _shaker.AddChild(_dragLayer);

        _desat = CombatFx.MakeDesaturateLayer();
        _desat.Visible = false;
        AddChild(_desat);

        _toast = CombatArt.Text(string.Empty, 14, new Color("f0b0a0"), HorizontalAlignment.Center);
        _toast.At(462, 470, 420, 30);
        _toast.Modulate = new Color(1, 1, 1, 0);
        AddChild(_toast);

        _overlay = new Control { MouseFilter = MouseFilterEnum.Stop, Visible = false };
        _overlay.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        AddChild(_overlay);
    }

    private static Texture2D Vignette()
    {
        var g = new Gradient();
        g.SetOffset(0, 0.30f);
        g.SetColor(0, new Color(0, 0, 0, 0));
        g.AddPoint(0.72f, new Color(0, 0, 0, 0.42f));
        g.SetOffset(g.GetPointCount() - 1, 1f);
        g.SetColor(g.GetPointCount() - 1, new Color(0, 0, 0, 0.86f));
        return new GradientTexture2D
        {
            Gradient = g, Width = 256, Height = 160,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.58f, 0.40f),
            FillTo = new Vector2(1.02f, 0.40f),
        };
    }

    // ══ 다시 그리기 ══════════════════════════════════════

    private void Paint()
    {
        foreach (var c in _stage.GetChildren()) c.QueueFree();
        foreach (var c in _hand.GetChildren()) c.QueueFree();
        _zones.Clear();
        _cards.Clear();

        BuildStage();
        BuildHand();

        // 카드를 고르거나 끌고 있으면 존 표시를 되살린다 (다시 그리면 상태가 날아가므로)
        if (_held is int uid && DefOf(uid) is { } d) MarkZones(d);
    }

    // ── 무대 ─────────────────────────────────────────────

    private void BuildStage()
    {
        var st = _s.St;

        _stage.AddChild(IntentBar());
        _stage.AddChild(EnemyPlate());

        // 판매자 본체 — 가장 큰 드롭 존
        var enemyZone = new DropZone { Kind = ZoneKind.Enemy, Aimed = true };
        enemyZone.At(700, 170, 288, 290);
        var art = CombatArt.EnemyTexture(_s.Enemy.Id);
        if (art is not null)
        {
            var img = new TextureRect
            {
                Texture = art,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            img.At(0, 0, 288, 290);
            if (st.Enemy.Stealth) img.Modulate = new Color(1, 1, 1, 0.28f);
            enemyZone.AddChild(img);
        }
        else
        {
            var thumb = CombatArt.Text(CombatArt.ThumbOf(_s.Enemy.Id), 120, CombatArt.Ink, HorizontalAlignment.Center);
            thumb.At(0, 70, 288, 160);
            enemyZone.AddChild(thumb);
        }
        if (st.Enemy.Stealth)
        {
            var note = Chip("🌫 판매자가 잠적했다 — [배송] 계열 리뷰만 도달한다", CombatArt.Ink, CombatArt.Slab, 12,
                CombatArt.EdgeHi);
            note.Position = new Vector2((288 - note.Size.X) / 2f, 88);
            enemyZone.AddChild(note);
        }
        Register(enemyZone, _stage);

        BuildEnemyGear();

        // 주인공 — 좌측 전경. 내 장비 드롭 존이기도 하다
        var hero = new DropZone { Kind = ZoneKind.MyEquipment, Index = _s.SelectedMyEq };
        hero.At(0, 122, 280, 350);
        var htex = CombatArt.HeroTexture();
        if (htex is not null)
        {
            var himg = new TextureRect
            {
                Texture = htex,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            himg.At(0, 0, 280, 350);
            hero.AddChild(himg);
        }
        Register(hero, _stage);

        BuildMyGear();
        BuildTrash();
    }

    private Control IntentBar()
    {
        var e = _s.St.Enemy;
        var a = e.Def.Actions.FirstOrDefault(x => x.Id == e.IntentId);
        var box = CombatArt.Slabbed(CombatArt.Panel, CombatArt.EdgeHi, 5);
        box.At(656, 6, 372, 40);

        string icon = a?.AType switch
        {
            EnemyActionType.Gimmick => "📢",
            EnemyActionType.Stealth => "🌫",
            EnemyActionType.Buff => "🛠",
            EnemyActionType.Steal => "🪙",
            _ => "📦",
        };
        box.AddChild(CombatArt.Text(icon, 17, CombatArt.Ink).At(10, 10, 24, 22));
        box.AddChild(CombatArt.Text("발송 예정", 10, CombatArt.Dim).At(38, 4, 200, 14));
        box.AddChild(CombatArt.Text(a?.Name ?? "?", 15, CombatArt.Ink).At(38, 17, 220, 20));

        var dmg = a?.Effects.FirstOrDefault(f => f.Op == "damage")?.Value;
        var bits = new List<string>();
        if (dmg is int d) bits.Add($"좋아요 {d}");
        if (e.Charging is not null) bits.Add($"준비 중 {e.Charging.Remaining}턴");
        else if (a is { ChargeTurns: > 0 }) bits.Add("준비형");
        if (e.StunTurns > 0) bits.Add($"기절 {e.StunTurns}턴");
        box.AddChild(CombatArt.Text(string.Join(" · ", bits), 12, CombatArt.Red, HorizontalAlignment.Right)
            .At(200, 12, 162, 18));
        return box;
    }

    private Control EnemyPlate()
    {
        var e = _s.St.Enemy;
        int w = Mathf.Max(0, e.Will);
        double ratio = e.MaxWill > 0 ? (double)w / e.MaxWill : 0;

        var box = CombatArt.Slabbed(CombatArt.Panel, CombatArt.Edge, 6);
        float y = 8f;

        var name = CombatArt.Text($"{e.Def.Name}   {CombatSession.TierLabel(e.Def.Tier)}", 17, CombatArt.Ink,
            HorizontalAlignment.Center);
        name.At(8, y, 356, 22);
        box.AddChild(name);
        y += 24f;

        var rating = CombatArt.Text(
            $"{CombatArt.Stars5(ratio)}   · 리뷰 {1000 + e.MaxWill * 7}건  · 턴 {_s.St.Turn}", 12,
            CombatArt.Gold, HorizontalAlignment.Center);
        rating.At(8, y, 356, 18);
        box.AddChild(rating);
        y += 20f;

        // 의지 게이지 — 잔상(흰 띠)이 깎인 만큼을 늦게 따라온다
        var frame = CombatArt.Slabbed(new Color(0, 0, 0, 0.55f), CombatArt.Edge, 3);
        frame.At(10, y, 352, 18);
        var ghost = new ColorRect { Color = new Color(1, 1, 1, 0.42f), MouseFilter = MouseFilterEnum.Ignore };
        ghost.At(1, 1, 350f * (float)(e.MaxWill > 0 ? (double)_prevEnemyWill / e.MaxWill : 0), 16);
        frame.AddChild(ghost);
        _eGhost = ghost;
        var fill = new ColorRect { Color = new Color("a83a26"), MouseFilter = MouseFilterEnum.Ignore };
        fill.At(1, 1, 350f * (float)ratio, 16);
        frame.AddChild(fill);
        frame.AddChild(CombatArt.Text($"의지 {w} / {e.MaxWill}", 11, CombatArt.Ink, HorizontalAlignment.Center)
            .At(0, 1, 352, 16));
        box.AddChild(frame);
        y += 22f;

        if (_prevEnemyWill != w)
        {
            var gt = CreateTween();
            gt.TweenInterval(0.18f);
            gt.TweenProperty(ghost, "size:x", 350f * (float)ratio, 0.75f).SetTrans(Tween.TransitionType.Cubic);
            _prevEnemyWill = w;
        }

        // 약점 / 평가 불가 — 판정의 근거라 항상 보인다
        float cx = 10f, cy = y;
        foreach (var t in e.Def.WeaknessTags)
            PlaceChip(box, $"약점: #{t}", new Color("8fce9e"), new Color("3c6b49"), ref cx, ref cy, 352);
        if (e.Def.NullTags.Count == 0)
            PlaceChip(box, "평가 불가 항목 없음", CombatArt.Dim, CombatArt.Edge, ref cx, ref cy, 352);
        foreach (var t in e.Def.NullTags)
            PlaceChip(box, $"평가 불가: {t}", new Color("e08b72"), new Color("7a3a2c"), ref cx, ref cy, 352);
        y = cy + 22f;

        // 버프·디버프
        cx = 10f; cy = y;
        foreach (var b in e.Buffs)
            PlaceChip(box, $"📈 공격 +{b.Value}" + (b.ProtectedBy is not null ? " (알바 리뷰)" : string.Empty),
                new Color("e08b72"), new Color("7a3a2c"), ref cx, ref cy, 352);
        if (e.DamageReductionNextHit > 0)
            PlaceChip(box, $"🛡 다음 리뷰 −{e.DamageReductionNextHit}", new Color("e08b72"), new Color("7a3a2c"),
                ref cx, ref cy, 352);
        if (e.ReflectNextHit > 0)
            PlaceChip(box, $"🪞 반사 {e.ReflectNextHit}", new Color("e08b72"), new Color("7a3a2c"), ref cx, ref cy, 352);
        foreach (var d in e.Debuffs)
        {
            string label = d.Kind == EnemyDebuffKind.AttackHalve ? "공격 −50%" : $"공격 −{d.Value}";
            PlaceChip(box, d.Suspended ? $"💬 {label} — 사장님 답글로 정지" : $"😡 내 악평: {label} [{d.Suit}]",
                d.Suspended ? CombatArt.Dim : new Color("8fce9e"),
                d.Suspended ? CombatArt.Edge : new Color("3c6b49"), ref cx, ref cy, 352);
        }
        y = (cx > 10f || cy > y) ? cy + 22f : y;

        box.At(656, 50, 372, y + 6f);
        return box;
    }

    private void BuildEnemyGear()
    {
        var eq = _s.St.Enemy.Equipment;
        float[] colY = { 222f, 222f };
        float[] colX = { 468f, 1050f };

        for (int side = 0; side < 2; side++)
        {
            if (eq.Count <= side) continue;
            var hd = CombatArt.Text("🛠 구성품 — 각각 별개 상품이다", 10, CombatArt.Dim, HorizontalAlignment.Center);
            hd.At(colX[side], colY[side] - 16, 170, 14);
            _stage.AddChild(hd);
        }

        for (int i = 0; i < eq.Count; i++)
        {
            int side = i % 2;
            var q = eq[i];
            var slot = new DropZone
            {
                Kind = ZoneKind.EnemyEquipment,
                Index = i,
                Dead = q.Destroyed,
                Aimed = i == _s.SelectedEnemyEq && !q.Destroyed,
            };

            var panel = CombatArt.Slabbed(CombatArt.Slab, CombatArt.Edge, 6);
            float y = 6f;
            panel.AddChild(CombatArt.Text(q.Name, 12, CombatArt.Ink).At(8, y, 154, 16));
            y += 18f;

            float cx = 8f, cy = y;
            foreach (var t in q.Tags)
                PlaceChip(panel, $"#{t}", new Color("e8d9ae"), new Color("2f2a20"), ref cx, ref cy, 154, 9);
            y = cy + 16f;

            if (q.Destroyed)
            {
                panel.AddChild(CombatArt.Text("품절 (파괴)", 10, CombatArt.Red).At(8, y, 154, 14));
                y += 16f;
            }
            else
            {
                int max = Mathf.Max(1, _s.Enemy.Equipment[i].Durability);
                var bar = CombatArt.Slabbed(new Color(0, 0, 0, 0.5f), CombatArt.Edge, 2);
                bar.At(8, y, 154, 7);
                var fill = new ColorRect { Color = new Color("8a7a4e"), MouseFilter = MouseFilterEnum.Ignore };
                fill.At(1, 1, 152f * Mathf.Clamp((float)q.Durability / max, 0f, 1f), 5);
                bar.AddChild(fill);
                panel.AddChild(bar);
                y += 10f;

                string extra = q.Dot is not null ? $" · 도트 −{q.Dot.Value} ({q.Dot.Remaining}턴)" : string.Empty;
                if (q.DisabledTurns > 0) extra += " · 반품 접수중";
                panel.AddChild(CombatArt.Text($"내구도 {q.Durability}{extra}", 10, CombatArt.Dim).At(8, y, 154, 14));
                y += 16f;
            }

            panel.At(0, 0, 170, y + 2f);
            slot.At(colX[side], colY[side], 170, y + 2f);
            slot.AddChild(panel);
            if (q.Destroyed) panel.Modulate = new Color(1, 1, 1, 0.4f);

            // 연결선 — 이 구성품이 저 상품 것임을 눈으로 잇는다
            var link = new ColorRect { Color = CombatArt.Edge with { A = 0.6f }, MouseFilter = MouseFilterEnum.Ignore };
            link.At(side == 0 ? colX[side] + 170 : colX[side] - 58, colY[side] + (y + 2f) / 2f, 58, 1);
            _stage.AddChild(link);

            Register(slot, _stage);
            colY[side] += y + 10f;
        }
    }

    private void BuildMyGear()
    {
        var hd = CombatArt.Text("🎒 내가 산 상품 — ★4~5 찬양 = 버프", 10, CombatArt.Dim);
        hd.At(264, 184, 182, 14);
        _stage.AddChild(hd);

        float y = 200f;
        var mine = _s.St.Player.Equipment;
        for (int i = 0; i < mine.Count; i++)
        {
            var q = mine[i];
            var slot = new DropZone { Kind = ZoneKind.MyEquipment, Index = i, Aimed = i == _s.SelectedMyEq };
            var panel = CombatArt.Slabbed(CombatArt.Slab, CombatArt.Edge, 6);

            float iy = 6f;
            panel.AddChild(CombatArt.Text(q.Def.Name, 12, CombatArt.Ink).At(8, iy, 166, 16));
            iy += 18f;

            float cx = 8f, cy = iy;
            foreach (var t in q.Def.Tags)
                PlaceChip(panel, $"#{t}", new Color("e8d9ae"), new Color("2f2a20"), ref cx, ref cy, 166, 9);
            foreach (var t in q.Def.NullTags)
                PlaceChip(panel, $"불가:{t}", new Color("e0a08b"), new Color("3a221c"), ref cx, ref cy, 166, 9);
            iy = cy + 16f;

            if (q.Defense > 0)
            {
                panel.AddChild(CombatArt.Text($"🛡 방어 {q.Defense} — 적 좋아요를 대신 맞는다", 10,
                    new Color("a9dcf7")).At(8, iy, 166, 14));
                iy += 15f;
            }
            int buff = q.Attachments.Where(a => a.Kind == AttachmentKind.DamageBuff).Sum(a => a.Value);
            if (buff > 0)
            {
                panel.AddChild(CombatArt.Text($"부착: 좋아요 +{buff}", 10, new Color("8fce9e")).At(8, iy, 166, 14));
                iy += 15f;
            }

            panel.At(0, 0, 182, iy + 2f);
            slot.At(264, y, 182, iy + 2f);
            slot.AddChild(panel);
            Register(slot, _stage);
            y += iy + 8f;
        }
    }

    private void BuildTrash()
    {
        var z = new DropZone { Kind = ZoneKind.Trash };
        z.At(268, CombatArt.ScreenH - CombatArt.HandH - 6 - 52, 178, 52);
        var panel = CombatArt.Slabbed(new Color(9f / 255f, 8f / 255f, 7f / 255f, 0.62f), CombatArt.Edge, 8);
        panel.At(0, 0, 178, 52);
        panel.AddChild(CombatArt.Text("🗑", 22, CombatArt.Ink).At(9, 14, 28, 26));
        panel.AddChild(CombatArt.Text("초고 폐기함", 12, CombatArt.Ink).At(42, 6, 130, 16));
        panel.AddChild(CombatArt.Text("여기에 놓으면 퇴고 — ✍1\n1장 버리고 1장 받는다", 9, CombatArt.Dim)
            .At(42, 22, 132, 26));
        z.AddChild(panel);
        Register(z, _stage);
    }

    // ── 손패 영역 ────────────────────────────────────────

    private void BuildHand()
    {
        var scrim = new ColorRect { Color = new Color(6f / 255f, 5f / 255f, 4f / 255f, 0.86f), MouseFilter = MouseFilterEnum.Ignore };
        scrim.At(0, 18, CombatArt.ScreenW, CombatArt.HandH - 18);
        _hand.AddChild(scrim);

        _hand.AddChild(HintBar());
        BuildStatus();
        BuildCards();
        BuildCtrl();
    }

    private Control HintBar()
    {
        var bar = CombatArt.Slabbed(CombatArt.Slab, CombatArt.Edge, 5);
        bar.At(218, 0, 918, 24);

        string msg;
        if (_s.PendingGiftFor is not null)
            msg = "🎁 무료 나눔 — 증정할 카드를 클릭하라 (이 런에서 제외)";
        else if (_held is int uid && DefOf(uid) is { } d)
            msg = $"✍ {d.Name} — 강조된 {TargetLabel(d.Target)} 위에 놓아라 (끌어다 놓기 또는 클릭)";
        else
            msg = "🎯 여기선 전부 상품이다 — 판매자도, 그가 든 물건도, 내 장비도. "
                + "리뷰를 그 상품 위로 끌어다 놓으면 제출 (클릭 → 상품 클릭도 된다)";
        bar.AddChild(CombatArt.Text(msg, 12, CombatArt.Ink).At(10, 3, 700, 18));
        bar.AddChild(CombatArt.Text("★원산지  ●팩트  ⚠헛소리", 12, CombatArt.Gold, HorizontalAlignment.Right)
            .At(700, 3, 208, 18));
        return bar;
    }

    private void BuildStatus()
    {
        var p = _s.St.Player;
        var zone = new DropZone { Kind = ZoneKind.MyEquipment, Index = _s.SelectedMyEq };
        zone.At(10, CombatArt.HandH - 12 - 152, 200, 152);

        var box = CombatArt.Slabbed(CombatArt.Slab, CombatArt.Edge, 8, 2);
        box.At(0, 0, 200, 152);

        float y = 8f;
        box.AddChild(CombatArt.Text($"✍ {RunStore.Penname} · {Types.DispositionLabel[p.Disposition]}", 11,
            CombatArt.Dim, wrap: true).At(10, y, 180, 26));
        y += 28f;

        int w = Mathf.Max(0, p.Will);
        box.AddChild(CombatArt.Text("🧠 의지", 11, CombatArt.Ink).At(10, y, 50, 18));
        var frame = CombatArt.Slabbed(new Color(0, 0, 0, 0.55f), CombatArt.Edge, 3);
        frame.At(62, y, 128, 17);
        var fill = new ColorRect { Color = new Color("3f8450"), MouseFilter = MouseFilterEnum.Ignore };
        fill.At(1, 1, 126f * (p.MaxWill > 0 ? Mathf.Clamp((float)w / p.MaxWill, 0f, 1f) : 0f), 15);
        frame.AddChild(fill);
        frame.AddChild(CombatArt.Text($"{w} / {p.MaxWill}", 11, CombatArt.Ink, HorizontalAlignment.Center)
            .At(0, 0, 128, 17));
        box.AddChild(frame);
        y += 22f;

        box.AddChild(CombatArt.Text("신뢰도", 10, CombatArt.Dim).At(10, y, 44, 16));
        for (int i = 0; i < 10; i++)
        {
            bool on = i < p.Gauge;
            var cell = CombatArt.Slabbed(on ? CombatArt.Gold : new Color(0, 0, 0, 0.5f),
                on ? CombatArt.Gold : CombatArt.Edge, 2);
            cell.At(56 + i * 13.4f, y + 2, 12, 11);
            box.AddChild(cell);
        }
        y += 20f;

        string crit = p.Gauge >= 10 && !p.CritUsedThisTurn
            ? "🔥 이 라운드 베스트 리뷰 — 발동 가능!"
            : p.CritUsedThisTurn
                ? $"신뢰도 {p.Gauge}/10 · 베스트 리뷰: 이번 턴 사용됨"
                : $"베스트 리뷰까지 {10 - p.Gauge}칸";
        box.AddChild(CombatArt.Text(crit, 10, p.Gauge >= 10 && !p.CritUsedThisTurn ? CombatArt.Gold : CombatArt.Dim,
            wrap: true).At(10, y, 180, 24));
        y += 26f;

        var extras = new List<string>();
        if (p.Reaction is not null) extras.Add("🛡 리액션 대기");
        if (p.StoredDamageBonus > 0) extras.Add($"💢 예약 +{p.StoredDamageBonus}");
        box.AddChild(CombatArt.Text($"🪙 {p.Gold}" + (extras.Count > 0 ? " · " + string.Join(" · ", extras) : string.Empty),
            10, CombatArt.Gold).At(10, y, 180, 14));
        y += 15f;
        box.AddChild(CombatArt.Text($"뽑을 카드 {p.Deck.Count} · 버린 카드 {p.Discard.Count}", 10, CombatArt.Dim)
            .At(10, y, 180, 14));

        zone.AddChild(box);
        Register(zone, _hand);
    }

    private void BuildCards()
    {
        var hand = _s.Hand();
        const float areaX = 218f, areaW = 918f;
        float gap = 10f;
        float total = hand.Count * CardView.W + Mathf.Max(0, hand.Count - 1) * gap;
        float x = areaX + Mathf.Max(0f, (areaW - total) / 2f);
        float top = CombatArt.HandH - 8 - CardView.H;

        foreach (var row in hand)
        {
            var (origin, here) = OriginLine(row.Def);
            var card = new CardView(row.Uid, row.Def, row.Preview, origin, here);
            card.At(x, top, CardView.W, CardView.H);
            _hand.AddChild(card);
            card.RememberHome();
            if (_held == row.Uid)
            {
                card.SetSelected(true);
                if (_dragging) card.SetGhost(true);
                else card.SetLift(-18f, 1.05f);
            }
            _cards.Add(card);
            x += CardView.W + gap;
        }
    }

    private void BuildCtrl()
    {
        var p = _s.St.Player;
        float x = CombatArt.ScreenW - 10 - 190;
        var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        holder.At(x, 40, 190, CombatArt.HandH - 52);
        _hand.AddChild(holder);
        _ctrlRect = new Rect2(x, CombatArt.ScreenH - CombatArt.HandH + 40, 190, CombatArt.HandH - 52);

        // 필력 오브
        var orb = CombatArt.Slabbed(new Color("1c1710"), CombatArt.EdgeHi, 36, 2);
        orb.At(59, 0, 72, 72);
        orb.AddChild(CombatArt.Text($"{p.Energy}", 26, CombatArt.Gold, HorizontalAlignment.Center).At(0, 12, 72, 30));
        orb.AddChild(CombatArt.Text("✍ 필력", 10, CombatArt.Dim, HorizontalAlignment.Center).At(0, 44, 72, 14));
        holder.AddChild(orb);

        float y = 84f;
        bool over = _s.St.Result is not null;

        if (p.Gauge >= 10 && !p.CritUsedThisTurn && !over)
        {
            holder.AddChild(CBtn("🔥 베스트 리뷰 등극", DoCrit, true, CombatArt.Gold).At(0, y, 190, 30));
            y += 34f;
        }
        if (_s.Battle.ParcelAvailable && !over)
        {
            holder.AddChild(CBtn("📦 택배 개봉 ✍1", DoParcel, p.Energy >= 1, new Color("f0d69a")).At(0, y, 190, 30));
            y += 34f;
        }
        holder.AddChild(CBtn("퇴고 ✍1 · 1장 교체", DoReviseSelected, p.Energy >= 1 && !over).At(0, y, 190, 30));
        y += 34f;

        if (_s.Enemy.Tier == EnemyTier.Boss)
        {
            holder.AddChild(CBtn("항복 불가 (보스)", null, false).At(0, y, 190, 30));
        }
        else
        {
            holder.AddChild(CBtn(_retreatArmed ? "정말 항복? (한 번 더)" : "🏳 항복 (+6G 뜯기)", DoRetreat, !over,
                new Color("f0b0a0")).At(0, y, 190, 30));
        }
        y += 38f;
        holder.AddChild(CBtn("영업 마감 (턴 종료)", DoEndTurn, !over, new Color("ffd9cc"), true).At(0, y, 190, 38));
    }

    private Button CBtn(string text, System.Action? onPressed, bool enabled, Color? fg = null, bool danger = false)
    {
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", danger ? 14 : 12);
        b.AddThemeColorOverride("font_color", fg ?? CombatArt.Ink);
        b.AddThemeColorOverride("font_hover_color", CombatArt.Ink);
        b.AddThemeColorOverride("font_disabled_color", CombatArt.Dim with { A = 0.5f });
        var bg = danger ? new Color("602115") : new Color("332a1b");
        b.AddThemeStyleboxOverride("normal", CombatArt.Box(bg, danger ? new Color("a4553c") : CombatArt.Edge, 6));
        b.AddThemeStyleboxOverride("hover", CombatArt.Box(bg.Lightened(0.15f), CombatArt.EdgeHi, 6));
        b.AddThemeStyleboxOverride("pressed", CombatArt.Box(bg.Darkened(0.2f), CombatArt.EdgeHi, 6));
        b.AddThemeStyleboxOverride("disabled", CombatArt.Box(bg with { A = 0.4f }, CombatArt.Edge with { A = 0.4f }, 6));
        if (onPressed is not null) b.Pressed += onPressed;
        return b;
    }

    // ══ 드래그 & 클릭 ════════════════════════════════════

    public override void _Input(InputEvent ev)
    {
        if (_overlay.Visible) return;
        if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            var pos = mb.Position;
            if (_ctrlRect.HasPoint(pos)) return;   // 조작 버튼은 Button 이 처리한다
            if (mb.Pressed) OnPress(pos);
            else OnRelease(pos);
            return;
        }
        if (ev is InputEventMouseMotion mm) OnMotion(mm.Position);
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape }) Unhold();
    }

    private void OnPress(Vector2 pos)
    {
        if (_busy || _s.St.Result is not null) return;
        var card = CardAt(pos);
        if (card is not null)
        {
            _pressed = true;
            _pressPos = pos;
            _toggleOff = _held == card.Uid;   // 같은 카드를 다시 누르면(끌지 않으면) 선택 해제
            if (!_toggleOff) SelectCard(card.Uid);
            return;
        }
        var zone = ZoneAt(pos);
        if (zone is not null)
        {
            if (_held is int uid) { PlayOnZone(uid, zone); return; }
            AimAt(zone);
            return;
        }
        Unhold();
    }

    private void OnRelease(Vector2 pos)
    {
        _pressed = false;
        if (!_dragging)
        {
            if (_toggleOff) { _toggleOff = false; Unhold(); }
            return;
        }
        _toggleOff = false;
        _dragging = false;
        _proxy?.QueueFree();
        _proxy = null;
        var zone = ZoneAt(pos) ?? _hot;
        if (_held is int uid && zone is not null) PlayOnZone(uid, zone);
        else Unhold();
    }

    private void OnMotion(Vector2 pos)
    {
        if (_pressed && !_dragging && _held is not null && pos.DistanceTo(_pressPos) > 6f) BeginDrag();
        if (_dragging) DragTo(pos);
    }

    private void SelectCard(int uid)
    {
        // X04 증정 대기 중이면 이 카드가 증정 대상이 되고 즉시 사용된다 (엔진이 판단한다)
        if (_s.PendingGiftFor is not null)
        {
            _s.SelectCard(uid);
            _held = null;
            ClearZones();
            Paint();
            MaybeResult();
            return;
        }
        _s.SelectCard(uid);
        _held = uid;
        _dragging = false;
        var d = DefOf(uid);
        Paint();
        if (d is not null)
        {
            MarkZones(d);
            var def = DefaultZone(d);
            if (def is not null) ShowVerdict(uid, d, def);
        }
    }

    private void BeginDrag()
    {
        if (_held is not int uid) return;
        _dragging = true;
        var src = _cards.FirstOrDefault(c => c.Uid == uid);
        src?.SetGhost(true);
        var row = _s.Hand().FirstOrDefault(r => r.Uid == uid);
        if (row is null) return;
        var (origin, here) = OriginLine(row.Def);
        _proxy = new CardView(uid, row.Def, row.Preview, origin, here);
        _proxy.At(0, 0, CardView.W, CardView.H);
        _proxy.Scale = new Vector2(0.92f, 0.92f);
        _proxy.Modulate = new Color(1, 1, 1, 0.95f);
        _dragLayer.AddChild(_proxy);
        DragTo(_pressPos);
    }

    /// <summary>끌고 있는 카드를 이 자리로. 존 강조와 말풍선이 여기서 갱신된다</summary>
    private void DragTo(Vector2 pos)
    {
        if (_proxy is not null) _proxy.Position = pos - new Vector2(CardView.W / 2f, 30f);
        var z = ZoneAt(pos);
        if (z != _hot)
        {
            if (_hot is not null) _hot.Hot = false;
            _hot = z;
            if (_hot is not null) _hot.Hot = true;
        }
        if (z is not null && _held is int uid && DefOf(uid) is { } d) ShowVerdict(uid, d, z);
        else _verdict.HideBubble();
    }

    private void Unhold()
    {
        _held = null;
        _dragging = false;
        _pressed = false;
        _proxy?.QueueFree();
        _proxy = null;
        _s.Unselect();
        ClearZones();
        Paint();
    }

    private void AimAt(DropZone z)
    {
        if (z.Dead) return;
        if (z.Kind == ZoneKind.EnemyEquipment) _s.SelectTarget(TargetSlot.EnemyEquipment, z.Index);
        else if (z.Kind == ZoneKind.MyEquipment) _s.SelectTarget(TargetSlot.MyEquipment, z.Index);
        else return;
        Paint();
    }

    // ── 존 판단 (규칙이 아니라 대상 종류 대조다) ─────────

    /// <summary>이 존이 이 카드의 대상인가 — 카드의 Target 이 곧 대상 종류다. 폐기함만 아무 카드나 받는다</summary>
    private bool Accepts(DropZone z, CardDef d)
    {
        if (z.Dead) return false;
        if (z.Kind == ZoneKind.Trash) return _s.St.Player.Energy >= 1;
        return z.Kind switch
        {
            ZoneKind.Enemy => d.Target == TargetKind.Enemy,
            ZoneKind.EnemyEquipment => d.Target == TargetKind.EnemyEquipment,
            _ => d.Target == TargetKind.MyEquipment,
        };
    }

    private void MarkZones(CardDef d)
    {
        foreach (var z in _zones) z.Mark(Accepts(z, d));
    }

    private void ClearZones()
    {
        foreach (var z in _zones) z.Unmark();
        _hot = null;
        _verdict.HideBubble();
    }

    private DropZone? DefaultZone(CardDef d) => d.Target switch
    {
        TargetKind.Enemy => _zones.FirstOrDefault(z => z.Kind == ZoneKind.Enemy),
        TargetKind.EnemyEquipment => _zones.FirstOrDefault(z => z.Kind == ZoneKind.EnemyEquipment
                && z.Index == _s.SelectedEnemyEq && !z.Dead)
            ?? _zones.FirstOrDefault(z => z.Kind == ZoneKind.EnemyEquipment && !z.Dead),
        _ => _zones.FirstOrDefault(z => z.Kind == ZoneKind.MyEquipment && z.Index == _s.SelectedMyEq),
    };

    // ══ 말풍선 ═══════════════════════════════════════════

    private void ShowVerdict(int uid, CardDef d, DropZone z)
    {
        var rect = new Rect2(z.GlobalPosition, z.Size);
        if (z.Kind == ZoneKind.Trash)
        {
            bool ok = Accepts(z, d);
            _verdict.ShowFor(ok ? new Color("5c5344") : new Color("a3301c"),
                (ok ? "초고 폐기" : "필력 부족") + $" — {d.Name}",
                ok ? "이 초고를 구겨 버리고 새 리뷰를 한 장 받는다 — 태그 사냥." : "퇴고에는 필력이 1 필요하다.",
                ok ? "✍1 소모 · 손패 1장 교체" : string.Empty, rect);
            return;
        }
        if (!Accepts(z, d))
        {
            _verdict.ShowFor(new Color("a3301c"), $"대상 아님 — {d.Name}",
                $"이 리뷰는 {TargetLabel(d.Target)} 앞으로만 낼 수 있다. 다른 상품엔 놓이지 않는다.",
                string.Empty, rect);
            return;
        }

        // ★ 판정·수치는 전부 엔진이 준다 (ADR-025) — 여기서 계산하지 않는다
        var slot = z.Kind switch
        {
            ZoneKind.EnemyEquipment => TargetSlot.EnemyEquipment,
            ZoneKind.MyEquipment => TargetSlot.MyEquipment,
            _ => TargetSlot.Enemy,
        };
        var pv = _s.PreviewOn(uid, slot, z.Index);

        var (label, color) = VerdictLabel(pv);
        var bits = new List<string>();
        if (pv.Likes is int likes)
        {
            bits.Add(pv.LikesKind switch
            {
                LikesKind.Defense => $"🛡 예상 방어 {likes}",
                LikesKind.Equipment => $"👍 예상 내구도 −{likes}",
                _ => $"👍 예상 좋아요 {likes}",
            });
        }
        if (pv.Gauge != 0) bits.Add($"신뢰도 {(pv.Gauge > 0 ? "+" : string.Empty)}{pv.Gauge}");
        if (pv.Heal != 0) bits.Add($"의지 +{pv.Heal}");
        if (bits.Count == 0) bits.Add(CombatSession.Likeify(d.Ui));
        bits.Add($"✍{d.Cost}");
        if (!pv.Affordable) bits.Add("필력 부족");

        _verdict.ShowFor(color, $"{label} — {d.Name}", VerdictNote(d, pv, z), string.Join(" · ", bits), rect);
    }

    /// <summary>말풍선 라벨엔 배율을 같이 박는다 — 「일반」이 상성 없음(×1.0)이라는 사실이 보여야 한다</summary>
    private static (string, Color) VerdictLabel(SubmitPreview pv)
    {
        if (pv.Blocked is BlockedReason b)
        {
            return b switch
            {
                BlockedReason.Miss => ("빗나감", CombatArt.JNone),
                BlockedReason.Void => ("리뷰할 상품 없음", CombatArt.JNone),
                _ => ("무판정", new Color("5c5344")),
            };
        }
        return pv.Judgement switch
        {
            Judgement.Origin => ("원산지! ×1.5 +1", CombatArt.JOrigin),
            Judgement.Fact => ("팩트! ×1.5", CombatArt.JFact),
            Judgement.Fumble => ("헛소리… ×0.5", CombatArt.JFumble),
            _ => ("일반 ×1.0", new Color("5c5344")),
        };
    }

    private string VerdictNote(CardDef d, SubmitPreview pv, DropZone z)
    {
        string tag = (d as ReviewCardDef)?.Tag ?? string.Empty;
        if (pv.Blocked is BlockedReason.Miss) return "판매자가 잠적했다 — [배송] 계열 리뷰만 도달한다";
        if (pv.Blocked is BlockedReason.Void) return "이 판매자의 구성품은 전량 품절 — 리뷰할 상품이 없다";
        if (pv.Blocked is BlockedReason.NotReview) return "진상 화법 — 팩트 원칙 바깥의 언어라 배율을 받지 않는다";
        return pv.Judgement switch
        {
            Judgement.Origin => z.Kind == ZoneKind.EnemyEquipment
                ? "직접 써 본 사람의 증언 — 평가 불가 항목도 통한다"
                : "직접 산 사람의 증언 — 평가 불가 항목도 통한다",
            Judgement.Fact => $"[{tag}]가 이 상품의 실제 약점이다 — 겪지 않았어도 맞는 말이다",
            Judgement.Fumble => $"이 상품엔 [{tag}] 항목이 없다 — 헛소리로 읽힌다",
            _ => z.Kind == ZoneKind.MyEquipment
                ? "찬양 리뷰 — 좋은 평가는 그 상품의 존재를 강화한다"
                // 「일반」은 맞은 게 아니라 상관없는 리뷰를 갖다 붙인 것이다. 그 사실이 읽혀야 한다
                : (BornOf(d) is { } born ? $"「{born}」 후기다" : "전생에 쓴 후기다")
                  + " — 이 상품과는 무관하다. 원산지도 약점도 아니라 배율이 없다.",
        };
    }

    // ══ 액션 ═════════════════════════════════════════════

    private sealed record Snap(int EnemyWill, int PlayerWill, int Gold, int[] Dur);

    private Snap Take() => new(
        Mathf.Max(0, _s.St.Enemy.Will), Mathf.Max(0, _s.St.Player.Will), _s.St.Player.Gold,
        _s.St.Enemy.Equipment.Select(q => q.Durability).ToArray());

    private void PlayOnZone(int uid, DropZone z)
    {
        var d = DefOf(uid);
        if (d is null) { Unhold(); return; }
        if (!Accepts(z, d))
        {
            Toast(z.Kind == ZoneKind.Trash
                ? "퇴고에는 필력이 1 필요하다"
                : $"「{d.Name}」은(는) {TargetLabel(d.Target)} 앞으로만 낼 수 있다");
            return;
        }
        if (z.Kind == ZoneKind.Trash) { DoTrash(uid, z); return; }
        if (z.Kind == ZoneKind.EnemyEquipment) _s.SelectTarget(TargetSlot.EnemyEquipment, z.Index);
        if (z.Kind == ZoneKind.MyEquipment) _s.SelectTarget(TargetSlot.MyEquipment, z.Index);
        _s.SelectCard(uid);
        DoSubmit(uid, z);
    }

    /// <summary>제출 — 엔진을 먼저 두드리고, 그 결과로 연출을 만든다 (연출이 판정을 정하지 않는다)</summary>
    private void DoSubmit(int uid, DropZone z)
    {
        if (_busy || _s.St.Result is not null) return;
        var card = _cards.FirstOrDefault(c => c.Uid == uid);
        var target = new Rect2(z.GlobalPosition, z.Size).GetCenter();
        var pre = Take();
        bool special = DefOf(uid) is SpecialDef;

        if (!_s.Play())
        {
            // 증정 대기(X04)는 실패가 아니라 「고르는 중」이다 — 안내만 갈아 끼운다
            if (_s.PendingGiftFor is not null) { _held = null; ClearZones(); Paint(); return; }
            Toast(_s.Status);
            return;
        }

        _busy = true;
        _held = null;
        _dragging = false;
        _proxy?.QueueFree();
        _proxy = null;
        ClearZones();

        var res = special ? (SubmitResult?)null : _s.LastSubmit;

        // ① 서명이 그어진다 = 게시 (worldview §1.1-2) → ② 카드가 날아간다 → ③ 도장이 찍힌다
        if (card is not null)
        {
            card.SetSelected(false);
            // 서명은 ClassDB 밖의 속성이라 TweenMethod 로 민다 (TweenProperty 는 등록된 속성만 건드린다)
            var ink = card.Sig;
            var t = CreateTween();
            t.TweenMethod(Callable.From<float>(v => { if (IsInstanceValid(ink)) ink.Progress = v; }), 0f, 1f, 0.40f)
                .SetTrans(Tween.TransitionType.Sine);
            t.Parallel().TweenProperty(card, "position:y", card.Position.Y - 30f, 0.24f);
            t.TweenCallback(Callable.From(() => FlyAway(card, target)));
            t.TweenInterval(0.36f);
            t.TweenCallback(Callable.From(() => Land(res, target, pre)));
        }
        else
        {
            var t = CreateTween();
            t.TweenInterval(0.2f);
            t.TweenCallback(Callable.From(() => Land(res, target, pre)));
        }
    }

    /// <summary>서명이 끝난 카드가 상품 위로 날아간다 (복제본은 이미 서명이 굳은 상태다)</summary>
    private void FlyAway(CardView card, Vector2 to)
    {
        var (origin, here) = OriginLine(card.Def);
        var fly = new CardView(card.Uid, card.Def, card.Preview, origin, here);
        fly.At(card.GlobalPosition.X, card.GlobalPosition.Y, CardView.W, CardView.H);
        _dragLayer.AddChild(fly);
        fly.Sig.Signed = true;
        card.Modulate = new Color(1, 1, 1, 0.22f);

        var t = CreateTween().SetParallel(true);
        t.TweenProperty(fly, "position", to - new Vector2(CardView.W, CardView.H) * 0.15f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic);
        t.TweenProperty(fly, "scale", new Vector2(0.3f, 0.3f), 0.3f);
        t.TweenProperty(fly, "rotation", Mathf.DegToRad(9), 0.3f);
        t.TweenProperty(fly, "modulate:a", 0f, 0.3f);
        t.Chain().TweenCallback(Callable.From(fly.QueueFree));
    }

    /// <summary>착탄 — 잉크 튀김·판정 도장·숫자·흔들림·불씨. 전부 엔진 결과를 읽어 고른다</summary>
    private void Land(SubmitResult? res, Vector2 at, Snap pre)
    {
        _fx.Splat(at);
        if (res is SubmitResult r)
        {
            var (label, color) = StampOf(r);
            _fx.Stamp(label, color, at - new Vector2(0, 96));   // 좋아요 숫자(−34)와 겹치지 않게 위로
            if (!r.Missed && r.Judgement is Judgement.Origin or Judgement.Fact)
            {
                _fx.Shake(_shaker);
                _pfx.Burst(at, r.Judgement == Judgement.Origin ? 30 : 20);
            }
            if (r.Missed || r.Judgement == Judgement.Fumble) _fx.Dud(_desat);
        }
        ShowDiffs(pre);
        _busy = false;
        Paint();
        MaybeResult();
    }

    private static (string, Color) StampOf(SubmitResult r)
    {
        if (r.Missed) return ("빗나감", CombatArt.StampMiss);
        return r.Judgement switch
        {
            Judgement.Origin => ("원산지!", CombatArt.StampOrigin),
            Judgement.Fact => ("팩트!", CombatArt.StampFact),
            Judgement.Fumble => ("헛소리…", CombatArt.StampFumble),
            _ => ("일반", CombatArt.StampNormal),
        };
    }

    /// <summary>전후 차이를 숫자로 띄운다 — 무엇이 얼마나 변했는지는 엔진 상태에서 읽는다</summary>
    private void ShowDiffs(Snap pre)
    {
        var st = _s.St;
        var e = ZoneCenter(ZoneKind.Enemy);
        var me = new Vector2(140, 300);
        int ew = Mathf.Max(0, st.Enemy.Will), pw = Mathf.Max(0, st.Player.Will);

        if (pre.EnemyWill > ew) _fx.Num($"좋아요 +{pre.EnemyWill - ew}", e - new Vector2(0, 34));
        if (pre.EnemyWill < ew) _fx.Num($"의지 +{ew - pre.EnemyWill}", e - new Vector2(0, 34), heal: true);
        if (pre.PlayerWill > pw) _fx.Num($"좋아요 +{pre.PlayerWill - pw}", me);
        if (pre.PlayerWill < pw) _fx.Num($"의지 +{pw - pre.PlayerWill}", me, heal: true);
        if (st.Player.Gold != pre.Gold)
        {
            int dg = st.Player.Gold - pre.Gold;
            _fx.Num($"🪙 {(dg > 0 ? "+" : string.Empty)}{dg}", me + new Vector2(0, 34), dg > 0, small: true);
        }
        // 별점 추락 — 의지 구간이 한 칸 내려앉으면 별이 하나 떨어진다
        if (st.Enemy.MaxWill > 0
            && Mathf.FloorToInt(pre.EnemyWill / (float)st.Enemy.MaxWill * 5) > Mathf.FloorToInt(ew / (float)st.Enemy.MaxWill * 5))
        {
            _fx.StarFall(new Vector2(790, 92));
        }
        for (int i = 0; i < st.Enemy.Equipment.Count && i < pre.Dur.Length; i++)
        {
            int now = st.Enemy.Equipment[i].Durability;
            if (now < pre.Dur[i])
            {
                var z = _zones.FirstOrDefault(x => x.Kind == ZoneKind.EnemyEquipment && x.Index == i);
                if (z is not null)
                    _fx.Num($"내구도 −{pre.Dur[i] - now}", new Rect2(z.GlobalPosition, z.Size).GetCenter(),
                        heal: false, small: true);
            }
        }
    }

    private void DoTrash(int uid, DropZone z)
    {
        if (_busy || _s.St.Result is not null) return;
        var card = _cards.FirstOrDefault(c => c.Uid == uid);
        var to = new Rect2(z.GlobalPosition, z.Size).GetCenter();
        if (!_s.Revise(uid)) { Toast(_s.Status); return; }

        _busy = true;
        _held = null;
        _dragging = false;
        _proxy?.QueueFree();
        _proxy = null;
        ClearZones();
        if (card is not null) FlyAway(card, to);

        var t = CreateTween();
        t.TweenInterval(0.34f);
        t.TweenCallback(Callable.From(() =>
        {
            _fx.Splat(to);
            _fx.Num("초고 폐기", to - new Vector2(0, 18), heal: false, small: true);
            _busy = false;
            Paint();
        }));
    }

    private void DoReviseSelected()
    {
        if (_busy || _s.St.Result is not null) return;
        if (_held is not int uid) { Toast("퇴고할 카드를 먼저 고르시오 (또는 초고 폐기함에 끌어다 놓아라)"); return; }
        var z = _zones.FirstOrDefault(x => x.Kind == ZoneKind.Trash);
        if (z is not null) DoTrash(uid, z);
    }

    private void DoCrit()
    {
        if (_busy || _s.St.Result is not null) return;
        var pre = Take();
        if (!_s.Critical()) { Toast(_s.Status); return; }
        _busy = true;
        var at = ZoneCenter(ZoneKind.Enemy);
        string name = _s.LastCritical is Disposition d ? Types.CriticalName[d] : "베스트 리뷰";
        _fx.Stamp($"베스트 리뷰 · {name}", CombatArt.Gold, at - new Vector2(0, 40), 26);
        _fx.Shake(_shaker, 8f);
        _pfx.Burst(at, 38);
        var t = CreateTween();
        t.TweenInterval(0.56f);
        t.TweenCallback(Callable.From(() =>
        {
            ShowDiffs(pre);
            _busy = false;
            Paint();
            MaybeResult();
        }));
    }

    /// <summary>택배 개봉 (ADR-024 ③) — 보스에게 가던 보급품을 뜯어 내 장비로 만든다</summary>
    private void DoParcel()
    {
        if (_busy || _s.St.Result is not null) return;
        if (!_s.Parcel()) { Toast(_s.Status); return; }
        RunStore.BumpParcelsOpened();
        Toast(_s.Status);
        _pfx.Burst(new Vector2(300, 320), 24);
        _fx.Stamp("📦 개봉", CombatArt.Gold, new Vector2(300, 300), 24);
        Paint();
    }

    private void DoEndTurn()
    {
        if (_busy || _s.St.Result is not null) return;
        Unhold();
        var pre = Take();
        _busy = true;
        _s.EndTurn();

        var t = CreateTween();
        t.TweenInterval(0.43f);
        t.TweenCallback(Callable.From(() =>
        {
            if (pre.PlayerWill > Mathf.Max(0, _s.St.Player.Will))
            {
                _fx.Splat(new Vector2(140, 300));
                _fx.Shake(_shaker);
            }
            ShowDiffs(pre);
            _busy = false;
            Paint();
            MaybeResult();
        }));
    }

    private void DoRetreat()
    {
        if (_busy || _s.St.Result is not null || _resultHandled) return;
        if (!_retreatArmed)
        {
            _retreatArmed = true;
            Paint();
            var t = CreateTween();
            t.TweenInterval(2.2f);
            t.TweenCallback(Callable.From(() => { _retreatArmed = false; if (_s.St.Result is null) Paint(); }));
            return;
        }
        Unhold();
        if (!_s.Surrender()) { Toast(_s.Status); return; }
        Paint();
        MaybeResult();
    }

    // ══ 종료 ═════════════════════════════════════════════

    private void MaybeResult()
    {
        if (_s.St.Result is null || _resultHandled) return;
        _resultHandled = true;
        Unhold();

        string result = _s.St.Result.Value.ToString().ToLowerInvariant();
        RunStore.MergeBattleStats(_s.St.Stats, result, Mathf.Max(0, _s.St.Player.Will));

        var outcome = CombatEnd.Resolve(_s, _run, CombatEntry.RewardRng(_s.Context.Seed));
        ShowOutcome(outcome);
    }

    private void ShowOutcome(CombatOutcome o)
    {
        _overlay.Visible = true;
        foreach (var c in _overlay.GetChildren()) c.QueueFree();

        bool death = o.Result is BattleResult.Lose or BattleResult.Timeout;
        var scrim = new ColorRect { Color = death ? new Color(0.01f, 0.01f, 0.01f, 0.96f) : new Color(0.02f, 0.016f, 0.012f, 0.72f) };
        scrim.At(0, 0, CombatArt.ScreenW, CombatArt.ScreenH);
        _overlay.AddChild(scrim);

        bool reward = o.Result == BattleResult.Win && o.RewardPool.Count > 0;
        float boxW = reward ? 780 : 470;
        float boxH = reward ? 500 : 300;
        var box = CombatArt.Slabbed(death ? new Color(0, 0, 0, 0) : CombatArt.Parch,
            death ? new Color(0, 0, 0, 0) : new Color("8a744d"), 10, death ? 0 : 1);
        box.At((CombatArt.ScreenW - boxW) / 2f, (CombatArt.ScreenH - boxH) / 2f, boxW, boxH);
        _overlay.AddChild(box);

        var fg = death ? CombatArt.Ink : CombatArt.Inkc;
        var sub = death ? CombatArt.Dim : new Color("4a4136");

        float y = 24f;
        box.AddChild(CombatArt.Text(o.Icon, 44, death ? CombatArt.Gold : fg, HorizontalAlignment.Center)
            .At(0, y, boxW, 52));
        y += 58f;
        box.AddChild(CombatArt.Text(o.Title, 22, fg, HorizontalAlignment.Center).At(20, y, boxW - 40, 28));
        y += 32f;
        box.AddChild(CombatArt.Text(o.Body, 13, sub, HorizontalAlignment.Center, wrap: true)
            .At(40, y, boxW - 80, 44));
        y += 48f;
        if (o.GoldLine.Length > 0)
        {
            box.AddChild(CombatArt.Text(o.GoldLine, 15, death ? CombatArt.Dim : new Color("9a6b12"),
                HorizontalAlignment.Center).At(20, y, boxW - 40, 22));
            y += 28f;
        }

        if (reward)
        {
            box.AddChild(CombatArt.Text(
                "전리품 — 이번 전투 대상의 리뷰를 내 목록에 등재한다. 직접 이긴 상대의 리뷰라 원산지 보너스가 살아 있다. 1장을 고른다.",
                12, sub, HorizontalAlignment.Center, wrap: true).At(50, y, boxW - 100, 34));
            y += 40f;

            float cw = 236f, gap = 12f;
            float x = (boxW - (o.RewardPool.Count * cw + (o.RewardPool.Count - 1) * gap)) / 2f;
            foreach (var c in o.RewardPool)
            {
                var btn = RewardCard(c, o);
                btn.At(x, y, cw, 190);
                box.AddChild(btn);
                x += cw + gap;
            }
            y += 198f;
            var skip = CBtn(_s.Enemy.Tier == EnemyTier.Boss ? "등재 없이 정복 후기 작성하기" : "등재 없이 지도로 돌아가기",
                () => Leave(CombatEnd.PickReward(_run, o, null)), true, CombatArt.Ink);
            skip.At((boxW - 300) / 2f, y, 300, 32);
            box.AddChild(skip);
            return;
        }

        string label = o.NextScene is null
            ? "다시 전투"
            : o.Result == BattleResult.Win && _s.Enemy.Tier == EnemyTier.Boss ? "정복 후기 작성하기"
            : death ? "마지막 리뷰 남기기" : "지도로 돌아가기";
        var go = CBtn(label, () => Leave(o.NextScene ?? SceneRouter.Title), true, CombatArt.Ink);
        go.At((boxW - 280) / 2f, boxH - 58, 280, 36);
        box.AddChild(go);
    }

    private Control RewardCard(CardDef c, CombatOutcome o)
    {
        var b = new Button { FocusMode = FocusModeEnum.None, ClipText = false };
        b.AddThemeStyleboxOverride("normal", CombatArt.Box(CombatArt.Parch, new Color("8a744d"), 8));
        b.AddThemeStyleboxOverride("hover", CombatArt.Box(CombatArt.Parch.Lightened(0.06f), CombatArt.Gold, 8, 2));
        b.AddThemeStyleboxOverride("pressed", CombatArt.Box(CombatArt.ParchD, CombatArt.Gold, 8, 2));
        b.Pressed += () =>
        {
            RunStore.RecordSeen(new[] { c.Id });
            Leave(CombatEnd.PickReward(_run, o, c.Id));
        };

        int stars = c switch { ReviewCardDef r => r.Stars, SpecialDef s => s.Stars ?? 1, _ => 1 };
        string tag = (c as ReviewCardDef)?.Tag ?? "진상 화법";
        b.AddChild(CombatArt.Text($"{new string('★', stars)}{new string('☆', 5 - stars)}", 11,
            new Color("9a6b12")).At(10, 8, 120, 15));
        b.AddChild(CombatArt.Text($"#{tag} · ✍{c.Cost}", 11, new Color("9a6b12"), HorizontalAlignment.Right)
            .At(110, 8, 116, 15));
        b.AddChild(CombatArt.Text(c.Name, 14, new Color("1c1812"), wrap: true).At(10, 26, 216, 20));
        b.AddChild(CombatArt.Text(
            c.Target == TargetKind.MyEquipment ? "🛡 내 장비에 붙이는 찬양 — 버틸 것"
            : c.Target == TargetKind.EnemyEquipment ? "🗡 구성품을 겨냥하는 악평"
            : "🗡 판매자를 겨냥하는 악평",
            10, c.Target == TargetKind.MyEquipment ? new Color("3d6f8c") : new Color("8a6f3f"))
            .At(10, 48, 216, 14));
        var body = CombatArt.Text((c.Text ?? string.Empty).Trim(), 10, new Color("3a3229"), wrap: true);
        body.AddThemeConstantOverride("line_spacing", 2);
        body.At(10, 66, 216, 88);
        b.AddChild(body);
        b.AddChild(CombatArt.Text(CombatSession.Likeify(c.Ui), 10, new Color("5a4c34"), wrap: true)
            .At(10, 158, 216, 26));
        return b;
    }

    private void Leave(string scenePath)
    {
        RunStore.EndCombat();
        SceneRouter.Go(string.IsNullOrEmpty(scenePath) ? SceneRouter.Map : scenePath);
    }

    // ══ 잡일 ═════════════════════════════════════════════

    private void Register(DropZone z, Control parent)
    {
        parent.AddChild(z);
        z.Seal();
        _zones.Add(z);
    }

    private CardDef? DefOf(int uid)
    {
        var c = _s.St.Player.Hand.FirstOrDefault(x => x.Uid == uid);
        return c is null ? null : _s.DefOf(c);
    }

    private CardView? CardAt(Vector2 p) =>
        _cards.LastOrDefault(c => new Rect2(c.GlobalPosition, c.Size * c.Scale).HasPoint(p));

    private DropZone? ZoneAt(Vector2 p)
    {
        DropZone? best = null;
        float bestArea = float.MaxValue;
        foreach (var z in _zones)
        {
            var r = new Rect2(z.GlobalPosition, z.Size);
            if (!r.HasPoint(p)) continue;
            float area = r.Size.X * r.Size.Y;
            if (area < bestArea) { best = z; bestArea = area; }   // 겹치면 작은 쪽(더 구체적인 상품)이 이긴다
        }
        return best;
    }

    private Vector2 ZoneCenter(ZoneKind k)
    {
        var z = _zones.FirstOrDefault(x => x.Kind == k);
        return z is null ? new Vector2(844, 315) : new Rect2(z.GlobalPosition, z.Size).GetCenter();
    }

    /// <summary>대상 표기 — 만물마켓에선 무엇이든 상품이다. 「본체/부속」이 아니라 상품 이름으로 부른다</summary>
    private string TargetLabel(TargetKind t) => t switch
    {
        TargetKind.Enemy => $"「{_s.Enemy.Name}」",
        TargetKind.EnemyEquipment => $"「{_s.Enemy.Name}」의 구성품",
        _ => "내가 산 상품 (내 장비)",
    };

    /// <summary>이 카드가 태어난 상품 이름 (없으면 null — 전생 상품·해금 카드는 원산지 영구 미발동)</summary>
    private string? BornOf(CardDef d)
    {
        if (d is not ReviewCardDef r || r.Origin is null) return null;
        if (r.Origin.Equipment is { } q) return q;
        return r.Origin.Enemy is { } id && _data.Enemies.TryGetValue(id, out var e) ? e.Name : r.Origin.Enemy;
    }

    private (string, bool) OriginLine(CardDef d)
    {
        if (d is not ReviewCardDef r) return ("📍 원산지 없음 · 진상 화법은 판정을 받지 않는다", false);
        string? born = BornOf(d);
        if (born is null) return ("📍 원산지 없음 · 전생에 쓴 리뷰", false);
        bool here = r.Origin!.Enemy == _s.Enemy.Id
            || (r.Origin.Equipment is { } q && _s.Enemy.Equipment.Any(x => x.Name == q));
        return ($"📍 원산지 · {born}" + (here ? " — 여기 있다" : string.Empty), here);
    }

    private static Control Chip(string text, Color fg, Color bg, int size, Color? border = null)
    {
        var font = CombatArt.Font();
        var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var p = CombatArt.Slabbed(bg, border, 4);
        p.Size = new Vector2(ts.X + 12, ts.Y + 4);
        var l = CombatArt.Text(text, size, fg, HorizontalAlignment.Center);
        l.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        l.VerticalAlignment = VerticalAlignment.Center;
        p.AddChild(l);
        return p;
    }

    private static void PlaceChip(Control parent, string text, Color fg, Color border, ref float x, ref float y,
        float maxW, int size = 10)
    {
        var c = Chip(text, fg, new Color(0, 0, 0, 0.35f), size, border);
        if (x > 10f && x + c.Size.X > maxW) { x = 10f; y += c.Size.Y + 4f; }
        c.Position = new Vector2(x, y);
        parent.AddChild(c);
        x += c.Size.X + 5f;
    }

    private void Toast(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        _toast.Text = msg;
        var t = CreateTween();
        t.TweenProperty(_toast, "modulate:a", 1f, 0.15f);
        t.TweenInterval(1.5f);
        t.TweenProperty(_toast, "modulate:a", 0f, 0.25f);
    }

    // ══ 디버그 캡처 (--rh-shot=…) ════════════════════════
    // 화면을 눈으로 확인하기 위한 자리다. 게임 흐름에는 관여하지 않는다.

    private void ApplyShot()
    {
        switch (_shot)
        {
            case "drag":
            {
                var row = _s.Hand().FirstOrDefault(r => r.Def.Target == TargetKind.Enemy) ?? _s.Hand().FirstOrDefault();
                if (row is null) return;
                SelectCard(row.Uid);
                _pressPos = _cards.First(c => c.Uid == row.Uid).GlobalPosition + new Vector2(CardView.W / 2f, 30);
                BeginDrag();
                DragTo(ZoneCenter(ZoneKind.Enemy));
                break;
            }
            case "stamp":
            case "fumble":
            {
                // 손패 × 모든 상품을 훑어 가장 센(또는 fumble 이면 가장 나쁜) 판정을 고른다.
                // 판정은 전부 엔진 미리보기가 준다 — 여기서 규칙을 흉내 내지 않는다.
                bool worst = _shot == "fumble";
                (int Uid, DropZone Zone, int Rank)? best = null;
                foreach (var row in _s.Hand())
                {
                    foreach (var z in _zones)
                    {
                        if (z.Kind == ZoneKind.Trash || !Accepts(z, row.Def)) continue;
                        var slot = z.Kind switch
                        {
                            ZoneKind.EnemyEquipment => TargetSlot.EnemyEquipment,
                            ZoneKind.MyEquipment => TargetSlot.MyEquipment,
                            _ => TargetSlot.Enemy,
                        };
                        var pv = _s.PreviewOn(row.Uid, slot, z.Index);
                        if (!pv.Affordable || pv.Blocked is not null) continue;
                        int rank = pv.Judgement switch
                        {
                            Judgement.Origin => 3, Judgement.Fact => 2, Judgement.Normal => 1, _ => 0,
                        };
                        if (best is null || (worst ? rank < best.Value.Rank : rank > best.Value.Rank))
                            best = (row.Uid, z, rank);
                    }
                }
                if (best is not null) PlayOnZone(best.Value.Uid, best.Value.Zone);
                break;
            }
            case "parcel":
            {
                DoParcel();
                break;
            }
            case "win":
            {
                _s.St.Enemy.Will = 0;
                _s.St.Result = BattleResult.Win;
                _s.Log("(디버그) 즉시 승리");
                MaybeResult();
                break;
            }
        }
    }
}
