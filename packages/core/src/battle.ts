// 이세계 리뷰용사 — 전투 상태머신 (GDD §2 공통 계산, §3 전투 전체)
// UI 무의존 순수 상태머신. fs/network 접근 금지, Date.now()/Math.random() 금지 — rng 주입.
//
// v2 (card-system-v2.md §2·§9, ADR-011): 접두+접미 조합 폐지 → submitReview(cardUid, opts),
// 판정 3단계 → 4단계(원산지 최우선·무효 태그 무시), modifier 적용부 전량 삭제.

import { type Rng, shuffle } from './rng.ts';
// 수치 연산은 formula 가, 밸런스 수치는 rules 가 소유한다 (ADR-025) — 여기서 다시 구현·재선언하지 않는다
import {
  applyMult,
  clampGauge,
  computeAbsorb,
  computeEnemyDamage,
  computeGaugeDelta,
  computeHeal,
  computeHealApplied,
  computeJudgement,
  computeLikes,
  computeMultipliers,
  computePhase2Threshold,
  computeReflect,
  weakenMult,
} from './formula.ts';
import { DEFAULT_RULES, mergeRules, type RulesConfig, type RulesOverride } from './rules.ts';
import {
  type Judgement,
  type CardDef,
  type CardIndex,
  type Disposition,
  type EnemyActionDef,
  type EnemyDef,
  type EnemyEffectDef,
  type PlayerEquipmentDef,
  type ReviewCardDef,
  type Suit,
  DISPOSITION_SUIT,
  BOSS_PARCEL_EQUIPMENT,
  STARTING_EQUIPMENT,
  SUIT_DISPOSITION,
} from './types.ts';


// ── 판정 표 호환 export (ADR-025) ─────────────────────
// **정본은 rules.ts 다.** 아래 3종은 기존 외부 호출자(시뮬 정책·UI)를 위한 얇은 별칭이며
// DEFAULT_RULES 를 그대로 가리킨다 — 여기에 값을 적지 않는다. 전투가 실제로 쓰는 것은
// `new Battle({ rules })` 로 확정된 인스턴스별 rules 이므로, A/B 오버라이드를 반영하려면
// 이 상수가 아니라 `battle.activeRules` 를 읽어야 한다.
/** @deprecated `DEFAULT_RULES.judge.mult` (또는 `battle.activeRules.judge.mult`) 를 쓸 것 */
export const JUDGE_MULT: Record<Judgement, number> = DEFAULT_RULES.judge.mult;
/** @deprecated `DEFAULT_RULES.judge.gauge` 를 쓸 것 */
export const JUDGE_GAUGE: Record<Judgement, number> = DEFAULT_RULES.judge.gauge;
/** @deprecated `DEFAULT_RULES.judge.heal` 를 쓸 것 */
export const JUDGE_HEAL: Record<Judgement, number> = DEFAULT_RULES.judge.heal;

// ── 상태 타입 ─────────────────────────────────────────

export interface CardInstance {
  uid: number;
  cardId: string;
}

export interface Attachment {
  kind: 'damage_buff';
  value: number;
  usesSlot: boolean;
}

export interface PlayerEquipmentState {
  def: PlayerEquipmentDef;
  attachments: Attachment[]; // 부착 슬롯 2칸 (GDD §3.9)
  /**
   * 방어 (ADR-023 ①) — 찬양 리뷰(defense_buff)가 이 장비에 쌓는 흡수량.
   * 피해를 흡수하며 소모되고, **남은 방어는 전투 내내 유지**된다(턴 리셋 없음).
   * 결정(ADR-023 근거): 방어는 부착 슬롯(GDD §3.9, 2칸)을 **쓰지 않는다** — 슬롯은
   * "부착물 개수"를 제한하는 자리인데 방어는 개별 부착물이 아니라 장비의 수치 누적이고,
   * 소모되면 사라져 슬롯을 점유·해제하는 수명 개념이 성립하지 않는다.
   * (Attachment 로 만들면 흡수로 0이 된 부착물의 슬롯 회수 시점을 따로 정의해야 한다.)
   */
  defense: number;
}

export interface EnemyEquipmentState {
  name: string;
  tags: string[];
  durability: number;
  disabledTurns: number; // S07
  dot?: { value: number; remaining: number }; // S06
  destroyed: boolean;
}

/** 플레이어가 적에게 부착한 디버프 (B01 사장님 답글의 반박 풀) */
export interface EnemyDebuff {
  uid: number;
  kind: 'attack_down' | 'attack_halve'; // attack_halve = 「힙스터 인증」 크리 (공격력 −50%)
  value: number; // attack_down의 감소량 / attack_halve는 50(위력 표기)
  suit: Suit; // 재반박 계열 매칭용
  tier: number; // 「힙스터 인증」 크리 = 3 (R22). 전투 부착 일반 디버프 = 1 (가정)
  suspended: boolean; // 사장님 답글로 "이번 전투 한정 정지"
  beenRebutted: boolean; // 디버프당 반박 1회 (once_per_debuff)
  createdAt: number; // 최근성 tiebreak
}

export interface EnemyBuff {
  uid: number;
  kind: 'attack_up';
  value: number;
  protectedBy?: string; // phase2 알바_리뷰: counter_card로만 제거 가능
  counterCard?: string; // 저격 카드 id (B02c) — remove_enemy_buff가 대조
}

export interface BattleStats {
  submissions: number;
  judgements: Record<Judgement, number>;
  gaugeGained: number; // 0~10 클램프 후 실제 반영된 획득분 (§3.4 판정 순증과는 정의가 다름 — 시뮬 해석 주의)
  gaugeLost: number;
  gaugeOverflowLost: number; // 상한 10 초과로 소실된 획득분 (GDD §2-2 "초과 소실" 계측)
  gaugeReached10: number; // 게이지 10 도달 이벤트 수 = 크리 "발동 가능" 횟수 (발동과 구분 — §3.4 검증용)
  crits: Disposition[];
  critMisses: number; // 은신 게이트에 빗나간 크리 (E04)
  surrender: boolean; // 전 장비 파괴 항복 승리
  willHealed: number; // 실제 반영된 의지 회복 총량 (maxWill 클램프 후 — 판정 회복 + 카드 heal 동반)
  defenseGained: number; // defense_buff 로 부여한 방어 총량 (판정 배율 적용 후)
  defenseAbsorbed: number; // 방어가 흡수해 의지에 닿지 않은 피해 총량 (ADR-023 밸런스 라운드 1 계측)
}

/** 온보딩 전투 보정 (GDD §3.3 버프 무판정, §4.4 1~2판 보정) — 런 레벨이 주입 */
export interface OnboardingMods {
  /** 적 공격 배율 (1판 0.75 / 2판 0.9 — §4.4). 배율이므로 §2-1 내림·최소 1 적용 */
  enemyDamageMult?: number;
  /** 헛소리 판정 게이지 증감 (1판 −1, 기본 −2 — §4.4) */
  fumbleGaugeDelta?: number;
  /** true면 버프 카드(내 장비 대상)는 무판정 = 항상 일반 (1판 한정 — §3.3) */
  buffNoJudgement?: boolean;
}

export interface BattleConfig {
  cards: CardIndex;
  enemy: EnemyDef;
  deck: string[]; // 카드 id 목록 (시작 덱 12장 등)
  rng: Rng;
  playerEquipment?: PlayerEquipmentDef[];
  gold?: number;
  /**
   * 밸런스 수치 부분 오버라이드 (ADR-025). 미지정 필드는 DEFAULT_RULES 를 따른다.
   *   new Battle({ ...cfg, rules: { judge: { mult: { normal: 0.75 } } } })
   */
  rules?: RulesOverride;
  /** 보스에게 가던 보급품 (ADR-024 ③). 주면 전투 중 openParcel() 로 개봉할 수 있다 */
  parcel?: PlayerEquipmentDef | null;
  maxTurns?: number; // 미지정 시 rules.battle.maxTurns — 초과 시 패배(timeout) 처리
  layer?: number; // 기본 1 (MVP). X09는 layer 2
  /** 논점 스냅샷용 런 누적 제출 카드 계열 카운터 (GDD §3.5 — 전투 시작 시 스냅샷 고정) */
  initialSuitCounters?: Partial<Record<Suit, number>>;
  initialLastSuit?: Suit;
  startGauge?: number; // 외부 보정 (캡 ±는 런 레벨 규칙 — 시뮬은 값 그대로 클램프만)
  sigmaP?: number; // X09용 악평 페널티 합 (Layer 2)
  onboarding?: OnboardingMods; // 온보딩 1~2판 보정 (§3.3/§4.4) — 미지정 시 정상 난이도
  noShuffle?: boolean; // 테스트 전용: 덱 순서 고정
  collectLog?: boolean;
}

export type BattleResult = 'win' | 'lose' | 'retreat' | 'timeout' | null;

