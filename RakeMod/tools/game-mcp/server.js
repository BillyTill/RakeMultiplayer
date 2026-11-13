#!/usr/bin/env node
/*
 * Rake Game MCP — local stdio MCP server for the in-game bridge.
 *
 * The BepInEx plugin listens on http://127.0.0.1:8765 when the game is launched
 * with -rakemcp (or Diagnostics/GameBridge = true). This process is spawned by
 * OpenCode and proxies MCP tool calls to that bridge.
 *
 * No npm dependencies: only Node's http/os/path/fs/readline modules.
 */
'use strict';

const http = require('http');
const os = require('os');
const fs = require('fs');
const path = require('path');
const readline = require('readline');

const BRIDGE = process.env.RAKE_BRIDGE || 'http://127.0.0.1:8765';
const SERVER_NAME = 'rake-game';
const SERVER_VERSION = '1.0.0';
const SHOT_DIR = path.join(os.tmpdir(), 'rake-game-shots');

function httpGet(route, binary) {
  return new Promise((resolve, reject) => {
    const url = new URL(BRIDGE + route);
    const req = http.request(
      {
        hostname: url.hostname,
        port: url.port || 80,
        path: url.pathname + url.search,
        method: 'GET',
        timeout: 20000
      },
      res => {
        const chunks = [];
        res.on('data', c => chunks.push(c));
        res.on('end', () => {
          const data = Buffer.concat(chunks);
          if (res.statusCode < 200 || res.statusCode >= 300) {
            reject(new Error('bridge HTTP ' + res.statusCode + ': ' + data.toString('utf8').slice(0, 500)));
            return;
          }
          resolve(binary ? data : data.toString('utf8'));
        });
      }
    );
    req.on('timeout', () => req.destroy(new Error('bridge timeout')));
    req.on('error', reject);
    req.end();
  });
}

function q(value) {
  return encodeURIComponent(String(value == null ? '' : value));
}

function textResult(text) {
  return { content: [{ type: 'text', text: String(text) }] };
}

function errorResult(err) {
  const message = err && err.message ? err.message : String(err);
  return {
    isError: true,
    content: [
      {
        type: 'text',
        text:
          message +
          '\n\nBridge: ' +
          BRIDGE +
          '\nLaunch Rake with -rakemcp (or set Diagnostics/GameBridge=true in BepInEx config).'
      }
    ]
  };
}

const TOOLS = [
  {
    name: 'rake_state',
    description: 'Scene, menu visibility, active panel, resolution, FPS and network state of the running game.',
    inputSchema: { type: 'object', properties: {}, additionalProperties: false }
  },
  {
    name: 'rake_ui',
    description: 'Dump the live multiplayer uGUI tree: names, active flags, screen rects, labels, buttons and inputs.',
    inputSchema: { type: 'object', properties: {}, additionalProperties: false }
  },
  {
    name: 'rake_screenshot',
    description: 'Capture the game screen (including the multiplayer UI) to a PNG and return it as an image.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Optional local output path. Defaults to the temp folder.' }
      },
      additionalProperties: false
    }
  },
  {
    name: 'rake_menu',
    description: 'Open the multiplayer menu on a specific panel.',
    inputSchema: {
      type: 'object',
      properties: {
        screen: {
          type: 'string',
          enum: ['main', 'host', 'join', 'lobby', 'settings', 'display', 'steam', 'steamcreate', 'browse', 'close'],
          description: 'Panel to show. Use close to hide the menu.'
        }
      },
      required: ['screen'],
      additionalProperties: false
    }
  },
  {
    name: 'rake_click',
    description: 'Click a uGUI Button by GameObject name or by its visible label (for example HostGameBtn).',
    inputSchema: {
      type: 'object',
      properties: {
        name: { type: 'string', description: 'Button GameObject name or label text.' }
      },
      required: ['name'],
      additionalProperties: false
    }
  },
  {
    name: 'rake_input',
    description: 'Set an InputField value by GameObject name and submit it (onEndEdit).',
    inputSchema: {
      type: 'object',
      properties: {
        name: { type: 'string', description: 'InputField GameObject name.' },
        value: { type: 'string', description: 'New value.' }
      },
      required: ['name', 'value'],
      additionalProperties: false
    }
  }
];

async function callTool(name, args) {
  switch (name) {
    case 'rake_state':
      return textResult(await httpGet('/state', false));

    case 'rake_ui':
      return textResult(await httpGet('/ui', false));

    case 'rake_menu':
      return textResult(await httpGet('/menu?screen=' + q(args.screen), false));

    case 'rake_click':
      return textResult(await httpGet('/click?name=' + q(args.name), false));

    case 'rake_input':
      return textResult(
        await httpGet('/input?name=' + q(args.name) + '&value=' + q(args.value), false)
      );

    case 'rake_screenshot': {
      const png = await httpGet('/screenshot', true);
      if (!png || png.length < 100) throw new Error('empty screenshot');

      let out = args.path;
      if (!out) {
        fs.mkdirSync(SHOT_DIR, { recursive: true });
        out = path.join(SHOT_DIR, 'rake-' + Date.now() + '.png');
      } else {
        fs.mkdirSync(path.dirname(path.resolve(out)), { recursive: true });
      }
      fs.writeFileSync(out, png);

      return {
        content: [
          { type: 'image', data: png.toString('base64'), mimeType: 'image/png' },
          { type: 'text', text: 'Screenshot saved: ' + path.resolve(out) + ' (' + png.length + ' bytes)' }
        ]
      };
    }

    default:
      throw new Error('unknown tool: ' + name);
  }
}

function send(message) {
  process.stdout.write(JSON.stringify(message) + '\n');
}

function reply(id, result) {
  send({ jsonrpc: '2.0', id, result });
}

function fail(id, code, message) {
  send({ jsonrpc: '2.0', id, error: { code, message } });
}

async function handle(message) {
  if (!message || typeof message !== 'object') return;

  const method = message.method;
  const id = message.id;
  if (!method) return;

  if (method === 'initialize') {
    reply(id, {
      protocolVersion: message.params && message.params.protocolVersion ? message.params.protocolVersion : '2024-11-05',
      capabilities: { tools: {} },
      serverInfo: { name: SERVER_NAME, version: SERVER_VERSION },
      instructions:
        'Controls the running Rake multiplayer mod. The game must be launched with -rakemcp. ' +
        'Use rake_state / rake_ui to inspect, rake_screenshot to see the UI, rake_click and rake_menu to drive it.'
    });
    return;
  }

  if (method === 'notifications/initialized' || method === 'initialized') return;
  if (method === 'ping') { reply(id, {}); return; }

  if (method === 'tools/list') {
    reply(id, { tools: TOOLS });
    return;
  }

  if (method === 'tools/call') {
    try {
      const params = message.params || {};
      const result = await callTool(params.name, params.arguments || {});
      reply(id, result);
    } catch (err) {
      reply(id, errorResult(err));
    }
    return;
  }

  if (method === 'resources/list') { reply(id, { resources: [] }); return; }
  if (method === 'resources/templates/list') { reply(id, { resourceTemplates: [] }); return; }
  if (method === 'prompts/list') { reply(id, { prompts: [] }); return; }

  if (id !== undefined) fail(id, -32601, 'method not found: ' + method);
}

const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
rl.on('line', line => {
  const trimmed = line.trim();
  if (!trimmed) return;
  let message;
  try {
    message = JSON.parse(trimmed);
  } catch (err) {
    return;
  }
  Promise.resolve(handle(message)).catch(err => {
    if (message && message.id !== undefined) fail(message.id, -32603, err.message);
  });
});
