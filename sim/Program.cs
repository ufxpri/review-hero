// 전투 시뮬레이터 CLI (v2 — 단일 리뷰 카드 체계). packages/sim/src/cli.ts 이관 (ADR-029).
// 사용: dotnet run --project sim -- --enemy B01 --policy standard --runs 1000 --seed 42 [--json]
//       [--deck Z01,G01,...] [--deck-preset boss1] [--layer N] [--disposition-suit 계열]
//       [--will N]  ← 적 의지 오버라이드 (재시뮬 전용, YAML은 불변)
//       [--rule <경로>=<값>]…  [--rules '<JSON>']  ← 밸런스 수치 A/B (ADR-025)
//       [--help]
//
// 밸런스 A/B (ADR-025): 코드를 고치지 않고 rules 를 주입해 같은 시드로 대조한다.
//   ... --enemy B01 --runs 200 --seed 42                       # 기준
//   ... --enemy B01 --runs 200 --seed 42 --rule judge.mult.normal=0.75
//   ... --rule judge.gauge.fact=4 --rule critical.factBomberDamage=24    # 여러 번 가능
//   ... --rules '{"player":{"will":24},"gauge":{"max":8}}'               # 통째로 주입
// 경로는 Rules.cs 의 RulesConfig 구조 그대로다(구획.필드 또는 구획.표.키). --rules 를 먼저 깔고
// --rule 이 그 위를 덮는다.
//
// 결과 해석 주의:
// - 판정 4단계(원산지/팩트/일반/헛소리 — card-system-v2 §2). 판정 비율은 "실현(achieved)"이며
//   정책이 제출 시점에 기대한 판정(attempted)과 병기된다(정상이면 양자 일치 — 괴리 시 엔진/정책 판정 규칙 불일치 진단).
// - 시작 덱 12장은 전생 카드(Z##) — 원산지 영구 미발동. 원산지 실측은 --deck/--deck-preset 주입 필요.
// - gauge 지표 3종: judgement_net(판정 유래 raw 순증 — v2 원산지 +4/팩트 +3/헛소리 −2, 클램프 전) /
//   applied_gain(0~10 클램프 후 실반영 획득) / overflow_lost(상한 초과 소실).
// - deadlock(리뷰 제출 0회 패배)은 v1 접두 교착의 잔재 계측 — v2는 손패 전장이 제출 가능해
//   구조적으로 0이어야 한다 (0이 아니면 회귀 신호).
// - 최소 런 수 가이드: 승률 ±2%p 판별에는 약 2,500판 필요(Wilson 95% CI 참조).
//
// ── TS→C# 이관 주의 ────────────────────────────────────
// · --json 의 키 이름·중첩 구조는 TS 와 **한 글자도 다르면 안 된다** — 밸런스 분석 스크립트가
//   win_rate / turns.avg_all / crits.avg_used / judgement_rates / avg_remaining_will_wins 를 읽는다.
//   그래서 직렬화를 객체 매핑에 맡기지 않고 Utf8JsonWriter 로 순서까지 손으로 적는다.
// · dist 는 TS 에서 `Record<number, number>` 였고 JS 는 정수 키를 **오름차순**으로 직렬화한다.
//   C# Dictionary 는 삽입 순이므로 키를 정렬해서 쓴다.
// · 숫자 포맷은 전부 InvariantCulture — 로캘 소수점이 바뀌면 A/B 대조가 깨진다.

using System.Globalization;
using System.Text;
using System.Text.Json;
using ReviewHero.Data;
using ReviewHero.Engine;
using ReviewHero.Sim;

