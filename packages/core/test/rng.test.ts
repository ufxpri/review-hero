import { test } from 'node:test';
import assert from 'node:assert/strict';
import { applyMult, mulberry32 } from '../src/index.ts';

test('공통 계산 §2-1: 배율 내림·최소 1', () => {
  assert.equal(applyMult(3, 1.5), 4); // 3×1.5=4.5 → 4 (GDD 예시)
  assert.equal(applyMult(5, 0.5), 2); // 5×0.5=2.5 → 2 (GDD 예시)
  assert.equal(applyMult(1, 0.5), 1); // 최소 1
  assert.equal(applyMult(0, 4), 1); // 최소 1 (X04 0코 증정)
});

test('mulberry32: 같은 시드 = 같은 수열 (결정적 리플레이 전제)', () => {
  const a = mulberry32(42);
  const b = mulberry32(42);
  for (let i = 0; i < 100; i++) assert.equal(a(), b());
  const c = mulberry32(43);
  assert.notEqual(mulberry32(42)(), c());
});
