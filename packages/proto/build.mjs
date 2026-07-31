// 프로토타입 빌드: 데이터 덤프 → esbuild 번들 → 단일 HTML 조립
// 실행: node packages/proto/build.mjs (저장소 루트에서)
import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';

execSync('npx tsx packages/proto/dump-data.ts', { stdio: 'inherit' });
execSync('npx esbuild packages/proto/src/main.ts --bundle --minify --format=iife --outfile=packages/proto/.bundle.js', { stdio: 'inherit' });

const js = readFileSync('packages/proto/.bundle.js', 'utf8').replaceAll('</script', '<\\/script');
const tpl = readFileSync('packages/proto/template.html', 'utf8');
mkdirSync('prototype', { recursive: true });
writeFileSync('prototype/index.html', tpl.replace('/*__BUNDLE__*/', () => js));
rmSync('packages/proto/.bundle.js');
console.log('→ prototype/index.html');
