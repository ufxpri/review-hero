// 전투 시뮬레이터 CLI (v2 — 단일 리뷰 카드 체계)
// 사용: npx tsx packages/sim/src/cli.ts --enemy B01 --policy standard --runs 1000 --seed 42 [--json]
//       [--deck Z01,G01,...] [--deck-preset boss1] [--layer N] [--disposition-suit 계열]
//       [--will N]  ← 적 의지 오버라이드 (재시뮬 전용, YAML은 불변)
//       [--rule <경로>=<값>]…  [--rules '<JSON>']  ← 밸런스 수치 A/B (ADR-025)
//       [--help]
//
// 밸런스 A/B (ADR-025): 코드를 고치지 않고 rules 를 주입해 같은 시드로 대조한다.
//   npx tsx packages/sim/src/cli.ts --enemy B01 --runs 200 --seed 42                       # 기준
//   npx tsx packages/sim/src/cli.ts --enemy B01 --runs 200 --seed 42 --rule judge.mult.normal=0.75
//   ... --rule judge.gauge.fact=4 --rule critical.factBomberDamage=24                       # 여러 번 가능
//   ... --rules '{"player":{"will":24},"gauge":{"max":8}}'                                  # 통째로 주입
// 경로는 rules.ts 의 RulesConfig 구조 그대로다(구획.필드 또는 구획.표.키). 값은 JSON 으로 읽고
// 실패하면 숫자로 읽는다. --rules 를 먼저 깔고 --rule 이 그 위를 덮는다.
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
import { Battle, DEFAULT_RULES, mergeRules, mulberry32, type RulesOverride, type Suit } from '../../core/src/index.ts';
import { loadAll } from './data.ts';
import { newTelemetry, playTurn, POLICIES, type PolicyName, type PolicyTelemetry } from './policies.ts';

interface Args {
  enemy: string;
  policy: PolicyName;
  runs: number;
  seed: number;
  json: boolean;
  layer: number;
  counters?: Partial<Record<Suit, number>>;
  deck?: string[];
  deckPreset?: string;
  will?: number; // 적 의지 오버라이드 (R11 재시뮬용)
  rules?: RulesOverride; // 밸런스 수치 오버라이드 (ADR-025 — A/B용)
}

/** DECK_PRESETS 가 아래에서 선언되므로 상수가 아니라 함수로 둔다 */
const help = (): string => `전투 시뮬레이터 (packages/sim)

  --enemy <id>              적 id (기본 E01)
  --policy <이름>           standard | skilled | reckless (기본 standard)
  --runs <N>                반복 판수 (기본 1000. 승률 ±2%p 판별에는 약 2,500판)
  --seed <N>                루트 시드 (기본 42)
  --layer <N>               카드 레이어 상한 (기본 1)
  --deck <id,id,...>        덱 직접 지정
  --deck-preset <이름>      덱 프리셋 (${Object.keys(DECK_PRESETS).join(', ')})
  --disposition-suit <계열> 성향 스냅샷 주입 (품질|성능|배송|감성)
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
경로는 rules.ts 의 RulesConfig 구조 그대로다 (구획.필드 또는 구획.표.키).`;

/** `judge.mult.normal=0.75` 한 줄을 RulesOverride 에 심는다 (2단계까지 — RulesConfig 의 깊이) */
function applyRulePath(over: Record<string, Record<string, unknown>>, spec: string): void {
  const eq = spec.indexOf('=');
  if (eq < 0) throw new Error(`--rule 형식은 경로=값 이다: ${spec}`);
  const path = spec.slice(0, eq).split('.');
  const raw = spec.slice(eq + 1);
  let value: unknown;
  try {
    value = JSON.parse(raw);
  } catch {
    const n = Number(raw);
    if (Number.isNaN(n)) throw new Error(`--rule 값을 읽을 수 없다: ${raw}`);
    value = n;
  }
  const [section, key, sub] = path;
  const known = Object.keys(DEFAULT_RULES);
  if (!section || !key || !known.includes(section)) {
    throw new Error(`알 수 없는 rules 경로: ${path.join('.')} (구획: ${known.join(', ')})`);
  }
  over[section] ??= {};
  if (sub === undefined) {
    over[section]![key] = value;
  } else {
    const table = (over[section]![key] ??= {}) as Record<string, unknown>;
    table[sub] = value;
  }
}

