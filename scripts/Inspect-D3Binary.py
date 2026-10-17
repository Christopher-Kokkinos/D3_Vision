"""Read-only PE scene/navigation reference inspection; never executes the target.

Requires capstone==5.0.9 and pefile==2024.8.26 in an isolated environment.
Outputs file identity, selected string references and containing x64 functions.
Addresses are RVAs, never assumed to be valid runtime pointers.
"""
import argparse
import bisect
import hashlib
import json
import re
import struct
from pathlib import Path

import capstone
import pefile


def inspect(path: Path) -> dict:
    data = path.read_bytes()
    pe = pefile.PE(data=data, fast_load=True)
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"],
                                          pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_RESOURCE"]])
    if pe.FILE_HEADER.Machine != 0x8664:
        raise ValueError("An x64 executable is required")
    base = pe.OPTIONAL_HEADER.ImageBase
    names = {b"Scene", b"World", b"Worlds", b"NavMeshDef", b"NavCellLookup",
             b"NavMeshSquare", b"CompNavMeshSquare", b"NavMeshConnectedSquare",
             b"NavCellPath", b"NavCellBorderData", b"CScenes", b"Client Scenes:",
             b"Server Scenes:", b"RevealSceneMessage", b"MapRevealSceneMessage",
             b"Item_Quality_Level", b"Ancient_Rank", b"Set_Item", b"Monster", b"ACDs"}
    targets = {}
    for match in re.finditer(rb"[ -~]{5,}", data):
        if match.group() in names:
            targets[pe.get_rva_from_offset(match.start())] = match.group().decode()
    functions = sorted((e.struct.BeginAddress, e.struct.EndAddress)
                       for e in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []))
    starts = [x[0] for x in functions]
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    decoder.detail = True
    references = []
    for section in pe.sections:
        if not section.Characteristics & 0x20000000:
            continue
        code = section.get_data()
        # RIP-relative LEA/MOV with REX prefix. Confirm every candidate using
        # the decoder and reject candidates outside a recorded function.
        for match in re.finditer(rb"[\x48-\x4f][\x8d\x8b\x89][\x05\x0d\x15\x1d\x25\x2d\x35\x3d]", code):
            offset = match.start()
            rva = section.VirtualAddress + offset
            if offset + 7 > len(code):
                continue
            if rva + 7 + struct.unpack_from("<i", code, offset + 3)[0] not in targets:
                continue
            instruction = next(decoder.disasm(code[offset:offset + 15], base + rva, count=1), None)
            if instruction is None:
                continue
            for operand in instruction.operands:
                if operand.type != capstone.CS_OP_MEM or operand.mem.base != capstone.x86.X86_REG_RIP:
                    continue
                target = rva + instruction.size + operand.mem.disp
                if target not in targets:
                    continue
                index = bisect.bisect_right(starts, rva) - 1
                function = functions[index] if index >= 0 and rva < functions[index][1] else None
                references.append({"name": targets[target], "stringRva": hex(target),
                                   "instructionRva": hex(rva),
                                   "instruction": instruction.mnemonic + " " + instruction.op_str,
                                   "function": [hex(x) for x in function] if function else None})
    version = None
    if getattr(pe, "VS_FIXEDFILEINFO", None):
        info = pe.VS_FIXEDFILEINFO[0]
        version = ".".join(str(x) for x in (info.FileVersionMS >> 16, info.FileVersionMS & 65535,
                                            info.FileVersionLS >> 16, info.FileVersionLS & 65535))
    fixed_version = version
    for group in getattr(pe, "FileInfo", []):
        for entry in group:
            for table in getattr(entry, "StringTable", []):
                if b"FileVersion" in table.entries:
                    version = table.entries[b"FileVersion"].decode().replace(", ", ".")
    return {"path": str(path.resolve()), "sha256": hashlib.sha256(data).hexdigest(),
            "fileVersion": version, "fixedFileVersion": fixed_version, "imageBase": hex(base),
            "imageSize": hex(pe.OPTIONAL_HEADER.SizeOfImage),
            "references": references}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = inspect(args.executable)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Build {report['fileVersion']}: {len(report['references'])} candidate references")
    for ref in report["references"]:
        print(ref["name"], ref["instructionRva"], ref["function"])