/** previewSubmit 결과 — 화면이 제출 전에 보여줄 값. 규칙 계산은 전부 엔진이 소유한다. */
export interface SubmitPreview {
  judgement: Judgement | null; // blocked 가 있으면 null
  blocked: 'miss' | 'void' | 'not_review' | null; // 은신 빗나감 / 남은 구성품 없음 / 무판정 카드
  likes: number | null; // 최종 좋아요 (피해가 없는 카드는 null)
  /**
   * 그 수치가 무엇에 쓰이는가. 'defense' 는 피해가 아니라 내 장비에 붙는 방어량이다
   * (UI 표기 "방어 +6" — 좋아요 아이콘을 붙이지 말 것). ADR-023 ①
   */
  likesKind: 'will' | 'equipment' | 'defense' | null;
  gauge: number; // 신뢰도 게이지 증감 (카드 인라인 gauge 포함)
  /** 의지 회복 예상치 — maxWill 클램프를 **반영한 실제 증가분** (판정 회복 + 카드 heal 동반). ADR-023 ② */
  heal: number;
  affordable: boolean; // 지금 필력으로 낼 수 있는가
  /** 절대 수치에 걸리는 배율 = 판정 × 조건부(E03 영창 약점). 지속 턴·%·개수는 비대상 */
  mult: number;
  /** 의지 피해 전용 추가 배율 (E05 vanity) */
  vanityMult: number;
  /** 내림 뒤 더해지는 고정 가산 (원산지 +1) */
  fixedAdd: number;
}

export interface PlayerState {
  will: number;
  maxWill: number;
  energy: number;
  energyNextTurnBonus: number; // S15
  energyNextTurnPenalty: number; // B01 야근 강요
  gold: number;
  gauge: number; // 0~10 (GDD §2-2)
  equipment: PlayerEquipmentState[];
  hand: CardInstance[];
  deck: CardInstance[];
  discard: CardInstance[];
  removedFromRun: CardInstance[]; // X04 증정 (GDD §3.6)
  parcelOpened: boolean; // 택배 개봉 여부 — 전투당 1회 (ADR-024 ③)
  critUsedThisTurn: boolean;
  inconvenienceGoldUsed: boolean; // 「진상 접수」 골드 갈취 전투당 1회
  viralBonusGranted: number; // 「바이럴 확산」 가산 누적 (상한 12, 크리 간 공유 — GDD §3.5)
  x05Armed: boolean;
  storedDamageBonus: number; // X05 예약 확정분 — 다음 리뷰 1회에 가산
  damageTakenThisTurn: number;
  reaction: { weakenPct: number; reflectPct: number } | null; // X06 대기 슬롯 1
  suitCounters: Record<Suit, number>;
  lastSuit: Suit | null;
  disposition: Disposition; // 전투 시작 스냅샷 (GDD §3.5)
  oncePerCombatUsed: Set<string>; // X09 등
}

export interface EnemyState {
  def: EnemyDef;
  will: number;
  maxWill: number;
  equipment: EnemyEquipmentState[];
  buffs: EnemyBuff[];
  debuffs: EnemyDebuff[];
  stunTurns: number;
  staggerImmunityTurns: number; // 기절 해제 후 1턴 경직 내성 (GDD §3.2)
  pendingDelay: boolean; // X01
  stealth: boolean;
  stealthEverBroken: boolean; // E04 ambush if_stealth_broken 판정용 (이번 사이클)
  charging: { actionId: string; remaining: number } | null;
  weakenNextActionPct: number; // S08 (음수 %)
  damageReductionNextHit: number;
  reflectNextHit: number;
  patternIndex: number;
  intentId: string;
  phase2Done: boolean;
  /** cooldown 있는 행동의 마지막 발동 턴 (B01 사장님 답글 "3턴마다" 하한 강제용) */
  cooldownLastFired: Record<string, number>;
}

export interface BattleState {
  turn: number; // 플레이어 턴 번호 (1부터)
  result: BattleResult;
  player: PlayerState;
  enemy: EnemyState;
  stats: BattleStats;
  log: string[];
}

// ── 전투 엔진 ─────────────────────────────────────────

export class Battle {
  readonly state: BattleState;
  private readonly cards: CardIndex;
  private readonly rng: Rng;
  /** 이 전투에 확정된 밸런스 수치 (ADR-025) — 코드에 수치를 박지 않고 전부 여기를 거친다 */
  private readonly rules: RulesConfig;
  private readonly parcel: PlayerEquipmentDef | null;
  private readonly maxTurns: number;
  private readonly layer: number;
  private readonly sigmaP: number;
  private readonly onboardingEnemyMult: number;
  /** 온보딩 1판 헛소리 게이지 완화값 — 미지정이면 rules.judge.gauge.fumble 를 그대로 쓴다 */
  private readonly fumbleGaugeOverride: number | undefined;
  private readonly buffNoJudgement: boolean;
  private readonly noShuffle: boolean;
  private readonly collectLog: boolean;
  private uidSeq = 1;

  constructor(cfg: BattleConfig) {
    this.cards = cfg.cards;
    this.rng = cfg.rng;
    this.rules = mergeRules(DEFAULT_RULES, cfg.rules);
    // 보스전에만 택배가 따라온다 — 미지정이면 보스일 때 기본 보급품을 쓴다 (ADR-024 ③)
    this.parcel = cfg.parcel !== undefined
      ? cfg.parcel
      : (cfg.enemy.tier === 'boss' ? BOSS_PARCEL_EQUIPMENT : null);
    this.maxTurns = cfg.maxTurns ?? this.rules.battle.maxTurns;
    this.layer = cfg.layer ?? 1;
    this.sigmaP = cfg.sigmaP ?? 0;
    this.onboardingEnemyMult = cfg.onboarding?.enemyDamageMult ?? 1;
    this.fumbleGaugeOverride = cfg.onboarding?.fumbleGaugeDelta;
    this.buffNoJudgement = cfg.onboarding?.buffNoJudgement ?? false;
    this.noShuffle = cfg.noShuffle ?? false;
    this.collectLog = cfg.collectLog ?? false;

    const deck = cfg.deck.map((cardId) => ({ uid: this.uidSeq++, cardId }));
    if (!this.noShuffle) shuffle(deck, this.rng);

    const counters: Record<Suit, number> = {
      품질: cfg.initialSuitCounters?.품질 ?? 0,
      성능: cfg.initialSuitCounters?.성능 ?? 0,
      배송: cfg.initialSuitCounters?.배송 ?? 0,
      감성: cfg.initialSuitCounters?.감성 ?? 0,
    };

    this.state = {
      turn: 1,
      result: null,
      player: {
        will: this.rules.player.will,
        maxWill: this.rules.player.will,
        energy: this.rules.player.energyPerTurn,
        energyNextTurnBonus: 0,
        energyNextTurnPenalty: 0,
        gold: cfg.gold ?? 0,
        gauge: clampGauge(cfg.startGauge ?? 0, this.rules),
        equipment: (cfg.playerEquipment ?? STARTING_EQUIPMENT).map((def) => ({ def, attachments: [], defense: 0 })),
        hand: [],
        deck,
        discard: [],
        removedFromRun: [],
        parcelOpened: false,
        critUsedThisTurn: false,
        inconvenienceGoldUsed: false,
        viralBonusGranted: 0,
        x05Armed: false,
        storedDamageBonus: 0,
        damageTakenThisTurn: 0,
        reaction: null,
        suitCounters: counters,
        lastSuit: cfg.initialLastSuit ?? null,
        disposition: '품질 논점', // 초기값 (아래에서 스냅샷 재계산)
        oncePerCombatUsed: new Set(),
      },
      enemy: {
        def: cfg.enemy,
        will: cfg.enemy.will,
        maxWill: cfg.enemy.will,
        equipment: cfg.enemy.equipment.map((e) => ({
          name: e.name,
          tags: [...e.tags],
          durability: e.durability,
          disabledTurns: 0,
          destroyed: false,
        })),
        buffs: [],
        debuffs: [],
        stunTurns: 0,
        staggerImmunityTurns: 0,
        pendingDelay: false,
        stealth: false,
        stealthEverBroken: false,
        charging: null,
        weakenNextActionPct: 0,
        damageReductionNextHit: 0,
        reflectNextHit: 0,
        patternIndex: 0,
        intentId: cfg.enemy.pattern[0]!,
        phase2Done: false,
        cooldownLastFired: {},
      },
      stats: {
        submissions: 0,
        judgements: { origin: 0, fact: 0, normal: 0, fumble: 0 },
        gaugeGained: 0,
        gaugeLost: 0,
        gaugeOverflowLost: 0,
        gaugeReached10: 0,
        crits: [],
        critMisses: 0,
        surrender: false,
        willHealed: 0,
        defenseGained: 0,
        defenseAbsorbed: 0,
      },
      log: [],
    };

    // 논점 스냅샷 (GDD §3.5: argmax, 동률 = 최근 제출 계열, 초기값 = 품질 논점)
    this.state.player.disposition = this.computeDisposition();

    // [전투 시작] 셔플 → 손패 수만큼 드로우 → 인텐트 공개 (GDD §3.2)
    this.draw(this.rules.player.handSize);
  }

  /** 이 전투에 적용된 밸런스 수치 (읽기 전용) — 시뮬 정책·UI 가 규칙을 재선언하지 않도록 노출 */
  get activeRules(): RulesConfig {
    return this.rules;
  }

  // ── 유틸 ──

  private log(msg: string): void {
    if (this.collectLog) this.state.log.push(msg);
  }