// 덱 프리셋 — GDD §3.6 「시뮬 검증 표준 덱」의 v2 정의 (밸런스 라운드 1-v2 확정).
//
// boss1 = "1막 보스 도달 기대 덱" 17장 = 시작 덱 12 + 전투 보상 3 + 상점 2.
//   · 전투 보상 3장: 1막 수지(GDD §4.2)가 전제하는 보스 전 전투 수는 3이고, 보상은 「이긴
//     대상의 리뷰 풀 택1」(card-system-v2 §1-①)이다. B01 약점(응대/개연성)을 겨냥해 고를 수
//     있는 것만 남기면 G01(응대, E01) · G02(개연성, E01 장비) · D02(응대, E04).
//     원산지 B01 카드(B01c·K##)는 B01 을 이겨야 나오므로 보스전 이전 획득 불가 — 그래서
//     이 덱의 원산지 발동률은 0% 이며, 그것이 1막 보스전의 정상 상태다.
//   · 상점 2장: 수입 ≈110G · 카드 25/45G(GDD §4.2)로 2장이 상한이다. 찬양·방어 카드는
//     전투 보상 풀에 들어갈 수 없어(origin 없음) **상점·이벤트가 유일한 획득 경로**다.
//     그 사실을 덱에 반영해 방어 1장(S02)만 넣고, 나머지 1장은 범용 화력(X02)으로 둔다.
// boss1_def = 방어 특화 대조군 19장 (상점·이벤트를 전부 찬양에 쓴 경우) — 방어 축 감도 측정용.
const DECK_PRESETS: Record<string, (startingDeck: string[]) => string[]> = {
  boss1: (s) => [...s, 'G01', 'G02', 'D02', 'S02', 'X02'],
  boss1_def: (s) => [...s, 'G01', 'G02', 'D02', 'S02', 'X02', 'H02', 'S04'],
};

function parseArgs(argv: string[]): Args {
  const args: Args = { enemy: 'E01', policy: 'standard', runs: 1000, seed: 42, json: false, layer: 1 };
  const over: Record<string, Record<string, unknown>> = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i]!;
    if (a === '--help' || a === '-h') {
      console.log(help());
      process.exit(0);
    } else if (a === '--rules') {
      // 통째 주입 — 구획 단위로 깔고, 뒤이은 --rule 이 그 위를 덮는다
      const json = JSON.parse(argv[++i]!) as Record<string, Record<string, unknown>>;
      for (const [k, v] of Object.entries(json)) over[k] = { ...(over[k] ?? {}), ...v };
    } else if (a === '--rule') applyRulePath(over, argv[++i]!);
    else if (a === '--enemy') args.enemy = argv[++i]!;
    else if (a === '--policy') args.policy = argv[++i] as PolicyName;
    else if (a === '--runs') args.runs = parseInt(argv[++i]!, 10);
    else if (a === '--seed') args.seed = parseInt(argv[++i]!, 10);
    else if (a === '--layer') args.layer = parseInt(argv[++i]!, 10);
    else if (a === '--json') args.json = true;
    else if (a === '--deck') args.deck = argv[++i]!.split(',').map((x) => x.trim()).filter(Boolean);
    else if (a === '--deck-preset') args.deckPreset = argv[++i]!;
    else if (a === '--will') args.will = parseInt(argv[++i]!, 10);
    else if (a === '--disposition-suit') {
      // 성향 스냅샷은 런 누적 카운터 기준(GDD §3.5) — 단일 전투 시뮬용 주입 옵션
      const suit = argv[++i] as Suit;
      args.counters = { [suit]: 1 } as Partial<Record<Suit, number>>;
    }
  }
  if (Object.keys(over).length > 0) args.rules = over as RulesOverride;
  if (!POLICIES[args.policy]) throw new Error(`알 수 없는 정책: ${args.policy} (standard|skilled|reckless)`);
  if (args.deckPreset && !DECK_PRESETS[args.deckPreset]) {
    throw new Error(`알 수 없는 덱 프리셋: ${args.deckPreset} (${Object.keys(DECK_PRESETS).join(', ')})`);
  }
  return args;
}

