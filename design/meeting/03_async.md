# 이세계 리뷰용사 — 비동기 온라인 시스템 상세 v0.1 (회의 03)

담당: 온라인 파트. 전제: combat-model-v0.1.md, enemies-v0.1.yaml(B01), Layer 3 범위.
서버는 Supabase(Postgres + Edge Function + pg_cron) 수준 CRUD로 한정한다.

## 0. 설계 원칙

1. **전투 중 서버 왕복 0회.** 모든 비동기 데이터는 런/층/전투 "시작 시점"에 배치로 내려받고, 결과는 종료 시점에 큐로 올린다.
2. **공유 데이터는 절대 실시간 소모되지 않는다.** 보스 리뷰·유언은 읽기 전용 스냅샷이며, 전투 내 변화(사장님 답글의 반박 등)는 그 전투에만 적용된다.
3. **밸런스 영향 보상은 골드/명성까지.** 리더보드 보상은 코스메틱 전용.
4. **시즌 단위 = 주간** (월요일 00:00 KST 리셋, ISO 주차 기준).

---

## 1. 보스 리뷰 시스템 (디버프 리뷰) 풀 스펙

### 1.1 작성 조건과 구매 인증 등급

보스전 **패배 시**에만 작성권이 발생한다(승리 시엔 리뷰 대신 배당을 받는 쪽 — §1.5).
인증 등급은 그 전투에서 깎은 보스 의지 비율로 결정한다.

| 등급 | 조건 (깎은 의지 %) | 인증 표기 | 가중치 W | 디버프 강도 |
|---|---|---|---|---|
| 미인증 | < 30% | 작성 불가 | — | — |
| ★ 구매 인증 | 30~59% | "구매 인증" | 1.0 | Tier 1 |
| ★★ 헤비 유저 | 60~89% | "헤비 유저 인증" | 1.5 | Tier 2 |
| ★★★ 최후의 증인 | ≥ 90% | "최후의 증인" | 2.0 | Tier 3 |

- 비율 판정 기준: `깎은 의지 / 보스 시작 의지` (B01 기준 60). 페이즈2 회복분(사장님 답글 +5)으로 되돌아간 양은 "깎은 의지" 누계에 그대로 남는다(순간값이 아니라 누적 가한 피해 기준).
- **작성 제한: 유저당 보스당 주간 1회.** 재작성 시 기존 리뷰는 삭제되고 공감 0에서 다시 시작(악용 방지: 갱신은 하향 위험을 감수하는 선택).

### 1.2 디버프 리뷰의 내용과 효과 풀

리뷰 = **자동 서식 텍스트 + 디버프 1개 선택**. 텍스트는 그 전투 로그에서 자동 생성(사용한 접두/접미 명칭 조합 + 별점 = 4−인증등급 성급). 자유 텍스트는 Layer 4 이후(§8-2).

선택 가능한 디버프는 **그 전투에서 팩트 판정을 1회 이상 낸 계열**만 노출된다(팩트 없이 죽으면 계열 무관 기본형만). 효과는 열람자(다음 도전자)의 보스전 시작 시 적용.

| 계열 | 디버프 | Tier 1 | Tier 2 | Tier 3 |
|---|---|---|---|---|
| 품질/마감 | 「마감 불량 고발」 보스 장비 시작 내구도 감소 | −3 | −5 | −7 |
| 성능/최적화 | 「스펙 뻥튀기 고발」 보스 공격 행동 위력 감소 | −1 | −2 | −3 |
| 배송/CS | 「응대 지연 고발」 보스 n번째 턴 행동 1턴 지연 | 1턴(첫 행동) | 첫+4턴째 | 첫+3·6턴째 |
| 감성/디자인 | 「감성 파탄 고발」 도전자 시작 신뢰도 게이지 | +1 | +2 | +3 |
| 기본형 | 「그냥 별로였음」 보스 시작 의지 감소 | −2 | −3 | −4 |