  private computeDisposition(): Disposition {
    const p = this.state.player;
    const suits = Object.keys(p.suitCounters) as Suit[];
    const max = Math.max(...suits.map((s) => p.suitCounters[s]));
    if (max === 0) return '품질 논점';
    const top = suits.filter((s) => p.suitCounters[s] === max);
    if (top.length === 1) return SUIT_DISPOSITION[top[0]!];
    if (p.lastSuit && top.includes(p.lastSuit)) return SUIT_DISPOSITION[p.lastSuit];
    // 가정(GDD §3.5 침묵): 동률인데 최근 제출 계열이 동률군에 없거나 없음(null)이면
    // 품질→성능→배송→감성 선언 순서로 결정 (결정적. GDD에 1줄 명시 필요 — 에스컬레이션 대상)
    return SUIT_DISPOSITION[top[0]!] ?? '품질 논점';
  }

  private def(cardId: string): CardDef {
    const d = this.cards.byId.get(cardId);
    if (!d) throw new Error(`카드 정의 없음: ${cardId}`);
    return d;
  }

  private gaugeChange(delta: number): void {
    const p = this.state.player;
    const s = this.state.stats;
    const { max } = this.rules.gauge;
    const before = p.gauge;
    p.gauge = clampGauge(p.gauge + delta, this.rules); // 초과 소실 (GDD §2-2)
    const applied = p.gauge - before;
    if (applied > 0) s.gaugeGained += applied;
    if (applied < 0) s.gaugeLost += -applied;
    if (delta > 0 && before + delta > max) s.gaugeOverflowLost += before + delta - max; // 초과 소실량 계측
    if (before < max && p.gauge >= max) s.gaugeReached10++; // 크리 "발동 가능" 이벤트 (§3.4 검증용)
  }

  private draw(n: number): void {
    const p = this.state.player;
    for (let i = 0; i < n; i++) {
      if (p.hand.length >= this.rules.player.handMax) break; // 손패 상한 — 초과분 드로우 중단, 소멸 없음 (GDD §3.1)
      if (p.deck.length === 0) {
        if (p.discard.length === 0) break;
        p.deck = p.discard;
        p.discard = [];
        if (!this.noShuffle) shuffle(p.deck, this.rng); // 묘지 셔플 순환 (GDD §3.6)
      }
      p.hand.push(p.deck.pop()!);
    }
  }

  private discardFromHand(uid: number): CardInstance {
    const p = this.state.player;
    const idx = p.hand.findIndex((c) => c.uid === uid);
    if (idx < 0) throw new Error(`손패에 없음: uid ${uid}`);
    const [card] = p.hand.splice(idx, 1);
    p.discard.push(card!);
    return card!;
  }

  private checkEnd(): void {
    if (this.state.result) return;
    const e = this.state.enemy;
    if (e.will <= 0) {
      this.state.result = 'win';
      return;
    }
    // 전 장비 파괴 → 항복 = 전투 승리 + 항복 보상 골드 (combat-model-v0.1, GDD §4.2)
    if (e.equipment.length > 0 && e.equipment.every((eq) => eq.destroyed)) {
      this.state.result = 'win';
      this.state.stats.surrender = true;
      this.state.player.gold += this.rules.battle.surrenderGold;
      return;
    }
    if (this.state.player.will <= 0) this.state.result = 'lose';
  }

  private enemyAttackDownTotal(): number {
    return this.state.enemy.debuffs
      .filter((d) => d.kind === 'attack_down' && !d.suspended)
      .reduce((s, d) => s + d.value, 0);
  }

  private enemyHipsterActive(): boolean {
    return this.state.enemy.debuffs.some((d) => d.kind === 'attack_halve' && !d.suspended);
  }

  private enemyAttackUpTotal(): number {
    return this.state.enemy.buffs.reduce((s, b) => s + b.value, 0);
  }

  private dealWillDamageToEnemy(amount: number, opts: { ignoreDefense?: boolean } = {}): number {
    const e = this.state.enemy;
    let v = amount;
    if (!opts.ignoreDefense) {
      if (e.damageReductionNextHit > 0) {
        const reduced = Math.min(v, e.damageReductionNextHit);
        v -= reduced;
        e.damageReductionNextHit = 0; // next_hit 소진
      }
      if (e.reflectNextHit > 0) {
        this.playerTakeDamage(e.reflectNextHit);
        e.reflectNextHit = 0;
      }
    }
    if (v > 0) e.will -= v;
    this.checkEnd();
    return v;
  }

  /** 내 장비 방어 총합 (ADR-023 ①) */
  private defenseTotal(): number {
    return this.state.player.equipment.reduce((s, eq) => s + eq.defense, 0);
  }

  /**
   * 플레이어가 피해를 받는다 — **방어가 먼저 흡수하고 남은 만큼만 의지를 깎는다** (ADR-023 ①).
   * 분배 계산은 formula.computeAbsorb 가 소유한다(소모 순서의 결정 근거는 그쪽 주석 참조).
   * 여기 남는 것은 상태 변경뿐 — 방어 차감·의지 차감·계측.
   *
   * 가정(GDD 침묵): 흡수된 몫은 「받은 피해」가 아니다 — damageTakenThisTurn(X05 예약분 산정)에는
   * 의지가 실제로 깎인 양만 넣는다. 방어로 막았다는 건 상처가 없다는 뜻이므로.
   */
  private playerTakeDamage(v: number): void {
    if (v <= 0) return;
    const p = this.state.player;
    const { spent, absorbed, toWill } = computeAbsorb(v, p.equipment.map((eq) => eq.defense));
    // 흡수한 만큼 소모 — 남은 방어는 전투 내내 유지(턴 리셋 없음)
    for (let i = 0; i < p.equipment.length; i++) p.equipment[i]!.defense -= spent[i]!;
    this.state.stats.defenseAbsorbed += absorbed;
    if (toWill > 0) {
      p.will -= toWill;
      p.damageTakenThisTurn += toWill;
    } else {
      this.log(`방어가 좋아요 ${v}를 전부 흡수`);
    }
    this.checkEnd();
  }

  /** 의지 회복 (maxWill 클램프) — 실제 증가분을 돌려주고 stats에 누적한다 */
  private healPlayer(amount: number): number {
    const p = this.state.player;
    const applied = computeHealApplied(p.will, p.maxWill, amount);
    if (applied <= 0) return 0;
    p.will += applied;
    this.state.stats.willHealed += applied;
    return applied;
  }

  // ── 판정 (v2 4단계 — card-system-v2 §2) ──

  /**
   * ①원산지 ②헛소리 ③팩트 ④일반.
   * 원산지는 무효 태그를 무시한다 — 직접 산 사람의 증언에는 "평가 불가 항목" 반박이 통하지 않는다.
   * tag는 정확히 1개(단일 초점 원칙)라 v1의 다중 태그 some() 검사가 단순 포함 검사로 바뀐다.
   * 규칙 본체는 formula.computeJudgement 가 소유한다 — 이 메서드는 외부 호출자용 얇은 래퍼다.
   */
  judge(card: ReviewCardDef, targetTags: string[], targetNullTags: string[], isOrigin: boolean): Judgement {
    return computeJudgement(card, targetTags, targetNullTags, isOrigin);
  }

  /**
   * E04 은신 게이트 — 은신 중 명중 가능 계열(배송)이 아니면 빗나간다.
   * 제출과 미리보기가 같은 판단을 쓰도록 분리했다.
   */
  private stealthBlocks(card: ReviewCardDef): boolean {
    const e = this.state.enemy;
    const gate = e.def.stealthGate;
    return !!(
      e.stealth &&
      gate &&
      (card.target === 'enemy' || card.target === 'enemy_equipment') &&
      !gate.hittableSuits.includes(card.suit)
    );
  }

  /**
   * 대상 결정 + 원산지 판정 범위 (card-system-v2 §2):
   *   적 본체 대상 제출 → origin.enemy 일치 / 구성품 대상 제출 → origin.equipment 일치(이름 완전 일치)
   *   내 장비 대상·origin 없는 카드(Z##·X##·P해금)는 원산지 영구 미발동
   * 상태를 바꾸지 않는다 — submitReview 와 previewSubmit 의 단일 경로다.
   */
  private resolveReviewTarget(
    card: ReviewCardDef,
    opts: { enemyEquipmentIndex?: number; myEquipmentIndex?: number },
  ): {
    void: boolean; // 구성품 대상인데 남은 구성품이 없다
    targetTags: string[];
    targetNull: string[];
    isOrigin: boolean;
    myEq: PlayerEquipmentState | null;
    enemyEq: EnemyEquipmentState | null;
  } {
    const p = this.state.player;
    const e = this.state.enemy;
    const none = { targetTags: [], targetNull: [], isOrigin: false, myEq: null, enemyEq: null };

    if (card.target === 'my_equipment') {
      // 버프 카드는 반드시 내 장비 1개 대상 (GDD §3.3)
      const myEq = p.equipment[opts.myEquipmentIndex ?? 0] ?? p.equipment[0]!;
      return { ...none, void: false, targetTags: myEq.def.tags, targetNull: myEq.def.nullTags, myEq };
    }
    if (card.target === 'enemy_equipment') {
      const alive = e.equipment.filter((eq) => !eq.destroyed);
      // 가정(v1 승계): 구성품 대상 카드인데 남은 구성품이 없으면 제출 자체가 낭비(효과 없음)
      if (alive.length === 0) return { ...none, void: true };
      const picked = e.equipment[opts.enemyEquipmentIndex ?? -1];
      const enemyEq = picked && !picked.destroyed ? picked : alive[0]!;
      return {
        ...none,
        void: false,
        targetTags: enemyEq.tags,
        // 가정(v1 승계): 구성품 대상의 무효 태그는 적 본체의 무효 태그를 따른다
        targetNull: e.def.nullTags,
        isOrigin: card.origin?.equipment !== undefined && card.origin.equipment === enemyEq.name,
        enemyEq,
      };
    }
    return {
      ...none,
      void: false,
      targetTags: e.def.weaknessTags,
      targetNull: e.def.nullTags,
      isOrigin: card.origin?.enemy !== undefined && card.origin.enemy === e.def.id,
    };
  }

