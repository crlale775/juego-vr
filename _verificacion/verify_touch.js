/*
 * Comprueba que el menu se puede pulsar TOCANDO la pantalla.
 *   node verify_touch.js <url> <x> <y> <carpetaSalida>
 * Hace un clic real (como un toque) en (x,y) en pixels de PANTALLA del juego
 * (los mismos que usa Screen.width/height) y luego informa del estado.
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const url = process.argv[2] || 'http://localhost:8080/?diag=1';
const clickX = Number(process.argv[3] || 622);
const clickY = Number(process.argv[4] || 946);
const outDir = process.argv[5] || '_verificacion';

const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9800 + Math.floor(Math.random() * 150);
const profileDir = 'C:/temp/vrbb_touch_' + Date.now();

fs.mkdirSync(outDir, { recursive: true });

const chrome = spawn(CHROME, [
  '--no-sandbox', '--no-first-run',
  '--disable-features=Translate,BackForwardCache',
  '--autoplay-policy=no-user-gesture-required',
  '--remote-debugging-port=' + PORT,
  '--window-size=1280,720',
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
      res.on('end', () => { try { resolve(JSON.parse(data)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

async function findPage() {
  for (let i = 0; i < 90; i++) {
    try {
      const targets = await httpJson('/json');
      const page = targets.find((t) => t.type === 'page' && t.webSocketDebuggerUrl);
      if (page) return page;
    } catch (e) { }
    await sleep(400);
  }
  throw new Error('Chrome no expuso ninguna pestaña');
}

async function main() {
  const page = await findPage();
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let nextId = 1;
  const pending = new Map();
  const logs = [];

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
      const text = (msg.params.args || []).map((a) => (a.value !== undefined ? a.value : a.description || a.type)).join(' ');
      logs.push(text);
    }
  });

  await new Promise((resolve, reject) => {
    ws.addEventListener('open', resolve);
    ws.addEventListener('error', reject);
  });

  await send('Runtime.enable');
  await send('Page.enable');

  // Esperar a que cargue y al menu
  await sleep(30000);

  const info = await send('Runtime.evaluate', {
    expression: 'JSON.stringify({w: window.innerWidth, h: window.innerHeight, dpr: window.devicePixelRatio})',
    returnByValue: true
  });
  console.log('ventana: ' + (info.result && info.result.value));

  const win = JSON.parse(info.result.value);
  const gameW = Number(process.argv[6] || 3072);
  const gameH = Number(process.argv[7] || 1808);

  // Unity mide el raton en pixels de su lienzo (Screen.width/height), asi que
  // hay que pasar de esos pixels a pixels CSS de la pagina.
  const cssX = clickX * win.w / gameW;
  const cssY = clickY * win.h / gameH;

  console.log('clic en pantalla del juego (' + clickX + ',' + clickY + ') -> CSS (' + cssX.toFixed(0) + ',' + cssY.toFixed(0) + ')');

  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: cssX, y: cssY, button: 'none' });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: cssX, y: cssY, button: 'left', buttons: 1, clickCount: 1 });
  await sleep(120);
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: cssX, y: cssY, button: 'left', buttons: 0, clickCount: 1 });

  await sleep(6000);

  const shot = await send('Page.captureScreenshot', { format: 'png' });
  const file = path.join(outDir, 'touch_resultado.png');
  fs.writeFileSync(file, Buffer.from(shot.data, 'base64'));

  const estados = [...new Set(logs.map((l) => (l.match(/estado=([A-Za-z]+)/) || [])[1]).filter(Boolean))];
  console.log('estados vistos: ' + JSON.stringify(estados));
  console.log('captura: ' + file);

  ws.close();
  chrome.kill();
  await sleep(500);
  process.exit(0);
}

main().catch((e) => {
  console.error('ERROR: ' + e.message);
  try { chrome.kill(); } catch (err) { }
  process.exit(1);
});
