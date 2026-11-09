const http = require('http');
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../Builds/ItchWeb-current');
const types = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.wasm': 'application/wasm', '.png': 'image/png', '.ico': 'image/x-icon' };
http.createServer((req, res) => {
  const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
  const file = path.resolve(root, '.' + (pathname === '/' ? '/index.html' : pathname));
  if (!file.startsWith(root + path.sep)) { res.writeHead(403); res.end(); return; }
  fs.readFile(file, (err, bytes) => {
    if (err) { console.log('404', pathname); res.writeHead(404); res.end(); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Content-Length': bytes.length, 'Cache-Control': 'no-store' });
    res.end(bytes);
    console.log('200', pathname, bytes.length);
  });
}).listen(8767, '127.0.0.1', () => console.log('Current-build preview: http://127.0.0.1:8767'));
