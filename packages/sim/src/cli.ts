// 전투 시뮬레이터 CLI
// 사용: npx tsx packages/sim/src/cli.ts --enemy B01 --policy standard --runs 1000 --seed 42 [--json]
//       [--deck P01,S01,...] [--deck-preset boss1] [--layer N] [--disposition-suit 계열]
//       [--will N]  ← 적 의지 오버라이드 (TBD R11: B01 60→48 하향 재시뮬 전용, YAML은 불변)
//
// 결과 해석 주의 (감사 반영):
// - 판정 비율은 "실현(achieved)"이며, 정책의 "의도(attempted)" 비율과 함께 병기된다.
//   손패 가용성 제약으로 실현치는 GDD §3.4 전제(50%/75%/30%)에 크게 못 미칠 수 있다.
// - gauge 지표 3종: judgement_net(§3.4 정의와 동일 — 판정 유래 raw 순증, 클램프 전) /
//   applied_gain(0~10 클램프 후 실반영 획득) / overflow_lost(상한 초과 소실).
// - 보스(B01) 시뮬은 시작 덱만으로는 적 본체 팩트가 구조적으로 불가(태그 교집합 공집합) —
//   --deck-preset boss1(보스 도달 기대 덱) 또는 --deck 주입 없이는 R11 재시뮬 도구로 쓰지 말 것.
// - 교착 패배(리뷰 제출 0회 패배: 접두 없는 손패 고정 — 손패 유지형 §3.1의 빈틈)는 정책 무관
//   강제 패배라 별도 집계하고, 교착 제외 조건부 승률을 병기한다. 설계 질의 대상.
// - 최소 런 수 가이드: 승률 ±2%p 판별에는 약 2,500판 필요(Wilson 95% CI 참조).
import { Battle, mulberry32, type Suit } from '../../core/src/index.ts';
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
}

// 덱 프리셋 — 가정(GDD 침묵: 보스 시뮬용 표준 덱 미정의. GDD '가정' 명시 필요, 에스컬레이션 대상):
// boss1 = "1막 보스 도달 기대 덱" = 시작 덱 12장 + 일반층 5회 보상 중 3장 획득 가정
//   (B01 약점 태그 응대/개연성 대응 접두 P11·P15 + 데미지 접미 S02 — 층당 3택1에서 보스 대비 픽)
const DECK_PRESETS: Record<string, (startingDeck: string[]) => string[]> = {
  boss1: (s) => [...s, 'P11', 'P15', 'S02'],
};

function parseArgs(argv: string[]): Args {
  const args: Args = { enemy: 'E01', policy: 'standard', runs: 1000, seed: 42, json: false, layer: 1 };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i]!;
    if (a === '--enemy') args.enemy = argv[++i]!;
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
  judgements: { fact: number; normal: number; fumble: number };
  surrender: boolean;
  deadlock: boolean; // 리뷰 제출 0회 패배 = 접두 없는 손패 교착 (정책 무관 강제 패배)
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
    (acc, x) => ({ fact: acc.fact + x.judgements.fact, normal: acc.normal + x.judgements.normal, fumble: acc.fumble + x.judgements.fumble }),
    { fact: 0, normal: 0, fumble: 0 },
  );
  const jSum = totalJ.fact + totalJ.normal + totalJ.fumble;
  const totalTurns = records.reduce((a, x) => a + x.turns, 0);
  const aSum = telemetry.attempted.fact + telemetry.attempted.normal + telemetry.attempted.fumble;
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
    // §3.4 "+1.44/턴"과 동일 정의 = 판정 유래 raw 순증(클램프 전): (팩트×2 − 헛소리×2) / 턴
    gauge: {
      judgement_net_per_turn: (totalJ.fact * 2 - totalJ.fumble * 2) / totalTurns,
      applied_gain_per_turn: records.reduce((a, x) => a + x.gaugeGained, 0) / totalTurns, // 클램프 후 실반영 획득
      lost_per_turn: records.reduce((a, x) => a + x.gaugeLost, 0) / totalTurns,
      overflow_lost_per_turn: records.reduce((a, x) => a + x.gaugeOverflowLost, 0) / totalTurns,
    },
    avg_remaining_will_all: avg(records.map((x) => x.remainingWill)),
    avg_remaining_will_wins: avg(wins.map((x) => x.remainingWill)),
    // achieved = 엔진 실현 판정 / attempted = 정책 목표 롤 (§3.4 전제와의 괴리 진단용)
    judgement_rates: jSum
      ? { fact: totalJ.fact / jSum, normal: totalJ.normal / jSum, fumble: totalJ.fumble / jSum, per_turn: jSum / totalTurns }
      : null,
    attempted_rates: aSum
      ? {
          fact: telemetry.attempted.fact / aSum,
          normal: telemetry.attempted.normal / aSum,
          fumble: telemetry.attempted.fumble / aSum,
          fallbacks: telemetry.noWantedFallbacks,
          fallback_rate: telemetry.noWantedFallbacks / aSum,
        }
      : null, // reckless(완전 무작위)는 목표 롤이 없어 미집계
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
  console.log(`교착 패배      : ${summary.deadlock_losses}판 (제출 0회 — 접두 없는 손패 고착. 교착 제외 승률 ${pct(summary.win_rate_excl_deadlock)})`);
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
      `판정 실현      : 팩트 ${pct(summary.judgement_rates.fact)} / 일반 ${pct(summary.judgement_rates.normal)} / 헛소리 ${pct(summary.judgement_rates.fumble)} (판정 ${summary.judgement_rates.per_turn.toFixed(2)}회/턴)`,
    );
  }
  if (summary.attempted_rates) {
    console.log(
      `판정 의도(롤)  : 팩트 ${pct(summary.attempted_rates.fact)} / 일반 ${pct(summary.attempted_rates.normal)} / 헛소리 ${pct(summary.attempted_rates.fumble)} — 목표 접두 부재 폴백 ${summary.attempted_rates.fallbacks}회(${pct(summary.attempted_rates.fallback_rate)})`,
    );
  }
}

main();