- 활성 슬롯 5개의 디버프는 **전부 중첩 적용**된다. 최악 조합(품질×5 Tier3 = 내구도 −35)은 장비 내구도 15인 B01에서 즉시 파괴가 되므로, **동일 디버프 종류는 슬롯 내 최대 2개까지만 효과 적용**(3번째부터는 표기만 되고 효과 무시 — 슬롯 진입 자체는 막지 않음).

### 1.3 슬롯 5개 경쟁 규칙

- 보스별·주간별로 **점수 상위 5개만 "활성"**(전투에 실제 적용 + 배당 자격). 나머지는 열람만 가능한 대기 리뷰.
- **점수 공식**: `score = V × W + F`
  - `V` = 공감 수, `W` = 인증 가중치(1.0/1.5/2.0)
  - `F`(신선도) = `max(0, 48 − 경과시간h) × 0.5` — 신규 리뷰는 최대 +24점을 받고 48시간에 걸쳐 소멸. 신규 진입 기회 보장 장치.
- **교체 판정 주기: 매시 정각 배치 재계산**(pg_cron → score 갱신 → 상위 5개 active 플래그 교체). 전투 중 스냅샷은 전투 시작 시점 기준이므로 도중 교체 영향 없음.
- 동점 시 최신 작성 우선. 슬롯에서 밀린 리뷰도 삭제되지 않으며 공감이 쌓이면 복귀 가능.
- 주간 리셋 시 전 리뷰 아카이브(명예의 전당 열람 전용) + 슬롯 공백 상태로 시작. 공백 슬롯은 **공식 프리셋 리뷰**(오프라인용 15종 풀에서 랜덤, §7)로 임시 충전 — 프리셋은 공감/배당 대상 아님.

### 1.4 공감 집계

- 보스전 **종료 시(승패 무관)** 결과 화면에서, 이번 전투에 적용됐던 활성 리뷰 5개 중 **최대 2개에 공감** 가능.
- 1인당 리뷰당 주간 1회(unique 제약). 자기 리뷰 공감 불가.
- 클라이언트는 공감을 로컬 큐에 쌓고 런 종료/재접속 시 일괄 전송.

### 1.5 정산 (처치 배당)

- 임의 유저가 보스를 처치할 때마다 **현상금 10G가 그 순간의 활성 리뷰 5개 작성자에게 분배 적립**된다. 처치 시점의 `active_review_ids` 스냅샷 기준.
- 분배 공식: `배당_i = 10G × C_i / ΣC`, `C_i(기여도) = W_i × (1 + V_i/10)` (V는 처치 시점 공감 수).
- 적립분은 **매일 04:00 KST 배치 정산** → 게임 내 우편으로 지급(다음 런 시작 골드에 가산되는 게 아니라 **메타 재화 계정 골드**로 — 런 경제 오염 방지, 경제 파트 협의 필요).
- 1인당 일일 배당 상한 **300G**. 초과분은 소멸(명성 +1/10G로 전환).
- 패배자에게도 낙수: 처치가 발생한 전투에서 적용된 리뷰 작성자 중 **그 주에 해당 보스에게 패배 기록이 있는 유저**는 배당 ×1.1 (복수 성공 보너스, 서사 장치).

### 1.6 사장님 답글 + 재반박

전투 내 로컬 기믹. 서버 데이터는 변경하지 않는다.

- **발동**: 보스 턴 3, 6, 9…턴째 시작 시 (enemies-v0.1 B01 명세 유지).
- **대상 선정 우선순위**: ① 활성 비동기 디버프 리뷰 중 Tier 높은 것(동률 시 공감 높은 것) → ② 없으면 플레이어가 이번 전투에서 건 디버프 중 가장 최근 것.
- **효과**: 대상 디버프를 **이번 전투 한정 정지**(제거 아님 — 서버의 리뷰는 그대로) + 보스 의지 +5. 연출: 해당 리뷰에 답글 텍스트 오버레이("고객님, 저희 제품은 정상입니다^^").
- **재반박**: 정지된 디버프와 **같은 계열 접두로 팩트 판정** 리뷰를 성공시키면 그 디버프가 원래 효과로 부활 + 신뢰도 게이지 +1 보너스. 
- **재반박 제한**: 디버프 1개당 재반박 1회. 보스도 이미 반박한 디버프는 다시 반박하지 않는다(다음 대상으로 넘어감). 반박할 대상이 없으면 사장님 답글은 의지 +5만 발동.