// 덱 프리셋 — GDD §3.6 「시뮬 검증 표준 덱」의 v2 정의 (밸런스 라운드 1-v2 확정).
//
// boss1 = "1막 보스 도달 기대 덱" 18장 = 시작 덱 12 + 전투 보상 3 + 상점 2 + 방어 …
//   · 전투 보상 3장: 1막 수지(GDD §4.2)가 전제하는 보스 전 전투 수는 3이고, 보상은 「이긴
//     대상의 리뷰 풀 택1」(card-system-v2 §1-①)이다. B01 약점(응대/개연성)을 겨냥해 고를 수
//     있는 것만 남기면 G01(응대, E01) · G02(개연성, E01 장비) · D02(응대, E04).
//     원산지 B01 카드(B01c·K##)는 B01 을 이겨야 나오므로 보스전 이전 획득 불가 — 그래서
//     이 덱의 원산지 발동률은 0% 이며, 그것이 1막 보스전의 정상 상태다.
//   · 상점 2장: 수입 ≈110G · 카드 25/45G(GDD §4.2)로 2장이 상한이다.
//   · 방어 2장 (ADR-027): 승리 보상 3칸 중 1칸이 「내 장비 리뷰」로 상시 배정되므로 방어는
//     이제 전투 보상으로도 들어온다. 3전투에서 전부 방어를 고르지는 않으므로 기대 보유량을
//     2장(H02·H04)으로 잡았다. 둘 다 비용 1 인 것이 중요하다 — 같은 18장·방어 2장이라도
//     비용 2 방어(S02)가 끼면 승률이 51.2% → 15.2% 로 무너진다.
// boss1_def = 방어 특화 대조군 (상점·이벤트를 전부 찬양에 쓴 경우) — 방어 축 감도 측정용.
var deckPresets = new Dictionary<string, Func<IReadOnlyList<string>, List<string>>>(StringComparer.Ordinal)
{
    ["boss1"] = s => s.Concat(new[] { "G01", "G02", "D02", "X02", "H02", "H04" }).ToList(),
    ["boss1_def"] = s => s.Concat(new[] { "G01", "G02", "D02", "S02", "X02", "H02", "S04" }).ToList(),
};

string Help() => $$$"""
전투 시뮬레이터 (sim)

  --enemy <id>              적 id (기본 E01)
  --policy <이름>           standard | skilled | reckless (기본 standard)
  --runs <N>                반복 판수 (기본 1000. 승률 ±2%p 판별에는 약 2,500판)
  --seed <N>                루트 시드 (기본 42)
  --layer <N>               카드 레이어 상한 (기본 1)
  --deck <id,id,...>        덱 직접 지정
  --deck-preset <이름>      덱 프리셋 ({{{string.Join(", ", deckPresets.Keys)}}})
  --disposition-suit <계열> 논점 스냅샷 주입 (품질|성능|배송|감성)
  --will <N>                적 의지 오버라이드 (YAML 불변, 메모리 사본만)
  --rule <경로>=<값>        밸런스 수치 1개 오버라이드. 여러 번 지정 가능 (ADR-025)
  --rules '<JSON>'          밸런스 수치를 통째로 오버라이드 (--rule 이 이 위를 덮는다)
  --json                    요약을 JSON 으로 출력
  --help                    이 도움말

밸런스 A/B — 코드를 고치지 않고 같은 시드로 대조한다:
  ... --enemy B01 --runs 200 --seed 42
  ... --enemy B01 --runs 200 --seed 42 --rule judge.mult.normal=0.75
  ... --rule judge.gauge.fact=4 --rule critical.factBomberDamage=24
  ... --rules '{"player":{"will":24},"gauge":{"max":8}}'
경로는 Rules.cs 의 RulesConfig 구조 그대로다 (구획.필드 또는 구획.표.키).
""";

// ── 인자 파싱 ────────────────────────────────────────
string enemyId = "E01";
var policy = PolicyName.Standard;
int runs = 1000, seed = 42, layer = 1;
bool json = false;
Dictionary<Suit, int>? counters = null;
List<string>? deckArg = null;
string? deckPreset = null;
int? willOverride = null;
var over = new RulesOverride();

for (int i = 0; i < args.Length; i++)
{
    string a = args[i];
    switch (a)
    {
        case "--help":
        case "-h":
            Console.WriteLine(Help());
            return 0;
        case "--rules":
            // 통째 주입 — 중첩 JSON 을 점 표기 경로로 펴서 깔고, 뒤이은 --rule 이 그 위를 덮는다
            foreach (var (path, value) in FlattenRules(args[++i])) over.Add(path, value);
            break;
        case "--rule":
            over.AddSpec(args[++i]);
            break;
        case "--enemy": enemyId = args[++i]; break;
        case "--policy":
            {
                string raw = args[++i];
                if (!Policies.TryParseName(raw, out policy))
                {
                    throw new ArgumentException($"알 수 없는 정책: {raw} (standard|skilled|reckless)");
                }
                break;
            }
        case "--runs": runs = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--layer": layer = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--json": json = true; break;
        case "--deck":
            deckArg = args[++i].Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            break;
        case "--deck-preset": deckPreset = args[++i]; break;
        case "--will": willOverride = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--disposition-suit":
            // 논점 스냅샷은 런 누적 카운터 기준(GDD §3.5) — 단일 전투 시뮬용 주입 옵션
            counters = new Dictionary<Suit, int> { [Types.ParseSuit(args[++i])] = 1 };
            break;
    }
}