interface RunRecord {
  win: boolean;
  result: string;
  turns: number;
  crits: number;
  critsAvailable: number; // 게이지 10 도달 이벤트 수 (발동 가능)
  critMisses: number;
  submissions: number;
  gaugeGained: number;
  gaugeLost: number;
  gaugeOverflowLost: number;
  remainingWill: number;
  judgements: { origin: number; fact: number; normal: number; fumble: number };
  surrender: boolean;
  deadlock: boolean; // 리뷰 제출 0회 패배 (v1 접두 교착 잔재 계측 — v2는 0이어야 정상)
}

/** 승률용 Wilson 95% 신뢰구간 */
function wilson95(p: number, n: number): [number, number] {
  if (n === 0) return [0, 0];
  const z = 1.96;
  const z2 = z * z;
  const denom = 1 + z2 / n;
  const center = (p + z2 / (2 * n)) / denom;
  const half = (z * Math.sqrt((p * (1 - p)) / n + z2 / (4 * n * n))) / denom;
  return [Math.max(0, center - half), Math.min(1, center + half)];
}

/** 평균의 표준오차 */
function seOfMean(xs: number[]): number {
  const n = xs.length;
  if (n < 2) return 0;
  const m = xs.reduce((a, b) => a + b, 0) / n;
  const varr = xs.reduce((a, b) => a + (b - m) * (b - m), 0) / (n - 1);
  return Math.sqrt(varr / n);
}