---

## 2. 유언 리뷰 (Epitaph)

### 2.1 작성 조건

- **모든 사망 시** 작성 가능(보스/일반 무관). 작성은 선택이며 15초 내 미입력 시 자동 생성문으로 게시할지 묻는다.
- 형식(v1, Layer 4 이전): **템플릿 조합만** — [고정 문구 20종] + [이번 런에서 사용한 접두/접미 카드 명칭] 조합. 자유 텍스트 금지(모더레이션 비용 0).
- 자동 부착 메타데이터: 층/방 노드 타입, 죽인 적 id, 사인(마지막 피격 행동명), **정보 칩** = 그 전투에서 팩트 판정을 낸 태그 1개(있을 경우, 가장 많이 적중한 태그).

### 2.2 노출 알고리즘

- **조회 시점: 층 진입 시 1회 배치 쿼리** (방마다 조회하지 않는다).
- 매칭 키 = `(층 번호, 방 노드 타입, 적 id)`. 방 진입 시 해당 키에 사망 기록이 있으면 바닥에 유언 마커 표시.
- **방당 최대 3개 노출**: ① 공감 상위 1 + ② 최신(72h 내) 1 + ③ 무작위 1 (중복 제거, 부족하면 있는 만큼).
- **층당 노출 상한 5개** — 초과하는 방은 마커 생략(스팸 피로 방지). 우선순위는 보스방·정예방 먼저.
- 자기 유언은 자기에게 노출되지 않음.

### 2.3 공감 보상

| 행동 | 작성자 보상 | 열람자 보상 |
|---|---|---|
| 공감 1회 수신 | 명성 +5, 계정 골드 +3 (일일 상한: 명성 50 / 골드 30) | — |
| 정보 칩 있는 유언에 공감 | 상동 | 해당 적 첫 조우 시 **약점 태그 1개 공개** |

- 열람자 공감은 유언당 1회, 큐 적재 후 일괄 전송.
- 90일 경과 + 공감 3 미만 유언은 배치로 삭제(용량 관리).

---

## 3. 전단지

- **작성 조건**: 상점 리뷰 네고로 **할인율 30% 이상** 성사 시 자동 생성 제안 → 수락 시 게시. 내용은 자동 서식: 아이템명 + 할인율 + 감수한 페널티("이 집 사장 [배송] 지적에 약함. 대신 런 내내 [지연] 달고 삶").
- **노출**: 상점 진입 시 벽보 **1장**. 후보 풀 = 같은 막(Act)의 전단지. 선정: 70% 확률로 공감 상위 10 중 랜덤, 30% 확률로 24h 내 최신 중 랜덤.
- **열람 효과**: 정보 제공이 기본(그 상점 주인의 네고 약점 계열 힌트). 수치 보정(첫 네고 판정 보너스 등)은 상점 파트와 협의 후 확정(미해결 쟁점).
- **공감 보상**: 작성자 계정 골드 +2/공감, 일일 상한 20.

---

## 4. 주간 시드 던전 & 리더보드

- **시드**: `seed = hash("RH-" + ISO주차)`. 던전 구조·적 배치·상점 재고·카드 보상 전부 고정. 스타팅 덱 고정(성향 결정에 영향 주는 초기 선택만 허용).
- **도전**: 주간 무제한. **최고 점수 1개만 리더보드 반영**, 시도 횟수는 기록에 병기(투명성).
- **점수 공식**:
  `점수 = 도달 층수×100 + 잔여 의지×10 + 팩트 판정 수×15 − 헛소리 판정 수×10 + 잔여 골드×1 + max(0, (80 − 총 전투 턴 수))×5`
  — 팩트 플레이를 시간 단축보다 위에 두는 가중치. 텍스트 게임이므로 실시간 타이머는 점수에서 배제.
