'use strict';

function compute(a, b) {
  let sum = a + b; const state = { nested: { value: sum }, get risky() { throw new Error('getter must not run during inspection'); } }; state.self = state;
  console.log('v8-inspector-result', sum);
  return sum * 2;
}

async function asyncCompute(a, b) {
  await Promise.resolve();
  return compute(a, b);
}

setTimeout(async () => {
  globalThis.inspectorResult = await asyncCompute(2, 3);
  setInterval(() => {}, 1000);
}, 25);
