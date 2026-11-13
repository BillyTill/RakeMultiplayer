'use strict';

const { spawn } = require('child_process');
const path = require('path');

const child = spawn(process.execPath, [path.join(__dirname, 'server.js')], {
  cwd: __dirname,
  stdio: ['pipe', 'pipe', 'pipe']
});

let buffer = '';
child.stdout.on('data', chunk => {
  buffer += chunk.toString('utf8');
  let index;
  while ((index = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, index).trim();
    buffer = buffer.slice(index + 1);
    if (!line) continue;
    try {
      const msg = JSON.parse(line);
      const result = msg.result;
      if (result && result.content) {
        const kinds = result.content.map(c => c.type).join(',');
        const text = result.content.filter(c => c.type === 'text').map(c => c.text).join(' | ');
        console.log('RESPONSE', msg.id, kinds, text.slice(0, 400));
      } else {
        console.log('RESPONSE', msg.id, JSON.stringify(result || msg.error).slice(0, 400));
      }
    } catch (err) {
      console.log('RAW', line);
    }
  }
});
child.stderr.on('data', d => console.log('ERR', d.toString()));

function send(obj) {
  child.stdin.write(JSON.stringify(obj) + '\n');
}

send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2024-11-05', capabilities: {}, clientInfo: { name: 'smoke', version: '1' } } });
send({ jsonrpc: '2.0', method: 'notifications/initialized' });
send({ jsonrpc: '2.0', id: 2, method: 'tools/list', params: {} });

setTimeout(() => send({ jsonrpc: '2.0', id: 3, method: 'tools/call', params: { name: 'rake_state', arguments: {} } }), 400);
setTimeout(() => send({ jsonrpc: '2.0', id: 4, method: 'tools/call', params: { name: 'rake_menu', arguments: { screen: 'main' } } }), 900);
setTimeout(() => send({ jsonrpc: '2.0', id: 5, method: 'tools/call', params: { name: 'rake_click', arguments: { name: 'HostGameBtn' } } }), 1400);
setTimeout(() => send({ jsonrpc: '2.0', id: 6, method: 'tools/call', params: { name: 'rake_state', arguments: {} } }), 1900);
setTimeout(() => send({ jsonrpc: '2.0', id: 7, method: 'tools/call', params: { name: 'rake_screenshot', arguments: {} } }), 2400);

setTimeout(() => {
  child.kill();
  process.exit(0);
}, 6000);
