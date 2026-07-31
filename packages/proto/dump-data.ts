// 프로토타입 데이터 빌드: YAML 정본 → src/data.json (엔진 defs + UI 표시 텍스트)
// 실행: npx tsx packages/proto/dump-data.ts (저장소 루트에서)
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import yaml from 'js-yaml';
import { loadAll } from '../sim/src/data.ts';

const here = dirname(fileURLToPath(import.meta.url));
const d = loadAll();
const rawCards = yaml.load(readFileSync(join(here, '../../design/cards-v1.0.yaml'), 'utf8')) as any;
const display: Record<string, any> = {};
for (const group of ['prefixes', 'suffixes', 'specials']) {
  for (const c of rawCards[group] ?? []) display[c.id] = { text: c.text ?? '', flavor: c.flavor ?? '', footer: c.footer ?? '' };
}
const out = {
  cards: [...d.cards.byId.values()],
  enemies: [...d.enemies.values()],
  startingDeck: d.startingDeck,
  bossExtra: ['P11', 'P15', 'S02'], // GDD §3.6 보스 표준 검증 덱
  display,
};
writeFileSync(join(here, 'src/data.json'), JSON.stringify(out));
console.log('cards', out.cards.length, 'enemies', out.enemies.length, 'deck', out.startingDeck.length);
