/*
 * Verifica el GESTO DE LANZAR con la mano (subirla de golpe).
 *
 * Chrome se abre con una cámara virtual cuyo video muestra una mano que se ve
 * quieta y de golpe SUBE (esa subida es el gesto). El juego arranca ya en partida
 * (?start=1) y el script comprueba, leyendo el diagnóstico, que:
 *   1. se abre la CÁMARA TRASERA,
 *   2. la mano se detecta,
 *   3. el gesto se ve (gestos >= 1),
 *   4. el balón sale de verdad con la mano (tirosMano >= 1),
 *   5. no hay errores.
 *
 *   node _verificacion/verify_lanzar.js "http://localhost:8080/?diag=1&start=1" _verificacion lanzar
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const URL = process.argv[2] || 'http://localhost:8080/?diag=1&start=1';
const OUT = process.argv[3] || '_verificacion';
const TAG = process.argv[4] || 'lanzar';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9700 + Math.floor(Math.random() * 250);
const PROFILE = 'C:/temp/vrbb_lanzar_' + Date.now();
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

function numeroDe(linea, clave) {
  const m = linea.match(new RegExp(clave + '=(-?[0-9]+)'));
  return m ? Number(m[1]) : -1;
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
  if (await evaluate("!!document.querySelector('#vr-start-button')")) {
    await evaluate("document.querySelector('#vr-start-button').click()");
  }

  let gestos = 0;
  let tirosMano = 0;
  let camara = false;
  let detectada = false;
  let jugando = false;

  for (let step = 0; step < 32; step++) {
    await sleep(3000);

    const texto = lines.join('\n');
    camara = camara || texto.indexOf('abriendo cámara trasera') >= 0;
    if (/\[DIAG\] mano camara=True/.test(texto)) camara = true;
    if (/\[DIAG\] mano .*detectada=True/.test(texto)) detectada = true;
    if (texto.indexOf('estado=Jugando') >= 0) jugando = true;

    const manos = lines.filter((l) => l.indexOf('[DIAG] mano') >= 0);
    for (const l of manos) {
      const g = numeroDe(l, 'gestos');
      const t = numeroDe(l, 'tirosMano');
      if (g > gestos) gestos = g;
      if (t > tirosMano) tirosMano = t;
    }

    if (step % 2 === 0) {
      console.log('t=' + (step + 1) * 3 + 's  camara=' + camara + ' mano=' + detectada +
        ' jugando=' + jugando + ' gestos=' + gestos + ' tirosMano=' + tirosMano);
    }

    if (tirosMano >= 3) break;
  }

  await shot('1_final');
  fs.writeFileSync(path.join(OUT, TAG + '_consola.txt'), lines.join('\n'));

  console.log('');
  console.log('=== ULTIMAS LECTURAS DE MANO ===');
  lines.filter((l) => l.indexOf('[DIAG] mano') >= 0).slice(-4).forEach((l) => console.log(l));

  const errores = lines.filter((l) => /EXCEPCION|\[error\]/.test(l));

  const ok = camara && detectada && gestos >= 1 && tirosMano >= 1 && errores.length === 0;

  console.log('');
  console.log('=== RESUMEN ===');
  console.log('  camara_trasera = ' + camara);
  console.log('  mano_detectada = ' + detectada);
  console.log('  jugando = ' + jugando);
  console.log('  gestos_vistos = ' + gestos);
  console.log('  tiros_con_mano = ' + tirosMano);
  console.log('  errores = ' + errores.length);

  console.log('');
  console.log('RESULTADO: ' + (ok ? 'OK - el gesto de subir la mano lanza' : 'FALLO'));

  ws.close();
  chrome.kill();
  await sleep(500);
  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error('ERROR: ' + e.message);
  try { chrome.kill(); } catch (err) { }
  process.exit(1);
});
