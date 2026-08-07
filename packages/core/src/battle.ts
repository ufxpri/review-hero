// 이세계 리뷰용사 — 전투 상태머신 (GDD §2 공통 계산, §3 전투 전체)
// UI 무의존 순수 상태머신. fs/network 접근 금지, Date.now()/Math.random() 금지 — rng 주입.
//
// v2 (card-system-v2.md §2·§9, ADR-011): 접두+접미 조합 폐지 → submitReview(cardUid, opts),
// 판정 3단계 → 4단계(원산지 최우선·무효 태그 무시), modifier 적용부 전량 삭제.

import { type Rng, shuffle } from './rng.ts';
import {
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
  STARTING_EQUIPMENT,
  SUIT_DISPOSITION,
} from './types.ts';

// ── 공통 계산 (GDD §2) ────────────────────────────────

/** 모든 배율은 내림, 최소 1 (GDD §2-1) */
export function applyMult(value: number, mult: number): number {
  return Math.max(1, Math.floor(value * mult));
}

/** 판정 4단계 (card-system-v2 §2): 원산지 > 헛소리 > 팩트 > 일반 순서로 검사 */
export type Judgement = 'origin' | 'fact' | 'normal' | 'fumble';

export const JUDGE_MULT: Record<Judgement, number> = { origin: 1.5, fact: 1.5, normal: 1.0, fumble: 0.5 };

/** 판정별 게이지 (v2 — 원산지 +4 / 팩트 +3 / 일반 0. 헛소리는 온보딩 보정 가능이라 별도) */
export const JUDGE_GAUGE: Record<Exclude<Judgement, 'fumble'>, number> = { origin: 4, fact: 3, normal: 0 };

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
  kind: 'attack_down' | 'attack_halve'; // attack_halve = 힙스터 크리 (공격력 −50%)
  value: number; // attack_down의 감소량 / attack_halve는 50(위력 표기)
  suit: Suit; // 재반박 계열 매칭용
  tier: number; // 힙스터 크리 = 3 (R22). 전투 부착 일반 디버프 = 1 (가정)
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
  maxTurns?: number; // 기본 30 — 초과 시 패배(timeout) 처리
  layer?: number; // 기본 1 (MVP). X09는 layer 2
  /** 성향 스냅샷용 런 누적 제출 카드 계열 카운터 (GDD §3.5 — 전투 시작 시 스냅샷 고정) */
  initialSuitCounters?: Partial<Record<Suit, number>>;
  initialLastSuit?: Suit;
  startGauge?: number; // 외부 보정 (캡 ±는 런 레벨 규칙 — 시뮬은 값 그대로 클램프만)
  sigmaP?: number; // X09용 악평 페널티 합 (Layer 2)
  onboarding?: OnboardingMods; // 온보딩 1~2판 보정 (§3.3/§4.4) — 미지정 시 정상 난이도
  noShuffle?: boolean; // 테스트 전용: 덱 순서 고정
  collectLog?: boolean;
}

