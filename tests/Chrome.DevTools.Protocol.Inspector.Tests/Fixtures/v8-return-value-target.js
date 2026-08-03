'use strict';

function computeReturnValue() {
  const value = 5;
  debugger;
  return value;
}

setTimeout(() => {
  globalThis.returnValueResult = computeReturnValue();
  setInterval(() => {}, 1000);
}, 25);
