/*
 * Verifica los cambios pedidos con Chrome headless y una cámara virtual que muestra
 * una mano (archivos Y4M):
 *
 *   FASE "flujo"   -> el juego arranca DIRECTO en el menú: ya no hay escaneo de la mano
 *                     (ni pantalla de Calibración ni texto de "ESCANEANDO LA MANO").
 *   FASE "juego"   -> en partida: la mano se detecta, la MANO VIRTUAL se dibuja y el
 *                     balón sale con el gesto de subir la mano.
 *   FASE "puno"    -> con ?gesto=puno y un vídeo de puño->mano abierta: el gesto de
 *                     cerrar el puño y abrir se reconoce y lanza.
 *   FASE "empuje"  -> con ?gesto=empujar y un vídeo de la mano acercándose: el empujón
 *                     hacia la cámara se reconoce y lanza.
 *
 *   node _verificacion/verify_manos_ia.js "http://localhost:8080" _verificacion ia
 *
 * Cada fase abre Chrome con un perfil nuevo, así que el juego parte sin datos
 * guardados: si quedara un escaneo inicial, la fase 1 fallaría.
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const http = require('http');

const BASE = (process.argv[2] || 'http://localhost:8080').replace(/\/$/, '');
const OUT = process.argv[3] || '_verificacion';
const TAG = process.argv[4] || 'ia';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';

fs.mkdirSync(OUT, { recursive: true });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function httpJson(port, route) {
  return new Promise((resolve, reject) => {
    http.get('http://127.0.0.1:' + port + route, (res) => {
      let d = '';
      res.on('data', (c) => (d += c));
      res.on('end', () => { try { resolve(JSON.parse(d)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

async function pageTarget(port) {
  for (let i = 0; i < 80; i++) {
    try {
      const list = await httpJson(port, '/json');
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

function maximoDe(lines, clave) {
  let mejor = 0;
  for (const l of lines) {
    if (l.indexOf('[DIAG] mano') < 0) continue;
    const v = numeroDe(l, clave);
    if (v > mejor) mejor = v;
  }
  return mejor;
}

/** Última lectura de "[DIAG] gestos ..." (cuántos gestos de cada tipo se han visto). */
function ultimoGestos(lines) {
  const propias = lines.filter((l) => l.indexOf('[DIAG] gestos modo=') >= 0);
  if (propias.length === 0) return null;

  const l = propias[propias.length - 1];
  return {
    linea: l,
    modo: (l.match(/modo=([a-z]+)/) || [])[1] || '',
    subir: numeroDe(l, 'subir'),
    empujar: numeroDe(l, 'empujar'),
    puno: numeroDe(l, 'puno'),
    pellizco: numeroDe(l, 'pellizco')
  };
}