if (deckPreset is not null && !deckPresets.ContainsKey(deckPreset))
{
    throw new ArgumentException($"알 수 없는 덱 프리셋: {deckPreset} ({string.Join(", ", deckPresets.Keys)})");
}

// ── 데이터 ───────────────────────────────────────────
var data = Loader.LoadAll();
if (!data.Enemies.TryGetValue(enemyId, out var enemy))
{
    throw new ArgumentException($"적 없음: {enemyId} ({string.Join(", ", data.Enemies.Keys)})");
}
if (willOverride is { } w) enemy = enemy with { Will = w }; // YAML 정본은 그대로, 메모리 사본만 변경

IReadOnlyList<string> deck = data.StartingDeck;
if (deckPreset is not null) deck = deckPresets[deckPreset](data.StartingDeck);
if (deckArg is not null) deck = deckArg;
foreach (var id in deck)
{
    if (!data.Cards.ById.ContainsKey(id)) throw new ArgumentException($"덱에 알 수 없는 카드: {id}");
}

// ── 실행 ─────────────────────────────────────────────
var rootRng = RngFactory.Mulberry32((uint)seed);
var records = new List<RunRecord>();
var telemetry = new PolicyTelemetry();

for (int r = 0; r < runs; r++)
{
    // 공통난수(CRN) 대조를 위해 엔진용·정책용 rng 스트림을 같은 런 시드에서 분기 —
    // 정책의 rng 소비 횟수가 셔플·드로우 스트림을 어긋나게 하지 않는다 (매치드 페어 비교 가능)
    uint runSeed = (uint)Math.Floor(rootRng() * 0xffffffff);
    var battleRng = RngFactory.Mulberry32(runSeed);
    var policyRng = RngFactory.Mulberry32(runSeed ^ 0x9e3779b9u);
    var battle = new Battle(new BattleConfig
    {
        Cards = data.Cards,
        Enemy = enemy,
        Deck = deck,
        Rng = battleRng,
        Layer = layer,
        InitialSuitCounters = counters,
        Rules = over.Count > 0 ? over : null,
    });
    int guard = 200;
    while (battle.State.Result is null && guard-- > 0)
    {
        Policies.PlayTurn(battle, data.Cards, policy, policyRng, telemetry);
    }
    var st = battle.State;
    records.Add(new RunRecord
    {
        Win = st.Result == BattleResult.Win,
        Result = st.Result?.ToString().ToLowerInvariant() ?? "stuck",
        Turns = st.Turn,
        Crits = st.Stats.Crits.Count,
        CritsAvailable = st.Stats.GaugeReached10,
        CritMisses = st.Stats.CritMisses,
        Submissions = st.Stats.Submissions,
        GaugeGained = st.Stats.GaugeGained,
        GaugeLost = st.Stats.GaugeLost,
        GaugeOverflowLost = st.Stats.GaugeOverflowLost,
        RemainingWill = st.Player.Will,
        Judgements = new Dictionary<Judgement, int>(st.Stats.Judgements),
        Surrender = st.Stats.Surrender,
        // 리뷰 제출 0회 패배 (v1 접두 교착 잔재 계측 — v2는 0이어야 정상)
        Deadlock = st.Result == BattleResult.Lose && st.Stats.Submissions == 0,
    });
}

// ── 집계 ─────────────────────────────────────────────
int n = records.Count;
var wins = records.Where(x => x.Win).ToList();
var losses = records.Where(x => x.Result == "lose").ToList();
var deadlocks = records.Where(x => x.Deadlock).ToList();

static double Avg(IEnumerable<int> xs)
{
    var list = xs.ToList();
    return list.Count > 0 ? list.Sum(x => (double)x) / list.Count : 0;
}

static SortedDictionary<int, int> Dist(IEnumerable<int> xs)
{
    var d = new SortedDictionary<int, int>();
    foreach (var x in xs) d[x] = d.TryGetValue(x, out var c) ? c + 1 : 1;
    return d;
}

