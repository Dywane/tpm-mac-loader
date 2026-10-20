#!/usr/bin/env python3
"""A1 bounded disasm: find s_Il2CppMetadataRegistration via CodeReg write path."""
from __future__ import annotations
import struct, sys, collections
from pathlib import Path
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import *

DYLIB = Path("/tmp/GameAssembly-x64-meta.dylib")
IL2CPP_INIT = 0x29B6D9
IL2CPP_INIT_UTF16 = 0x29B702
CODEREG_RVA = 0x34D4F68
BSS_START, BSS_END = 0x34CBD10, 0x34CBD10 + 0x21D21D
DATA_START, DATA_END = 0x2FD0FB0, 0x2FD0FB0 + 0x4FAD60
CONST_START, CONST_END = 0x2D2FA90, 0x2D2FA90 + 0x2A1520
TEXT_RANGES = [
    (0x08E0, 0x08E0 + 0x30C9EC),       # __text
    (0x30D2D0, 0x30D2D0 + 0x25836CB), # il2cpp
]
MAX_DEPTH = 3
MAX_FN_BYTES = 0x400
MAX_CALLS_PER_FN = 24

def load():
    data = DYLIB.read_bytes()
    # file offset == VA for thin x64 with vmaddr 0 (__TEXT + __DATA file-backed)
    return data

def va_to_off(va: int) -> int | None:
    # Thin slice: __TEXT fileoff 0; __DATA starts at fileoff 0x2d2f000 == va
    if 0 <= va < 0x2D2F000:
        return va  # __TEXT
    if 0x2D2F000 <= va < 0x34CBD10:
        return va  # __DATA file-backed (got..data)
    return None  # bss/common zerofill

def in_text(va: int) -> bool:
    return any(a <= va < b for a, b in TEXT_RANGES)

def region(va: int) -> str:
    if BSS_START <= va < BSS_END: return "BSS"
    if DATA_START <= va < DATA_END: return "DATA"
    if CONST_START <= va < CONST_END: return "CONST"
    if in_text(va): return "TEXT"
    if 0x2D2F000 <= va < 0x2D2FA90: return "GOT/LAZY/MODINIT"
    return "?"

md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True

def disasm_fn(blob: bytes, start: int, limit: int = MAX_FN_BYTES):
    off = va_to_off(start)
    if off is None or off >= len(blob):
        return []
    chunk = blob[off: off + limit]
    out = []
    for insn in md.disasm(chunk, start):
        out.append(insn)
        if insn.mnemonic == "ret" or insn.mnemonic == "retq":
            break
        # stop at int3 padding runs
        if insn.bytes == b"\xcc" and len(out) > 1:
            break
    return out

def rip_target(insn) -> int | None:
    """Return absolute VA referenced by RIP-relative mem operand, else None."""
    if not insn.operands:
        return None
    for op in insn.operands:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return insn.address + insn.size + op.mem.disp
    return None

def is_store_to_mem(insn) -> bool:
    # mov/movabs/xchg with first operand memory
    if not insn.operands or len(insn.operands) < 1:
        return False
    if insn.mnemonic.startswith("mov") or insn.mnemonic in ("xchg", "lea"):
        return insn.operands[0].type == X86_OP_MEM
    return False

def is_lea_rip(insn) -> bool:
    return insn.mnemonic == "lea" and rip_target(insn) is not None

def collect_calls(insns):
    calls = []
    for insn in insns:
        if insn.mnemonic != "call":
            continue
        if not insn.operands:
            continue
        op = insn.operands[0]
        if op.type == X86_OP_IMM:
            calls.append(op.imm)
    return calls

