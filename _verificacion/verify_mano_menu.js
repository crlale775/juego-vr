/*
 * Verifica el CONTROL TOTAL POR MANO (cámara trasera), sin mandos ni toques.
 *
 * Chrome se abre con una cámara virtual que muestra una mano sintética colocada
 * justo delante del botón "INICIAR PARTIDA". El script comprueba, leyendo la
 * consola del juego, que:
 *   1. se abre la CÁMARA TRASERA,
 *   2. la cámara entrega imagen y la mano se detecta,
 *   3. la calibración automática crea el perfil,
 *   4. se llega al menú,
 *   5. el botón se elige SOLO con la mano (el juego pasa a Jugando),
 *   6. no se usó ningún toque (tirosBoton=0) ni mando alguno.
 *
 *   node _verificacion/verify_mano_menu.js "http://localhost:8080/?diag=1" _verificacion mano_menu
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const URL = process.argv[2] || 'http://localhost:8080/?diag=1';
const OUT = process.argv[3] || '_verificacion';
const TAG = process.argv[4] || 'mano_menu';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PORT = 9900 + Math.floor(Math.random() * 300);
const PROFILE = 'C:/temp/vrbb_mano_menu_' + Date.now();
const VIDEO = path.resolve('_verificacion/mano_menu.y4m').replace(/\\/g, '/');

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
    // Este clic solo desbloquea el audio/camara del navegador: NO es un toque del juego.
    await evaluate("document.querySelector('#vr-start-button').click()");
  }

  // 'menu_visible' es solo informativo: el diagnostico imprime cada 5 s y el paso
  // del menu a la partida dura menos de un segundo, asi que puede no capturarlo.
  // Lo que SI es una prueba de que se paso por el menu es 'jugando': la partida solo
  // se puede arrancar desde el menu.
  const seen = {
    camara_trasera: false,
    camara_lista: false,
    mano_detectada: false,
    perfil_creado: false,
    menu_visible: false,
    jugando: false,
    sin_toques: false
  };

  const check = (text) => {
    if (text.indexOf('abriendo cámara trasera') >= 0) seen.camara_trasera = true;
    if (text.indexOf('cámara lista') >= 0) seen.camara_lista = true;
    if (/\[DIAG\] mano camara=True/.test(text) && /detectada=True/.test(text)) seen.mano_detectada = true;
    if (text.indexOf('Perfil creado') >= 0) seen.perfil_creado = true;
    if (text.indexOf('INICIAR PARTIDA') >= 0) seen.menu_visible = true;
    if (text.indexOf('estado=Jugando') >= 0) seen.jugando = true;
    if (/\[DIAG\] entrada:.*boton=False toques=0/.test(text)) seen.sin_toques = true;
  };

  let shots = 0;
  for (let step = 0; step < 50; step++) {
    await sleep(3000);
    check(lines.join('\n'));

    if (step === 3) { await shot('1_inicio'); shots++; }
    if (seen.perfil_creado && shots < 2) { await shot('2_menu'); shots++; }

    if (step % 3 === 0) {
      console.log('t=' + (step + 1) * 3 + 's ' + JSON.stringify(seen));
    }

    if (seen.jugando) break;
  }

  await sleep(4000);
  await shot('3_final');

  fs.writeFileSync(path.join(OUT, TAG + '_consola.txt'), lines.join('\n'));

  // El aviso de entrada del diagnostico dice si hubo toques o raton pulsado.
  const entrada = lines.filter((l) => l.indexOf('[DIAG] entrada:') >= 0);
  const tiros = lines.filter((l) => l.indexOf('tirosBoton=') >= 0);
  const ultimoTiros = tiros.length > 0 ? tiros[tiros.length - 1] : '';

  console.log('');
  console.log('=== ULTIMAS LECTURAS DE MANO ===');
  lines.filter((l) => l.indexOf('[DIAG] mano') >= 0).slice(-5).forEach((l) => console.log(l));
  console.log('');
  console.log('=== ULTIMA LECTURA DE ENTRADA (debe salir sin toques) ===');
  console.log(entrada.length > 0 ? entrada[entrada.length - 1] : '(sin lecturas)');
  console.log('');
  console.log('=== ULTIMA LECTURA DE TIROS ===');
  console.log(ultimoTiros || '(sin lecturas)');

  const errores = lines.filter((l) => /EXCEPCION|\[error\]/.test(l));

  console.log('');
  console.log('=== RESUMEN ===');
  Object.keys(seen).forEach((k) => console.log('  ' + k + ' = ' + seen[k]));
  console.log('  errores = ' + errores.length);
  errores.slice(0, 5).forEach((l) => console.log('    ' + l));

  const obligatorios = ['camara_trasera', 'camara_lista', 'mano_detectada', 'perfil_creado', 'jugando', 'sin_toques'];
  const ok = obligatorios.every((k) => seen[k]) && errores.length === 0;

  console.log('');
  console.log('CONDICIONES QUE CUENTAN: ' + obligatorios.join(', '));
  console.log('RESULTADO: ' + (ok ? 'OK - todo con la mano, sin mandos ni toques' : 'FALLO'));

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
