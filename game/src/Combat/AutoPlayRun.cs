using ReviewHero.Data;
using ReviewHero.Engine;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Combat;

/// <summary>
/// 헤드리스 1막 완주 하네스 — ADR-029 2차의 **완료 판정**이다.
///
///   Godot --headless --path game -- --autoplay
///
/// 씬을 띄우지 않고 <see cref="RunStore"/> 와 전투 세션을 직접 굴려
/// 「타이틀 → 지도 → 전투 → … → 보스 → 결과 → 정산」이 끊기지 않는지 본다.
/// 화면이 아니라 **흐름**을 검증하는 것이라 미술·연출과 무관하게 성립한다.
/// </summary>
public static partial class AutoPlay
{
    public static bool Requested()
    {
        foreach (var a in Godot.OS.GetCmdlineUserArgs())
            if (a == "--autoplay") return true;
        return false;
    }

    /// <summary>완주를 돌리고 즉시 종료한다. 실패하면 종료 코드 1.</summary>
    public static void RunAndQuit(Godot.SceneTree tree)
    {
        int code;
        try { code = RunOnce(seed: 42) ? 0 : 1; }
        catch (Exception e)
        {
            Godot.GD.PrintErr($"[autoplay] 예외: {e}");
            code = 1;
        }
        tree.Quit(code);
    }

    /// <summary>한 판을 끝까지 굴린다. 성공 = 결과 정산까지 도달.</summary>
    public static bool RunOnce(uint seed)
    {
        var data = Loader.LoadAll();
        var run = RunStore.NewRun(seed);
        Log($"새 원정 시드 {seed} · 덱 {run.Deck.Count}장 · 의지 {run.Will}/{run.MaxWill}");

        for (int guard = 0; guard < 40; guard++)
        {
            if (RunStore.Current is not { } cur) break;
            if (cur.Ended is not null) break;

            var open = RunStore.Reachable();
            if (open.Count == 0) { Err("갈 수 있는 노드가 없다"); return false; }

            var nodeId = open[0];
            var node = FindNode(cur, nodeId);
            if (node is null) { Err($"노드를 찾을 수 없다: {nodeId}"); return false; }

            // 지도에서 노드를 고른다. navigate:false — 헤드리스라 씬 전환을 하지 않는다
            RunStore.EnterNode(nodeId, navigate: false);

            if (node.Type.IsCombat())
            {
                if (!PlayCombat(data, cur, node)) return false;
            }
            else
            {
                // 이벤트·상점·휴식은 2차 범위 밖 — 통과 처리 (지도가 「(미구현) 통과」로 노출하는 것과 같다)
                Log($"{cur.Floor}층 {node.Type.Label()} — 통과");
                RunStore.CompleteNode();
            }
        }

        var end = RunStore.Current?.Ended;
        if (end is null) { Err("런이 끝나지 않았다 (40노드 상한 도달)"); return false; }

        Log($"런 종료: {end}");
        var meta = RunStore.FinalizeRun(end, stars: 3, text: "자동 완주 검증");
        if (RunStore.Current is not null) { Err("정산 후에도 런이 남아 있다"); return false; }

        var last = meta.Expedition.Count > 0 ? meta.Expedition[0] : null;
        Log($"정산: 원정 {meta.Runs} · 클리어 {meta.Wins} · 최고 {meta.BestFloor}층 · "
          + $"등재 {meta.Seen.Count}장 · 명단 「{last?.Review}」({last?.Status})");
        return true;
    }

    private static bool PlayCombat(LoadedData data, RunState cur, MapNode node)
    {
        var bridge = RunBridge.TryAttach(out var why);
        if (bridge is null) { Err($"런 연결 실패: {why}"); return false; }

        RunStore.BeginCombat(node.Id);
        var ctx = CombatEntry.Build(data, bridge, Array.Empty<string>(), out var note);
        var session = new CombatSession(data, ctx);
        Log($"{cur.Floor}층 {node.Type.Label()} — {session.St.Enemy.Def.Name} 의지 {session.St.Enemy.Will} ({note})");

        var turns = RunToEnd(session, turnCap: 40);
        var result = session.St.Result;
        if (result is null) { Err($"전투가 {turns}턴에 끝나지 않았다"); return false; }

        var outcome = CombatEnd.Resolve(session, bridge, CombatEntry.RewardRng(cur.Seed));
        Log($"  → {result} ({turns}턴) · 의지 {session.St.Player.Will} · {outcome.GoldLine}");

        // NextScene 이 이미 정해졌으면(패배·이탈) 그대로 따른다 — CompleteNode 를 부르면 안 된다.
        // 보상 대기(승리 + 보상 풀)일 때만 카드를 고르고 그때 CompleteNode 가 실행된다.
        var next = outcome.NextScene
            ?? CombatEnd.PickReward(bridge, outcome, outcome.RewardPool.Count > 0 ? outcome.RewardPool[0].Id : null);
        RunStore.EndCombat();
        if (next.Length == 0) { Err("다음 씬 경로가 비었다"); return false; }
        return true;
    }

    private static MapNode? FindNode(RunState run, string id)
    {
        foreach (var row in run.Map.Floors)
            foreach (var n in row)
                if (n.Id == id) return n;
        return null;
    }

    private static void Log(string m) => Godot.GD.Print($"[autoplay] {m}");
    private static void Err(string m) => Godot.GD.PrintErr($"[autoplay] ✗ {m}");
}
