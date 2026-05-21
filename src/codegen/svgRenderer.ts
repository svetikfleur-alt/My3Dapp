import fs from 'node:fs/promises';
import path from 'node:path';

export type TemplateName = 'github_hero_banner' | 'pipeline_diagram' | 'social_launch_card' | 'feature_card';

export interface RenderCodeImageInput {
  template: TemplateName;
  title: string;
  subtitle?: string;
  bullets?: string[];
  width?: number;
  height?: number;
  output_name?: string;
}

export interface RenderCodeImageResult {
  status: 'ok';
  template: TemplateName;
  file_path: string;
  width: number;
  height: number;
  warnings: string[];
}

export function sanitizeFileStem(name: string): string {
  const s = name.trim().toLowerCase().replace(/[^a-z0-9-_]/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '');
  if (!s) throw new Error('Invalid output_name. Cause: name became empty after sanitization. Suggested fix: use letters, numbers, dash, underscore.');
  return s;
}

function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function dims(template: TemplateName, width?: number, height?: number) {
  if (width && height) return { width, height };
  if (template === 'github_hero_banner') return { width: 1600, height: 900 };
  if (template === 'pipeline_diagram') return { width: 1600, height: 900 };
  if (template === 'social_launch_card') return { width: 1200, height: 630 };
  return { width: 1200, height: 800 };
}

function renderTemplate(input: RenderCodeImageInput, width: number, height: number): string {
  const title = esc(input.title);
  const subtitle = esc(input.subtitle ?? '');
  const bullets = (input.bullets ?? []).slice(0, 5).map((b) => esc(b));

  const baseStart = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}"><defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#0B1220"/><stop offset="100%" stop-color="#1F2A44"/></linearGradient></defs><rect width="100%" height="100%" fill="url(#bg)"/>`;
  const footer = `</svg>`;

  if (input.template === 'pipeline_diagram') {
    return `${baseStart}
      <text x="80" y="90" fill="#E6EEF8" font-size="54" font-family="Inter,Arial,sans-serif" font-weight="700">${title}</text>
      <text x="80" y="138" fill="#B8C7DD" font-size="26" font-family="Inter,Arial,sans-serif">${subtitle}</text>
      <rect x="80" y="240" rx="18" ry="18" width="290" height="120" fill="#243654" stroke="#4B6A9B"/>
      <rect x="470" y="240" rx="18" ry="18" width="290" height="120" fill="#243654" stroke="#4B6A9B"/>
      <rect x="860" y="240" rx="18" ry="18" width="290" height="120" fill="#243654" stroke="#4B6A9B"/>
      <rect x="1250" y="240" rx="18" ry="18" width="270" height="120" fill="#243654" stroke="#4B6A9B"/>
      <text x="115" y="310" fill="#E6EEF8" font-size="30" font-family="Inter,Arial,sans-serif">Claude Desktop</text>
      <text x="507" y="310" fill="#E6EEF8" font-size="30" font-family="Inter,Arial,sans-serif">MCP Runner</text>
      <text x="915" y="310" fill="#E6EEF8" font-size="30" font-family="Inter,Arial,sans-serif">Local ComfyUI</text>
      <text x="1290" y="310" fill="#E6EEF8" font-size="30" font-family="Inter,Arial,sans-serif">Outputs</text>
      <line x1="370" y1="300" x2="470" y2="300" stroke="#8FB3FF" stroke-width="6"/>
      <line x1="760" y1="300" x2="860" y2="300" stroke="#8FB3FF" stroke-width="6"/>
      <line x1="1150" y1="300" x2="1250" y2="300" stroke="#8FB3FF" stroke-width="6"/>
    ${footer}`;
  }

  const bulletSvg = bullets.map((b, i) => `<text x="100" y="${330 + i * 58}" fill="#C7D7F0" font-size="30" font-family="Inter,Arial,sans-serif">• ${b}</text>`).join('');
  return `${baseStart}
    <rect x="70" y="70" width="${width - 140}" height="${height - 140}" rx="24" ry="24" fill="rgba(10,16,28,0.42)" stroke="#3D5480"/>
    <text x="100" y="170" fill="#F2F7FF" font-size="${input.template === 'social_launch_card' ? 64 : 72}" font-family="Inter,Arial,sans-serif" font-weight="700">${title}</text>
    <text x="100" y="235" fill="#AFC3E6" font-size="32" font-family="Inter,Arial,sans-serif">${subtitle}</text>
    ${bulletSvg}
    <text x="100" y="${height - 90}" fill="#8BA7D6" font-size="24" font-family="Inter,Arial,sans-serif">local-first • no hosted server • reproducible assets</text>
  ${footer}`;
}

export async function renderCodeImage(input: RenderCodeImageInput, outputDir: string): Promise<RenderCodeImageResult> {
  const { width, height } = dims(input.template, input.width, input.height);
  const outName = sanitizeFileStem(input.output_name ?? `${input.template}-${Date.now()}`);
  const outPath = path.resolve(outputDir, `${outName}.svg`);
  const outBase = path.resolve(outputDir);
  if (!(outPath === outBase || outPath.startsWith(`${outBase}${path.sep}`))) {
    throw new Error('Output path safety error. Cause: file path escaped output directory. Suggested fix: use a safe output_name.');
  }
  await fs.mkdir(outBase, { recursive: true });
  const svg = renderTemplate(input, width, height);
  await fs.writeFile(outPath, svg, 'utf8');
  return { status: 'ok', template: input.template, file_path: outPath, width, height, warnings: [] };
}
