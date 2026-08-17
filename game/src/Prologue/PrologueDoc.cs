// 프롤로그 본문 — design/prologue-v1.md 를 그대로 읽는다.
//
// ── 왜 문서를 읽는가 ────────────────────────────────────
// 슬라이드 텍스트의 정본은 `design/prologue-v1.md` **하나뿐**이다(문서 §2). 웹판도 사본을
// 만들지 않고 tools/ui/build_prologue.py 가 같은 파일을 파싱해 화면을 만든다. 여기서 C# 배열로
// 굳혀 두면 문장을 고칠 자리가 두 곳이 되고, 그러면 반드시 어긋난다 — 카드·적 YAML 을
// design/ 링크로 두는 것과 같은 이유다(GameData 머리말).
//
//   1순위 res://data/prologue-v1.md  — design/ 정본으로 건 심볼릭 링크.
//                                      export_presets.cfg 의 include_filter="*.yaml,*.md" 가
//                                      이 파일을 .pck 에 넣는다. 내보낸 빌드가 타는 유일한 경로다.
//   2순위 design/prologue-v1.md      — 링크가 빠졌거나 .pck 에 안 들어간 개발 환경 폴백.
// 어느 쪽을 탔는지 한 줄 찍는다 — 내보낸 빌드가 조용히 빈 프롤로그로 뜨는 사고를 막는다.
//
// ── 문서 형식 (build_prologue.py 와 같은 규칙) ──────────
//   #### P07b — 폭발 ⚡     슬라이드 머리. 제목 끝의 ⚡ 는 충격 연출(섬광·흔들림) 플래그다
//   > 본문 줄               인용문이 본문이고, **빈 `>` 줄이 비트 구분**이다
//   > (빈 줄)               한 슬라이드 안에서 클릭할 때마다 비트가 넘어가고 이미지는 유지된다
//   ```                     인용문이 끝나면(이미지 프롬프트 등) 그 슬라이드 수집도 끝난다
//   ## 다음 절              절이 바뀌면 수집을 멈춘다 (§3 구성안의 불릿을 본문으로 먹지 않게)
//
// 인용문 안에서 `**이름**` 으로 시작하는 줄은 대사다. 화자를 금색으로 떼어내고, 이어지는
// 줄은 화자 이름 폭만큼 들여쓴다(웹판 .say / .say.cont 와 같은 규칙).

using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace ReviewHero.Game.Prologue;

/// <summary>본문 한 줄. 화면은 이 세 가지 모양만 그린다 — 지문 · 대사 첫 줄 · 대사 이어지는 줄</summary>
/// <param name="Speaker">대사 첫 줄의 화자(지문이면 null)</param>
/// <param name="Text">화자를 뗀 본문. BBCode 로 변환돼 있다(`**강조**` → [b], `[` → [lb])</param>
/// <param name="Owner">이 줄이 속한 대사의 화자(들여쓰기 폭 계산용). 지문이면 null</param>
public sealed record PrologueLine(string? Speaker, string Text, string? Owner)
{
    /// <summary>대사 블록의 줄인가</summary>
    public bool Say => Owner is not null;

    /// <summary>화자를 다시 적지 않고 이어 쓰는 줄인가</summary>
    public bool Continues => Owner is not null && Speaker is null;
}

/// <summary>클릭 한 번에 넘어가는 단위. 한 슬라이드는 1~7비트다</summary>
public sealed record PrologueBeat(IReadOnlyList<PrologueLine> Lines);

public sealed record PrologueSlide(string Key, string Title, bool Impact, IReadOnlyList<PrologueBeat> Beats)
{
    /// <summary>슬라이드 키와 파일명이 1:1 이다 (P07b → res://assets/pro-p07b.png)</summary>
    public string ImagePath => $"res://assets/pro-{Key.ToLowerInvariant()}.png";
}

public static class PrologueDoc
{
    public const string FileName = "prologue-v1.md";
    private const string ResPath = "res://data/" + FileName;

    private static IReadOnlyList<PrologueSlide>? _slides;

    /// <summary>진단용 — 마지막으로 탄 경로</summary>
    public static string Source { get; private set; } = "(미로드)";

    public static IReadOnlyList<PrologueSlide> Slides => _slides ??= Load();

    /// <summary>슬라이드 전체 비트 수 (정본 §2 가 명시한 비트 수와 대조하는 값 — 정본이 바뀌면 그쪽을 따른다)</summary>
    public static int BeatCount(IReadOnlyList<PrologueSlide> slides)
    {
        int n = 0;
        foreach (var s in slides) n += s.Beats.Count;
        return n;
    }

    private static IReadOnlyList<PrologueSlide> Load()
    {
        string? text = ReadRes() ?? ReadDesign();
        if (text is null)
        {
            GD.PushError($"[PrologueDoc] {FileName} 을 어디서도 못 읽었다 — 프롤로그가 빈 채로 뜬다");
            Source = "(없음)";
            return System.Array.Empty<PrologueSlide>();
        }
        var slides = Parse(text);
        GD.Print($"[PrologueDoc] {Source} 에서 로드 — 슬라이드 {slides.Count}장 · 비트 {BeatCount(slides)}개");
        return slides;
    }

    private static string? ReadRes()
    {
        try
        {
            // GetFileAsString 은 파일이 없으면 예외가 아니라 빈 문자열을 준다 — 직접 걸러낸다
            string s = Godot.FileAccess.GetFileAsString(ResPath);
            if (s.Length == 0)
            {
                GD.Print($"[PrologueDoc] {ResPath} 없음(err={Godot.FileAccess.GetOpenError()}) — design/ 폴백");
                return null;
            }
            Source = ResPath;
            return s;
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[PrologueDoc] res:// 로드 실패: {e.Message} — design/ 폴백 시도");
            return null;
        }
    }