  /** 내 장비에 붙은 damage_buff 가산 합 — "제출당 1회" (GDD §3.3) */
  private attachDamageBuffTotal(): number {
    return this.state.player.equipment.reduce(
      (s, eq) => s + eq.attachments.filter((a) => a.kind === 'damage_buff').reduce((x, a) => x + a.value, 0),
      0,
    );
  }

  /**
   * 좋아요 환산식 (GDD §2) — 계산 본체는 formula.computeLikes. 여기서는 상태(부착 버프·X05 예약분)만 모은다.
   * 카드의 **첫** 의지 피해에만 부착 버프·고정 가산·X05 예약분이 붙는다.
   */
  private firstWillDamage(base: number, mult: number, vanityMult: number, fixedAdd: number): number {
    return computeLikes({
      base,
      attachBonus: this.attachDamageBuffTotal(),
      mult,
      vanityMult,
      fixedAdd,
      storedBonus: this.state.player.storedDamageBonus,
    });
  }

  /**
   * 판정 배율·의지 전용 배율·고정 가산 (formula.computeMultipliers).
   * previewSubmit 과 submitReview 가 이 하나를 공유한다 — 미리보기와 실제가 어긋날 자리가 없다.
   */
  private multipliersFor(card: ReviewCardDef, judgement: Judgement): { mult: number; vanityMult: number; fixedAdd: number } {
    const e = this.state.enemy;
    return computeMultipliers({
      judgement,
      cardTag: card.tag,
      cardSuit: card.suit,
      charging: !!e.charging,
      castingWeakness: e.def.castingWeakness,
      suitDamageMult: e.def.suitDamageMult,
      rules: this.rules,
    });
  }

  /** 카드가 내는 의지 피해의 기본 수치 (없으면 null) */
  private cardWillBase(card: ReviewCardDef): number | null {
    const ef = card.effect;
    if (ef.type === 'damage') return ef.value ?? 0;
    return ef.damage ?? null;
  }

  /**
   * 제출 미리보기 — 상태를 바꾸지 않고 판정·최종 좋아요·게이지를 계산한다.
   * UI 가 규칙을 재구현하지 않도록 submitReview 와 같은 경로(resolveReviewTarget·firstWillDamage)를 쓴다.
   */
  previewSubmit(cardUid: number, opts: { enemyEquipmentIndex?: number; myEquipmentIndex?: number } = {}): SubmitPreview {
    const p = this.state.player;
    const inst = p.hand.find((c) => c.uid === cardUid);
    if (!inst) throw new Error('손패에 없는 카드');
    const card = this.def(inst.cardId);
    const affordable = p.energy >= card.cost;
    // blocked 3종은 판정이 없으므로 회복도 0 (ADR-023 ② — 빗나감·void에 호응은 없다)
    if (card.kind !== 'review') {
      return { judgement: null, blocked: 'not_review', likes: null, likesKind: null, gauge: 0, heal: 0, affordable, mult: 0, vanityMult: 1, fixedAdd: 0 };
    }
    if (this.stealthBlocks(card)) {
      return { judgement: null, blocked: 'miss', likes: null, likesKind: null, gauge: 0, heal: 0, affordable, mult: 0, vanityMult: 1, fixedAdd: 0 };
    }
    const t = this.resolveReviewTarget(card, opts);
    if (t.void) {
      return { judgement: null, blocked: 'void', likes: null, likesKind: null, gauge: 0, heal: 0, affordable, mult: 0, vanityMult: 1, fixedAdd: 0 };
    }

    let judgement = this.judge(card, t.targetTags, t.targetNull, t.isOrigin);
    if (this.buffNoJudgement && card.target === 'my_equipment') judgement = 'normal';

    const { mult, vanityMult, fixedAdd } = this.multipliersFor(card, judgement);

    // 화면에 띄울 수치 — 방어 부여 > 의지 피해 > 구성품 내구도 피해(내구도도 좋아요 단위 · ADR-015)
    let likes: number | null = null;
    let likesKind: SubmitPreview['likesKind'] = null;
    const willBase = this.cardWillBase(card);
    if (card.effect.type === 'defense_buff' && t.myEq) {
      likes = applyMult(card.effect.value ?? 0, mult); // 원산지 고정 +1 비대상 (applyReviewEffect 주석 참조)
      likesKind = 'defense';
    } else if (willBase !== null) {
      likes = this.firstWillDamage(willBase, mult, vanityMult, fixedAdd);
      likesKind = 'will';
    } else if (card.effect.type === 'equipment_damage' && t.enemyEq) {
      likes = applyMult(card.effect.value ?? 0, mult) + fixedAdd; // vanity(의지 전용) 비대상
      likesKind = 'equipment';
    }

    // 게이지·회복 모두 **클램프 후 실제 증감**을 준다 (§2-2 초과 소실 / ADR-023 ② maxWill 상한).
    // 판정분과 카드 인라인분을 제출과 같은 순서로 따로 반영해야 값이 맞는다
    // (예: 게이지 0에서 헛소리 −2 → 0, 이어서 인라인 +2 → 2. 합산 후 클램프와 결과가 다르다) —
    // 그 순서 규칙을 formula.computeGaugeDelta 가 소유한다.
    const gauge = computeGaugeDelta({
      current: p.gauge,
      judgement,
      inlineGauge: card.effect.gauge ?? 0,
      fumbleOverride: this.fumbleGaugeOverride,
      rules: this.rules,
    });

    // 가정: 제출 도중 의지가 줄어드는 경우(적 반사 reflect 피격)는 미리보기가 알 수 없어 반영하지 않는다.
    const judgeHeal = computeHeal({ judgement, will: p.will, maxWill: p.maxWill, rules: this.rules });
    const cardHeal = computeHealApplied(p.will + judgeHeal, p.maxWill, card.effect.heal ?? 0); // G03 동반
    const heal = judgeHeal + cardHeal;

    return { judgement, blocked: null, likes, likesKind, gauge, heal, affordable, mult, vanityMult, fixedAdd };
  }

  // ── 리뷰 제출 (v2 — 카드 1장 = 완성 리뷰) ──

  submitReview(
    cardUid: number,
    opts: { enemyEquipmentIndex?: number; myEquipmentIndex?: number } = {},
  ): { missed: boolean; judgement: Judgement | null } {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    const p = st.player;
    const inst = p.hand.find((c) => c.uid === cardUid);
    if (!inst) throw new Error('손패에 없는 카드');
    const card = this.def(inst.cardId);
    if (card.kind !== 'review') throw new Error('리뷰 카드가 아님 (진상 화법은 playSpecial)');

    if (p.energy < card.cost) throw new Error('필력 부족');
    p.energy -= card.cost;
    this.discardFromHand(cardUid);

    st.stats.submissions++;
    p.suitCounters[card.suit]++;
    p.lastSuit = card.suit; // 스냅샷 이후의 누적 — 다음 전투용 (GDD §3.5)

    const e = st.enemy;
    const gate = e.def.stealthGate;

    // E04 은신: 은신 중에는 명중 가능 계열(배송)만 명중. 그 외는 빗나감.
    // 가정(v1 승계): 빗나간 리뷰는 판정·게이지 없이 소모만 된다 ("평가 불가" — 물건이 안 옴).
    if (this.stealthBlocks(card)) {
      this.log(`${card.name}: 은신 중 — 빗나감`);
      return { missed: true, judgement: null };
    }
    // 은신 중 명중 계열 명중 시 은신 해제
    if (e.stealth && gate && gate.breakOnHit && (card.target === 'enemy' || card.target === 'enemy_equipment')) {
      e.stealth = false;
      e.stealthEverBroken = true;
      this.log('은신 해제!');
    }

    const t = this.resolveReviewTarget(card, opts);
    if (t.void) return { missed: true, judgement: null };
    const { myEq, enemyEq } = t;

    let judgement = this.judge(card, t.targetTags, t.targetNull, t.isOrigin);
    // 온보딩 1판 한정: 버프 카드(내 장비 대상)는 무판정 = 항상 일반 (GDD §3.3)
    if (this.buffNoJudgement && card.target === 'my_equipment') judgement = 'normal';
    // 배율·고정 가산은 previewSubmit 과 같은 함수에서 나온다 (미리보기 드리프트 구조적 봉쇄)
    const { mult, vanityMult, fixedAdd } = this.multipliersFor(card, judgement);

    // 판정·게이지는 제출당 1회 (v2는 다중 히트 카드 없음). 헛소리는 온보딩 1판 완화 가능 (§4.4)
    st.stats.judgements[judgement]++;
    this.gaugeChange(
      judgement === 'fumble' && this.fumbleGaugeOverride !== undefined
        ? this.fumbleGaugeOverride
        : this.rules.judge.gauge[judgement],
    );

    // 호응 회복 (ADR-023 ②): 판정 성공 시 회복, maxWill 상한.
    // 대상 무관 — 내 장비 대상 찬양 리뷰의 팩트 판정도 회복한다("잘 쓴 글에 좋아요가 눌린다").
    this.healPlayer(this.rules.judge.heal[judgement]);

    // 재반박 (B01 counter_rebut): "같은 계열 팩트 리뷰 제출" — 해석: 원산지는 팩트의 상위 판정이므로 포함
    // (직접 산 사람의 증언이 일반 팩트보다 약한 재반박 근거일 수 없다)
    if (judgement === 'fact' || judgement === 'origin') this.tryCounterRebut(card.suit);

    // mult = 판정 배율 × E03 casting_weakness(영창 중 해당 태그 ×N) — v1의 P06 modifier를 적 특성으로 이관
    // vanityMult = E05 계열별 "의지 데미지" 배수 (내구도 등 비대상 — applyReviewEffect에서 의지 피해에만)
    this.applyReviewEffect(card, judgement, mult, vanityMult, fixedAdd, myEq, enemyEq);

    // 인라인 게이지 동반 (B02c·A04) — 가정(v1 승계): 제출당 1회, 판정 배율 미적용 고정치
    if (card.effect.gauge) this.gaugeChange(card.effect.gauge);

    this.checkEnd();
    return { missed: false, judgement };
  }