/// <summary>승률용 Wilson 95% 신뢰구간</summary>
static (double Lo, double Hi) Wilson95(double p, int n)
{
    if (n == 0) return (0, 0);
    const double z = 1.96;
    double z2 = z * z;
    double denom = 1 + z2 / n;
    double center = (p + z2 / (2.0 * n)) / denom;
    double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / denom;
    return (Math.Max(0, center - half), Math.Min(1, center + half));
}

/// <summary>평균의 표준오차</summary>
static double SeOfMean(IReadOnlyList<int> xs)
{
    int n = xs.Count;
    if (n < 2) return 0;
    double m = xs.Sum(x => (double)x) / n;
    double varr = xs.Sum(x => (x - m) * (x - m)) / (n - 1);
    return Math.Sqrt(varr / n);
}

var totalJ = new Dictionary<Judgement, int>
{
    [Judgement.Origin] = records.Sum(x => x.Judgements[Judgement.Origin]),
    [Judgement.Fact] = records.Sum(x => x.Judgements[Judgement.Fact]),
    [Judgement.Normal] = records.Sum(x => x.Judgements[Judgement.Normal]),
    [Judgement.Fumble] = records.Sum(x => x.Judgements[Judgement.Fumble]),
};
int jSum = totalJ.Values.Sum();
int totalTurns = records.Sum(x => x.Turns);
int aSum = telemetry.Attempted.Values.Sum();
double winRate = (double)wins.Count / n;
var (ciLo, ciHi) = Wilson95(winRate, n);
int nExDeadlock = n - deadlocks.Count;

string deckPresetLabel = deckPreset ?? (deckArg is not null ? "custom" : "starting");
var turnsDist = Dist(records.Select(x => x.Turns));
var critsDist = Dist(records.Select(x => x.Crits));
var critList = records.Select(x => x.Crits).ToList();

// 판정 유래 raw 순증(클램프 전, v2 값: 원산지 +4 / 팩트 +3 / 헛소리 −2) / 턴
double judgementNetPerTurn =
    (totalJ[Judgement.Origin] * 4.0 + totalJ[Judgement.Fact] * 3.0 - totalJ[Judgement.Fumble] * 2.0) / totalTurns;

if (json)
{
    Console.Out.Write(BuildJson());
    Console.Out.Write('\n');
    return 0;
}

string Pct(double x) => $"{(x * 100).ToString("F1", CultureInfo.InvariantCulture)}%";
string F(double x, int digits) => x.ToString("F" + digits, CultureInfo.InvariantCulture);

Console.WriteLine($"── 전투 시뮬레이션: {enemy.Name}({enemyId}, 의지 {enemy.Will}) × {Policies.ToCliName(policy)} × {n}판 (seed {seed}, 덱 {deckPresetLabel} {deck.Count}장) ──");
Console.WriteLine($"승률           : {Pct(winRate)}  [95% CI {Pct(ciLo)}~{Pct(ciHi)}]  (승 {wins.Count} / 패 {losses.Count} / 시간초과 {records.Count(x => x.Result == "timeout")}, 항복승 {records.Count(x => x.Surrender)})");
Console.WriteLine($"교착 패배      : {deadlocks.Count}판 (제출 0회 — v2에선 0이어야 정상. 교착 제외 승률 {Pct(nExDeadlock != 0 ? (double)wins.Count / nExDeadlock : 0)})");
Console.WriteLine($"평균 턴 수     : 전체 {F(Avg(records.Select(x => x.Turns)), 2)} / 승리 {F(Avg(wins.Select(x => x.Turns)), 2)} / 패배 {F(Avg(losses.Select(x => x.Turns)), 2)}");
Console.WriteLine($"턴 분포        : {string.Join(" ", turnsDist.Select(kv => $"{kv.Key}턴:{kv.Value}"))}");
Console.WriteLine($"크리티컬       : 발동 평균 {F(Avg(critList), 2)} (±SE {F(SeOfMean(critList), 3)}) / 가능(게이지10 도달) 평균 {F(Avg(records.Select(x => x.CritsAvailable)), 2)} / 은신 빗나감 {records.Sum(x => x.CritMisses)}");
Console.WriteLine($"크리 발동 분포 : {string.Join(" ", critsDist.Select(kv => $"{kv.Key}회:{kv.Value}"))}");
Console.WriteLine($"게이지/턴      : 판정 순증(§3.4 정의) {(judgementNetPerTurn >= 0 ? "+" : "")}{F(judgementNetPerTurn, 2)} / 실반영 획득 +{F((double)records.Sum(x => x.GaugeGained) / totalTurns, 2)} / 감소 −{F((double)records.Sum(x => x.GaugeLost) / totalTurns, 2)} / 초과 소실 {F((double)records.Sum(x => x.GaugeOverflowLost) / totalTurns, 2)}");
Console.WriteLine($"평균 잔여 의지 : 전체 {F(Avg(records.Select(x => x.RemainingWill)), 1)} / 승리 시 {F(Avg(wins.Select(x => x.RemainingWill)), 1)} (최대 30)");
if (jSum != 0)
{
    Console.WriteLine($"판정 실현      : 원산지 {Pct((double)totalJ[Judgement.Origin] / jSum)} / 팩트 {Pct((double)totalJ[Judgement.Fact] / jSum)} / 일반 {Pct((double)totalJ[Judgement.Normal] / jSum)} / 헛소리 {Pct((double)totalJ[Judgement.Fumble] / jSum)} (판정 {F((double)jSum / totalTurns, 2)}회/턴)");
}
if (aSum != 0)
{
    Console.WriteLine($"판정 의도      : 원산지 {Pct((double)telemetry.Attempted[Judgement.Origin] / aSum)} / 팩트 {Pct((double)telemetry.Attempted[Judgement.Fact] / aSum)} / 일반 {Pct((double)telemetry.Attempted[Judgement.Normal] / aSum)} / 헛소리 {Pct((double)telemetry.Attempted[Judgement.Fumble] / aSum)} — 우위 판정 부재 제출 {telemetry.NoWantedFallbacks}회({Pct((double)telemetry.NoWantedFallbacks / aSum)})");
}
return 0;