- **리더보드**: 상위 100명 = **"이번 주 심사위원단"** (세계관의 신 100명 콘셉트 연동). 보상은 칭호 + 카드 뒷면 스킨 + 다음 주 자기 유언/전단지 노출 가중 ×1.2. **밸런스 영향 보상 없음.**
- 오프라인 완주 기록은 리더보드 제외(§7), 개인 기록실에만 남는다.

---

## 5. 서버 데이터 모델 (Supabase 기준)

### 5.1 테이블 스키마 초안

```sql
-- 플레이어 (auth.users 연동)
create table players (
  id uuid primary key references auth.users,
  handle text not null,
  fame int default 0,
  account_gold int default 0,
  created_at timestamptz default now()
);

-- 보스 디버프 리뷰
create table boss_reviews (
  id uuid primary key default gen_random_uuid(),
  boss_id text not null,            -- 'B01'
  season_week text not null,        -- '2026-W31'
  player_id uuid references players,
  debuff_id text not null,          -- 효과 풀 키
  cert_tier smallint not null,      -- 1/2/3
  dmg_pct numeric not null,         -- 깎은 의지 %
  body_template jsonb not null,     -- 자동 서식 조각
  upvotes int default 0,
  score numeric default 0,          -- 배치 재계산
  active bool default false,        -- 상위 5 플래그
  created_at timestamptz default now(),
  unique (boss_id, season_week, player_id)
);
create index on boss_reviews (boss_id, season_week, active, score desc);

-- 공감 (3종 공용)
create table votes (
  review_type text not null check (review_type in ('boss','epitaph','flyer')),
  review_id uuid not null,
  voter_id uuid references players,
  created_at timestamptz default now(),
  primary key (review_type, review_id, voter_id)
);

-- 처치 기록 (배당 적립의 원천)
create table boss_kills (
  id bigint generated always as identity primary key,
  boss_id text not null,
  killer_id uuid references players,
  active_review_ids uuid[] not null,  -- 처치 시점 스냅샷
  settled bool default false,
  killed_at timestamptz default now()
);

-- 일일 정산 결과
create table settlements (
  id bigint generated always as identity primary key,
  player_id uuid references players,
  amount_gold int not null,
  kill_count int not null,
  settle_date date not null,
  claimed bool default false,
  unique (player_id, settle_date)
);

-- 유언 리뷰
create table epitaphs (
  id uuid primary key default gen_random_uuid(),
  player_id uuid references players,
  floor int not null,
  node_type text not null,
  enemy_id text not null,
  cause text not null,
  template_ids int[] not null,
  hint_tag text,                    -- 정보 칩 (nullable)
  upvotes int default 0,
  created_at timestamptz default now()
);
create index on epitaphs (floor, node_type, enemy_id, upvotes desc);
create index on epitaphs (floor, node_type, enemy_id, created_at desc);

-- 전단지
create table flyers (
  id uuid primary key default gen_random_uuid(),
  player_id uuid references players,
  act int not null,
  item_id text not null,
  discount_pct int not null,
  penalty_id text not null,
  upvotes int default 0,
  created_at timestamptz default now()
);
create index on flyers (act, upvotes desc);

-- 주간 시드 기록
create table weekly_runs (
  player_id uuid references players,
  season_week text not null,
  best_score int not null,
  attempts int default 1,
  floor_reached int, fact_count int, offline bool default false,
  updated_at timestamptz default now(),
  primary key (player_id, season_week)
);
create index on weekly_runs (season_week, best_score desc);
```

### 5.2 쿼리 패턴