  /**
   * 리뷰 효과 적용 — 좋아요 환산식 (GDD §2):
   *   최종 좋아요 = ⌊ 기본 × 판정 배율 × 기타 배율 ⌋ + 고정 가산   (내림 1회·최소 1, 고정 가산은 내림 후)
   * - 기본 = 카드 인쇄 수치 + 부착 버프 가산("제출당 1회" — GDD §3.3, 카드의 첫 의지 피해에만)
   * - 기타 배율 = casting_weakness(E03, mult에 합산) × vanity(E05, 의지 피해에만)
   * - 고정 가산 = 원산지 +1(card-system-v2 §2) + X05 예약분. 배율의 영향을 받지 않고 내림 후 더한다.
   *   해석: 카드의 첫 피해 산출 1회에 적용 — 의지 피해 우선, 의지 피해가 없으면 구성품 내구도 피해
   *   (내구도도 좋아요 단위 — ADR-015). 피해가 전혀 없는 카드(기절·버프·도트)에선 소멸.
   * - 판정 배율은 절대 피해 수치에만: 지속 턴·%·개수·드로우 장수·회복량은 판정 무관 (v1 정교화 승계)
   */
  private applyReviewEffect(
    card: ReviewCardDef,
    judgement: Judgement,
    mult: number, // 판정 × casting_weakness
    vanityMult: number,
    originFixedAdd: number, // 원산지 고정 좋아요 (multipliersFor 산출 — 첫 피해 1회에만 소비)
    myEq: PlayerEquipmentState | null,
    enemyEq: EnemyEquipmentState | null,
  ): void {
    const st = this.state;
    const p = st.player;
    const e = st.enemy;
    const ef = card.effect;
    const d = this.rules.effectDefaults;

    let fixedAdd = originFixedAdd;

    // 환산식은 firstWillDamage 가 소유한다 (previewSubmit 과 공유 — 미리보기가 규칙을 재구현하지 않도록)
    const dealCardWillDamage = (base: number): void => {
      const dmg = this.firstWillDamage(base, mult, vanityMult, fixedAdd);
      fixedAdd = 0;
      p.storedDamageBonus = 0; // X05 예약분은 첫 피해에서 소진 (GDD §2)
      this.dealWillDamageToEnemy(dmg);
    };

    switch (ef.type) {
      case 'damage': {
        dealCardWillDamage(ef.value ?? 0);
        if (st.result) break;
        if (ef.weaken_next_action !== undefined) e.weakenNextActionPct = ef.weaken_next_action; // C02c 동반
        break;
      }
      case 'delay_enemy_action': {
        // O02·L01·W02 (X01은 playSpecial 경유 동일 로직)
        if (ef.damage !== undefined) dealCardWillDamage(ef.damage);
        if (st.result) break;
        this.applyDelayToEnemy();
        break;
      }
      case 'stun': {
        // L03·W03 — v2는 기절 턴 수를 value로 표기. 경직 내성 면역 (GDD §3.2)
        if (ef.damage !== undefined) dealCardWillDamage(ef.damage);
        if (st.result) break;
        if (e.staggerImmunityTurns > 0) this.log('경직 내성 — 기절 무효');
        else e.stunTurns = Math.max(e.stunTurns, ef.value ?? d.stunTurns);
        break;
      }
      case 'weaken_next_action': {
        // D02·B03c·K04 — %는 판정 무관 고정 (배율은 절대 수치에만 — v1 정교화 승계)
        if (ef.damage !== undefined) dealCardWillDamage(ef.damage);
        if (st.result) break;
        e.weakenNextActionPct = ef.value ?? d.weakenNextActionPct;
        break;
      }
      case 'remove_enemy_buff': {
        // O03·N02·B02c — 개수는 판정 무관. phase2 알바_리뷰(protectedBy)는 counter_card 일치 카드로만 제거
        if (ef.damage !== undefined) dealCardWillDamage(ef.damage);
        if (st.result) break;
        for (let i = 0; i < (ef.value ?? d.removeBuffCount); i++) {
          const idx = [...e.buffs]
            .map((b, k) => ({ b, k }))
            .filter(({ b }) => !b.protectedBy || b.counterCard === card.id)
            .map(({ k }) => k)
            .pop();
          if (idx === undefined) {
            // 가정(v1 승계): 제거할 버프가 없으면 다음 받는 피해 감소/반사(포즈·마나 실드)를 대신 해제
            if (e.damageReductionNextHit > 0 || e.reflectNextHit > 0) {
              e.damageReductionNextHit = 0;
              e.reflectNextHit = 0;
            }
            break;
          }
          e.buffs.splice(idx, 1);
        }
        break;
      }
      case 'equipment_damage': {
        // Q03·C03c — 내구도도 좋아요 단위 (ADR-015): 판정 배율 + 원산지 고정 +1 적용. vanity(의지 전용)는 비대상
        if (!enemyEq) break;
        const dmg = applyMult(ef.value ?? 0, mult) + fixedAdd;
        fixedAdd = 0;
        this.damageEnemyEquipment(enemyEq, dmg);
        break;
      }
      case 'equipment_dot': {
        // M03·A02 — 가정(v1 승계): 판정 배율은 틱 값에 적용(지속 턴 미적용), 기존 도트는 갱신(중첩 없음).
        // 원산지 +1은 미적용 (즉발 피해가 아님 — 해석)
        if (!enemyEq) break;
        const dur = typeof ef.duration === 'number' ? ef.duration : d.dotDuration;
        enemyEq.dot = { value: applyMult(ef.value ?? 0, mult), remaining: dur };
        break;
      }
      case 'attack_down': {
        // K01 — 판정 적중 시 수치 강화 (GDD §3.3). duration: combat은 전투 스코프 상태라 별도 처리 불요
        e.debuffs.push({
          uid: this.uidSeq++,
          kind: 'attack_down',
          value: applyMult(ef.value ?? 0, mult),
          suit: card.suit,
          tier: this.rules.battle.attachedDebuffTier, // 가정(v1 승계): 전투 중 부착 일반 디버프 (「힙스터 인증」 크리만 별도 등급)
          suspended: false,
          beenRebutted: false,
          createdAt: this.uidSeq,
        });
        break;
      }
      case 'disable_equipment': {
        // v2 실데이터 미사용 (YAML 스키마 예비). 경직 내성 면역 — S07 무한 락 봉쇄 규칙 유지
        if (!enemyEq) break;
        if (e.staggerImmunityTurns > 0) {
          this.log('경직 내성 — 비활성화 무효');
          break;
        }
        const dur = typeof ef.duration === 'number' ? ef.duration : d.disableDuration;
        enemyEq.disabledTurns = Math.max(enemyEq.disabledTurns, dur);
        break;
      }
      case 'damage_buff': {
        // D03·N03·A01 — 부착은 슬롯 2칸 점유 (GDD §3.9).
        // v2 결정: 리뷰 유래 부착은 전부 슬롯 사용 (v1 uses_attach_slot 필드는 YAML에서 사라짐), 크리 산출물만 예외
        if (!myEq) break;
        const used = myEq.attachments.filter((a) => a.usesSlot).length;
        if (used >= this.rules.player.attachSlots) {
          this.log('부착 슬롯 가득 참 — 부착 실패'); // GDD §3.9 (R15)
          break;
        }
        myEq.attachments.push({ kind: 'damage_buff', value: applyMult(ef.value ?? 0, mult), usesSlot: true });
        break;
      }
      case 'defense_buff': {
        // ADR-023 ① — 찬양 리뷰(★4~5)가 내 장비에 방어를 부여한다.
        // · 판정 배율을 받는다: 카드 태그가 그 장비 태그에 맞으면 팩트 ×1.5 (GDD §3.3 찬양 규칙과 동일 경로).
        //   mult 에는 E03 casting_weakness 조건 배율도 이미 곱해져 있다(다른 효과와 동일 취급).
        // · **원산지 고정 +1은 적용하지 않는다** — 고정 가산은 GDD §2 좋아요 환산식의 항이고,
        //   그 대상은 의지·내구도 피해(= 좋아요)다. 방어는 좋아요가 아니라 내 장비의 수치이므로
        //   환산식 밖이다. 어차피 내 장비 대상 카드는 원산지 판정이 영구 미발동이라(card-system-v2 §2)
        //   현재 데이터에선 도달 불가 경로지만, 해석을 명시해 둔다.
        // · vanity(E05 계열별 의지 피해 배수)도 비대상 — 적에게 가는 피해 전용이다.
        // · 부착 슬롯(GDD §3.9) 미사용 — PlayerEquipmentState.defense 주석의 결정 근거 참조.
        if (!myEq) break;
        const gain = applyMult(ef.value ?? 0, mult);
        myEq.defense += gain;
        st.stats.defenseGained += gain;
        break;
      }
      default:
        throw new Error(`미구현 리뷰 effect: ${ef.type}`);
    }

    if (st.result) return;
    // 동반 효과 (판정 배율 미적용 — 드로우 장수·회복량은 절대 피해 수치가 아님)
    if (ef.draw) this.draw(ef.draw); // Z03·A01
    if (ef.heal) this.healPlayer(ef.heal); // G03 (판정 회복과 같은 클램프 경로 — stats.willHealed 합산)
    void judgement;
  }

