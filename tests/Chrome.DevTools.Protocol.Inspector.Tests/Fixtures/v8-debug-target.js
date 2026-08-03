'use strict';

function compute(a, b) {
  const sum = a + b;
  console.log('v8-inspector-result', sum);
  return sum * 2;
}

setTimeout(() => {
  globalThis.inspectorResult = compute(2, 3);
  setInterval(() => {}, 1000);
}, 25);
