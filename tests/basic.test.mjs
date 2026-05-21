import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

async function readJson(p) { return JSON.parse(await fs.readFile(p, 'utf8')); }

function sanitizePresetName(name) {
  const sanitized = name.trim().toLowerCase().replace(/[^a-z0-9-_]/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '');
  if (!sanitized) throw new Error('invalid');
  return sanitized;
}

test('workflow inspection basic shape', async () => {
  const wf = await readJson('workflows/example-workflow.json');
  const keys = Object.keys(wf);
  assert.ok(keys.length > 0);
});

test('mapping fixture includes required fields', async () => {
  const cfg = await readJson('config.example.json');
  const m = cfg.workflow_mappings['example-workflow'];
  for (const k of ['positive_prompt', 'negative_prompt', 'seed', 'width', 'height']) assert.ok(m[k]);
});

test('safe preset name validation', async () => {
  assert.equal(sanitizePresetName('My Preset 01'), 'my-preset-01');
  assert.throws(() => sanitizePresetName('***'));
});

test('output index is array', async () => {
  const parsed = await readJson('outputs/index.json');
  assert.equal(Array.isArray(parsed), true);
});

test('codegen script creates launch SVG assets', async () => {
  await execFileAsync('node', ['scripts/generate-demo-assets.mjs']);
  const files = ['hero-banner.svg', 'pipeline-diagram.svg', 'social-launch-card.svg'];
  for (const f of files) {
    const p = path.join('demo-assets', f);
    const st = await fs.stat(p);
    assert.ok(st.size > 0, `expected non-empty: ${p}`);
  }
});

test('missing workflow file errors', async () => {
  await assert.rejects(() => fs.readFile('workflows/does-not-exist.json', 'utf8'));
});