  /**
   * 지연 적용 공용 경로 (X01·O02·L01·W02 — GDD §3.2). 경직 내성이면 면역.
   * 준비(charge) 중 지연 적중 시 cancel_on 검사 — 선재 버그 수정: enemies-v1.0의 표기는
   * 'delay_enemy_action'인데 구현이 구 표기 '지연'만 비교해 E02 내려찍기 캔슬이 불발했다. 양쪽 지원.
   */
  private applyDelayToEnemy(): void {
    const e = this.state.enemy;
    if (e.staggerImmunityTurns > 0) {
      this.log('경직 내성 — 지연 무효');
      return;
    }
    if (e.charging) {
      const chargingAction = e.def.actions.find((a) => a.id === e.charging!.actionId);
      if (chargingAction?.cancelOn.some((c) => c === 'delay_enemy_action' || c === '지연')) {
        // E02 내려찍기: 준비 중 지연 적중 시 발동 캔슬 (행동 소멸, 패턴 진행)
        e.charging = null;
        this.advancePattern();
        this.log('준비 행동 캔슬!');
        return;
      }
      e.pendingDelay = true; // 준비만 1턴 늦춤
      return;
    }
    e.pendingDelay = true;
  }

  private damageEnemyEquipment(eq: EnemyEquipmentState, dmg: number): void {
    eq.durability -= dmg;
    if (eq.durability <= 0) {
      eq.durability = 0;
      eq.destroyed = true;
      this.log(`장비 파괴: ${eq.name}`);
    }
    this.checkEnd(); // 전 장비 파괴 → 항복
  }

  /**
   * 재반박: 정지된 디버프와 같은 계열의 팩트 리뷰 → 부활 + 게이지 +1 (§3.4/§3.8). 제출당 1개 (가정)
   * 가정(문언 준수): §3.8·enemies-v1.0 counter_rebut 조건은 "같은 계열 팩트 판정 리뷰 제출"이 전부라
   * 리뷰의 대상은 보지 않는다 — 내 장비 대상 찬양 리뷰(★4~5, 예: N03)의 팩트 판정도 재반박 성립.
   * 대상 제한(적 대상 리뷰만)을 둘지는 GDD 명시 필요(에스컬레이션 대상).
   */
  private tryCounterRebut(suit: Suit): void {
    const target = this.state.enemy.debuffs.find((d) => d.suspended && d.suit === suit);
    if (!target) return;
    target.suspended = false;
    this.gaugeChange(this.rules.gauge.counterRebutGain); // 재반박 성공 (GDD §3.4)
    this.log(`재반박 성공: ${target.kind} 부활`);
  }

  // ── 특수 카드 (단독 사용, 무판정) ──

  playSpecial(uid: number, opts: { giftUid?: number } = {}): void {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    const p = st.player;
    const card = p.hand.find((c) => c.uid === uid);
    if (!card) throw new Error('손패에 없는 카드');
    const spec = this.def(card.cardId);
    if (spec.kind !== 'special') throw new Error('특수 카드가 아님');
    if (spec.layer > this.layer) throw new Error(`Layer ${spec.layer} 카드 — 현재 Layer ${this.layer}`);
    if (p.energy < spec.cost) throw new Error('필력 부족');
    if (spec.oncePerCombat && p.oncePerCombatUsed.has(spec.id)) throw new Error('전투당 1회 소진');

    const e = st.enemy;
    const ef = spec.effect;
    const d = this.rules.effectDefaults;

    // X04는 증정 대상 확인을 지불 전에
    let giftCard: CardInstance | undefined;
    if (ef.type === 'gift_card') {
      giftCard = p.hand.find((c) => c.uid === opts.giftUid && c.uid !== uid);
      if (!giftCard) throw new Error('증정할 카드 지정 필요');
    }

    p.energy -= spec.cost;
    this.discardFromHand(uid);
    if (spec.oncePerCombat) p.oncePerCombatUsed.add(spec.id);

    switch (ef.type) {
      case 'delay_enemy_action': {
        // X01 지연 (GDD §3.2) — 리뷰 카드 지연(O02 등)과 공용 경로 (경직 내성 면역·cancel_on 캔슬)
        this.applyDelayToEnemy();
        break;
      }
      case 'damage': {
        // X02 별점 테러 — 무판정: 판정 배율·부착 버프 가산 비대상 (진상 화법은 팩트 원칙 바깥 — worldview §1.1).
        // 가정(v1 승계): 특수 카드는 "리뷰"가 아니므로 E04 은신 게이트("리뷰만 명중")의 비대상 — 은신 중에도 적용.
        this.dealWillDamageToEnemy(ef.value ?? 0);
        break;
      }
      case 'equipment_damage': {
        // v2 실데이터 미사용 (v1 X02 전체 장비 −3 잔재 — 스키마 예비로 유지)
        const dmg = ef.value ?? 0;
        for (const eq of e.equipment.filter((q) => !q.destroyed)) this.damageEnemyEquipment(eq, dmg);
        break;
      }
      case 'create_card': {
        // X03: pool: any — 전체 카드 풀에서 무작위 생성 (현재 레이어 초과 카드 제외)
        const pool = this.cards.allIds.filter((id) => {
          const d = this.cards.byId.get(id);
          return d !== undefined && d.layer <= this.layer;
        });
        for (let i = 0; i < (ef.value ?? d.createCardCount) && p.hand.length < this.rules.player.handMax && pool.length > 0; i++) {
          const id = pool[Math.floor(this.rng() * pool.length)]!;
          p.hand.push({ uid: this.uidSeq++, cardId: id });
        }
        break;
      }
      case 'gift_card': {
        // X04: 증정 카드는 "런 동안" 제외 (GDD §3.6), 비용 ×multiplier 의지 데미지
        const gDef = this.def(giftCard!.cardId);
        const idx = p.hand.findIndex((c) => c.uid === giftCard!.uid);
        p.hand.splice(idx, 1);
        p.removedFromRun.push(giftCard!);
        // 가정(GDD 침묵): 0코스트 증정도 §2-1 "최소 1" 적용 → 최소 1 데미지
        this.dealWillDamageToEnemy(applyMult(gDef.cost, ef.multiplier ?? d.giftMultiplier));
        break;
      }
      case 'store_damage_taken': {
        p.x05Armed = true; // 이번 턴 받은 피해량 → 다음 턴 시작 시 예약 확정 (GDD §3.2 step1)
        break;
      }
      case 'reaction_counter': {
        // X06: 설치형, 대기 슬롯 1 (GDD §3.2)
        if (p.reaction) throw new Error('리액션 대기 슬롯 사용 중');
        p.reaction = { weakenPct: ef.weaken_pct ?? d.reactionWeakenPct, reflectPct: ef.reflect_pct ?? d.reactionReflectPct };
        break;
      }
      case 'retreat': {
        // X07: 전투 이탈 (보상 포기). v2 YAML엔 condition이 없어 전 전투 허용 — 있으면 v1 규칙 존중
        if (ef.condition === 'normal_battle_only' && e.def.tier !== 'normal') throw new Error('일반 전투에서만 이탈 가능');
        st.result = 'retreat';
        break;
      }
      case 'gauge': {
        this.gaugeChange(ef.value ?? 0); // X08 별점 구걸 +3 (v2)
        break;
      }
      case 'damage_per_penalty': {
        // X09 (Layer 2): Σp(상한 cap_points) × per_point
        const sp = Math.min(this.sigmaP, ef.cap_points ?? d.penaltyCapPoints);
        if (sp > 0) this.dealWillDamageToEnemy(sp * (ef.per_point ?? d.penaltyPerPoint));
        break;
      }
      default:
        throw new Error(`미구현 특수 effect: ${ef.type}`);
    }

    if (ef.gauge) this.gaugeChange(ef.gauge);
    this.checkEnd();
  }

  // ── 퇴고 (GDD §3.2 v1.1 신설 → v2에서 태그 사냥 도구로 승격 — card-system-v2 §7) ──

