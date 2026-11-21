#!/usr/bin/env python3
"""Static checks for the Unity project that need no editor or licence.

Runs in every cloud session and in CI before the licensed Unity jobs, so most structural
mistakes in hand-authored assets are caught for free. Exit code 1 lists every problem.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
problems = []


def rel(path):
    return os.path.relpath(path, ROOT)


def read(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        return f.read()


def check(condition, message):
    if not condition:
        problems.append(message)


def pinned_editor():
    match = re.search(r"^m_EditorVersion:\s*(\S+)", read("ProjectSettings/ProjectVersion.txt"), re.M)
    return match.group(1) if match else None


def check_versions():
    version = pinned_editor()
    check(version is not None, "ProjectVersion.txt has no m_EditorVersion")
    check(version and version.startswith("6000.3."), f"Pinned editor {version} is not Unity 6.3 LTS (6000.3.x)")
    for workflow in (".github/workflows/ci.yml", ".github/workflows/ios-testflight.yml"):
        text = read(workflow)
        images = re.findall(r"unityci/editor:ubuntu-([0-9a-z.]+)-(base|ios|android)-\d+", text)
        check(images, f"{workflow} does not pin a unityci/editor image")
        for image_version, _ in images:
            check(image_version == version, f"{workflow} pins editor image {image_version} but project pins {version}")
        check(re.search(r"unityci/editor:[^\s@]+@sha256:[0-9a-f]{64}", text),
              f"{workflow} must pin the editor image by digest")


def check_manifest():
    manifest = json.loads(read("Packages/manifest.json"))
    deps = manifest.get("dependencies", {})
    for package in ("com.unity.render-pipelines.universal", "com.unity.inputsystem", "com.unity.cinemachine",
                    "com.unity.ugui", "com.unity.test-framework"):
        check(package in deps, f"manifest is missing {package}")
    for name, version in deps.items():
        check(re.fullmatch(r"\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?", version) is not None,
              f"{name} must use an exact registry version, found '{version}'")


def unity_yaml_documents(path):
    text = read(path)
    check(text.startswith("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"), f"{path} lacks the Unity YAML header")
    return text


def check_project_settings():
    settings = unity_yaml_documents("ProjectSettings/ProjectSettings.asset")
    expectations = {
        "activeInputHandler": "1",
        "defaultScreenOrientation": "4",
        "allowedAutorotateToPortrait": "0",
        "allowedAutorotateToPortraitUpsideDown": "0",
        "allowedAutorotateToLandscapeRight": "1",
        "allowedAutorotateToLandscapeLeft": "1",
        "appleEnableAutomaticSigning": "0",
        "runInBackground": "0",
    }
    for key, expected in expectations.items():
        match = re.search(rf"^  {key}: (\S+)", settings, re.M)
        check(match and match.group(1) == expected, f"ProjectSettings {key} should be {expected}")
    check(re.search(r"^    iPhone: 1$", settings.split("scriptingBackend:")[1].split("il2cpp")[0], re.M),
          "iOS scripting backend must be IL2CPP")
    for path in sorted(os.listdir(os.path.join(ROOT, "ProjectSettings"))):
        if path.endswith(".asset"):
            unity_yaml_documents("ProjectSettings/" + path)


def meta_guids():
    guids = {}
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
        for name in filenames:
            if name.endswith(".meta"):
                with open(os.path.join(dirpath, name), encoding="utf-8") as f:
                    match = re.search(r"^guid: ([0-9a-f]{32})$", f.read(), re.M)
                check(match is not None, f"{rel(os.path.join(dirpath, name))} has no valid guid")
                if match:
                    guids[match.group(1)] = rel(os.path.join(dirpath, name[:-5]))
    return guids


# Script/asset GUIDs that live in packages (URP); verified against the Unity Graphics repo for 6000.3.
PACKAGE_GUIDS = {
    "bf2edee5c58d82540a51f03df9d42094": "URP UniversalRenderPipelineAsset script",
    "de640fe3d0db1804a85f9fc8f5cadab6": "URP UniversalRendererData script",
    "41439944d30ece34e96484bdb6645b55": "URP PostProcessData asset",
}


def check_references(guids):
    reference = re.compile(r"guid: ([0-9a-f]{32})")
    targets = []
    for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
        for name in filenames:
            if name.endswith((".unity", ".asset", ".prefab", ".mat")):
                targets.append(os.path.join(dirpath, name))
    for name in ("EditorBuildSettings.asset", "GraphicsSettings.asset", "QualitySettings.asset"):
        targets.append(os.path.join(ROOT, "ProjectSettings", name))
    for path in targets:
        with open(path, encoding="utf-8") as f:
            text = f.read()
        for guid in reference.findall(text):
            if guid.startswith("0000000000000000"):
                continue  # built-in resources
            check(guid in guids or guid in PACKAGE_GUIDS, f"{rel(path)} references unknown guid {guid}")
    build = read("ProjectSettings/EditorBuildSettings.asset")
    for path, guid in re.findall(r"path: (\S+)\n\s+guid: ([0-9a-f]{32})", build):
        check(guids.get(guid) == path, f"EditorBuildSettings guid for {path} does not match its .meta")
    graphics = read("ProjectSettings/GraphicsSettings.asset")
    match = re.search(r"m_CustomRenderPipeline: \{fileID: 11400000, guid: ([0-9a-f]{32})", graphics)
    check(match and guids.get(match.group(1), "").endswith(".asset"), "GraphicsSettings must point at a URP asset")


def check_asmdefs():
    names = set()
    for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
        for name in filenames:
            if name.endswith(".asmdef"):
                data = json.loads(read(rel(os.path.join(dirpath, name))))
                check(data["name"] not in names, f"duplicate asmdef name {data['name']}")
                names.add(data["name"])
                if data["name"] == "SpookyStreet.Core":
                    check(data.get("noEngineReferences") is True, "SpookyStreet.Core must stay engine-free")


def check_core_is_engine_free():
    core = os.path.join(ROOT, "Assets", "SpookyStreet", "Core")
    for dirpath, _, filenames in os.walk(core):
        for name in filenames:
            if name.endswith(".cs"):
                text = read(rel(os.path.join(dirpath, name)))
                check(not re.search(r"^\s*using\s+Unity", text, re.M), f"{rel(os.path.join(dirpath, name))} uses Unity APIs")


def check_localization():
    table = json.loads(read("Assets/SpookyStreet/Content/Resources/Localization/en.json"))
    check(table.get("schemaVersion") == 1 and table.get("locale") == "en", "en.json must be schema 1, locale en")
    keys = [e["key"] for e in table["entries"]]
    check(len(keys) == len(set(keys)), "en.json has duplicate keys")
    for entry in table["entries"]:
        check(re.fullmatch(r"[a-z0-9]+(\.[a-z0-9_]+)+", entry["key"]) is not None, f"key '{entry['key']}' is not semantic")
        check(entry.get("context"), f"key '{entry['key']}' has no translator context")
    code_keys = re.findall(r'public const string \w+ = "([^"]+)";',
                           read("Assets/SpookyStreet/Core/Localization/LocalizationKeys.cs"))
    for key in code_keys:
        check(key in keys, f"LocalizationKeys constant {key} missing from en.json")
    # User-facing literals belong in the table: flag string literals assigned to Text.text in runtime code.
    runtime = os.path.join(ROOT, "Assets", "SpookyStreet", "Runtime")
    for dirpath, _, filenames in os.walk(runtime):
        for name in filenames:
            if name.endswith(".cs"):
                text = read(rel(os.path.join(dirpath, name)))
                for literal in re.findall(r'\.text\s*=\s*"([^"]*)"', text):
                    problems.append(f"{name}: hard-coded UI text \"{literal}\"; use a localization key")


def check_no_secrets():
    # Match real material (a key body or an encoded licence value), not code that mentions the markers.
    pattern = re.compile(r"-----BEGIN (?:RSA |EC )?PRIVATE KEY-----\s*\n[A-Za-z0-9+/]{40}"
                         r"|<DeveloperData Value=\"[A-Za-z0-9+/=]{24,}\"")
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in (".git", "Library", "Temp", "bin", "obj", "node_modules")]
        for name in filenames:
            if name.endswith((".p12", ".mobileprovision", ".p8", ".ulf")):
                problems.append(f"credential-like file committed: {rel(os.path.join(dirpath, name))}")
                continue
            path = os.path.join(dirpath, name)
            try:
                with open(path, encoding="utf-8") as f:
                    if pattern.search(f.read()):
                        problems.append(f"possible secret material in {rel(path)}")
            except (UnicodeDecodeError, OSError):
                pass


def main():
    check_versions()
    check_manifest()
    check_project_settings()
    guids = meta_guids()
    check_references(guids)
    check_asmdefs()
    check_core_is_engine_free()
    check_localization()
    check_no_secrets()
    for problem in problems:
        print("FAIL", problem)
    print(f"validate_project: {len(problems)} problem(s); pinned Unity {pinned_editor()}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
