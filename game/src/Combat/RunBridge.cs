// 런 ↔ 전투 연결부 (ADR-029 2차).
//
// ⚠️ **왜 리플렉션인가**
// 2차는 런(RunStore·SceneRouter)과 전투를 다른 작업자가 동시에 만든다. 전투 쪽이 `RunStore` 를
// 직접 참조하면 저쪽이 들어오기 전까지 `dotnet build` 가 깨지고, 그러면 전투를 헤드리스로
// 완주 검증할 수 없다 — 2차의 완료 기준이 그것이다. 그래서 **연결은 이 파일 하나에 가두고
// 이름으로 늦게 붙인다.** 붙지 않으면 조용히 실패하지 않고 무엇이 없었는지 진단 문자열에 남긴다.
//
// 이 파일은 **일회용 어댑터다.** `game/src/Run/` 이 확정되면 여기의 리플렉션을
// 그대로 정적 호출로 바꾸면 되고, 그 외 전투 코드는 손댈 자리가 없다.
//
// 전제한 계약 (2차 지시서):
//   RunStore.Current → RunState?  { Seed, Floor, Pos, Gold, Will, MaxWill, Deck, SuitCounters,
//                                   LastSuit, BattlesWon, Parcel, Combat, Ended }
//   RunStore.BeginCombat(nodeId) / .EndCombat() / .Save() / .MarkEnded(reason)
//   RunStore.CompleteNode(gold: n, deckAdd: id) → string   (다음 씬 경로)
//   SceneRouter.Go(string scenePath)

using System.Collections;
using System.Reflection;
using ReviewHero.Engine;

namespace ReviewHero.Game.Combat;

public sealed class RunBridge
{
    private const string RunStoreTypeName = "ReviewHero.Game.Run.RunStore";
    private const string SceneRouterTypeName = "ReviewHero.Game.Run.SceneRouter";

    private readonly Type _store;
    private readonly object _run;
    private readonly List<string> _notes = new();

    private RunBridge(Type store, object run)
    {
        _store = store;
        _run = run;
    }

    /// <summary>무엇이 붙고 무엇이 없었는지 — 화면·로그에 그대로 띄운다</summary>
    public string Diagnostics => _notes.Count == 0 ? "런 연결 정상" : string.Join(" / ", _notes);

    /// <summary>런에 붙는다. RunStore 가 없거나 진행 중 런이 없으면 null (= 디버그 단독 전투)</summary>
    public static RunBridge? TryAttach(out string why)
    {
        var store = FindType(RunStoreTypeName);
        if (store is null)
        {
            why = $"{RunStoreTypeName} 없음 — 디버그 단독 전투";
            return null;
        }
        object? run = GetStatic(store, "Current");
        if (run is null)
        {
            why = "RunStore.Current 가 null — 디버그 단독 전투";
            return null;
        }
        why = "런 연결됨";
        return new RunBridge(store, run);
    }

    // ── 런 상태 읽기 ───────────────────────────────────

    public uint Seed => ToUInt(Read("Seed"));
    public int Floor => ToInt(Read("Floor"), 1);
    public int Gold => ToInt(Read("Gold"), 0);
    public int Will => ToInt(Read("Will"), 30);
    public int MaxWill => ToInt(Read("MaxWill"), 30);
    public int BattlesWon => ToInt(Read("BattlesWon"), 0);

    /// <summary>현재 노드 id (RunState.Pos)</summary>
    public string? NodeId => Read("Pos") as string;

    public IReadOnlyList<string>? Deck
    {
        get
        {
            if (Read("Deck") is not IEnumerable e) return null;
            var list = new List<string>();
            foreach (var o in e) if (o is string s) list.Add(s);
            return list.Count == 0 ? null : list;
        }
    }

    public IReadOnlyDictionary<Suit, int>? SuitCounters
    {
        get
        {
            if (Read("SuitCounters") is not IDictionary d) return null;
            var map = new Dictionary<Suit, int>();
            foreach (DictionaryEntry kv in d)
            {
                Suit? suit = kv.Key switch
                {
                    Suit s => s,
                    string str when Enum.TryParse<Suit>(str, out var p) => p,
                    _ => null,
                };
                if (suit is Suit k && kv.Value is not null) map[k] = ToInt(kv.Value, 0);
            }
            return map.Count == 0 ? null : map;
        }
    }

    public Suit? LastSuit => Read("LastSuit") switch
    {
        Suit s => s,
        string str when Enum.TryParse<Suit>(str, out var p) => p,
        _ => null,
    };

    /// <summary>
    /// 이번 노드의 적 id. 지시서의 RunState 필드 목록에는 노드 접근자가 없어 여러 이름을 시도한다 —
    /// 전부 실패하면 null 을 돌려주고 호출자가 디버그 기본값(E01)으로 떨어진다.
    /// </summary>
    public string? EnemyId()
    {
        foreach (var candidate in new[] { "CurrentNode", "Node", "CurrentEnemy", "EnemyId" })
        {
            object? v = CallOrGet(_store, candidate) ?? CallOrGet(_run, candidate);
            if (v is null) continue;
            if (v is string s) return s;
            if (v.GetType().GetProperty("Enemy") is { } p && p.GetValue(v) is string id) return id;
        }
        _notes.Add("적 id 를 런에서 못 읽었다 (RunStore.CurrentNode()?.Enemy 부재)");
        return null;
    }

    // ── 런 상태 쓰기 ───────────────────────────────────

