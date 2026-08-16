// 등록된 서명 한 벌 — 저장 형식 (ADR-020·022).
//
// 웹판 localStorage['reviewhero.sig'] 와 **같은 개념의 형식**을 그대로 쓴다:
//
//   { "v": 1, "box": [660, 236], "strokes": [ [[x,y], [x,y], …], … ] }
//
// 좌표는 등록 패드의 660×236(A4 가로비) 기준으로 저장하고, 그리는 쪽에서 비율만 맞춘다
// (전투 카드 서명란은 140×38 — SignatureInk.Normalize 가 담당). 좌표계를 저장해 두는 이유는
// 나중에 패드 크기가 바뀌어도 옛 서명을 같은 비율로 되살릴 수 있게 하기 위해서다.
//
// 저장 위치는 `user://save.json` 의 `meta.signature` 다 — 런이 아니라 **계정 속성**이라
// 원정을 새로 시작해도 남는다. 필드가 없는 옛 세이브는 null 로 읽히고, 그러면 카드에는
// 기본 필체가 그어진다(SignatureInk.DefaultStrokes).

using System.Text.Json.Serialization;
using Godot;

namespace ReviewHero.Game.Signature;

/// <summary>등록된 서명 획 묶음. 점 좌표계는 <see cref="BoxW"/>×<see cref="BoxH"/></summary>
public sealed class SignatureData
{
    /// <summary>등록 패드의 좌표계 — 웹판 signature.html 의 viewBox 그대로</summary>
    public const float BoxW = 660f;

    public const float BoxH = 236f;

    /// <summary>형식 버전. 점 배열의 의미가 바뀌면 올린다</summary>
    public int V { get; set; } = 1;

    /// <summary>이 획들이 그려진 좌표계 [폭, 높이]</summary>
    public float[] Box { get; set; } = { BoxW, BoxH };

    /// <summary>획 목록. 획 하나 = 점 배열, 점 하나 = [x, y]</summary>
    public List<float[][]> Strokes { get; set; } = new();

    /// <summary>그을 것이 있는가 (점 2개 미만인 획은 선이 되지 않는다)</summary>
    [JsonIgnore]
    public bool HasStrokes => Strokes.Any(s => s is { Length: >= 2 });

    /// <summary>패드가 모은 점을 저장 형식으로. 소수점 1자리로 줄여 세이브가 붇지 않게 한다</summary>
    public static SignatureData FromVectors(IEnumerable<IReadOnlyList<Vector2>> strokes)
    {
        var data = new SignatureData();
        foreach (var s in strokes)
        {
            if (s.Count < 2) continue;   // 점 하나짜리 「톡」은 획이 아니다
            var pts = new float[s.Count][];
            for (int i = 0; i < s.Count; i++)
                pts[i] = new[] { Round1(s[i].X), Round1(s[i].Y) };
            data.Strokes.Add(pts);
        }
        return data;
    }

    /// <summary>그리는 쪽이 쓰는 형태로. 좌표계는 저장된 그대로 — 크기 맞추기는 그리는 쪽 몫이다</summary>
    public List<Vector2[]> ToVectors()
    {
        var outp = new List<Vector2[]>();
        foreach (var s in Strokes)
        {
            if (s is not { Length: >= 2 }) continue;
            var a = new Vector2[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                var p = s[i];
                a[i] = new Vector2(p.Length > 0 ? p[0] : 0f, p.Length > 1 ? p[1] : 0f);
            }
            outp.Add(a);
        }
        return outp;
    }

    private static float Round1(float v) => Mathf.Round(v * 10f) / 10f;
}
