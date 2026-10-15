#!/usr/bin/env node
/*
 * Servidor local para probar la beta en el celular dentro de los lentes.
 *
 *   node tools/serve.js            -> sirve Builds/WebGL en HTTPS (puerto 8443)
 *   node tools/serve.js --http     -> sirve en HTTP (puerto 8080)
 *
 * HTTPS es necesario para que el navegador permita usar la CAMARA (calibración
 * de la mano) y los SENSORES de orientación. El certificado es autofirmado:
 * el navegador mostrará una advertencia la primera vez ("Avanzado -> Continuar").
 */

const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const os = require('os');
const { execFileSync } = require('child_process');

const useHttp = process.argv.includes('--http');
const port = Number(process.env.PORT || (useHttp ? 8080 : 8443));

const projectRoot = path.resolve(__dirname, '..');
const root = path.resolve(
  process.env.WEBROOT || path.join(projectRoot, 'Builds', 'WebGL')
);

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.mjs': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
  '.symbols': 'application/octet-stream',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
  '.br': 'application/octet-stream',
  '.gz': 'application/octet-stream'
};

function handler(req, res) {
  const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
  let filePath = path.join(root, urlPath === '/' ? 'index.html' : urlPath);

  if (!filePath.startsWith(root)) {
    res.writeHead(403);
    res.end('Prohibido');
    return;
  }

  fs.stat(filePath, (err, stat) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end('No encontrado: ' + urlPath);
      return;
    }

    if (stat.isDirectory()) {
      filePath = path.join(filePath, 'index.html');
    }

    const ext = path.extname(filePath).toLowerCase();
    const headers = {
      'Content-Type': MIME[ext] || 'application/octet-stream',
      'Cache-Control': 'no-store',
      'Cross-Origin-Opener-Policy': 'same-origin',
      'Cross-Origin-Embedder-Policy': 'require-corp'
    };

    if (ext === '.wasm') {
      headers['Content-Type'] = 'application/wasm';
    }
    if (ext === '.br') {
      headers['Content-Encoding'] = 'br';
    }
    if (ext === '.gz') {
      headers['Content-Encoding'] = 'gzip';
    }

    fs.createReadStream(filePath)
      .on('error', () => {
        res.writeHead(500);
        res.end('Error leyendo el archivo');
      })
      .pipe(res.writeHead(200, headers) && res);
  });
}

function ensureCertificate() {
  const certDir = path.join(__dirname, 'certs');
  const keyPath = path.join(certDir, 'key.pem');
  const certPath = path.join(certDir, 'cert.pem');

  if (fs.existsSync(keyPath) && fs.existsSync(certPath)) {
    return { keyPath, certPath };
  }

  fs.mkdirSync(certDir, { recursive: true });

  try {
    execFileSync('openssl', [
      'req', '-x509', '-newkey', 'rsa:2048', '-nodes',
      '-keyout', keyPath, '-out', certPath,
      '-days', '365', '-subj', '/CN=vr-basketball-local'
    ], { stdio: 'ignore' });
  } catch (e) {
    console.warn('No se pudo generar el certificado con openssl:', e.message);
    return null;
  }

  return { keyPath, certPath };
}

function lanAddresses() {
  const result = [];
  const interfaces = os.networkInterfaces();
  for (const name of Object.keys(interfaces)) {
    for (const info of interfaces[name] || []) {
      if (info.family === 'IPv4' && !info.internal) {
        result.push(info.address);
      }
    }
  }
  return result;
}

function banner(scheme) {
  console.log('');
  console.log('  VR Basketball Challenge - servidor de pruebas');
  console.log('  Carpeta: ' + root);
  console.log('');
  if (!fs.existsSync(root)) {
    console.log('  [!] La carpeta no existe todavía. Compila la versión WebGL primero.');
    console.log('');
  }
  console.log('  En esta PC:  ' + scheme + '://localhost:' + port);
  for (const ip of lanAddresses()) {
    console.log('  En el celular: ' + scheme + '://' + ip + ':' + port);
  }
  console.log('');
  if (scheme === 'https') {
    console.log('  El certificado es autofirmado: acepta la advertencia en el navegador.');
  }
  console.log('  Pon el teléfono en los lentes y abre la dirección del celular.');
  console.log('');
}

if (useHttp) {
  http.createServer(handler).listen(port, '0.0.0.0', () => banner('http'));
} else {
  const cert = ensureCertificate();
  if (!cert) {
    console.log('Sin certificado: arrancando en HTTP (la cámara no funcionará en el celular).');
    http.createServer(handler).listen(port, '0.0.0.0', () => banner('http'));
  } else {
    https.createServer({
      key: fs.readFileSync(cert.keyPath),
      cert: fs.readFileSync(cert.certPath)
    }, handler).listen(port, '0.0.0.0', () => banner('https'));
  }
}