async function fase(nombre, url, video, segundos, revisar) {
  const port = 9100 + Math.floor(Math.random() * 500);
  const perfil = 'C:/temp/vrbb_ia_' + nombre + '_' + Date.now();
  const rutaVideo = path.resolve(video).replace(/\\/g, '/');

  console.log('');
  console.log('=== FASE ' + nombre.toUpperCase() + ' -> ' + url);
  console.log('    vídeo: ' + rutaVideo);

  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--enable-unsafe-swiftshader', '--no-sandbox',
    '--no-first-run', '--autoplay-policy=no-user-gesture-required',
    '--use-fake-ui-for-media-stream',
    '--use-fake-device-for-media-stream',
    '--use-file-for-fake-video-capture=' + rutaVideo,
    '--remote-debugging-port=' + port, '--window-size=1280,720', '--hide-scrollbars',
    '--user-data-dir=' + perfil, url
  ], { stdio: 'ignore' });

  const target = await pageTarget(port);
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  const lines = [];
  let nextId = 1;
  const pendientes = new Map();

  const send = (method, params) => new Promise((resolve, reject) => {
    const id = nextId++;
    pendientes.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params: params || {} }));
  });

  ws.addEventListener('message', (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pendientes.has(msg.id)) {
      const p = pendientes.get(msg.id);
      pendientes.delete(msg.id);
      msg.error ? p.reject(new Error(JSON.stringify(msg.error))) : p.resolve(msg.result);
      return;
    }
    if (msg.method === 'Runtime.consoleAPICalled') {
      const texto = (msg.params.args || [])
        .map((a) => (a.value !== undefined ? a.value : (a.description || a.type)))
        .join(' ');
      lines.push(texto);
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

  await sleep(1500);

  if (await evaluate("!!document.querySelector('#vr-start-button')")) {
    // Este clic solo desbloquea cámara y audio del navegador; no es un toque del juego.
    await evaluate("document.querySelector('#vr-start-button').click()");
  }

  const visto = {};

  for (let paso = 0; paso < Math.ceil(segundos / 3); paso++) {
    await sleep(3000);
    revisar(lines, visto);

    if (paso % 4 === 3) {
      console.log('  t=' + (paso + 1) * 3 + 's ' + JSON.stringify(visto));
    }

    if (visto.hecho) {
      break;
    }
  }

  try {
    const r = await send('Page.captureScreenshot', { format: 'png' });
    const archivo = path.join(OUT, TAG + '_' + nombre + '.png');
    fs.writeFileSync(archivo, Buffer.from(r.data, 'base64'));
    console.log('  captura -> ' + archivo);
  } catch (e) { }

  fs.writeFileSync(path.join(OUT, TAG + '_' + nombre + '_consola.txt'), lines.join('\n'));

  ws.close();
  chrome.kill();
  await sleep(600);

  return {
    lines,
    visto,
    gestos: ultimoGestos(lines),
    gestosVistos: maximoDe(lines, 'gestos'),
    tiros: maximoDe(lines, 'tirosMano'),
    errores: lines.filter((l) => /EXCEPCION|\[error\]/.test(l))
  };
}

async function main() {
  const resultados = [];
  const texto = (lines) => lines.join('\n');

  // ---- FASE 1: arranque directo al menú, sin escaneo de la mano ----
  const flujo = await fase('flujo', BASE + '/?diag=1', '_verificacion/mano.y4m', 30, (lines, visto) => {
    const t = texto(lines);

    if (t.indexOf('estado=Menu') >= 0 || t.indexOf('INICIAR PARTIDA') >= 0) visto.menu_sin_escaneo = true;
    if (t.indexOf('estado=Calibracion') >= 0 || t.indexOf('ESCANEANDO LA MANO') >= 0
        || t.indexOf('CALIBRACIÓN DE LA MANO') >= 0) visto.escaneo_inicial = true;

    if (/\[DIAG\] manofuente fuente=|abriendo cámara trasera/.test(t)) visto.camara = true;
    if (/\[DIAG\] mano .*detectada=True/.test(t)) visto.mano_detectada = true;
    if (/\[DIAG\] manovirtual visible=True/.test(t)) visto.mano_virtual = true;
  });

  const flujoOk =
    flujo.visto.menu_sin_escaneo === true &&
    flujo.visto.escaneo_inicial !== true &&
    flujo.visto.camara === true &&
    flujo.visto.mano_detectada === true &&
    flujo.visto.mano_virtual === true &&
    flujo.errores.length === 0;

  resultados.push({ nombre: 'flujo (arranque sin escaneo)', ok: flujoOk, visto: flujo.visto, errores: flujo.errores });
  console.log('-> flujo OK = ' + flujoOk);

  // ---- FASE 2: en partida, mano virtual y tiro subiendo la mano ----
  const juego = await fase('juego', BASE + '/?diag=1&start=1&gesto=subir', '_verificacion/mano.y4m', 60, (lines, visto) => {
    const t = texto(lines);

    if (t.indexOf('estado=Jugando') >= 0) visto.jugando = true;
    if (/\[DIAG\] manovirtual visible=True/.test(t)) visto.mano_virtual = true;
    if (maximoDe(lines, 'gestos') >= 1) visto.gestos = true;
    if (maximoDe(lines, 'tirosMano') >= 1) visto.tiros_con_mano = true;

    if (visto.tiros_con_mano && visto.mano_virtual) visto.hecho = true;
  });

  const juegoOk =
    juego.visto.jugando === true &&
    juego.visto.gestos === true &&
    juego.visto.tiros_con_mano === true &&
    juego.visto.mano_virtual === true &&
    juego.errores.length === 0;

  resultados.push({ nombre: 'juego (mano virtual + gesto subir)', ok: juegoOk, visto: juego.visto, errores: juego.errores });
  console.log('-> juego OK = ' + juegoOk);

  // ---- FASE 3: gesto de puño -> mano abierta ----
  const puno = await fase('puno', BASE + '/?diag=1&start=1&gesto=puno', '_verificacion/mano_puno.y4m', 45, (lines, visto) => {
    if (texto(lines).indexOf('estado=Jugando') >= 0) visto.jugando = true;

    const g = ultimoGestos(lines);
    if (g) {
      visto.modo = g.modo;
      visto.puno = g.puno;
      visto.tiros_con_mano = maximoDe(lines, 'tirosMano');
    }

    if (visto.puno > 0 && visto.tiros_con_mano >= 1) visto.hecho = true;
  });

  const punoOk =
    puno.visto.jugando === true &&
    (puno.visto.puno || 0) > 0 &&
    (puno.visto.tiros_con_mano || 0) >= 1 &&
    puno.gestos && puno.gestos.modo === 'puno' &&
    puno.errores.length === 0;

  resultados.push({ nombre: 'gesto puño y abrir', ok: punoOk, visto: puno.visto, errores: puno.errores });
  console.log('-> puño OK = ' + punoOk);

  // ---- FASE 4: gesto de empujar hacia la cámara ----
  const empuje = await fase('empuje', BASE + '/?diag=1&start=1&gesto=empujar', '_verificacion/mano_empuje.y4m', 45, (lines, visto) => {
    if (texto(lines).indexOf('estado=Jugando') >= 0) visto.jugando = true;

    const g = ultimoGestos(lines);
    if (g) {
      visto.modo = g.modo;
      visto.empujar = g.empujar;
      visto.tiros_con_mano = maximoDe(lines, 'tirosMano');
    }

    if (visto.empujar > 0 && visto.tiros_con_mano >= 1) visto.hecho = true;
  });

  const empujeOk =
    empuje.visto.jugando === true &&
    (empuje.visto.empujar || 0) > 0 &&
    (empuje.visto.tiros_con_mano || 0) >= 1 &&
    empuje.gestos && empuje.gestos.modo === 'empujar' &&
    empuje.errores.length === 0;

  resultados.push({ nombre: 'gesto empujar hacia la cámara', ok: empujeOk, visto: empuje.visto, errores: empuje.errores });
  console.log('-> empuje OK = ' + empujeOk);

  // ---- FASE 5: esqueleto de la mano y gesto de pellizco (mano simulada) ----
  // El vídeo no se usa: con ?simular=1 el juego se inventa la mano con sus 21 puntos,
  // que es justo lo que hace MediaPipe en el móvil. Así se comprueba lo que depende de
  // los puntos articulados: el esqueleto dibujado y el gesto de pellizco.
  const esqueleto = await fase('esqueleto', BASE + '/?diag=1&start=1&simular=1&guion=pellizco&gesto=pellizco',
    '_verificacion/mano.y4m', 45, (lines, visto) => {
      const t = texto(lines);

      if (/\[DIAG\] manofuente fuente=simulada puntos=True/.test(t)) visto.puntos_articulados = true;
      if (texto(lines).indexOf('estado=Jugando') >= 0) visto.jugando = true;

      const g = ultimoGestos(lines);
      if (g) {
        visto.modo = g.modo;
        visto.pellizco = g.pellizco;
        visto.tiros_con_mano = maximoDe(lines, 'tirosMano');
      }

      if (visto.puntos_articulados && visto.pellizco > 0 && visto.tiros_con_mano >= 1) visto.hecho = true;
    });

  const esqueletoOk =
    esqueleto.visto.puntos_articulados === true &&
    (esqueleto.visto.pellizco || 0) > 0 &&
    (esqueleto.visto.tiros_con_mano || 0) >= 1 &&
    esqueleto.gestos && esqueleto.gestos.modo === 'pellizco' &&
    esqueleto.errores.length === 0;

  resultados.push({ nombre: 'esqueleto y gesto de pellizco', ok: esqueletoOk, visto: esqueleto.visto, errores: esqueleto.errores });
  console.log('-> esqueleto y pellizco OK = ' + esqueletoOk);

  // ---- Resumen ----
  console.log('');
  console.log('=== RESUMEN ===');
  for (const r of resultados) {
    console.log('  ' + r.nombre + ' -> ' + (r.ok ? 'OK' : 'FALLO'));
    console.log('    ' + JSON.stringify(r.visto));
    r.errores.slice(0, 4).forEach((e) => console.log('    ' + e));
  }

  const mezcla = [juego, puno, empuje, esqueleto].map((f) => f.gestos).filter(Boolean);
  for (const g of mezcla) {
    console.log('  gestos [' + g.modo + '] subir=' + g.subir + ' empujar=' + g.empujar +
      ' puno=' + g.puno + ' pellizco=' + g.pellizco);
  }

  const ok = flujoOk && juegoOk && punoOk && empujeOk && esqueletoOk;
  console.log('');
  console.log('RESULTADO: ' + (ok
    ? 'OK - sin escaneo inicial, manos virtuales y varias formas de lanzar'
    : 'FALLO'));

  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error('ERROR: ' + e.message);
  process.exit(1);
});