  /** 손패 1장을 버리고 1장 드로우(비용 = rules.player.reviseCost). 턴 제한 없음(필력이 상한). v2엔 교착이 없어 원하는 태그·원산지를 찾는 용도 */
  revise(uid: number): void {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    const p = st.player;
    const cost = this.rules.player.reviseCost;
    if (p.energy < cost) throw new Error('필력 부족');
    if (p.deck.length + p.discard.length === 0) throw new Error('뽑을 카드 없음');
    p.energy -= cost;
    this.discardFromHand(uid);
    this.draw(this.rules.player.reviseDraw);
    this.log('퇴고 — 손패 1장 교체');
  }

  /** 개봉할 택배가 남아 있는가 — UI 가 버튼 노출을 판단한다 */
  get parcelAvailable(): boolean {
    return this.parcel !== null && !this.state.player.parcelOpened;
  }

  /**
   * 택배 개봉 (ADR-024 ③) — 보스에게 가던 보급품을 뜯어 내 장비로 쓴다.
   * 필력을 쓰므로 **언제 여는가가 결정이 된다** — 일찍 열면 찬양 리뷰를 오래 굴리고,
   * 미루면 그 턴의 필력을 딜에 쓴다. 전투당 1회.
   */
  openParcel(): PlayerEquipmentDef {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    if (this.parcel === null) throw new Error('개봉할 택배가 없다');
    const p = st.player;
    if (p.parcelOpened) throw new Error('이미 개봉했다');
    const cost = this.rules.player.parcelCost;
    if (p.energy < cost) throw new Error('필력 부족');
    p.energy -= cost;
    p.parcelOpened = true;
    p.equipment.push({ def: this.parcel, attachments: [], defense: 0 });
    this.log(`택배 개봉 — ${this.parcel.name} 입수 (내 장비)`);
    return this.parcel;
  }

  // ── 크리티컬 리뷰 (GDD §3.5) ──

  useCritical(): Disposition {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    const p = st.player;
    const crit = this.rules.critical;
    if (p.gauge < this.rules.gauge.max) throw new Error('게이지 부족');
    if (p.critUsedThisTurn) throw new Error('크리티컬은 턴당 1회');
    const spent = p.gauge;
    p.gauge = this.rules.gauge.min; // 게이지 전량 소모 (에너지 비용 0)
    st.stats.gaugeLost += spent;
    p.critUsedThisTurn = true;
    const d = p.disposition; // 전투 시작 스냅샷 (GDD §3.5)
    st.stats.crits.push(d);
    const e = st.enemy;

    // E04 은신 게이트 (§3.8 특성 문언 "은신 중에는 배송/CS 계열 리뷰만 명중" — 크리티컬 리뷰도 리뷰다)
    // 가정(GDD 침묵): 적을 향하지 않는 크리(감성 논점 = 내 버프 대상)는 은신 무관.
    // 빗나간 크리도 게이지·턴 사용은 소모된다(빗나간 일반 리뷰가 필력·카드를 소모하는 것과 일관).
    const gate = e.def.stealthGate;
    if (e.stealth && gate && d !== '감성 논점') {
      if (!gate.hittableSuits.includes(DISPOSITION_SUIT[d])) {
        st.stats.critMisses++;
        this.log('은신 중 — 크리티컬 리뷰 빗나감');
        return d;
      }
      if (gate.breakOnHit) {
        e.stealth = false;
        e.stealthEverBroken = true;
        this.log('은신 해제! (크리티컬 리뷰 명중)');
      }
    }

    switch (d) {
      case '품질 논점':
        // 방어·저항 무시 고정 피해 — 가정(GDD 침묵): 반사(포즈)도 무시
        this.dealWillDamageToEnemy(crit.factBomberDamage, { ignoreDefense: true });
        break;
      case '성능 논점':
        e.debuffs.push({
          uid: this.uidSeq++,
          kind: 'attack_halve',
          value: crit.hipsterAttackDownPct,
          suit: '성능',
          tier: crit.hipsterTier, // 사장님 답글 최우선 반박 대상 (R22)
          suspended: false,
          beenRebutted: false,
          createdAt: this.uidSeq,
        });
        break;
      case '배송 논점': {
        if (e.staggerImmunityTurns > 0) this.log('경직 내성 — 크리 기절 무효');
        else e.stunTurns = Math.max(e.stunTurns, crit.inconvenienceStunTurns);
        // v1.1(제안 6): 기절과 별개로 다음 행동 위력 감소 — 기절 면역(경직 내성)·기믹 대상에도
        // 크리 가치가 남도록 피해 등가 보강 (보스전 등가 재설계)
        e.weakenNextActionPct = Math.min(e.weakenNextActionPct, crit.inconvenienceWeakenPct);
        // GDD §3.5 (v1.1 명문화): "전투당 1회"는 골드 갈취에만 적용, 기절·위력 감소는 크리마다
        if (!p.inconvenienceGoldUsed) {
          p.gold += crit.inconvenienceGold[e.def.tier];
          p.inconvenienceGoldUsed = true;
        }
        break;
      }
      case '감성 논점': {
        // 현재 버프 효과 2배 — 가산 합산 상한, 크리 간 상한 공유 (GDD §3.5)
        let budget = crit.viralBonusCap - p.viralBonusGranted;
        const hasBuff = p.equipment.some((eq) => eq.attachments.some((a) => a.kind === 'damage_buff'));
        if (!hasBuff) {
          // v1.1(제안 5): 바닥 보장 — 버프 0개면 S13 상당 가산 버프 1개 즉시 부착
          // (크리 산출물이므로 부착 슬롯 미점유, 상한 공유)
          const add = Math.min(crit.viralFloorBonus, budget);
          if (add > 0 && p.equipment[0]) {
            p.equipment[0].attachments.push({ kind: 'damage_buff', value: add, usesSlot: false });
            p.viralBonusGranted += add;
          }
          break;
        }
        for (const eq of p.equipment) {
          for (const a of eq.attachments) {
            if (a.kind !== 'damage_buff' || budget <= 0) continue;
            const add = Math.min(a.value, budget);
            a.value += add;
            budget -= add;
            p.viralBonusGranted += add;
          }
        }
        break;
      }
    }
    this.checkEnd();
    return d;
  }

  // ── 턴 종료 → 적 턴 → 다음 플레이어 턴 (GDD §3.2) ──

  endTurn(): void {
    const st = this.state;
    if (st.result) return;
    this.enemyTurn();
    if (st.result) return;
    this.startPlayerTurn();
  }

  private advancePattern(): void {
    const e = this.state.enemy;
    e.patternIndex = (e.patternIndex + 1) % e.def.pattern.length;
    e.intentId = e.def.pattern[e.patternIndex]!;
  }

  private enemyTurn(): void {
    const st = this.state;
    const e = st.enemy;
    const intent = e.def.actions.find((a) => a.id === e.intentId);
    if (!intent) throw new Error(`행동 정의 없음: ${e.intentId}`);

    if (e.pendingDelay) {
      // X01 지연: 이번 행동 스킵, 인텐트 유지 (가정: 지연 = 1턴 늦춤, 행동 소멸 아님)
      e.pendingDelay = false;
      this.log(`지연 — ${intent.name} 스킵`);
    } else if (e.stunTurns > 0 && intent.aType !== 'gimmick') {
      // 기절: attack/buff/steal/stealth 불가. gimmick은 기절 무시 (GDD §3.2)
      // 가정(GDD 침묵): 기절로 막힌 행동은 소멸하고 패턴은 진행된다.
      this.log(`기절 — ${intent.name} 불발`);
      if (e.charging) e.charging = null;
      this.advancePattern();
    } else if (intent.aType === 'gimmick' && this.gimmickOnCooldown(intent)) {
      // 가정(GDD 침묵): cooldown("N턴마다 발동")은 하한으로 강제 — 기절 불발 등으로 패턴이
      // 앞당겨져도 마지막 발동 후 N턴 전엔 불발(패턴 진행). 정상 패턴(길이 3)에서는 무영향.
      this.log(`${intent.name} — 재사용 대기(cooldown)`);
      this.advancePattern();
    } else if (this.allEquipmentDisabled()) {
      // S07 비활성화: 해당 행동 봉인 — 가정(GDD 침묵): 장비↔행동 매핑 미정의라
      // "활성 장비가 하나도 없으면 그 턴 비(非)기믹 행동 봉인"으로 해석.
      // 가정(§3.2 악용 #3 봉쇄 의도 준용): 봉인 불발된 적은 장비 재활성 후 1턴간 경직 내성
      // (기절·지연·비활성화 면역) — S07 매턴 재시전 무한 락(S09 락과 동형) 봉쇄. GDD 개정 필요.
      if (intent.aType !== 'gimmick') {
        this.log(`장비 비활성 — ${intent.name} 봉인`);
        // 이번 턴 정리에서 1 감소 → 다음 플레이어 턴까지 유지 (rules.battle.equipmentLockImmunityTurns 주석 참조)
        e.staggerImmunityTurns = Math.max(e.staggerImmunityTurns, this.rules.battle.equipmentLockImmunityTurns);
        this.advancePattern();
      } else {
        this.executeEnemyAction(intent);
      }
    } else {
      this.executeEnemyAction(intent);
    }
    if (st.result) return;

    // 7. 정리: 지속효과 tick, 기믹 카운터, 다음 인텐트 공개 (GDD §3.2)
    for (const eq of e.equipment) if (eq.disabledTurns > 0) eq.disabledTurns--;
    if (e.staggerImmunityTurns > 0) e.staggerImmunityTurns--;
    if (e.stunTurns > 0) {
      e.stunTurns--;
      if (e.stunTurns === 0) e.staggerImmunityTurns = this.rules.battle.staggerImmunityTurns; // 기상 → 경직 내성 (GDD §3.2)
    }
    // 보스 페이즈2 (B01 리뷰 조작): 의지 트리거 도달 시 1회 발동 — 가정: 정리 단계에서 체크
    // v1.1(제안 3): 비례 트리거("의지 N% 이하") 우선 — 의지 하향이 실제 완화가 되게 함 (R11 해소)
    if (e.def.phase2 && !e.phase2Done && e.will <= this.phase2Threshold(e)) {
      e.phase2Done = true;
      for (const ef of e.def.phase2.effects) this.applyEnemyEffect(ef, { weaken: 1, reactionApplied: false });
      this.log('페이즈2: 리뷰 조작');
    }
  }