| 시점 | 호출 | 내용 |
|---|---|---|
| 런 시작 | Edge Function `run_packet` 1회 | 보스별 활성 리뷰 top5 + 주간 시드 메타 + 내 정산 우편 (3쿼리를 1응답으로 묶음) |
| 층 진입 | select 1회 | 해당 층 유언 배치 (limit 15, 클라에서 방별 3개 선별) |
| 상점 진입 | select 1회 | 전단지 후보 (limit 11) |
| 런 종료/사망 | insert 배치 1회 | 유언 1 + votes n + weekly_run upsert + boss_kill(해당 시) |
| pg_cron 매시 | update | boss_reviews score 재계산 + active 교체 (주간·보스별 행 수백 규모) |
| pg_cron 매일 04:00 | insert | boss_kills → settlements 집계 (settled 플래그) |

### 5.3 비용 추정 (DAU 1,000 / 1인 3런 가정)

- 읽기: 런당 API 6~8회 → 일 ~24k 요청, 월 ~72만. 쓰기: 런당 1~2회 배치. **Supabase Free(500MB, egress 5GB) 내에서 시작 가능**, 응답 행이 작아(행당 <1KB) egress 월 ~1GB 수준.
- 저장: 유언 일 3천 행 × 300B ≈ 월 27MB가 최대 성분 → 90일 저공감 삭제 정책으로 상쇄.
- 성장 시 Pro($25/월)로 충분. Realtime 구독·Storage 미사용. RLS: 본인 행만 insert/update, 읽기는 public select(익명 노출 데이터만 뷰로 분리).

---

## 6. 오프라인 폴백

| 기능 | 오프라인 동작 | 재접속 시 |
|---|---|---|
| 보스 디버프 리뷰(열람) | 클라 내장 **공식 프리셋 15종**에서 5개 랜덤 적용 (배당·공감 없음, "오프라인 모드" 표기) | 실 데이터로 교체 |
| 보스 리뷰(작성) | 로컬 큐 저장 | 업로드. 신선도 F는 **서버 수신 시각** 기준(백데이트 이득 차단) |
| 유언 작성 / 공감 | 로컬 큐 | 일괄 flush (중복은 PK로 무시) |
| 유언/전단지 열람 | 미표시 (마커 자체 생략) | — |
| 정산 우편 | 미수령 | 접속 시 수령 |
| 주간 시드 | 시드가 날짜 결정적이라 **플레이는 가능** | 기록은 `offline=true`로 제출, **리더보드 제외**·개인 기록실만 |
| 사장님 답글 | 정상 동작 (전투 로컬 기믹) | — |

싱글 코어(Layer 1~2)는 네트워크 없이 100% 동작하는 것이 검수 기준이다.

---

## 7. 변경 제안 (기존 v0.1과의 차이)

1. **enemies-v0.1 B01 `사장님 답글`의 "반박·제거" → "이번 전투 한정 정지"로 문구 변경.** 근거: 공유 리뷰가 서버에서 제거되는 것으로 오해될 표현이며, 재반박(부활) 규칙과 논리적으로 충돌. 데이터 수치 변경 없음.
2. **enemies-v0.1 B01 `on_player_defeat`의 "HP 30% 이상" 단일 조건 → 30/60/90% 3등급 인증으로 확장** (본 문서 §1.1). 근거: 단일 컷은 30% 직후 자살 작성이 최적행동이 됨. 등급 가중치로 "더 깊이 싸운 리뷰"가 슬롯 경쟁에서 이기게 함.
3. **cards-v0.1 X08 「공감 좀 눌러주세요」 툴팁에 주석 추가 제안**: 전투 내 신뢰도 게이지와 온라인 공감은 별개 시스템임을 명시(용어 충돌 예방). 효과 변경 없음.

---

## 8. 후속 확장 예약 (이번 스펙에 미포함)

1. 유언·보스 리뷰 자유 텍스트: Layer 4 LLM 파이프라인(등록 시 1회 판정) 완성 후 개방. 그때까지 템플릿 조합 고정.
2. 보스 2종 이상 추가 시 슬롯·정산 파라미터는 보스별 컬럼이 아니라 config 테이블로 분리.