export type BattleResult = 'win' | 'lose' | 'retreat' | 'timeout' | null;

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
  critUsedThisTurn: boolean;
  inconvenienceGoldUsed: boolean; // 프로 불편러 골드 갈취 전투당 1회
  viralBonusGranted: number; // 바이럴 크리 가산 누적 (상한 12, 크리 간 공유 — GDD §3.5)
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
  private readonly maxTurns: number;
  private readonly layer: number;
  private readonly sigmaP: number;
  private readonly onboardingEnemyMult: number;
  private readonly fumbleGaugeDelta: number;
  private readonly buffNoJudgement: boolean;
  private readonly noShuffle: boolean;
  private readonly collectLog: boolean;
  private uidSeq = 1;

  constructor(cfg: BattleConfig) {
    this.cards = cfg.cards;
    this.rng = cfg.rng;
    this.maxTurns = cfg.maxTurns ?? 30;
    this.layer = cfg.layer ?? 1;
    this.sigmaP = cfg.sigmaP ?? 0;
    this.onboardingEnemyMult = cfg.onboarding?.enemyDamageMult ?? 1;
    this.fumbleGaugeDelta = cfg.onboarding?.fumbleGaugeDelta ?? -2;
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
        will: 30,
        maxWill: 30,
        energy: 3,
        energyNextTurnBonus: 0,
        energyNextTurnPenalty: 0,
        gold: cfg.gold ?? 0,
        gauge: Math.max(0, Math.min(10, cfg.startGauge ?? 0)),
        equipment: (cfg.playerEquipment ?? STARTING_EQUIPMENT).map((def) => ({ def, attachments: [] })),
        hand: [],
        deck,
        discard: [],
        removedFromRun: [],
        critUsedThisTurn: false,
        inconvenienceGoldUsed: false,
        viralBonusGranted: 0,
        x05Armed: false,
        storedDamageBonus: 0,
        damageTakenThisTurn: 0,
        reaction: null,
        suitCounters: counters,
        lastSuit: cfg.initialLastSuit ?? null,
        disposition: '팩트 폭격기', // 초기값 (아래에서 스냅샷 재계산)
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
      },
      log: [],
    };

    // 성향 스냅샷 (GDD §3.5: argmax, 동률 = 최근 제출 계열, 초기값 = 팩트 폭격기)
    this.state.player.disposition = this.computeDisposition();

    // [전투 시작] 셔플 → 5장 드로우 → 인텐트 공개 (GDD §3.2)
    this.draw(5);
  }

  // ── 유틸 ──

  private log(msg: string): void {
    if (this.collectLog) this.state.log.push(msg);
  }

  private computeDisposition(): Disposition {
    const p = this.state.player;
    const suits = Object.keys(p.suitCounters) as Suit[];
    const max = Math.max(...suits.map((s) => p.suitCounters[s]));
    if (max === 0) return '팩트 폭격기';
    const top = suits.filter((s) => p.suitCounters[s] === max);
    if (top.length === 1) return SUIT_DISPOSITION[top[0]!];
    if (p.lastSuit && top.includes(p.lastSuit)) return SUIT_DISPOSITION[p.lastSuit];
    // 가정(GDD §3.5 침묵): 동률인데 최근 제출 계열이 동률군에 없거나 없음(null)이면
    // 품질→성능→배송→감성 선언 순서로 결정 (결정적. GDD에 1줄 명시 필요 — 에스컬레이션 대상)
    return SUIT_DISPOSITION[top[0]!] ?? '팩트 폭격기';
  }

  private def(cardId: string): CardDef {
    const d = this.cards.byId.get(cardId);
    if (!d) throw new Error(`카드 정의 없음: ${cardId}`);
    return d;
  }

  private gaugeChange(delta: number): void {
    const p = this.state.player;
    const s = this.state.stats;
    const before = p.gauge;
    p.gauge = Math.max(0, Math.min(10, p.gauge + delta)); // 0~10, 초과 소실 (GDD §2-2)
    const applied = p.gauge - before;
    if (applied > 0) s.gaugeGained += applied;
    if (applied < 0) s.gaugeLost += -applied;
    if (delta > 0 && before + delta > 10) s.gaugeOverflowLost += before + delta - 10; // 초과 소실량 계측
    if (before < 10 && p.gauge >= 10) s.gaugeReached10++; // 크리 "발동 가능" 이벤트 (§3.4 검증용)
  }

  private draw(n: number): void {
    const p = this.state.player;
    for (let i = 0; i < n; i++) {
      if (p.hand.length >= 8) break; // 손패 상한 8 — 초과분 드로우 중단, 소멸 없음 (GDD §3.1)
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
    // 전 장비 파괴 → 항복 = 전투 승리 + 6G (combat-model-v0.1, GDD §4.2 "항복 +6G")
    if (e.equipment.length > 0 && e.equipment.every((eq) => eq.destroyed)) {
      this.state.result = 'win';
      this.state.stats.surrender = true;
      this.state.player.gold += 6;
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

  private playerTakeDamage(v: number): void {
    if (v <= 0) return;
    const p = this.state.player;
    p.will -= v;
    p.damageTakenThisTurn += v;
    this.checkEnd();
  }

  // ── 판정 (v2 4단계 — card-system-v2 §2) ──

  /**
   * ①원산지 ②헛소리 ③팩트 ④일반.
   * 원산지는 무효 태그를 무시한다 — 직접 산 사람의 증언에는 "평가 불가 항목" 반박이 통하지 않는다.
   * tag는 정확히 1개(단일 초점 원칙)라 v1의 다중 태그 some() 검사가 단순 포함 검사로 바뀐다.
   */
  judge(card: ReviewCardDef, targetTags: string[], targetNullTags: string[], isOrigin: boolean): Judgement {
    if (isOrigin) return 'origin';
    if (targetNullTags.includes(card.tag)) return 'fumble';
    if (targetTags.includes(card.tag)) return 'fact';
    return 'normal';
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
    if (
      e.stealth &&
      gate &&
      (card.target === 'enemy' || card.target === 'enemy_equipment') &&
      !gate.hittableSuits.includes(card.suit)
    ) {
      this.log(`${card.name}: 은신 중 — 빗나감`);
      return { missed: true, judgement: null };
    }
    // 은신 중 명중 계열 명중 시 은신 해제
    if (e.stealth && gate && gate.breakOnHit && (card.target === 'enemy' || card.target === 'enemy_equipment')) {
      e.stealth = false;
      e.stealthEverBroken = true;
      this.log('은신 해제!');
    }

    // 대상 결정 + 원산지 판정 범위 (card-system-v2 §2):
    //   적 본체 대상 제출 → origin.enemy 일치 / 구성품 대상 제출 → origin.equipment 일치(이름 완전 일치)
    //   내 장비 대상·origin 없는 카드(Z##·X##·P해금)는 원산지 영구 미발동
    let targetTags: string[];
    let targetNull: string[];
    let isOrigin = false;
    let myEq: PlayerEquipmentState | null = null;
    let enemyEq: EnemyEquipmentState | null = null;
    if (card.target === 'my_equipment') {
      // 버프 카드는 반드시 내 장비 1개 대상 (GDD §3.3)
      const idx = opts.myEquipmentIndex ?? 0;
      myEq = p.equipment[idx] ?? p.equipment[0]!;
      targetTags = myEq.def.tags;
      targetNull = myEq.def.nullTags;
    } else if (card.target === 'enemy_equipment') {
      const alive = e.equipment.filter((eq) => !eq.destroyed);
      if (alive.length === 0) {
        // 가정(v1 승계): 구성품 대상 카드인데 남은 구성품이 없으면 제출 자체가 낭비(효과 없음)
        return { missed: true, judgement: null };
      }
      enemyEq =
        opts.enemyEquipmentIndex !== undefined && e.equipment[opts.enemyEquipmentIndex] && !e.equipment[opts.enemyEquipmentIndex]!.destroyed
          ? e.equipment[opts.enemyEquipmentIndex]!
          : alive[0]!;
      targetTags = enemyEq.tags;
      targetNull = e.def.nullTags; // 가정(v1 승계): 구성품 대상의 무효 태그는 적 본체의 무효 태그를 따른다
      isOrigin = card.origin?.equipment !== undefined && card.origin.equipment === enemyEq.name;
    } else {
      targetTags = e.def.weaknessTags;
      targetNull = e.def.nullTags;
      isOrigin = card.origin?.enemy !== undefined && card.origin.enemy === e.def.id;
    }

    let judgement = this.judge(card, targetTags, targetNull, isOrigin);
    // 온보딩 1판 한정: 버프 카드(내 장비 대상)는 무판정 = 항상 일반 (GDD §3.3)
    if (this.buffNoJudgement && card.target === 'my_equipment') judgement = 'normal';
    const jm = JUDGE_MULT[judgement];

    // 판정·게이지는 제출당 1회 (v2는 다중 히트 카드 없음). 헛소리는 온보딩 1판 −1 완화 가능 (§4.4)
    st.stats.judgements[judgement]++;
    if (judgement === 'fumble') this.gaugeChange(this.fumbleGaugeDelta);
    else this.gaugeChange(JUDGE_GAUGE[judgement]); // 원산지 +4 / 팩트 +3 / 일반 0

    // 재반박 (B01 counter_rebut): "같은 계열 팩트 리뷰 제출" — 해석: 원산지는 팩트의 상위 판정이므로 포함
    // (직접 산 사람의 증언이 일반 팩트보다 약한 재반박 근거일 수 없다)
    if (judgement === 'fact' || judgement === 'origin') this.tryCounterRebut(card.suit);

    // 기타 배율 ①: E03 casting_weakness — 영창(준비) 중 해당 태그 리뷰 효과 ×2.
    // v1은 P06 modifier(vs_casting_mult)로 구현했으나 modifier 폐지로 적 특성(트레잇) 판정으로 이관.
    let condMult = 1;
    const cw = e.def.castingWeakness;
    if (cw && e.charging && card.tag === cw.tag) condMult *= cw.multiplier;

    // 기타 배율 ②: E05 vanity — 계열별 "의지 데미지" 배수 (내구도 등 비대상, applyReviewEffect에서 의지 피해에만 곱함)
    const vanityMult = e.def.suitDamageMult?.[card.suit] ?? 1;

    this.applyReviewEffect(card, judgement, jm * condMult, vanityMult, myEq, enemyEq);

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
    myEq: PlayerEquipmentState | null,
    enemyEq: EnemyEquipmentState | null,
  ): void {
    const st = this.state;
    const p = st.player;
    const e = st.enemy;
    const ef = card.effect;

    let fixedAdd = judgement === 'origin' ? 1 : 0; // 원산지 고정 좋아요 +1

    const dealCardWillDamage = (base: number): void => {
      let b = base;
      // 부착 버프 가산 — "제출당 1회" (카드당 의지 피해 소스는 1개뿐이라 자연 충족)
      b += p.equipment.reduce(
        (s, eq) => s + eq.attachments.filter((a) => a.kind === 'damage_buff').reduce((x, a) => x + a.value, 0),
        0,
      );
      let dmg = applyMult(b, mult * vanityMult); // 내림 1회, 최소 1 (GDD §2-1)
      dmg += fixedAdd;
      fixedAdd = 0;
      if (p.storedDamageBonus > 0) {
        dmg += p.storedDamageBonus; // X05 — 고정 가산 (내림 후 — GDD §2)
        p.storedDamageBonus = 0;
      }
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
        else e.stunTurns = Math.max(e.stunTurns, ef.value ?? 1);
        break;
      }
      case 'weaken_next_action': {
        // D02·B03c·K04 — %는 판정 무관 고정 (배율은 절대 수치에만 — v1 정교화 승계)
        if (ef.damage !== undefined) dealCardWillDamage(ef.damage);
        if (st.result) break;
        e.weakenNextActionPct = ef.value ?? -50;
        break;
      }
      case 'remove_enemy_buff': {
        // O03·N02·B02c — 개수는 판정 무관. phase2 알바_리뷰(protectedBy)는 counter_card 일치 카드로만 제거
        if (ef.damage !== undefined) dealCardWillDamage(ef.damage);
        if (st.result) break;
        for (let i = 0; i < (ef.value ?? 1); i++) {
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
        const dur = typeof ef.duration === 'number' ? ef.duration : 2;
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
          tier: 1, // 가정(v1 승계): 전투 중 부착 일반 디버프 = Tier 1 (힙스터 크리 Tier 3만 명시됨)
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
        const dur = typeof ef.duration === 'number' ? ef.duration : 1;
        enemyEq.disabledTurns = Math.max(enemyEq.disabledTurns, dur);
        break;
      }
      case 'damage_buff': {
        // D03·N03·A01 — 부착은 슬롯 2칸 점유 (GDD §3.9).
        // v2 결정: 리뷰 유래 부착은 전부 슬롯 사용 (v1 uses_attach_slot 필드는 YAML에서 사라짐), 크리 산출물만 예외
        if (!myEq) break;
        const used = myEq.attachments.filter((a) => a.usesSlot).length;
        if (used >= 2) {
          this.log('부착 슬롯 가득 참 — 부착 실패'); // GDD §3.9 (R15)
          break;
        }
        myEq.attachments.push({ kind: 'damage_buff', value: applyMult(ef.value ?? 0, mult), usesSlot: true });
        break;
      }
      default:
        throw new Error(`미구현 리뷰 effect: ${ef.type}`);
    }

    if (st.result) return;
    // 동반 효과 (판정 배율 미적용 — 드로우 장수·회복량은 절대 피해 수치가 아님)
    if (ef.draw) this.draw(ef.draw); // Z03·A01
    if (ef.heal) p.will = Math.min(p.maxWill, p.will + ef.heal); // G03
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
    this.gaugeChange(+1); // 재반박 성공 +1 (GDD §3.4)
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
        for (let i = 0; i < (ef.value ?? 1) && p.hand.length < 8 && pool.length > 0; i++) {
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
        this.dealWillDamageToEnemy(applyMult(gDef.cost, ef.multiplier ?? 4));
        break;
      }
      case 'store_damage_taken': {
        p.x05Armed = true; // 이번 턴 받은 피해량 → 다음 턴 시작 시 예약 확정 (GDD §3.2 step1)
        break;
      }
      case 'reaction_counter': {
        // X06: 설치형, 대기 슬롯 1 (GDD §3.2)
        if (p.reaction) throw new Error('리액션 대기 슬롯 사용 중');
        p.reaction = { weakenPct: ef.weaken_pct ?? -50, reflectPct: ef.reflect_pct ?? 50 };
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
        // X09 (Layer 2): Σp(상한 cap_points, v2 기본 5) × per_point
        const sp = Math.min(this.sigmaP, ef.cap_points ?? 5);
        if (sp > 0) this.dealWillDamageToEnemy(sp * (ef.per_point ?? 3));
        break;
      }
      default:
        throw new Error(`미구현 특수 effect: ${ef.type}`);
    }

    if (ef.gauge) this.gaugeChange(ef.gauge);
    this.checkEnd();
  }

  // ── 퇴고 (GDD §3.2 v1.1 신설 → v2에서 태그 사냥 도구로 승격 — card-system-v2 §7) ──

  /** 필력 1: 손패 1장을 버리고 1장 드로우. 턴 제한 없음(필력이 상한). v2엔 교착이 없어 원하는 태그·원산지를 찾는 용도 */
  revise(uid: number): void {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    const p = st.player;
    if (p.energy < 1) throw new Error('필력 부족');
    if (p.deck.length + p.discard.length === 0) throw new Error('뽑을 카드 없음');
    p.energy -= 1;
    this.discardFromHand(uid);
    this.draw(1);
    this.log('퇴고 — 손패 1장 교체');
  }

  // ── 크리티컬 리뷰 (GDD §3.5) ──

  useCritical(): Disposition {
    const st = this.state;
    if (st.result) throw new Error('전투 종료됨');
    const p = st.player;
    if (p.gauge < 10) throw new Error('게이지 부족');
    if (p.critUsedThisTurn) throw new Error('크리티컬은 턴당 1회');
    p.gauge = 0; // 게이지 전량 소모 (에너지 비용 0)
    st.stats.gaugeLost += 10;
    p.critUsedThisTurn = true;
    const d = p.disposition; // 전투 시작 스냅샷 (GDD §3.5)
    st.stats.crits.push(d);
    const e = st.enemy;

    // E04 은신 게이트 (§3.8 특성 문언 "은신 중에는 배송/CS 계열 리뷰만 명중" — 크리티컬 리뷰도 리뷰다)
    // 가정(GDD 침묵): 적을 향하지 않는 크리(바이럴 앞잡이 = 내 버프 대상)는 은신 무관.
    // 빗나간 크리도 게이지·턴 사용은 소모된다(빗나간 일반 리뷰가 필력·카드를 소모하는 것과 일관).
    const gate = e.def.stealthGate;
    if (e.stealth && gate && d !== '바이럴 앞잡이') {
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
      case '팩트 폭격기':
        // 방어·저항 무시 고정 20 — 가정(GDD 침묵): 반사(포즈)도 무시
        this.dealWillDamageToEnemy(20, { ignoreDefense: true });
        break;
      case '힙스터 평론가':
        e.debuffs.push({
          uid: this.uidSeq++,
          kind: 'attack_halve',
          value: 50,
          suit: '성능',
          tier: 3, // 사장님 답글 최우선 반박 대상 (R22)
          suspended: false,
          beenRebutted: false,
          createdAt: this.uidSeq,
        });
        break;
      case '프로 불편러': {
        if (e.staggerImmunityTurns > 0) this.log('경직 내성 — 크리 기절 무효');
        else e.stunTurns = Math.max(e.stunTurns, 1);
        // v1.1(제안 6): 기절과 별개로 다음 행동 위력 −50% — 기절 면역(경직 내성)·기믹 대상에도
        // 크리 가치가 남도록 피해 등가 보강 (보스전 등가 재설계)
        e.weakenNextActionPct = Math.min(e.weakenNextActionPct, -50);
        // GDD §3.5 (v1.1 명문화): "전투당 1회"는 골드 갈취에만 적용, 기절·위력 감소는 크리마다
        if (!p.inconvenienceGoldUsed) {
          const gold = e.def.tier === 'boss' ? 25 : e.def.tier === 'elite' ? 15 : 8;
          p.gold += gold;
          p.inconvenienceGoldUsed = true;
        }
        break;
      }
      case '바이럴 앞잡이': {
        // 현재 버프 효과 2배 — 가산 합산 상한 +12, 크리 간 상한 공유 (GDD §3.5)
        let budget = 12 - p.viralBonusGranted;
        const hasBuff = p.equipment.some((eq) => eq.attachments.some((a) => a.kind === 'damage_buff'));
        if (!hasBuff) {
          // v1.1(제안 5): 바닥 보장 — 버프 0개면 S13 상당(+3) 가산 버프 1개 즉시 부착
          // (크리 산출물이므로 부착 슬롯 미점유, +12 상한 공유)
          const add = Math.min(3, budget);
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
        e.staggerImmunityTurns = Math.max(e.staggerImmunityTurns, 2); // 이번 턴 정리에서 1 감소 → 다음 플레이어 턴 1턴 유지
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
      if (e.stunTurns === 0) e.staggerImmunityTurns = 1; // 기상 → 경직 내성 1턴 (GDD §3.2)
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
    const p2 = e.def.phase2!;
    if (p2.triggerPct !== undefined) return Math.floor((e.maxWill * p2.triggerPct) / 100);
    return p2.triggerWill ?? 0;
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
      weaken *= 1 + e.weakenNextActionPct / 100;
      e.weakenNextActionPct = 0; // 소진
    }
    let reaction: PlayerState['reaction'] = null;
    if (intent.aType === 'attack' && p.reaction) {
      // 가정(GDD §3.2 문언 준수): type: attack이면 데미지 0인 행동(E05 찬양 강요)에도 리액션 발동·소진
      reaction = p.reaction;
      p.reaction = null;
      weaken *= 1 + reaction.weakenPct / 100;
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
        // 가정(GDD 침묵): 감산(attack_down)은 배율이 아니므로 §2-1 미적용 — 하한 0 (피해 0 가능)
        let v = Math.max(0, base + this.enemyAttackUpTotal() - this.enemyAttackDownTotal());
        // 배율 경로(힙스터 ×0.5, S08/X06 −50%, 온보딩 ×0.75)는 §2-1 "내림, 최소 1" 적용 —
        // 감산으로 이미 0이 아닌 한 배율만으로는 0이 되지 않는다.
        if (v > 0) {
          if (this.enemyHipsterActive()) v = applyMult(v, 0.5); // 힙스터 크리 −50% (GDD §3.5)
          if (ctx.weaken !== 1) v = applyMult(v, ctx.weaken);
          if (this.onboardingEnemyMult !== 1) v = applyMult(v, this.onboardingEnemyMult); // §4.4 적 공격 배율
        }
        this.playerTakeDamage(v);
        if (ctx.reactionApplied && ctx.reflectPct) {
          const refl = Math.floor((v * ctx.reflectPct) / 100); // 받은 피해의 50% 반사 (X06)
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

    // 2. 드로우: 5장까지 / 3. 에너지: 3 + 보정 (이월 없음 — GDD §2-3)
    this.draw(Math.max(0, 5 - p.hand.length));
    p.energy = Math.max(0, 3 + p.energyNextTurnBonus - p.energyNextTurnPenalty);
    p.energyNextTurnBonus = 0;
    p.energyNextTurnPenalty = 0;
  }
}
