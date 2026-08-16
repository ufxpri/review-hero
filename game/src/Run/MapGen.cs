// 1막 지도 생성 — ui/game/state.js 의 genMap() 이관 (ADR-029 2차).
//
// **웹판과 같은 시드에서 같은 지도가 나와야 한다.** 그래야 이관이 규칙을 흘리지 않았음을
// 지도 한 장으로 대조할 수 있다. 그래서 여기서는 세 가지를 그대로 지킨다:
//   ① 난수는 엔진 RngFactory.Mulberry32 — TS rng.ts 와 비트 단위로 같은 수열을 낸다.
//   ② r() 을 부르는 **횟수와 순서**가 JS 와 완전히 같다. 조건이 거짓이어도 JS 가 왼쪽 피연산자로
//      r() 을 이미 소비했다면(`r() < 0.5 && nxt.length > 1`) 여기서도 먼저 소비한다.
//   ③ JS Math.round 는 0.5 를 위로 올린다. C# Math.Round 는 기본이 짝수 반올림(0.5 → 0)이라
//      그대로 쓰면 층 3개 → 2개 구간에서 간선이 어긋난다. JsRound 로 floor(x + 0.5) 를 쓴다.
//
// 규칙(GDD §4.1): 6층(일반 5 + 보스 1), 층당 2~3 노드, 보스 직전 5층은 휴식 1개 보장,
// 1~2층 전투는 E01 고정(E03/E04 는 tier:elite 라 초반 일반 전투에 과중).

using ReviewHero.Engine;

namespace ReviewHero.Game.Run;

public static class MapGen
{
    private static readonly string[] PoolNormal = { "E01", "E01", "E03", "E04" };
    private static readonly string[] PoolElite = { "E02", "E05" };

    /// <summary>JS Math.round (0.5 는 위로) — C# 의 짝수 반올림과 다르다</summary>
    private static int JsRound(double x) => (int)Math.Floor(x + 0.5);

    public static MapData Generate(uint seed)
    {
        var r = RngFactory.Mulberry32(seed);
        string Pick(IReadOnlyList<string> arr) => arr[(int)Math.Floor(r() * arr.Count)];

        var floors = new List<List<MapNode>>();
        for (int f = 1; f <= 6; f++)
        {
            if (f == 6)
            {
                floors.Add(new List<MapNode> { new() { Id = "f6n0", Type = NodeType.Boss, Enemy = "B01" } });
                continue;
            }

            int n = 2 + (r() < 0.5 ? 1 : 0);
            var row = new List<MapNode>();
            for (int i = 0; i < n; i++)
            {
                var type = NodeType.Battle;
                if (f > 1)
                {
                    double roll = r();
                    if (roll < 0.40) type = NodeType.Battle;
                    else if (roll < 0.55) type = f >= 3 ? NodeType.Elite : NodeType.Battle;
                    else if (roll < 0.75) type = NodeType.Event;
                    else if (roll < 0.90) type = NodeType.Shop;
                    else type = NodeType.Rest;
                }

                var node = new MapNode { Id = $"f{f}n{i}", Type = type };
                if (type == NodeType.Battle) node.Enemy = f <= 2 ? "E01" : Pick(PoolNormal);
                if (type == NodeType.Elite) node.Enemy = Pick(PoolElite);
                row.Add(node);
            }

            if (f == 5 && row.All(x => x.Type != NodeType.Rest))
            {
                int i = (int)Math.Floor(r() * row.Count);
                row[i] = new MapNode { Id = row[i].Id, Type = NodeType.Rest };   // 적 정보는 버린다(JS 와 동일)
            }
            floors.Add(row);
        }

        // 간선 — 각 노드는 다음 층에서 1~2곳으로 이어지고, 다음 층의 모든 노드는 최소 1개의 진입로를 갖는다.
        for (int f = 0; f < floors.Count - 1; f++)
        {
            var cur = floors[f];
            var nxt = floors[f + 1];
            for (int i = 0; i < cur.Count; i++)
            {
                // 위치 비율을 맞춰 이어 붙인다 — 왼쪽 노드는 왼쪽으로, 오른쪽은 오른쪽으로
                int j = Math.Min(nxt.Count - 1, JsRound((double)i / Math.Max(1, cur.Count - 1) * (nxt.Count - 1)));
                var next = new List<string> { nxt[j].Id };
                bool branch = r() < 0.5;                    // JS 는 && 왼쪽이라 항상 소비된다
                if (branch && nxt.Count > 1)
                {
                    string id = nxt[Math.Min(nxt.Count - 1, j + 1)].Id;
                    if (!next.Contains(id)) next.Add(id);   // JS Set 의 중복 제거 + 삽입 순서
                }
                cur[i].Next = next;
            }

            // 고아 노드 방지 — 아무도 안 가리키는 곳은 가장 가까운 이전 노드가 떠맡는다
            for (int j = 0; j < nxt.Count; j++)
            {
                var t = nxt[j];
                if (cur.Any(nn => nn.Next.Contains(t.Id))) continue;
                int i = Math.Min(cur.Count - 1, JsRound((double)j / Math.Max(1, nxt.Count - 1) * (cur.Count - 1)));
                cur[i].Next.Add(t.Id);
            }
        }

        return new MapData { Floors = floors };
    }

    /// <summary>웹판(state.js)과 대조하기 좋은 한 줄 표기 — 자동 플레이 로그와 시드 대조에 쓴다</summary>
    public static string Dump(MapData map)
    {
        var sb = new System.Text.StringBuilder();
        for (int f = 0; f < map.Floors.Count; f++)
        {
            sb.Append($"F{f + 1}: ");
            sb.AppendJoin(" | ", map.Floors[f].Select(n =>
                $"{n.Id} {n.Type.Key()}{(n.Enemy is null ? "" : "/" + n.Enemy)}->[{string.Join(",", n.Next)}]"));
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
