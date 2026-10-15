/*
 * Verificación del juego en Chrome (headless) usando el protocolo DevTools.
 *   node verify.js <url> <carpetaSalida> <segundos,separados,por,comas>
 * Guarda capturas reales y el registro de consola/errores del juego.
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const url = process.argv[2] || 'http://localhost:8080/';
const outDir = process.argv[3] || '_verificacion';
const moments = (process.argv[4] || '20,40,65').split(',').map(Number);
const tag = process.argv[5] || 'juego';

const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9222 + Math.floor(Math.random() * 300);
const profileDir = 'C:/temp/vrbb_cdp_' + Date.now();

fs.mkdirSync(outDir, { recursive: true });

const chrome = spawn(CHROME, [
  
  
  
  '--no-sandbox',
  '--no-first-run',
  '--disable-features=Translate,BackForwardCache',
  '--autoplay-policy=no-user-gesture-required',
  '--remote-debugging-port=' + PORT,
  '--window-size=1600,900',
  '--force-device-scale-factor=1',
  '--hide-scrollbars',
  '--user-data-dir=' + profileDir,
  url
], { stdio: 'ignore' });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function httpJson(route) {
  return new Promise((resolve, reject) => {
    http.get('http://127.0.0.1:' + PORT + route, (res) => {
      let data = '';
      res.on('data', (c) => (data += c));
      res.on('end', () => {
        try { resolve(JSON.parse(data)); } catch (e) { reject(e); }
      });
    }).on('error', reject);
  });
}

async function findPage() {
  for (let i = 0; i < 60; i++) {
    try {
      const targets = await httpJson('/json');
      const page = targets.find((t) => t.type === 'page' && t.webSocketDebuggerUrl);
      if (page) return page;
    } catch (e) { /* todavía no arrancó */ }
    await sleep(400);
  }
  throw new Error('Chrome no expuso ninguna pestaña');
}

async function main() {
  const page = await findPage();
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let nextId = 1;
  const pending = new Map();
  const consoleLines = [];

  const send = (method, params) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params: params || {} }));
  });

  ws.addEventListener('message', (event) => {
    const msg = JSON.parse(event.data);
    if (msg.id && pending.has(msg.id)) {
      const p = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? p.reject(new Error(JSON.stringify(msg.error))) : p.resolve(msg.result);
      return;
    }
    if (msg.method === 'Runtime.consoleAPICalled') {
      const text = (msg.params.args || [])
        .map((a) => (a.value !== undefined ? a.value : a.description || a.type))
        .join(' ');
      consoleLines.push('[' + msg.params.type + '] ' + text);
    }
    if (msg.method === 'Runtime.exceptionThrown') {
      const d = msg.params.exceptionDetails;
      consoleLines.push('[EXCEPCION] ' + (d.exception && d.exception.description ? d.exception.description : d.text));
    }
    if (msg.method === 'Log.entryAdded') {
      const e = msg.params.entry;
      if (e.level === 'error' || e.level === 'warning') {
        consoleLines.push('[' + e.level + '] ' + e.text);
      }
    }
  });

  await new Promise((resolve, reject) => {
    ws.addEventListener('open', resolve);
    ws.addEventListener('error', reject);
  });

  await send('Runtime.enable');
  await send('Log.enable');
  await send('Page.enable');

  const start = Date.now();
  const shots = [];

  for (const moment of moments) {
    const wait = moment * 1000 - (Date.now() - start);
    if (wait > 0) await sleep(wait);

    const result = await send('Page.captureScreenshot', { format: 'png' });
    const file = path.join(outDir, tag + '_' + moment + 's.png');
    fs.writeFileSync(file, Buffer.from(result.data, 'base64'));
    shots.push(file + ' (' + fs.statSync(file).size + ' bytes)');
    console.log('captura t=' + moment + 's -> ' + file);
  }

  try {
    const info = await send('Runtime.evaluate', {
      expression: 'JSON.stringify({url: location.href, canvas: !!document.querySelector("#unity-canvas"), w: window.innerWidth, h: window.innerHeight})',
      returnByValue: true
    });
    console.log('pagina: ' + (info.result && info.result.value));
  } catch (e) { /* opcional */ }

  fs.writeFileSync(path.join(outDir, tag + '_consola.txt'), consoleLines.join('\n') || '(sin mensajes)');
  console.log('mensajes de consola: ' + consoleLines.length);
  consoleLines.slice(0, 30).forEach((l) => console.log('   ' + l));

  ws.close();
  chrome.kill();
  await sleep(600);
  process.exit(0);
}

main().catch((e) => {
  console.error('ERROR: ' + e.message);
  try { chrome.kill(); } catch (err) { }
  process.exit(1);
});
