/*
 * Verificación de flujo completo del juego en Chrome.
 * Hace clics reales: boton de arranque, perfil de mano y continuar.
 *
 *   node verify_flujo.js
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const URL = process.argv[2] || 'http://localhost:8080/?diag=1';
const OUT = process.argv[3] || '_verificacion';
const TAG = process.argv[4] || 'flujo';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9600 + Math.floor(Math.random() * 300);
const PROFILE = 'C:/temp/vrbb_flow_' + Date.now();

fs.mkdirSync(OUT, { recursive: true });

const chrome = spawn(CHROME, [
  '--headless=new', '--disable-gpu', '--enable-unsafe-swiftshader', '--no-sandbox',
  '--no-first-run', '--autoplay-policy=no-user-gesture-required',
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
      lines.push('[' + msg.params.type + '] ' + text);
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

  // Unity mide y desde abajo; el navegador desde arriba. Hay que invertirla.
  const clickUnity = async (x, unityY) => {
    const h = await evaluate('innerHeight');
    const vx = Math.round(x);
    const vy = Math.round(h - unityY);
    await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: vx, y: vy, button: 'none' });
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: vx, y: vy, button: 'left', clickCount: 1 });
    await sleep(80);
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: vx, y: vy, button: 'left', clickCount: 1 });
    console.log('   clic en pantalla (' + vx + ',' + vy + ')  [unity y=' + unityY + ']');
  };

  // Busca la posición en pantalla de un texto registrado por el modo diagnóstico.
  const findText = (label) => {
    const needle = "contenido='" + label + "'";
    for (let i = lines.length - 1; i >= 0; i--) {
      if (lines[i].indexOf(needle) >= 0) {
        const m = lines[i].match(/pantalla=\((-?[\d.]+),\s*(-?[\d.]+)\)/);
        if (m) return { x: Math.round(parseFloat(m[1])), y: Math.round(parseFloat(m[2])), line: lines[i] };
      }
    }
    return null;
  };

  await sleep(1500);

  // 1) Botón de arranque de la plantilla HTML
  const hasStart = await evaluate("!!document.querySelector('#vr-start-button')");
  console.log('boton de arranque presente: ' + hasStart);
  if (hasStart) {
    await evaluate("document.querySelector('#vr-start-button').click()");
    console.log('   clic en TOCA PARA EMPEZAR');
  }

  const demo = URL.indexOf('demo=1') >= 0;

  // 2) Esperar la carga del juego
  await sleep(30000);
  await shot('1_calibracion');

  if (demo) {
    // El modo demo arranca la partida solo: solo hay que ver como evoluciona.
    await sleep(12000);
    await shot('2_jugando');
    await sleep(18000);
    await shot('3_jugando');
  } else {
    // 3) Elegir un perfil de mano manual
    const manual = findText('MANO MEDIA');
    if (manual) {
      await clickUnity(manual.x, manual.y);
      await sleep(7000);
      await shot('2_perfil');
    } else {
      console.log('   [!] no se encontro el boton MANO MEDIA');
    }

    // 4) Continuar al modo lentes
    const go = findText('PONER LOS LENTES Y JUGAR');
    if (go) {
      await clickUnity(go.x, go.y);
      await sleep(7000);
      await shot('3_menu');
    } else {
      console.log('   [!] no se encontro el boton PONER LOS LENTES Y JUGAR');
    }
  }

  // 5) Estado final
  console.log('info: ' + await evaluate(
    "JSON.stringify({url: location.href, canvas: !!document.querySelector('#unity-canvas'), w: innerWidth, h: innerHeight, dpr: devicePixelRatio})"
  ));

  const diag = lines.filter((l) => l.indexOf('[DIAG') >= 0);
  fs.writeFileSync(path.join(OUT, TAG + '_diag.txt'), diag.join('\n'));
  fs.writeFileSync(path.join(OUT, TAG + '_consola.txt'), lines.join('\n'));

  console.log('');
  console.log('=== ESTADO INTERNO (DIAG) ===');
  const interesting = diag.filter((l) => l.indexOf('lienzo') < 0 || l.indexOf('Calibracion') >= 0);
  interesting.slice(0, 60).forEach((l) => console.log(l.replace(/^\[log\]\s*/, '')));

  console.log('');
  console.log('=== ERRORES ===');
  lines.filter((l) => /^\[error\]|EXCEPCION/.test(l)).slice(0, 12).forEach((l) => console.log(l));

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
