/*
 * Verifica el escaneo AUTOMÁTICO de la mano.
 *
 * Chrome se abre con una cámara virtual que muestra una mano (archivo Y4M) y el
 * script comprueba que la calibración avanza sola, sin pulsar ningún botón,
 * hasta el panel de resultado y luego al menú.
 *
 *   node _verificacion/verify_mano.js "http://localhost:8081/?diag=1" _verificacion mano
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const URL = process.argv[2] || 'http://localhost:8081/?diag=1';
const OUT = process.argv[3] || '_verificacion';
const TAG = process.argv[4] || 'mano';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9600 + Math.floor(Math.random() * 300);
const PROFILE = 'C:/temp/vrbb_mano_' + Date.now();
const VIDEO = path.resolve('_verificacion/mano.y4m').replace(/\\/g, '/');

fs.mkdirSync(OUT, { recursive: true });

const chrome = spawn(CHROME, [
  '--headless=new', '--disable-gpu', '--enable-unsafe-swiftshader', '--no-sandbox',
  '--no-first-run', '--autoplay-policy=no-user-gesture-required',
  '--use-fake-ui-for-media-stream',
  '--use-fake-device-for-media-stream',
  '--use-file-for-fake-video-capture=' + VIDEO,
  '--remote-debugging-port=' + PORT, '--window-size=1280,720', '--hide-scrollbars',
  '--user-data-dir=' + PROFILE, URL
], { stdio: 'ignore' });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function httpJson(route) {
  return new Promise((resolve, reject) => {
    http.get('http://127.0.0.1:' + PORT + route, (res) => {
      let d = '';
      res.on('data', (c) => (d += c));
      res.on('end', () => { try { resolve(JSON.parse(d)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

async function pageTarget() {
  for (let i = 0; i < 80; i++) {
    try {
      const list = await httpJson('/json');
      const page = list.find((t) => t.type === 'page' && t.webSocketDebuggerUrl);
      if (page) return page;
    } catch (e) { }
    await sleep(300);
  }
  throw new Error('Chrome no expuso la pestaña');
}

async function main() {
  const target = await pageTarget();
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  let nextId = 1;
  const pending = new Map();
  const lines = [];

  const send = (method, params) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params: params || {} }));
  });

  ws.addEventListener('message', (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pending.has(msg.id)) {
      const p = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? p.reject(new Error(JSON.stringify(msg.error))) : p.resolve(msg.result);
      return;
    }
    if (msg.method === 'Runtime.consoleAPICalled') {
      const text = (msg.params.args || [])
        .map((a) => (a.value !== undefined ? a.value : (a.description || a.type)))
        .join(' ');
      lines.push(text);
    }
    if (msg.method === 'Runtime.exceptionThrown') {
      const d = msg.params.exceptionDetails;
      lines.push('[EXCEPCION] ' + ((d.exception && d.exception.description) || d.text));
    }
  });

  await new Promise((res, rej) => {
    ws.addEventListener('open', res);
    ws.addEventListener('error', rej);
  });

  await send('Runtime.enable');
  await send('Page.enable');

  const evaluate = async (expr) => {
    const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true });
    return r.result ? r.result.value : undefined;
  };

  const shot = async (name) => {
    const r = await send('Page.captureScreenshot', { format: 'png' });
    const file = path.join(OUT, TAG + '_' + name + '.png');
    fs.writeFileSync(file, Buffer.from(r.data, 'base64'));
    console.log('captura ' + name + ' -> ' + file);
  };

  await sleep(1500);

  const hasStart = await evaluate("!!document.querySelector('#vr-start-button')");
  console.log('boton de arranque presente: ' + hasStart);
  if (hasStart) {
    await evaluate("document.querySelector('#vr-start-button').click()");
  }

  // El juego tarda en cargar; después se deja correr el escaneo automático.
  await sleep(30000);
  await shot('1_escaneo');

  let reachedResult = false;
  let reachedMenu = false;

  for (let step = 0; step < 14; step++) {
    await sleep(7000);

    const text = lines.join('\n');
    reachedResult = reachedResult || text.indexOf('Perfil creado') >= 0;
    reachedMenu = reachedMenu || text.indexOf('INICIAR PARTIDA') >= 0;

    console.log('t=' + (30 + (step + 1) * 7) + 's  perfil=' + reachedResult + '  menu=' + reachedMenu);

    if (step === 1) await shot('2_escaneo');
    if (reachedResult && step >= 2) { await shot('3_resultado'); break; }
  }

  await sleep(6000);
  await shot('4_final');

  fs.writeFileSync(path.join(OUT, TAG + '_consola.txt'), lines.join('\n'));

  const interesting = lines.filter((l) =>
    l.indexOf('mano detectada') >= 0 ||
    l.indexOf('Perfil creado') >= 0 ||
    l.indexOf('buscando la mano') >= 0 ||
    l.indexOf('Escaneo') >= 0 ||
    l.indexOf('Escaneando') >= 0 ||
    l.indexOf('Calibracion') >= 0);

  console.log('');
  console.log('=== LECTURAS DEL ESCANEO (ultimas 12) ===');
  interesting.slice(-12).forEach((l) => console.log(l.replace(/^\[log\]\s*/, '').trim()));

  console.log('');
  console.log('=== ERRORES ===');
  const errors = lines.filter((l) => /EXCEPCION|\[error\]/.test(l));
  errors.slice(0, 10).forEach((l) => console.log(l));
  if (errors.length === 0) console.log('(ninguno)');

  console.log('');
  console.log('RESULTADO: perfil_creado=' + reachedResult + '  menu=' + reachedMenu);
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