def analyze_fn(blob, va, depth, visited, findings):
    if va in visited or depth > MAX_DEPTH or not in_text(va):
        return
    visited.add(va)
    insns = disasm_fn(blob, va)
    stores = []
    leas = []
    for insn in insns:
        tgt = rip_target(insn)
        if tgt is None:
            continue
        if is_lea_rip(insn):
            leas.append((insn.address, tgt, region(tgt), insn.mnemonic, insn.op_str))
        if is_store_to_mem(insn) and insn.mnemonic.startswith("mov"):
            stores.append((insn.address, tgt, region(tgt), insn.mnemonic, insn.op_str))
        # also record any rip mem access (load or store) to BSS/DATA near CodeReg
        if BSS_START - 0x1000 <= tgt < BSS_END + 0x1000 or DATA_START <= tgt < DATA_END:
            findings["rip_access"].append({
                "fn": hex(va), "at": hex(insn.address), "tgt": hex(tgt),
                "reg": region(tgt), "mn": insn.mnemonic, "op": insn.op_str, "depth": depth
            })
    # pair consecutive BSS/DATA stores in same function
    bss_stores = [(a, t, r, m, o) for a, t, r, m, o in stores if r in ("BSS", "DATA")]
    if len(bss_stores) >= 2:
        findings["multi_store"].append({"fn": hex(va), "stores": [
            {"at": hex(a), "tgt": hex(t), "reg": r, "op": o} for a, t, r, m, o in bss_stores
        ]})
    # CodeReg hit?
    for a, t, r, m, o in stores + leas:
        if t == CODEREG_RVA or abs(t - CODEREG_RVA) < 0x80:
            findings["codereg_near"].append({
                "fn": hex(va), "at": hex(a), "tgt": hex(t), "delta": t - CODEREG_RVA,
                "reg": r, "mn": m, "op": o, "depth": depth,
                "sibling_stores": [{"at": hex(x), "tgt": hex(y), "reg": z} for x, y, z, *_ in stores if y != t]
            })
    calls = collect_calls(insns)[:MAX_CALLS_PER_FN]
    findings["fns"].append({
        "va": hex(va), "depth": depth, "n_insns": len(insns),
        "n_calls": len(calls), "n_bss_stores": len(bss_stores),
        "bss_stores": [{"at": hex(a), "tgt": hex(t)} for a, t, *_ in bss_stores[:12]],
    })
    for c in calls:
        analyze_fn(blob, c, depth + 1, visited, findings)

def scan_text_for_codereg_xref(blob: bytes):
    """Brute RIP-disp encodings whose target == CodeReg (or nearby)."""
    hits = []
    # Pattern: look for disp32 such that insn_end + disp == CODEREG
    # Common: 48 89 0D / 48 89 05 / 48 8B 0D / 48 8D 0D etc. with 4-byte disp
    # Scan all possible 4-byte LE disp positions in text
    for ta, tb in TEXT_RANGES:
        off_a = va_to_off(ta)
        if off_a is None:
            continue
        chunk = blob[off_a: va_to_off(tb) or off_a]
        # sliding window for disp that lands on CodeReg
        # For each possible insn_end position e in [ta+1, tb], disp = CODEREG - e
        # Check if bytes at e-4 encode that disp (too many). Better: decode with Capstone in chunks.
        # Faster: for each candidate target T in {CodeReg, CodeReg±8..0x40}, compute required disp
        # and search for the LE bytes of that disp preceded by common ModRM of [rip+disp].
        targets = [CODEREG_RVA + d for d in range(-0x100, 0x101, 8)]
        targets = [t for t in targets if BSS_START <= t < BSS_END]
        # Also absolute: search LE64 of CodeReg as immediate (rare for Mach-O PIC)
        le = struct.pack("<Q", CODEREG_RVA)
        idx = 0
        while True:
            j = chunk.find(le, idx)
            if j < 0:
                break
            hits.append({"kind": "imm64", "at": hex(ta + j), "tgt": hex(CODEREG_RVA)})
            idx = j + 1
    return hits