// ── JSON (키 이름·순서는 TS 와 동일해야 한다) ──────────
string BuildJson()
{
    using var ms = new MemoryStream();
    using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
        w.WriteStartObject();
        w.WriteString("enemy", enemyId);
        w.WriteNumber("enemy_will", enemy.Will); // --will 오버라이드 추적용
        w.WriteString("policy", Policies.ToCliName(policy));
        w.WriteNumber("runs", n);
        w.WriteNumber("seed", seed);
        w.WriteNumber("deck_size", deck.Count);
        w.WriteString("deck_preset", deckPresetLabel);
        w.WriteNumber("win_rate", winRate);
        w.WriteStartArray("win_rate_ci95"); // Wilson 95% CI — ±2%p 판별에는 약 2,500판 필요
        w.WriteNumberValue(ciLo);
        w.WriteNumberValue(ciHi);
        w.WriteEndArray();
        w.WriteNumber("deadlock_losses", deadlocks.Count); // 리뷰 제출 0회 패배
        w.WriteNumber("win_rate_excl_deadlock", nExDeadlock != 0 ? (double)wins.Count / nExDeadlock : 0);
        w.WriteNumber("surrender_wins", records.Count(x => x.Surrender));

        w.WriteStartObject("results");
        w.WriteNumber("win", wins.Count);
        w.WriteNumber("lose", losses.Count);
        w.WriteNumber("timeout", records.Count(x => x.Result == "timeout"));
        w.WriteNumber("retreat", records.Count(x => x.Result == "retreat"));
        w.WriteEndObject();

        // 사망 절단 혼합 방지: 전투 길이는 all/wins/losses 3분할 (§3.1 기준선 대조는 avg_wins 사용)
        w.WriteStartObject("turns");
        w.WriteNumber("avg_all", Avg(records.Select(x => x.Turns)));
        w.WriteNumber("avg_wins", Avg(wins.Select(x => x.Turns)));
        w.WriteNumber("avg_losses", Avg(losses.Select(x => x.Turns)));
        WriteDist(w, "dist", turnsDist);
        w.WriteEndObject();

        // 크리: "발동 가능"(게이지 10 도달)과 "발동"을 구분 — 정책은 도달 즉시 사용(타이밍 최적화 없음)
        w.WriteStartObject("crits");
        w.WriteNumber("avg_used", Avg(critList));
        w.WriteNumber("se_used", SeOfMean(critList));
        w.WriteNumber("avg_available", Avg(records.Select(x => x.CritsAvailable)));
        w.WriteNumber("stealth_misses", records.Sum(x => x.CritMisses));
        WriteDist(w, "dist", critsDist);
        w.WriteEndObject();

        w.WriteStartObject("gauge");
        w.WriteNumber("judgement_net_per_turn", judgementNetPerTurn);
        w.WriteNumber("applied_gain_per_turn", (double)records.Sum(x => x.GaugeGained) / totalTurns);
        w.WriteNumber("lost_per_turn", (double)records.Sum(x => x.GaugeLost) / totalTurns);
        w.WriteNumber("overflow_lost_per_turn", (double)records.Sum(x => x.GaugeOverflowLost) / totalTurns);
        w.WriteEndObject();

        w.WriteNumber("avg_remaining_will_all", Avg(records.Select(x => x.RemainingWill)));
        w.WriteNumber("avg_remaining_will_wins", Avg(wins.Select(x => x.RemainingWill)));

        // achieved = 엔진 실현 판정 / attempted = 정책 기대 판정 (괴리 = 판정 규칙 불일치 진단)
        if (jSum != 0)
        {
            w.WriteStartObject("judgement_rates");
            w.WriteNumber("origin", (double)totalJ[Judgement.Origin] / jSum);
            w.WriteNumber("fact", (double)totalJ[Judgement.Fact] / jSum);
            w.WriteNumber("normal", (double)totalJ[Judgement.Normal] / jSum);
            w.WriteNumber("fumble", (double)totalJ[Judgement.Fumble] / jSum);
            w.WriteNumber("per_turn", (double)jSum / totalTurns);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("judgement_rates");
        }

        if (aSum != 0)
        {
            w.WriteStartObject("attempted_rates");
            w.WriteNumber("origin", (double)telemetry.Attempted[Judgement.Origin] / aSum);
            w.WriteNumber("fact", (double)telemetry.Attempted[Judgement.Fact] / aSum);
            w.WriteNumber("normal", (double)telemetry.Attempted[Judgement.Normal] / aSum);
            w.WriteNumber("fumble", (double)telemetry.Attempted[Judgement.Fumble] / aSum);
            w.WriteNumber("fallbacks", telemetry.NoWantedFallbacks); // 우위 판정(원산지/팩트) 없이 제출한 횟수
            w.WriteNumber("fallback_rate", (double)telemetry.NoWantedFallbacks / aSum);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("attempted_rates"); // reckless(완전 무작위)는 미집계
        }

        w.WriteEndObject();
    }
    return Encoding.UTF8.GetString(ms.ToArray());
}

