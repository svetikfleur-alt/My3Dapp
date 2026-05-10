from pathlib import Path
import re

PACK_FILE = "blueprint_pack.txt"
OUTPUT_ROOT = Path("Blueprint")

HEADER_RE = re.compile(r"^=== FILE: (.+?) ===\s*$", re.MULTILINE)


def parse_pack(text: str):
    matches = list(HEADER_RE.finditer(text))
    if not matches:
        raise ValueError("No file markers found. Expected lines like: === FILE: Core/01_PRODUCT_CONCEPT.md ===")

    parts = []
    for i, match in enumerate(matches):
        rel_path = match.group(1).strip()
        start = match.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        content = text[start:end].lstrip("\n").rstrip() + "\n"
        parts.append((rel_path, content))
    return parts


def safe_write(path: Path, content: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def main():
    pack_path = Path(PACK_FILE)
    if not pack_path.exists():
        raise FileNotFoundError(f"Pack file not found: {PACK_FILE}")

    text = pack_path.read_text(encoding="utf-8")
    files = parse_pack(text)

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)

    created = []
    for rel_path, content in files:
        target = OUTPUT_ROOT / rel_path
        safe_write(target, content)
        created.append(target)

    print("✅ Blueprint deployed.")
    print(f"Created/updated {len(created)} files:")
    for p in created:
        print(f" - {p}")


if __name__ == "__main__":
    main()