    public void WriteBack(BattleState st, bool alive)
    {
        Write("Will", alive ? Math.Max(1, st.Player.Will) : 0);
        WriteSuitCounters(st.Player.SuitCounters);
        Write("LastSuit", st.Player.LastSuit);
    }

    public void BumpBattlesWon() => Write("BattlesWon", BattlesWon + 1);

    private void WriteSuitCounters(IReadOnlyDictionary<Suit, int> counters)
    {
        if (Read("SuitCounters") is IDictionary d)
        {
            foreach (var (suit, n) in counters)
            {
                object key = d.Keys.Cast<object>().FirstOrDefault(k => k is string) is not null
                    ? suit.ToString()
                    : suit;
                try { d[key] = n; }
                catch (Exception ex) { _notes.Add($"SuitCounters 되써넣기 실패: {ex.GetType().Name}"); return; }
            }
            return;
        }
        Write("SuitCounters", new Dictionary<Suit, int>(counters.ToDictionary(kv => kv.Key, kv => kv.Value)));
    }

    // ── 런 호출 ────────────────────────────────────────

    public void BeginCombat(string? nodeId) => Invoke("BeginCombat", nodeId ?? string.Empty);

    public void EndCombat() => Invoke("EndCombat");

    public void Save() => Invoke("Save");

    public void MarkEnded(string reason) => Invoke("MarkEnded", reason);

    /// <summary>노드 종료 → 다음 씬 경로. 실패하면 null (호출자가 지도로 폴백)</summary>
    public string? CompleteNode(int gold, string? deckAdd)
    {
        var m = _store.GetMethod("CompleteNode", BindingFlags.Public | BindingFlags.Static);
        if (m is null)
        {
            _notes.Add("RunStore.CompleteNode 없음");
            return null;
        }
        var ps = m.GetParameters();
        var args = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            string n = ps[i].Name ?? string.Empty;
            args[i] = n.Equals("gold", StringComparison.OrdinalIgnoreCase) ? gold
                : n.Equals("deckAdd", StringComparison.OrdinalIgnoreCase) ? deckAdd
                : ps[i].HasDefaultValue ? ps[i].DefaultValue
                : null;
        }
        try
        {
            return m.Invoke(null, args) as string;
        }
        catch (Exception ex)
        {
            _notes.Add($"CompleteNode 호출 실패: {Unwrap(ex)}");
            return null;
        }
    }

    /// <summary>SceneRouter.Go — 없으면 false (호출자가 Godot ChangeSceneToFile 로 폴백)</summary>
    public static bool TryGo(string scenePath)
    {
        var router = FindType(SceneRouterTypeName);
        var m = router?.GetMethod("Go", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
        if (m is null) return false;
        try
        {
            m.Invoke(null, new object?[] { scenePath });
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── 리플렉션 잡일 ──────────────────────────────────

    private object? Read(string prop) => _run.GetType()
        .GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(_run)
        ?? _run.GetType()
            .GetField(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(_run);

    private void Write(string name, object? value)
    {
        var p = _run.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p is not null && p.CanWrite)
        {
            try { p.SetValue(_run, Coerce(value, p.PropertyType)); return; }
            catch (Exception ex) { _notes.Add($"{name} 쓰기 실패: {Unwrap(ex)}"); return; }
        }
        var f = _run.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f is not null)
        {
            try { f.SetValue(_run, Coerce(value, f.FieldType)); return; }
            catch (Exception ex) { _notes.Add($"{name} 쓰기 실패: {Unwrap(ex)}"); return; }
        }
        _notes.Add($"RunState.{name} 없음 (쓰기 무시)");
    }

    private void Invoke(string method, params object?[] args)
    {
        var m = _store.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
        if (m is null)
        {
            _notes.Add($"RunStore.{method} 없음");
            return;
        }
        try { m.Invoke(null, args); }
        catch (Exception ex) { _notes.Add($"{method} 호출 실패: {Unwrap(ex)}"); }
    }

    private static object? Coerce(object? v, Type t)
    {
        if (v is null) return null;
        var target = Nullable.GetUnderlyingType(t) ?? t;
        if (target.IsInstanceOfType(v)) return v;
        if (target == typeof(string)) return v.ToString();
        if (target.IsEnum && v is string s) return Enum.Parse(target, s);
        if (v is Suit suit && target == typeof(string)) return suit.ToString();
        return Convert.ChangeType(v, target);
    }

    private static object? CallOrGet(object target, string name)
    {
        var type = target as Type ?? target.GetType();
        object? instance = target as Type is null ? target : null;
        var flags = BindingFlags.Public | (instance is null ? BindingFlags.Static : BindingFlags.Instance);
        try
        {
            if (type.GetMethod(name, flags, Type.EmptyTypes) is { } m) return m.Invoke(instance, null);
            if (type.GetProperty(name, flags) is { } p) return p.GetValue(instance);
        }
        catch
        {
            // 런 쪽 구현이 던지면 그냥 못 읽은 것으로 친다
        }
        return null;
    }

    private static object? GetStatic(Type t, string name) =>
        t.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
        ?? t.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, throwOnError: false);
            if (t is not null) return t;
        }
        return null;
    }

    private static int ToInt(object? v, int fallback) =>
        v is null ? fallback : Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);

    private static uint ToUInt(object? v) =>
        v is null ? 0u : unchecked((uint)Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture));

    private static string Unwrap(Exception ex) => (ex.InnerException ?? ex).Message;
}