    private static string? ReadDesign()
    {
        try
        {
            // 저장소 안에서 도는 개발 환경 전용. 내보낸 빌드에는 design/ 이 없다
            string path = System.IO.Path.Combine(ReviewHero.Data.Loader.DesignDir, FileName);
            if (!System.IO.File.Exists(path)) return null;
            Source = path;
            return System.IO.File.ReadAllText(path);
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[PrologueDoc] design/ 폴백도 실패: {e.Message}");
            return null;
        }
    }

    // ── 파싱 ─────────────────────────────────────────

    private static readonly Regex Head =
        new(@"^#{3,4}\s+(P\d+[a-z]?)\s*—\s*(.+?)\s*$", RegexOptions.Compiled);

    public static IReadOnlyList<PrologueSlide> Parse(string doc)
    {
        var slides = new List<(string Key, string Title, bool Impact, List<List<string>> Beats)>();
        (string Key, string Title, bool Impact, List<List<string>> Beats)? cur = null;
        var beat = new List<string>();

        void Flush()
        {
            if (cur is not null && beat.Count > 0) cur.Value.Beats.Add(new List<string>(beat));
            beat.Clear();
        }

        foreach (string raw in doc.Replace("\r\n", "\n").Split('\n'))
        {
            var m = Head.Match(raw);
            if (m.Success)
            {
                Flush();
                string title = m.Groups[2].Value;
                bool impact = title.EndsWith("⚡", System.StringComparison.Ordinal);
                cur = (m.Groups[1].Value, title.TrimEnd(' ', '⚡', '⌨'), impact, new List<List<string>>());
                slides.Add(cur.Value);
                continue;
            }
            if (raw.StartsWith("## ", System.StringComparison.Ordinal))
            {
                Flush();
                cur = null;
                continue;
            }
            if (cur is null) continue;

            if (raw.StartsWith(">", System.StringComparison.Ordinal))
            {
                string txt = raw.TrimStart('>').Trim();
                if (txt.Length > 0) beat.Add(txt);
                else Flush();                       // 빈 `>` 줄 = 비트 구분
            }
            else if (raw.Trim().Length == 0)
            {
                continue;                           // 인용문 사이의 빈 줄은 무시
            }
            else
            {
                Flush();                            // 인용문이 끝났다 (이미지 프롬프트 등)
            }
        }
        Flush();

        var outp = new List<PrologueSlide>();
        foreach (var (key, title, impact, beats) in slides)
        {
            if (beats.Count == 0) continue;          // 본문 없는 머리는 슬라이드가 아니다
            var bs = new List<PrologueBeat>();
            foreach (var lines in beats) bs.Add(new PrologueBeat(BuildLines(lines)));
            outp.Add(new PrologueSlide(key, title, impact, bs));
        }
        return outp;
    }

    /// <summary>한 비트의 원문 줄들을 화면이 그릴 모양(지문·대사·이어지는 대사)으로 옮긴다</summary>
    private static IReadOnlyList<PrologueLine> BuildLines(IReadOnlyList<string> raw)
    {
        var outp = new List<PrologueLine>();
        string? owner = null;                        // 지금 이어지고 있는 대사의 화자
        foreach (string line in raw)
        {
            if (SpeakerOf(line) is { } head)
            {
                owner = head.Speaker;
                outp.Add(new PrologueLine(head.Speaker, Bb(head.Body), owner));
            }
            else if (owner is not null)
            {
                outp.Add(new PrologueLine(null, Bb(line), owner));   // 대사 둘째 줄부터
            }
            else
            {
                outp.Add(new PrologueLine(null, Bb(line), null));    // 지문
            }
        }
        return outp;
    }

    /// <summary>`**이름** 본문` 이면 (이름, 본문). 아니면 null</summary>
    private static (string Speaker, string Body)? SpeakerOf(string line)
    {
        if (!line.StartsWith("**", System.StringComparison.Ordinal)) return null;
        int end = line.IndexOf("**", 2, System.StringComparison.Ordinal);
        if (end < 0) return null;
        string speaker = line[2..end].Trim();
        if (speaker.Length == 0) return null;
        return (speaker, line[(end + 2)..].Trim());
    }

    /// <summary>
    /// 최소 마크다운 → BBCode. `**강조**` 만 옮기고 나머지는 글자 그대로다.
    /// **`[` 를 먼저 막는 것이 핵심** — 본문에 「[보조배터리 체험단]」 같은 대괄호가 실제로 있고,
    /// 그대로 두면 RichTextLabel 이 태그로 읽어 그 줄이 통째로 사라진다.
    /// </summary>
    private static string Bb(string s)
    {
        var sb = new StringBuilder(s.Length + 16);
        sb.Append(s.Replace("[", "[lb]"));
        // `**` 를 번갈아 열고 닫는다. 짝이 안 맞으면 마지막 것은 글자로 남는다
        bool open = true;
        while (true)
        {
            int i = sb.ToString().IndexOf("**", System.StringComparison.Ordinal);
            if (i < 0) break;
            string tag = open ? "[b]" : "[/b]";
            sb.Remove(i, 2).Insert(i, tag);
            open = !open;
        }
        string outp = sb.ToString();
        if (!open) outp += "[/b]";                   // 열어 두고 끝났으면 닫아 준다
        return outp;
    }
}
