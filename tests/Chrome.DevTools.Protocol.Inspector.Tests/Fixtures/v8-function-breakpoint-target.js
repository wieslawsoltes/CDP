'use strict';

function observedFunction(value) {
  return value * 2;
}

globalThis.functionBreakpointFixtureReady = true;
debugger;
setTimeout(() => {
  globalThis.functionBreakpointResult = observedFunction(21);
  setInterval(() => {}, 1000);
}, 25);
