// 프로토타입/게임 UI 데이터 빌드: YAML 정본 → src/data.json
// 실행: npx tsx packages/proto/dump-data.ts (저장소 루트에서)
//
// v2 (ADR-011): 카드는 loadAll()이 변환한 ReviewCardDef/SpecialDef 그대로 내보낸다.
// 리뷰 본문(text)·효과 요약(ui)이 카드 정의에 포함되므로 v1의 별도 display 맵은 폐지.
import { writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadAll } from '../sim/src/data.ts';

const here = dirname(fileURLToPath(import.meta.url));
const d = loadAll();
const out = {
  cards: [...d.cards.byId.values()],
  enemies: [...d.enemies.values()],
  startingDeck: d.startingDeck,
  irremovable: [...d.irremovable],
  // 보스 도달 기대 덱 추가분 — sim/src/cli.ts 의 boss1 프리셋과 같은 값을 유지할 것
  // (전투 보상 3 + 상점 2. 근거는 그쪽 주석과 balance-report-v2-round1.md §5)
  bossExtra: ['G01', 'G02', 'D02', 'X02', 'H02', 'H04'],
};
writeFileSync(join(here, 'src/data.json'), JSON.stringify(out));
console.log('cards', out.cards.length, 'enemies', out.enemies.length, 'deck', out.startingDeck.length);