  /** 페이즈2 발동 문턱 (v1.1): triggerPct는 maxWill 비례(내림), 없으면 절대값 triggerWill */
  private phase2Threshold(e: EnemyState): number {
    return computePhase2Threshold(e.maxWill, e.def.phase2!);
  }

  private allEquipmentDisabled(): boolean {
    const alive = this.state.enemy.equipment.filter((eq) => !eq.destroyed);
    return alive.length > 0 && alive.every((eq) => eq.disabledTurns > 0);
  }

  /** cooldown 있는 행동이 아직 재사용 대기인지 (마지막 발동 턴 기준 — enemies-v1.0 "N턴마다 발동") */
  private gimmickOnCooldown(intent: EnemyActionDef): boolean {
    if (intent.cooldown === undefined) return false;
    const last = this.state.enemy.cooldownLastFired[intent.id];
    return last !== undefined && this.state.turn - last < intent.cooldown;
  }

  private executeEnemyAction(intent: EnemyActionDef): void {
    const e = this.state.enemy;
    // 준비(charge) 행동: 준비 턴 → 발동 턴 (2턴 점유)
    if (intent.chargeTurns > 0) {
      if (!e.charging || e.charging.actionId !== intent.id) {
        e.charging = { actionId: intent.id, remaining: intent.chargeTurns };
        this.log(`${intent.name} 준비 중…`);
        return; // 패턴 유지 — 발동 시 진행
      }
      e.charging.remaining--;
      if (e.charging.remaining > 0) return;
      e.charging = null;
    }
    this.fireEnemyAction(intent);
    this.advancePattern();
  }

  private fireEnemyAction(intent: EnemyActionDef): void {
    const st = this.state;
    const p = st.player;
    const e = st.enemy;

    if (intent.cooldown !== undefined) e.cooldownLastFired[intent.id] = st.turn; // cooldown 하한 강제용 기록

    // 행동 단위 위력 보정: S08(다음 행동 −50%) + X06 리액션(attack에만, −50% 후 반사)
    let weaken = 1;
    if (e.weakenNextActionPct !== 0) {
      weaken *= weakenMult(e.weakenNextActionPct);
      e.weakenNextActionPct = 0; // 소진
    }
    let reaction: PlayerState['reaction'] = null;
    if (intent.aType === 'attack' && p.reaction) {
      // 가정(GDD §3.2 문언 준수): type: attack이면 데미지 0인 행동(E05 찬양 강요)에도 리액션 발동·소진
      reaction = p.reaction;
      p.reaction = null;
      weaken *= weakenMult(reaction.weakenPct);
    }

    let noRebutTarget = false;
    for (const ef of intent.effects) {
      if (ef.condition === 'no_rebut_target' && !noRebutTarget) continue;
      const result = this.applyEnemyEffect(ef, { weaken, reactionApplied: !!reaction, reflectPct: reaction?.reflectPct ?? 0 });
      if (result === 'no_rebut_target') noRebutTarget = true;
      if (st.result) return;
    }
  }

  private applyEnemyEffect(
    ef: EnemyEffectDef,
    ctx: { weaken: number; reactionApplied: boolean; reflectPct?: number },
  ): 'no_rebut_target' | void {
    const st = this.state;
    const p = st.player;
    const e = st.enemy;

    switch (ef.op) {
      case 'damage': {
        // E04 기습: 은신이 해제된 상태면 if_stealth_broken 값으로 (가정: 행동 시점에 은신 아님 = 해제됨)
        let base = ef.value ?? 0;
        if (ef.if_stealth_broken !== undefined && !e.stealth) base = ef.if_stealth_broken;
        // 가·감산과 배율 순서는 formula.computeEnemyDamage 가 소유한다 (「힙스터 인증」 크리 → S08/X06 → 온보딩)
        const v = computeEnemyDamage({
          base,
          attackUp: this.enemyAttackUpTotal(),
          attackDown: this.enemyAttackDownTotal(),
          hipsterActive: this.enemyHipsterActive(),
          weaken: ctx.weaken,
          onboardingMult: this.onboardingEnemyMult, // §4.4 적 공격 배율
          rules: this.rules,
        });
        this.playerTakeDamage(v);
        if (ctx.reactionApplied && ctx.reflectPct) {
          const refl = computeReflect(v, ctx.reflectPct); // 받은 피해의 N% 반사 (X06)
          if (refl > 0) {
            e.will -= refl;
            this.checkEnd();
          }
        }
        // E04: 기습 후 은신 종료·해제 플래그 리셋 (다음 사이클 다시 은신)
        if (ef.if_stealth_broken !== undefined) {
          e.stealth = false;
          e.stealthEverBroken = false;
        }
        break;
      }
      case 'gold_steal': {
        const floor = ef.floor ?? 0;
        p.gold = Math.max(floor, p.gold - (ef.value ?? 0)); // 골드 하한 0 (GDD §3.8)
        break;
      }
      case 'attack_up': {
        e.buffs.push({
          uid: this.uidSeq++,
          kind: 'attack_up',
          value: ef.value ?? 0,
          protectedBy: ef.attachment as string | undefined,
          counterCard: ef.counter_card as string | undefined, // phase2 알바_리뷰 → B02c로만 제거
        });
        break;
      }
      case 'damage_reduction': {
        e.damageReductionNextHit = ef.value ?? 0;
        break;
      }
      case 'reflect': {
        e.reflectNextHit = ef.value ?? 0;
        break;
      }
      case 'gauge_down': {
        this.gaugeChange(-(ef.value ?? 0)); // E05 찬양 강요 (GDD §3.4)
        break;
      }
      case 'gauge_up': {
        this.gaugeChange(ef.value ?? 0);
        break;
      }
      case 'energy_down': {
        if (ef.when === 'next_turn') p.energyNextTurnPenalty += ef.value ?? 0; // B01 야근 강요
        break;
      }
      case 'stealth_on': {
        e.stealth = true;
        e.stealthEverBroken = false;
        break;
      }
      case 'heal': {
        e.will = Math.min(e.maxWill, e.will + (ef.value ?? 0));
        break;
      }
      case 'rebut_debuff': {
        // B01 사장님 답글 (GDD §3.8, R22): 활성 디버프 중 Tier/위력 최상위를 "이번 전투 한정 정지"
        const pool = e.debuffs.filter((d) => !d.suspended && !d.beenRebutted);
        if (pool.length === 0) return 'no_rebut_target'; // → 의지 +5 효과로
        pool.sort((a, b) => b.tier - a.tier || b.value - a.value || b.createdAt - a.createdAt); // 동률: (좋아요 — 시뮬 밖) → 최근
        const target = pool[0]!;
        target.suspended = true;
        target.beenRebutted = true; // 디버프당 반박 1회 — 재반박 부활 후 다시 반박 불가
        this.log(`사장님 답글: ${target.kind} 정지`);
        break;
      }
      default:
        throw new Error(`미구현 적 effect op: ${ef.op}`);
    }
  }

  private startPlayerTurn(): void {
    const st = this.state;
    const p = st.player;
    const e = st.enemy;
    st.turn++;
    if (st.turn > this.maxTurns) {
      st.turn = this.maxTurns; // 기록 턴 클램프 (통계 off-by-one 방지 — 실제 진행된 마지막 턴)
      st.result = 'timeout';
      return;
    }
    p.critUsedThisTurn = false;

    // 1. 턴 시작: 내 도트 발동·만료 (S06), 예약 효과(X05) — GDD §3.2
    for (const eq of e.equipment) {
      if (eq.dot && !eq.destroyed) {
        this.damageEnemyEquipment(eq, eq.dot.value);
        eq.dot.remaining--;
        if (eq.dot.remaining <= 0) eq.dot = undefined;
        if (st.result) return;
      }
    }
    if (p.x05Armed) {
      p.storedDamageBonus += p.damageTakenThisTurn; // "이번 턴 받은 피해량" 확정
      p.x05Armed = false;
    }
    p.damageTakenThisTurn = 0;

    // 2. 드로우: 손패 수까지 / 3. 필력: 기본치 + 보정 (이월 없음 — GDD §2-3)
    this.draw(Math.max(0, this.rules.player.handSize - p.hand.length));
    p.energy = Math.max(0, this.rules.player.energyPerTurn + p.energyNextTurnBonus - p.energyNextTurnPenalty);
    p.energyNextTurnBonus = 0;
    p.energyNextTurnPenalty = 0;
  }
}
