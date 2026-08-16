// 씬 전환 한 곳 — 씬 경로 문자열을 코드 곳곳에 흩뿌리지 않는다.
//
// 웹판은 location.href 에 페이지 이름을 직접 박았고(state.js NODE_PAGE), 파일명이 바뀌면
// 여섯 군데를 같이 고쳐야 했다. 여기서는 경로가 상수 하나씩이고 노드→씬 대응도 여기 산다.
//
// 2차 시점의 현실: 이벤트·상점·휴식 씬은 범위 밖이고 전투 씬은 다른 작업자 소관이라
// 아직 없을 수 있다. **없는 씬으로 보내 흐름을 끊는 대신 지도로 되돌린다** — 지도가
// 「(미구현) 통과」 화면을 띄워 CompleteNode 로 넘겨 준다. 1막 완주가 끊기지 않는 것이 우선이다.

using Godot;

namespace ReviewHero.Game.Run;

public static class SceneRouter
{
    public const string Title = "res://scenes/Title.tscn";
    public const string Map = "res://scenes/Map.tscn";
    public const string Result = "res://scenes/Result.tscn";

    /// <summary>전투 씬 — 다른 작업자 소관이라 없을 수 있다 (Exists 로 확인하고 쓴다)</summary>
    public const string Combat = "res://scenes/Combat.tscn";

    /// <summary>현재 씬 트리. 씬 밖(자동 플레이 하네스)에서는 null 일 수 있다</summary>
    public static SceneTree? Tree { get; set; }

    public static bool Exists(string path) => ResourceLoader.Exists(path);

    public static void Go(string scenePath)
    {
        // `Engine` 은 우리 네임스페이스(ReviewHero.Engine)와 이름이 겹친다 — Godot 쪽을 명시한다
        var tree = Tree ?? (Godot.Engine.GetMainLoop() as SceneTree);
        if (tree is null)
        {
            GD.PushWarning($"[SceneRouter] 씬 트리가 없다 — 이동 생략: {scenePath}");
            return;
        }
        // 씬 전환은 프레임 끝에서 일어난다. _Ready 안에서 불러도 안전하다.
        var err = tree.ChangeSceneToFile(scenePath);
        if (err != Error.Ok) GD.PushError($"[SceneRouter] 씬 전환 실패({err}): {scenePath}");
    }

    public static void GoTitle() => Go(Title);

    public static void GoMap() => Go(Map);

    public static void GoResult() => Go(Result);

    /// <summary>노드 종류에 맞는 씬으로. 아직 없는 씬이면 지도로 돌려보내 통과 화면을 띄운다</summary>
    public static void GoToNode(MapNode node)
    {
        if (node.Type.IsCombat() && Exists(Combat)) { Go(Combat); return; }
        Go(Map);
    }

    /// <summary>이 노드를 전용 씬으로 처리할 수 있는가 (지도가 통과 화면을 띄울지 판단한다)</summary>
    public static bool HasSceneFor(MapNode node) => node.Type.IsCombat() && Exists(Combat);
}
