const { chromium } = require('playwright');
const http = require('http');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

async function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          resolve(JSON.parse(data));
        } catch (e) {
          reject(e);
        }
      });
    }).on('error', reject);
  });
}

async function waitPort(url, timeoutMs = 15000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      await getJson(url);
      return true;
    } catch (e) {
      await new Promise(r => setTimeout(r, 500));
    }
  }
  return false;
}

async function main() {
  const artifactsDir = '/Users/wieslawsoltes/.gemini/antigravity/brain/32ddf4c1-722a-41d6-a34b-c408166cc350';
  const prefix = process.argv[2] || 'before';
  const screenshotPath = path.join(artifactsDir, `inspector_${prefix}.png`);

  const sampleLog = fs.createWriteStream(path.join(artifactsDir, 'sample_run.log'));
  const inspectorLog = fs.createWriteStream(path.join(artifactsDir, 'inspector_run.log'));

  console.log("Starting CdpSampleApp...");
  const sampleProcess = spawn('dotnet', ['run', '--project', 'samples/CdpSampleApp/CdpSampleApp.csproj']);
  sampleProcess.stdout.pipe(sampleLog);
  sampleProcess.stderr.pipe(sampleLog);

  console.log("Starting CdpInspectorApp...");
  const inspectorProcess = spawn('dotnet', ['run', '--project', 'samples/CdpInspectorApp/CdpInspectorApp.csproj']);
  inspectorProcess.stdout.pipe(inspectorLog);
  inspectorProcess.stderr.pipe(inspectorLog);

  console.log("Waiting for apps to listen on ports 9222 and 9223...");
  const sampleOk = await waitPort('http://127.0.0.1:9222/json');
  const inspectorOk = await waitPort('http://127.0.0.1:9223/json');

  if (!sampleOk || !inspectorOk) {
    console.error(`Failed to start apps! Sample status: ${sampleOk}, Inspector status: ${inspectorOk}`);
    cleanup(sampleProcess, inspectorProcess);
    process.exit(1);
  }
  console.log("Apps started and listening successfully!");

  // Wait extra time for UI windows to initialize and display
  await new Promise(r => setTimeout(r, 6000));

  try {
    console.log("Connecting Playwright to CdpInspectorApp at http://127.0.0.1:9223...");
    const browser = await chromium.connectOverCDP('http://127.0.0.1:9223');
    const context = browser.contexts()[0];
    const pages = context.pages();
    console.log("Inspector pages discovered:", pages.map(p => p.url()));
    
    const inspectorPage = pages[0];
    if (!inspectorPage) {
      throw new Error("No inspector page found!");
    }

    // Connect inspector to sample app:
    // 1. Click #btnRefreshTargets
    console.log("Clicking #btnRefreshTargets on inspector...");
    await inspectorPage.locator('#btnRefreshTargets').click();
    await inspectorPage.waitForTimeout(1500);

    // 2. Click #btnConnect
    console.log("Clicking #btnConnect on inspector...");
    await inspectorPage.locator('#btnConnect').click();
    await inspectorPage.waitForTimeout(3000);

    // Get the webSocketDebuggerUrl of the inspector window
    const inspectorTargets = await getJson('http://127.0.0.1:9223/json');
    const mainTarget = inspectorTargets[0];
    if (!mainTarget) throw new Error("Could not find inspector target in /json!");
    const wsUrl = mainTarget.webSocketDebuggerUrl;

    console.log(`Connecting direct WebSocket to inspector: ${wsUrl}...`);
    const directWs = new WebSocket(wsUrl);
    
    const screenshotPromise = new Promise((resolve, reject) => {
      directWs.onopen = () => {
        console.log("WebSocket connected. Sending Page.captureScreenshot...");
        directWs.send(JSON.stringify({ id: 999, method: 'Page.captureScreenshot' }));
      };
      directWs.onmessage = (event) => {
        try {
          const res = JSON.parse(event.data);
          if (res.id === 999) {
            if (res.result && res.result.data) {
              resolve(res.result.data);
            } else if (res.error) {
              reject(new Error("CDP error: " + JSON.stringify(res.error)));
            } else {
              reject(new Error("Unknown response format: " + event.data));
            }
            directWs.close();
          }
        } catch (e) {
          reject(e);
          directWs.close();
        }
      };
      directWs.onerror = (err) => {
        reject(err);
      };
    });

    const base64Data = await screenshotPromise;
    const buffer = Buffer.from(base64Data, 'base64');
    fs.writeFileSync(screenshotPath, buffer);
    console.log("Screenshot successfully saved via direct WebSocket to:", screenshotPath);

    await browser.close();
  } catch (err) {
    console.error("Error during inspector automation:", err);
  }

  console.log("Shutting down CdpSampleApp and CdpInspectorApp...");
  cleanup(sampleProcess, inspectorProcess);
}

function cleanup(sampleProc, inspectorProc) {
  try {
    process.kill(sampleProc.pid);
  } catch (e) {}
  try {
    process.kill(inspectorProc.pid);
  } catch (e) {}
  
  // Extra kill via shell just in case port remains bound
  const { execSync } = require('child_process');
  try {
    const pids = execSync("lsof -t -iTCP:9222 -sTCP:LISTEN").toString().trim();
    if (pids) {
      console.log("Killing remaining processes on port 9222:", pids);
      pids.split('\n').forEach(pid => {
        try { process.kill(Number(pid), 'SIGKILL'); } catch(e) {}
      });
    }
  } catch(e) {}
  try {
    const pids = execSync("lsof -t -iTCP:9223 -sTCP:LISTEN").toString().trim();
    if (pids) {
      console.log("Killing remaining processes on port 9223:", pids);
      pids.split('\n').forEach(pid => {
        try { process.kill(Number(pid), 'SIGKILL'); } catch(e) {}
      });
    }
  } catch(e) {}
}

main().catch(console.error);