def scan_rip_disp_to_targets(blob: bytes, targets: set[int], label: str):
    """Capstone-walk __text+il2cpp looking for rip+disp resolving into targets."""
    hits = []
    for ta, tb in TEXT_RANGES:
        off = va_to_off(ta)
        if off is None:
            continue
        # walk in 64KB windows
        va = ta
        while va < tb:
            end = min(va + 0x10000, tb)
            chunk = blob[off + (va - ta): off + (end - ta)]
            # realign: use from va
            chunk = blob[va_to_off(va): va_to_off(end) if va_to_off(end) else len(blob)]
            for insn in md.disasm(chunk, va):
                tgt = rip_target(insn)
                if tgt is not None and tgt in targets:
                    hits.append({
                        "label": label, "at": hex(insn.address), "tgt": hex(tgt),
                        "mn": insn.mnemonic, "op": insn.op_str,
                        "is_store": is_store_to_mem(insn) and insn.mnemonic.startswith("mov"),
                        "is_lea": insn.mnemonic == "lea",
                    })
            # advance past last decoded; if stuck, skip a byte
            if not chunk:
                break
            # Capstone may stop early on bad bytes; jump by window
            va = end
    return hits

def build_bss_slot_targets():
    # every 8-byte aligned slot in BSS near CodeReg ±0x200, plus full BSS is too big —
    # for full scan we use a different approach: collect ALL rip stores into BSS.
    return set(range(CODEREG_RVA - 0x200, CODEREG_RVA + 0x200, 8))

def scan_all_bss_rip_stores(blob: bytes):
    """Full text walk: every RIP-relative store/lea into BSS or DATA globals."""
    stores, leas = [], []
    for ta, tb in TEXT_RANGES:
        va = ta
        while va < tb:
            end = min(va + 0x20000, tb)
            off_s = va_to_off(va)
            off_e = va_to_off(end)
            if off_s is None:
                break
            chunk = blob[off_s: off_e or off_s]
            last = va
            for insn in md.disasm(chunk, va):
                last = insn.address + insn.size
                tgt = rip_target(insn)
                if tgt is None:
                    continue
                reg = region(tgt)
                if reg not in ("BSS", "DATA"):
                    continue
                rec = {"at": hex(insn.address), "tgt": hex(tgt), "reg": reg,
                       "mn": insn.mnemonic, "op": insn.op_str}
                if insn.mnemonic == "lea":
                    leas.append(rec)
                elif insn.mnemonic.startswith("mov") and is_store_to_mem(insn):
                    stores.append(rec)
            if last <= va:
                va = end
            else:
                va = last if last < end else end
    return stores, leas

def dump_fn_preview(blob, va, n=40):
    lines = []
    for insn in disasm_fn(blob, va)[:n]:
        tgt = rip_target(insn)
        extra = ""
        if tgt is not None:
            extra = f"  ; -> {hex(tgt)} [{region(tgt)}]"
            if abs(tgt - CODEREG_RVA) < 0x100:
                extra += f" CODEREG{tgt-CODEREG_RVA:+#x}"
        lines.append(f"  {insn.address:08x}: {insn.mnemonic:8s} {insn.op_str}{extra}")
    return "\n".join(lines)

