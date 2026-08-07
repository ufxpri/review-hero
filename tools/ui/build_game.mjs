// ui/game/ 의 생성 산출물 2종을 빌드한다 (커밋하지 않음 — .gitignore 참조).
//   engine.js  packages/core 전투 엔진의 iife 번들 (전역 RHEngine)
//   data.js    packages/proto 데이터 덤프를 window.RH_DATA 로 감싼 것
// 실행: node tools/ui/build_game.mjs (저장소 루트에서)
import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';

execSync('npx tsx packages/proto/dump-data.ts', { stdio: 'inherit' });
execSync('npx esbuild packages/core/src/index.ts --bundle --minify --format=iife --global-name=RHEngine --outfile=ui/game/engine.js', { stdio: 'inherit' });

const data = readFileSync('packages/proto/src/data.json', 'utf8');
writeFileSync('ui/game/data.js', '// 자동 생성 — tools/ui/build_game.mjs 가 만든다. 수정 금지.\nwindow.RH_DATA = ' + data + ';\n');
console.log('→ ui/game/engine.js, ui/game/data.js');
