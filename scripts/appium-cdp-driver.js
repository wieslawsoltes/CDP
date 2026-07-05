const http = require('node:http');
const fs = require('node:fs');

let PORT = 4723;
let CDP_HOST = 'http://127.0.0.1:9222';

// Parse command line arguments: --port <port> --cdp-host <host>
for (let i = 2; i < process.argv.length; i++) {
  if (process.argv[i] === '--port' && i + 1 < process.argv.length) {
    PORT = parseInt(process.argv[++i], 10);
  } else if (process.argv[i] === '--cdp-host' && i + 1 < process.argv.length) {
    CDP_HOST = process.argv[++i];
  }
}
const LOG_FILE = 'appium.log';

function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}\n`;
  fs.appendFileSync(LOG_FILE, line);
  console.log(line.trim());
}

// Clear old logs
if (fs.existsSync(LOG_FILE)) {
  fs.unlinkSync(LOG_FILE);
}

log(`Starting Appium CDP Driver on port ${PORT}...`);

let wsConnection = null;
let currentSessionId = null;
let messageId = 1;
const pendingRequests = new Map();

// Helper to send CDP command over WebSocket
function sendCdpCommand(method, params = {}) {
  return new Promise((resolve, reject) => {
    if (!wsConnection || wsConnection.readyState !== 1) {
      return reject(new Error('WebSocket connection is not open'));
    }
    const id = messageId++;
    pendingRequests.set(id, { resolve, reject });
    const payload = JSON.stringify({ id, method, params });
    log(`CDP OUT: ${payload}`);
    wsConnection.send(payload);
  });
}

// Discover targets and connect WebSocket
async function connectToCdp() {
  log(`Discovering targets at ${CDP_HOST}/json...`);
  let res;
  for (let i = 0; i < 30; i++) {
    try {
      res = await fetch(`${CDP_HOST}/json`);
      if (res.ok) break;
    } catch (e) {
      log(`CDP Discovery attempt ${i + 1} failed: ${e.message}`);
    }
    await new Promise(r => setTimeout(r, 500));
  }

  if (!res || !res.ok) {
    throw new Error('CDP server is not reachable');
  }

  const targets = await res.json();
  log(`Found targets: ${JSON.stringify(targets)}`);
  
  // Get first target of type 'page'
  const target = targets.find(t => t.type === 'page');
  if (!target) {
    throw new Error('No active page targets found');
  }

  const wsUrl = target.webSocketDebuggerUrl;
  log(`Connecting WebSocket to ${wsUrl}...`);

  wsConnection = new WebSocket(wsUrl);

  return new Promise((resolve, reject) => {
    wsConnection.onopen = async () => {
      log('WebSocket connection established.');
      try {
        wsConnection.onmessage = (event) => {
          log(`CDP IN: ${event.data}`);
          const data = JSON.parse(event.data);
          if (data.id && pendingRequests.has(data.id)) {
            const { resolve, reject } = pendingRequests.get(data.id);
            pendingRequests.delete(data.id);
            if (data.error) {
              reject(data.error);
            } else {
              resolve(data.result);
            }
          }
        };

        // Enable necessary domains
        await sendCdpCommand('DOM.enable');
        await sendCdpCommand('Input.enable');
        await sendCdpCommand('Runtime.enable');
        await sendCdpCommand('Page.enable');
        resolve();
      } catch (err) {
        reject(err);
      }
    };

    wsConnection.onerror = (err) => {
      log(`WebSocket error: ${err.message || err}`);
      reject(err);
    };

    wsConnection.onclose = () => {
      log('WebSocket connection closed.');
      wsConnection = null;
    };
  });
}

// Request dispatcher
async function handleRequest(req, res) {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const method = req.method;
  const path = url.pathname;

  log(`HTTP Request: ${method} ${path}`);

  // Collect request body
  let body = '';
  await new Promise((resolve) => {
    req.on('data', chunk => body += chunk);
    req.on('end', resolve);
  });

  const params = body ? JSON.parse(body) : {};
  if (body) {
    log(`HTTP Request Body: ${body}`);
  }

  // Response helper
  function sendResponse(statusCode, data) {
    log(`HTTP Response (${statusCode}): ${JSON.stringify(data)}`);
    res.writeHead(statusCode, { 'Content-Type': 'application/json; charset=utf-8' });
    res.end(JSON.stringify(data));
  }

  function sendError(errorName, message, statusCode = 400) {
    sendResponse(statusCode, {
      value: {
        error: errorName,
        message: message,
        stacktrace: ''
      }
    });
  }

  // Match routes
  try {
    // 0. Status
    if (method === 'GET' && /^\/(wd\/hub\/)?status$/.test(path)) {
      return sendResponse(200, {
        value: {
          ready: true,
          message: 'Appium CDP Driver is ready'
        }
      });
    }

    // 1. Create Session
    if (method === 'POST' && /^\/(wd\/hub\/)?session$/.test(path)) {
      if (!currentSessionId) {
        await connectToCdp();
        currentSessionId = `session-${Date.now()}`;
      }
      return sendResponse(200, {
        value: {
          sessionId: currentSessionId,
          capabilities: {
            platformName: 'windows',
            browserName: 'cdp-app'
          }
        }
      });
    }

    // Ensure session exists for subsequent commands
    if (currentSessionId && path.includes(currentSessionId)) {
      // Get Window Handle
      if (method === 'GET' && /^\/(wd\/hub\/)?session\/[^\/]+\/window$/.test(path)) {
        return sendResponse(200, { value: 'main-window-handle' });
      }

      // Get Window Handles
      if (method === 'GET' && /^\/(wd\/hub\/)?session\/[^\/]+\/window\/handles$/.test(path)) {
        return sendResponse(200, { value: ['main-window-handle'] });
      }

      // Get Timeouts
      if (method === 'GET' && /^\/(wd\/hub\/)?session\/[^\/]+\/timeouts$/.test(path)) {
        return sendResponse(200, { value: { implicit: 0, pageLoad: 300000, script: 30000 } });
      }

      // 2. Delete Session
      if (method === 'DELETE' && /^\/(wd\/hub\/)?session\/[^\/]+$/.test(path)) {
        if (wsConnection) {
          wsConnection.close();
        }
        currentSessionId = null;
        return sendResponse(200, { value: null });
      }

      // 3. Navigate to URL
      if (method === 'POST' && /^\/(wd\/hub\/)?session\/[^\/]+\/url$/.test(path)) {
        const destUrl = params.url;
        await sendCdpCommand('Page.navigate', { url: destUrl });
        // Allow navigation and layout to settle
        await new Promise(r => setTimeout(r, 500));
        return sendResponse(200, { value: null });
      }

      // 4. Get Current URL
      if (method === 'GET' && /^\/(wd\/hub\/)?session\/[^\/]+\/url$/.test(path)) {
        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: 'window.location.href',
          returnByValue: true
        });
        const urlValue = evalResult.result?.value || '';
        return sendResponse(200, { value: urlValue });
      }

      // 5. Get Title
      if (method === 'GET' && /^\/(wd\/hub\/)?session\/[^\/]+\/title$/.test(path)) {
        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: 'document.title',
          returnByValue: true
        });
        const titleValue = evalResult.result?.value || '';
        return sendResponse(200, { value: titleValue });
      }

      // 6. Find Element
      if (method === 'POST' && /^\/(wd\/hub\/)?session\/[^\/]+\/element$/.test(path)) {
        const using = params.using;
        const selectorValue = params.value;
        let cdpSelector = selectorValue;

        if (using === 'accessibility id') {
          cdpSelector = `[AccessibilityId="${selectorValue}"]`;
        } else if (using === 'id') {
          cdpSelector = `#${selectorValue}`;
        } else if (using === 'name') {
          cdpSelector = `[Name="${selectorValue}"]`;
        }

        // Get document to start query
        const docResult = await sendCdpCommand('DOM.getDocument', { depth: -1, pierce: true });
        const documentNodeId = docResult.root.nodeId;

        // Query selector
        try {
          const queryResult = await sendCdpCommand('DOM.querySelector', {
            nodeId: documentNodeId,
            selector: cdpSelector
          });

          if (!queryResult || queryResult.nodeId === 0) {
            return sendError('no such element', `Unable to locate element with selector: ${cdpSelector}`, 444);
          }

          return sendResponse(200, {
            value: {
              'element-6066-11e4-a52e-4f735466cecf': String(queryResult.nodeId)
            }
          });
        } catch (e) {
          return sendError('no such element', `Error querying element with selector ${cdpSelector}: ${e.message}`, 444);
        }
      }

      // 7. Element Action: Click
      const clickMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/click$/);
      if (method === 'POST' && clickMatch) {
        const elementId = parseInt(clickMatch[2], 10);
        
        // Get coordinates using box model
        const boxResult = await sendCdpCommand('DOM.getBoxModel', { nodeId: elementId });
        if (!boxResult || !boxResult.model || !boxResult.model.content) {
          return sendError('stale element reference', 'Element quad not found', 404);
        }

        const quad = boxResult.model.content;
        const x = (quad[0] + quad[2] + quad[4] + quad[6]) / 4;
        const y = (quad[1] + quad[3] + quad[5] + quad[7]) / 4;

        // Dispatch mouse sequence
        await sendCdpCommand('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none' });
        await sendCdpCommand('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 });
        await sendCdpCommand('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 });

        return sendResponse(200, { value: null });
      }

      // 8. Element Action: Send Keys (value)
      const valueMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/value$/);
      if (method === 'POST' && valueMatch) {
        const elementId = parseInt(valueMatch[2], 10);
        const textVal = Array.isArray(params.value) ? params.value.join('') : params.text || '';

        // Focus then type
        await sendCdpCommand('DOM.focus', { nodeId: elementId });
        await sendCdpCommand('Input.insertText', { text: textVal });

        return sendResponse(200, { value: null });
      }

      // 9. Element Action: Clear
      const clearMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/clear$/);
      if (method === 'POST' && clearMatch) {
        const elementId = parseInt(clearMatch[2], 10);

        // Evaluate clearing script with elementId as inspectedNode
        await sendCdpCommand('Runtime.evaluate', {
          expression: "$0.value = '';",
          inspectedNodeId: elementId
        });

        return sendResponse(200, { value: null });
      }

      // 10. Element Info: Text
      const textMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/text$/);
      if (method === 'GET' && textMatch) {
        const elementId = parseInt(textMatch[2], 10);

        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: '$0.textContent',
          inspectedNodeId: elementId,
          returnByValue: true
        });

        const txt = evalResult.result?.value || '';
        return sendResponse(200, { value: txt });
      }

      // 11. Element Info: Attribute
      const attributeMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/attribute\/([^\/]+)$/);
      if (method === 'GET' && attributeMatch) {
        const elementId = parseInt(attributeMatch[2], 10);
        const name = attributeMatch[3];

        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: `typeof $0.getAttribute === 'function' ? $0.getAttribute('${name}') : $0['${name}']`,
          inspectedNodeId: elementId,
          returnByValue: true
        });

        const val = evalResult.result?.value !== undefined ? evalResult.result.value : null;
        return sendResponse(200, { value: val });
      }

      // 12. Element Info: Property
      const propertyMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/property\/([^\/]+)$/);
      if (method === 'GET' && propertyMatch) {
        const elementId = parseInt(propertyMatch[2], 10);
        const name = propertyMatch[3];

        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: `$0['${name}']`,
          inspectedNodeId: elementId,
          returnByValue: true
        });

        const val = evalResult.result?.value !== undefined ? evalResult.result.value : null;
        return sendResponse(200, { value: val });
      }

      // 13. Element Info: Displayed
      const displayedMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/displayed$/);
      if (method === 'GET' && displayedMatch) {
        const elementId = parseInt(displayedMatch[2], 10);

        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: '$0.isVisible',
          inspectedNodeId: elementId,
          returnByValue: true
        });

        const val = !!evalResult.result?.value;
        return sendResponse(200, { value: val });
      }

      // 14. Element Info: Selected
      const selectedMatch = path.match(/^\/(wd\/hub\/)?session\/[^\/]+\/element\/([^\/]+)\/selected$/);
      if (method === 'GET' && selectedMatch) {
        const elementId = parseInt(selectedMatch[2], 10);

        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: '$0.isChecked || $0.checked',
          inspectedNodeId: elementId,
          returnByValue: true
        });

        const val = !!evalResult.result?.value;
        return sendResponse(200, { value: val });
      }

      // 15. Execute Script Sync
      if (method === 'POST' && /^\/(wd\/hub\/)?session\/[^\/]+\/execute\/sync$/.test(path)) {
        const script = params.script;
        const args = params.args || [];
        const resolvedArgs = args.map(arg => {
          if (arg && typeof arg === 'object') {
            const elementId = arg['element-6066-11e4-a52e-4f735466cecf'] || arg['ELEMENT'];
            if (elementId !== undefined) {
              return `globalThis.__resolveNode(${elementId})`;
            }
          }
          return JSON.stringify(arg);
        });

        const wrappedScript = `(function() {
          const arguments = [${resolvedArgs.join(', ')}];
          ${script}
        })()`;

        const evalResult = await sendCdpCommand('Runtime.evaluate', {
          expression: wrappedScript,
          returnByValue: true
        });
        const val = evalResult.result?.value !== undefined ? evalResult.result.value : null;
        return sendResponse(200, { value: val });
      }

      // 16. Window Rect (size/resize)
      if (method === 'POST' && (/^\/(wd\/hub\/)?session\/[^\/]+\/window\/rect$/.test(path) || /^\/(wd\/hub\/)?session\/[^\/]+\/window\/[^\/]+\/size$/.test(path))) {
        const width = params.width || 1024;
        const height = params.height || 768;

        // Execute resize on backing Window state
        await sendCdpCommand('Runtime.evaluate', {
          expression: `Window.Width = ${width}; Window.Height = ${height};`
        });

        return sendResponse(200, { value: { width, height } });
      }
    }

    // Default error for unhandled requests
    return sendError('unknown command', `Unhandled endpoint: ${method} ${path}`, 404);
  } catch (err) {
    log(`ERROR: ${err.stack || err.message}`);
    return sendError('unknown error', err.message || 'Internal driver error', 500);
  }
}

// Start HTTP Server
const server = http.createServer(handleRequest);
server.listen(PORT, '127.0.0.1', () => {
  log(`Appium CDP Driver server listening on http://127.0.0.1:${PORT}`);
});