function main(): void {
  const args = parseArgs(process.argv.slice(2));
  const data = loadAll();
  let enemy = data.enemies.get(args.enemy);
  if (!enemy) throw new Error(`적 없음: ${args.enemy} (${[...data.enemies.keys()].join(', ')})`);
  if (args.will !== undefined) enemy = { ...enemy, will: args.will }; // YAML 정본은 그대로, 메모리 사본만 변경

  let deck = data.startingDeck;
  if (args.deckPreset) deck = DECK_PRESETS[args.deckPreset]!(data.startingDeck);
  if (args.deck) deck = args.deck;
  for (const id of deck) if (!data.cards.byId.has(id)) throw new Error(`덱에 알 수 없는 카드: ${id}`);

  const rootRng = mulberry32(args.seed);
  const records: RunRecord[] = [];
  const telemetry: PolicyTelemetry = newTelemetry();
  // 통계 산출도 엔진과 같은 수치를 봐야 한다 (판정 순증·최대 의지 표기) — ADR-025
  const rules = mergeRules(DEFAULT_RULES, args.rules);

  for (let r = 0; r < args.runs; r++) {
    // 공통난수(CRN) 대조를 위해 엔진용·정책용 rng 스트림을 같은 런 시드에서 분기 —
    // 정책의 rng 소비 횟수가 셔플·드로우 스트림을 어긋나게 하지 않는다 (매치드 페어 비교 가능)
    const runSeed = Math.floor(rootRng() * 0xffffffff);
    const battleRng = mulberry32(runSeed);
    const policyRng = mulberry32((runSeed ^ 0x9e3779b9) >>> 0);
    const battle = new Battle({
      cards: data.cards,
      enemy,
      deck,
      rng: battleRng,
      layer: args.layer,
      initialSuitCounters: args.counters,
      rules: args.rules,
    });
    let guard = 200;
    while (!battle.state.result && guard-- > 0) playTurn(battle, data.cards, args.policy, policyRng, telemetry);
    const st = battle.state;
    records.push({
      win: st.result === 'win',
      result: st.result ?? 'stuck',
      turns: st.turn,
      crits: st.stats.crits.length,
      critsAvailable: st.stats.gaugeReached10,
      critMisses: st.stats.critMisses,
      submissions: st.stats.submissions,
      gaugeGained: st.stats.gaugeGained,
      gaugeLost: st.stats.gaugeLost,
      gaugeOverflowLost: st.stats.gaugeOverflowLost,
      remainingWill: st.player.will,
      judgements: st.stats.judgements,
      surrender: st.stats.surrender,
      deadlock: st.result === 'lose' && st.stats.submissions === 0,
    });
  }

  const n = records.length;
  const wins = records.filter((x) => x.win);
  const losses = records.filter((x) => x.result === 'lose');
  const deadlocks = records.filter((x) => x.deadlock);
  const avg = (xs: number[]) => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : 0);
  const dist = (xs: number[]) => {
    const d: Record<number, number> = {};
    for (const x of xs) d[x] = (d[x] ?? 0) + 1;
    return d;
  };
  const totalJ = records.reduce(
    (acc, x) => ({
      origin: acc.origin + x.judgements.origin,
      fact: acc.fact + x.judgements.fact,
      normal: acc.normal + x.judgements.normal,
      fumble: acc.fumble + x.judgements.fumble,
    }),
    { origin: 0, fact: 0, normal: 0, fumble: 0 },
  );
  const jSum = totalJ.origin + totalJ.fact + totalJ.normal + totalJ.fumble;
  const totalTurns = records.reduce((a, x) => a + x.turns, 0);
  const aSum = telemetry.attempted.origin + telemetry.attempted.fact + telemetry.attempted.normal + telemetry.attempted.fumble;
  const winRate = wins.length / n;
  const [ciLo, ciHi] = wilson95(winRate, n);
  const nExDeadlock = n - deadlocks.length;

  const summary = {
    enemy: args.enemy,
    enemy_will: enemy.will, // --will 오버라이드 추적용
    policy: args.policy,
    runs: n,
    seed: args.seed,
    deck_size: deck.length,
    deck_preset: args.deckPreset ?? (args.deck ? 'custom' : 'starting'),
    win_rate: winRate,
    win_rate_ci95: [ciLo, ciHi], // Wilson 95% CI — ±2%p 판별에는 약 2,500판 필요
    deadlock_losses: deadlocks.length, // 리뷰 제출 0회 패배 (접두 없는 손패 교착 — §3.1 설계 질의 대상)
    win_rate_excl_deadlock: nExDeadlock ? wins.length / nExDeadlock : 0,
    surrender_wins: records.filter((x) => x.surrender).length,
    results: {
      win: wins.length,
      lose: losses.length,
      timeout: records.filter((x) => x.result === 'timeout').length,
      retreat: records.filter((x) => x.result === 'retreat').length,
    },
    // 사망 절단 혼합 방지: 전투 길이는 all/wins/losses 3분할 (§3.1 기준선 대조는 avg_wins 사용)
    turns: {
      avg_all: avg(records.map((x) => x.turns)),
      avg_wins: avg(wins.map((x) => x.turns)),
      avg_losses: avg(losses.map((x) => x.turns)),
      dist: dist(records.map((x) => x.turns)),
    },
    // 크리: "발동 가능"(게이지 10 도달)과 "발동"을 구분 — 정책은 도달 즉시 사용(타이밍 최적화 없음)
    crits: {
      avg_used: avg(records.map((x) => x.crits)),
      se_used: seOfMean(records.map((x) => x.crits)),
      avg_available: avg(records.map((x) => x.critsAvailable)),
      stealth_misses: records.reduce((a, x) => a + x.critMisses, 0),
      dist: dist(records.map((x) => x.crits)),
    },
    // 판정 유래 raw 순증(클램프 전, v2 값: 원산지 +4 / 팩트 +3 / 헛소리 −2) / 턴
    gauge: {
      judgement_net_per_turn: (totalJ.origin * 4 + totalJ.fact * 3 - totalJ.fumble * 2) / totalTurns,
      applied_gain_per_turn: records.reduce((a, x) => a + x.gaugeGained, 0) / totalTurns, // 클램프 후 실반영 획득
      lost_per_turn: records.reduce((a, x) => a + x.gaugeLost, 0) / totalTurns,
      overflow_lost_per_turn: records.reduce((a, x) => a + x.gaugeOverflowLost, 0) / totalTurns,
    },
    avg_remaining_will_all: avg(records.map((x) => x.remainingWill)),
    avg_remaining_will_wins: avg(wins.map((x) => x.remainingWill)),
    // achieved = 엔진 실현 판정 / attempted = 정책 기대 판정 (괴리 = 판정 규칙 불일치 진단)
    judgement_rates: jSum
      ? {
          origin: totalJ.origin / jSum,
          fact: totalJ.fact / jSum,
          normal: totalJ.normal / jSum,
          fumble: totalJ.fumble / jSum,
          per_turn: jSum / totalTurns,
        }
      : null,
    attempted_rates: aSum
      ? {
          origin: telemetry.attempted.origin / aSum,
          fact: telemetry.attempted.fact / aSum,
          normal: telemetry.attempted.normal / aSum,
          fumble: telemetry.attempted.fumble / aSum,
          fallbacks: telemetry.noWantedFallbacks, // 우위 판정(원산지/팩트) 없이 제출한 횟수
          fallback_rate: telemetry.noWantedFallbacks / aSum,
        }
      : null, // reckless(완전 무작위)는 미집계
  };

  if (args.json) {
    console.log(JSON.stringify(summary, null, 2));
    return;
  }

  const pct = (x: number) => `${(x * 100).toFixed(1)}%`;
  console.log(`── 전투 시뮬레이션: ${enemy.name}(${args.enemy}, 의지 ${enemy.will}) × ${args.policy} × ${n}판 (seed ${args.seed}, 덱 ${summary.deck_preset} ${deck.length}장) ──`);
  console.log(
    `승률           : ${pct(summary.win_rate)}  [95% CI ${pct(ciLo)}~${pct(ciHi)}]  (승 ${summary.results.win} / 패 ${summary.results.lose} / 시간초과 ${summary.results.timeout}, 항복승 ${summary.surrender_wins})`,
  );
  console.log(`교착 패배      : ${summary.deadlock_losses}판 (제출 0회 — v2에선 0이어야 정상. 교착 제외 승률 ${pct(summary.win_rate_excl_deadlock)})`);
  console.log(`평균 턴 수     : 전체 ${summary.turns.avg_all.toFixed(2)} / 승리 ${summary.turns.avg_wins.toFixed(2)} / 패배 ${summary.turns.avg_losses.toFixed(2)}`);
  console.log(`턴 분포        : ${Object.entries(summary.turns.dist).map(([k, v]) => `${k}턴:${v}`).join(' ')}`);
  console.log(
    `크리티컬       : 발동 평균 ${summary.crits.avg_used.toFixed(2)} (±SE ${summary.crits.se_used.toFixed(3)}) / 가능(게이지10 도달) 평균 ${summary.crits.avg_available.toFixed(2)} / 은신 빗나감 ${summary.crits.stealth_misses}`,
  );
  console.log(`크리 발동 분포 : ${Object.entries(summary.crits.dist).map(([k, v]) => `${k}회:${v}`).join(' ')}`);
  console.log(
    `게이지/턴      : 판정 순증(§3.4 정의) ${summary.gauge.judgement_net_per_turn >= 0 ? '+' : ''}${summary.gauge.judgement_net_per_turn.toFixed(2)} / 실반영 획득 +${summary.gauge.applied_gain_per_turn.toFixed(2)} / 감소 −${summary.gauge.lost_per_turn.toFixed(2)} / 초과 소실 ${summary.gauge.overflow_lost_per_turn.toFixed(2)}`,
  );
  console.log(`평균 잔여 의지 : 전체 ${summary.avg_remaining_will_all.toFixed(1)} / 승리 시 ${summary.avg_remaining_will_wins.toFixed(1)} (최대 30)`);
  if (summary.judgement_rates) {
    console.log(
      `판정 실현      : 원산지 ${pct(summary.judgement_rates.origin)} / 팩트 ${pct(summary.judgement_rates.fact)} / 일반 ${pct(summary.judgement_rates.normal)} / 헛소리 ${pct(summary.judgement_rates.fumble)} (판정 ${summary.judgement_rates.per_turn.toFixed(2)}회/턴)`,
    );
  }
  if (summary.attempted_rates) {
    console.log(
      `판정 의도      : 원산지 ${pct(summary.attempted_rates.origin)} / 팩트 ${pct(summary.attempted_rates.fact)} / 일반 ${pct(summary.attempted_rates.normal)} / 헛소리 ${pct(summary.attempted_rates.fumble)} — 우위 판정 부재 제출 ${summary.attempted_rates.fallbacks}회(${pct(summary.attempted_rates.fallback_rate)})`,
    );
  }
}

main();