static void WriteDist(Utf8JsonWriter w, string name, SortedDictionary<int, int> dist)
{
    w.WriteStartObject(name);
    foreach (var (k, v) in dist) w.WriteNumber(k.ToString(CultureInfo.InvariantCulture), v);
    w.WriteEndObject();
}

/// <summary>--rules 의 중첩 JSON 을 점 표기 경로로 편다 (TS 의 구획 단위 스프레드와 등가)</summary>
static IEnumerable<(string Path, double Value)> FlattenRules(string jsonText)
{
    using var doc = JsonDocument.Parse(jsonText);
    var acc = new List<(string, double)>();
    Walk(doc.RootElement, "", acc);
    return acc;

    static void Walk(JsonElement el, string prefix, List<(string, double)> acc)
    {
        foreach (var prop in el.EnumerateObject())
        {
            string path = prefix.Length == 0 ? prop.Name : $"{prefix}.{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Object) Walk(prop.Value, path, acc);
            else if (prop.Value.ValueKind == JsonValueKind.Number) acc.Add((path, prop.Value.GetDouble()));
            else if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                acc.Add((path, prop.Value.GetBoolean() ? 1 : 0));
            }
            else throw new ArgumentException($"--rules 값을 읽을 수 없다: {path}");
        }
    }
}

/// <summary>한 판의 결과 한 줄 (TS RunRecord)</summary>
internal sealed class RunRecord
{
    public bool Win { get; init; }
    public string Result { get; init; } = string.Empty;
    public int Turns { get; init; }
    public int Crits { get; init; }

    /// <summary>게이지 10 도달 이벤트 수 (발동 가능)</summary>
    public int CritsAvailable { get; init; }

    public int CritMisses { get; init; }
    public int Submissions { get; init; }
    public int GaugeGained { get; init; }
    public int GaugeLost { get; init; }
    public int GaugeOverflowLost { get; init; }
    public int RemainingWill { get; init; }
    public Dictionary<Judgement, int> Judgements { get; init; } = new();
    public bool Surrender { get; init; }

    /// <summary>리뷰 제출 0회 패배 (v1 접두 교착 잔재 계측 — v2는 0이어야 정상)</summary>
    public bool Deadlock { get; init; }
}
