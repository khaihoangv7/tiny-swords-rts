"""Bake a presentation-only prefab from the authored Map scene (no Unity process needed).
Run from the project root: python Tools/MainMenu/bake_backdrop.py
Only rendering components and the two verified ambient scripts are copied.
"""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Scenes/Map.unity"
TARGET = ROOT / "Assets/Prefabs/UI/MainMenuBackdrop.prefab"
ALLOWED_TYPES = {4, 212, 156049354, 1839735485, 483693784}
AMBIENT_GUIDS = {"80f9bfe7d0b44c6cb8374c7ddfcdb3b5", "3740e23bbc5349aeb859304c0957ea64"}

def ensure_meta(path, folder=False):
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        text = "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\n"
        if folder:
            text += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        else:
            text += "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        meta.write_text(text, encoding="utf-8")
    return re.search(r"^guid: (\w+)", meta.read_text(), re.M)[1]

def bake():
    source = SOURCE.read_text(encoding="utf-8-sig")
    blocks = {}
    for match in re.finditer(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", source, re.M | re.S):
        blocks[int(match[2])] = (int(match[1]), match[3])
    names = {}
    transforms = {}
    owners = {}
    for fid, (kind, body) in blocks.items():
        if kind == 1:
            names[fid] = re.search(r"^  m_Name: (.*)", body, re.M)[1]
        if kind == 4:
            transforms[fid] = body
            owners[fid] = int(re.search(r"m_GameObject: \{fileID: (\d+)", body)[1])
    root = next(fid for fid, go in owners.items() if names[go] == "Map")
    roots = [fid for fid, go in owners.items()
             if names[go] in {"Grid", "BattlefieldScenery", "Battlefield_EditorPreview"}]
    assert len(roots) == 3, "The authored map visual hierarchy changed."
    retained_transforms = {root}
    def visit(fid):
        go = owners[fid]
        if names[go] in {"Blue_Pawn", "Red_Pawn"}:
            return
        retained_transforms.add(fid)
        for child in re.findall(r"^  - \{fileID: (\d+)\}", transforms[fid], re.M):
            visit(int(child))
    for fid in roots:
        visit(fid)
    retained = set(retained_transforms)
    for fid in retained_transforms:
        go = owners[fid]
        retained.add(go)
        for component in re.findall(r"component: \{fileID: (\d+)", blocks[go][1]):
            cid = int(component)
            kind, body = blocks[cid]
            script = re.search(r"m_Script:.*guid: (\w+)", body)
            if kind in ALLOWED_TYPES or (kind == 114 and script and script[1] in AMBIENT_GUIDS):
                retained.add(cid)
    output = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"]
    for fid, (kind, original) in blocks.items():
        if fid not in retained:
            continue
        body = original
        if kind == 1:
            body = re.sub(r"^  - component: \{fileID: (\d+)\}\n",
                          lambda m: m[0] if int(m[1]) in retained else "", body, flags=re.M)
            body = body.replace("m_TagString: EditorOnly", "m_TagString: Untagged")
            if fid == owners[root]:
                body = body.replace("m_Name: Map", "m_Name: MainMenuBackdrop")
        elif kind == 4:
            children = roots if fid == root else [int(x) for x in re.findall(r"^  - \{fileID: (\d+)\}", body, re.M)
                                                  if int(x) in retained_transforms]
            replacement = "  m_Children:\n" + "".join("  - {fileID: " + str(x) + "}\n" for x in children) if children else "  m_Children: []\n"
            body = re.sub(r"  m_Children:(?: \[\])?\n(?:  - \{fileID: \d+\}\n)*", replacement, body)
            if fid == root:
                body = re.sub(r"m_Father: \{fileID: \d+\}", "m_Father: {fileID: 0}", body)
        output.append("--- !u!" + str(kind) + " &" + str(fid) + "\n" + body)
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    ensure_meta(TARGET.parent, folder=True)
    TARGET.write_text("".join(output), encoding="utf-8")
    guid = ensure_meta(TARGET)
    # Reject any accidental local reference to a discarded gameplay object.
    for fid, body in [(fid, block) for fid, block in ((int(m[1]), m[2]) for m in
                      re.finditer(r"^--- !u!\d+ &(\d+)\n(.*?)(?=^--- !u!|\Z)", TARGET.read_text(), re.M | re.S))]:
        for ref in re.finditer(r"\{fileID: (-?\d+)\}", body):
            assert int(ref[1]) == 0 or int(ref[1]) in retained, (fid, ref[1])
    print("Baked", TARGET.relative_to(ROOT), "with", len(retained_transforms), "visual transforms.")
    print("Root GameObject:", owners[root], "GUID:", guid)
    return guid, owners[root]

if __name__ == "__main__":
    bake()