def main():
    blob = load()
    print(f"dylib={DYLIB} size={len(blob)}")
    print(f"il2cpp_init={hex(IL2CPP_INIT)} CodeReg={hex(CODEREG_RVA)} BSS={hex(BSS_START)}..{hex(BSS_END)}")

    findings = collections.defaultdict(list)
    visited = set()
    print("\n=== BFS from _il2cpp_init / _il2cpp_init_utf16 ===")
    analyze_fn(blob, IL2CPP_INIT, 0, visited, findings)
    analyze_fn(blob, IL2CPP_INIT_UTF16, 0, visited, findings)
    print(f"visited_fns={len(visited)}")
    for fn in findings["fns"]:
        if fn["n_bss_stores"] or fn["depth"] <= 1:
            print(f"  fn {fn['va']} depth={fn['depth']} insns={fn['n_insns']} calls={fn['n_calls']} bss_stores={fn['n_bss_stores']} {fn['bss_stores']}")

    print("\n=== CodeReg-near from call graph ===")
    for h in findings["codereg_near"]:
        print(h)
    print("\n=== Multi BSS/DATA stores same fn ===")
    for h in findings["multi_store"][:30]:
        print(h)
    print("\n=== RIP access near BSS/DATA (callgraph) sample ===")
    for h in findings["rip_access"][:40]:
        print(h)

    print("\n=== Imm64 CodeReg xref scan ===")
    imm = scan_text_for_codereg_xref(blob)
    print(f"imm64_hits={len(imm)}")
    for h in imm[:20]:
        print(h)

    print("\n=== RIP-disp to CodeReg±0x200 (full text) ===")
    near = build_bss_slot_targets()
    near_hits = scan_rip_disp_to_targets(blob, near, "codereg_near")
    print(f"near_hits={len(near_hits)}")
    for h in near_hits:
        print(h)

    print("\n=== ALL RIP stores/leas into BSS+DATA (may take a bit) ===")
    stores, leas = scan_all_bss_rip_stores(blob)
    print(f"bss/data stores={len(stores)} leas={len(leas)}")
    # Focus CodeReg-near stores
    cr_stores = [s for s in stores if abs(int(s["tgt"], 16) - CODEREG_RVA) < 0x200]
    cr_leas = [s for s in leas if abs(int(s["tgt"], 16) - CODEREG_RVA) < 0x200]
    print(f"CodeReg±0x200 stores={len(cr_stores)} leas={len(cr_leas)}")
    for s in cr_stores:
        print("STORE", s)
    for s in cr_leas:
        print("LEA", s)

    # Group stores by function-ish (align down to 0x10) — better: for each store into BSS,
    # disassemble surrounding 0x80 bytes and look for sibling BSS stores
    print("\n=== Sibling analysis around CodeReg-near writers ===")
    writers = cr_stores + cr_leas
    seen_ctx = set()
    for w in writers:
        at = int(w["at"], 16)
        # back up to find function start heuristically: previous 0xCC or align
        start = max(at - 0x80, TEXT_RANGES[0][0] if in_text(TEXT_RANGES[0][0]) else at - 0x80)
        # find a nearby plausible start
        ctx_key = at & ~0x3F
        if ctx_key in seen_ctx:
            continue
        seen_ctx.add(ctx_key)
        print(f"\n-- context around {w['at']} -> {w['tgt']} --")
        # disasm from at-0x40
        base = max(at - 0x40, 0)
        off = va_to_off(base)
        if off is None:
            continue
        for insn in list(md.disasm(blob[off:off+0xC0], base))[:50]:
            tgt = rip_target(insn)
            mark = ""
            if tgt is not None:
                mark = f"  ; -> {hex(tgt)} [{region(tgt)}]"
                if abs(tgt - CODEREG_RVA) < 0x200:
                    mark += " ***"
            print(f"  {insn.address:08x}: {insn.mnemonic:8s} {insn.op_str}{mark}")

    # Also dump il2cpp_init itself
    print("\n=== _il2cpp_init preview ===")
    print(dump_fn_preview(blob, IL2CPP_INIT, 60))

    # mod_init_func dump
    print("\n=== __mod_init_func pointers ===")
    mod_va, mod_sz = 0x2D2F9D8, 0xB8
    off = va_to_off(mod_va)
    ptrs = []
    for i in range(0, mod_sz, 8):
        p = struct.unpack_from("<Q", blob, off + i)[0]
        ptrs.append(p)
        print(f"  [{i//8}] {hex(p)}")
    # analyze each mod_init
    print("\n=== BFS from mod_init_func ===")
    findings2 = collections.defaultdict(list)
    visited2 = set()
    for p in ptrs:
        if in_text(p):
            analyze_fn(blob, p, 0, visited2, findings2)
    print(f"mod_init visited={len(visited2)}")
    for h in findings2["codereg_near"]:
        print("mod_init CodeReg-near", h)
    for h in findings2["multi_store"][:20]:
        print("mod_init multi", h)

if __name__ == "__main__":
    main()
