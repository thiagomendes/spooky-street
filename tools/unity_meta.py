#!/usr/bin/env python3
"""Create missing Unity .meta files without opening the editor.

Cloud sessions have no Unity editor, so assets authored as text (C#, asmdef,
YAML assets, JSON) need .meta files with stable GUIDs before they are
committed. This tool only ADDS missing .meta files; it never rewrites an
existing one, so GUIDs that Unity (or an earlier run) assigned are preserved.

Usage:
  python3 tools/unity_meta.py            # create missing metas under Assets/
  python3 tools/unity_meta.py --check    # exit 1 if any meta is missing/orphaned
"""
import argparse
import os
import sys
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")

COMMON_TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

TEMPLATES = {
    "folder": "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".cs": (
        "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
        "  executionOrder: 0\n  icon: {instanceID: 0}\n" + COMMON_TAIL
    ),
    ".asmdef": "AssemblyDefinitionImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".asmref": "AssemblyDefinitionReferenceImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".json": "TextScriptImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".txt": "TextScriptImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".md": "TextScriptImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".asset": "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n" + COMMON_TAIL,
    ".mat": "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n" + COMMON_TAIL,
    ".unity": "DefaultImporter:\n  externalObjects: {}\n" + COMMON_TAIL,
    ".ttf": "TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n"
            "  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n"
            "  fontNames: []\n  fallbackFontReferences: []\n  customCharacters: \n  fontRenderingMode: 0\n"
            "  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n  shouldRoundAdvanceValue: 1\n" + COMMON_TAIL,
}

IGNORED_NAMES = {".DS_Store", "Thumbs.db"}


def is_ignored(name):
    # Unity ignores hidden files/folders and names ending with '~'.
    return name.startswith(".") or name.endswith("~") or name in IGNORED_NAMES


def template_for(path):
    if os.path.isdir(path):
        return TEMPLATES["folder"]
    ext = os.path.splitext(path)[1].lower()
    if ext not in TEMPLATES:
        raise SystemExit(f"unity_meta.py: no importer template for {os.path.relpath(path, ROOT)}; "
                         "let Unity import it or add a template")
    return TEMPLATES[ext]


def walk_assets():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = sorted(d for d in dirnames if not is_ignored(d))
        for name in dirnames + sorted(filenames):
            if is_ignored(name) or name.endswith(".meta"):
                continue
            yield os.path.join(dirpath, name)


def orphan_metas():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = [d for d in dirnames if not is_ignored(d)]
        for name in filenames:
            if name.endswith(".meta") and not os.path.exists(os.path.join(dirpath, name[:-5])):
                yield os.path.join(dirpath, name)


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="report problems instead of writing")
    args = parser.parse_args()

    missing, created = [], []
    for path in walk_assets():
        meta = path + ".meta"
        if os.path.exists(meta):
            continue
        if args.check:
            missing.append(path)
            continue
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{template_for(path)}")
        created.append(path)

    orphans = list(orphan_metas())
    guids = {}
    duplicates = []
    for path in walk_assets():
        meta = path + ".meta"
        if os.path.exists(meta):
            guid = read_guid(meta)
            if guid in guids:
                duplicates.append((guid, guids[guid], meta))
            guids[guid] = meta

    for p in created:
        print("created", os.path.relpath(p, ROOT) + ".meta")
    for p in missing:
        print("MISSING meta for", os.path.relpath(p, ROOT))
    for p in orphans:
        print("ORPHAN meta", os.path.relpath(p, ROOT))
    for guid, a, b in duplicates:
        print("DUPLICATE guid", guid, os.path.relpath(a, ROOT), os.path.relpath(b, ROOT))
    if missing or orphans or duplicates:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
