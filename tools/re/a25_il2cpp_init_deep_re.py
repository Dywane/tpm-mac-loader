#!/usr/bin/env python3
"""Task 2.5-A deepened RE: depth>=5, larger windows, MetaReg 0x78FA, dual-store, CodeGenModules trail."""
from __future__ import annotations
import struct, collections, json, sys
from pathlib import Path
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import *

DYLIB = Path("/tmp/GameAssembly-x64-meta.dylib")
IL2CPP_INIT = 0x29B6D9
MAIN_INIT = 0x2E975A
STUB = 0x2890f66
MODULES = 0x2D2FA90
TYPECOUNT = 0x78FA
DEPRECATED = {0x34D4F68, 0x34CC0D0}
BSS_START, BSS_END = 0x34CBD10, 0x34CBD10 + 0x21D21D
DATA_START, DATA_END = 0x2FD0FB0, 0x2FD0FB0 + 0x4FAD60
CONST_START, CONST_END = 0x2D2FA90, 0x2D2FA90 + 0x2A1520
TEXT_RANGES = [(0x08E0, 0x08E0 + 0x30C9EC), (0x30D2D0, 0x30D2D0 + 0x25836CB)]

# Deepen vs A1: depth 3->6, fn 0x400->0x1800, calls/fn 24->48
MAX_DEPTH = 6
MAX_FN_BYTES = 0x1800
MAX_CALLS_PER_FN = 48

md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True

def va_to_off(va):
    if 0 <= va < 0x2D2F000: return va
    if 0x2D2F000 <= va < 0x34CBD10: return va
    return None

def in_text(va):
    return any(a <= va < b for a, b in TEXT_RANGES)

def region(va):
    if BSS_START <= va < BSS_END: return "BSS"
    if DATA_START <= va < DATA_END: return "DATA"
    if CONST_START <= va < CONST_END: return "CONST"
    if in_text(va): return "TEXT"
    if 0x2D2F000 <= va < 0x2D2FA90: return "GOT"
    return "?"

def rip_target(insn):
    for op in insn.operands or []:
        if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
            return insn.address + insn.size + op.mem.disp
    return None

def disasm_fn(blob, start, limit=MAX_FN_BYTES):
    off = va_to_off(start)
    if off is None or off >= len(blob): return []
    out = []
    for insn in md.disasm(blob[off:off+limit], start):
        out.append(insn)
        if insn.mnemonic in ("ret", "retq"): break
        if insn.bytes == b"\xcc" and len(out) > 1: break
    return out

def collect_calls(insns):
    calls = []
    for insn in insns:
        if insn.mnemonic == "call" and insn.operands and insn.operands[0].type == X86_OP_IMM:
            calls.append((insn.address, insn.operands[0].imm))
    return calls

blob = DYLIB.read_bytes()
print(f"dylib size={len(blob)} MAX_DEPTH={MAX_DEPTH} MAX_FN={MAX_FN_BYTES:#x}")

# ---------- 1) Deep BFS from main init + stub + il2cpp_init ----------
findings = {
    "dual_ptr_stores": [],
    "typecount_imm": [],
    "modules_uses": [],
    "interesting_fns": [],
    "bss_ptr_pair_near": [],
    "lea_then_store_reg": [],
}
visited = {}
call_edges = []  # (from_fn, call_at, to)

def analyze(va, depth, parent=None):
    if va in visited or depth > MAX_DEPTH or not in_text(va):
        return
    # skip PLT/stub helpers spam except recording
    visited[va] = depth
    insns = disasm_fn(blob, va)
    stores, leas, imm_hits = [], [], []
    for insn in insns:
        # imm 0x78FA / 30970
        for op in insn.operands or []:
            if op.type == X86_OP_IMM and op.imm in (TYPECOUNT, 30970, TYPECOUNT | (0 << 32)):
                imm_hits.append((insn.address, op.imm, insn.mnemonic, insn.op_str))
                findings["typecount_imm"].append({
                    "fn": hex(va), "at": hex(insn.address), "imm": hex(op.imm),
                    "mn": insn.mnemonic, "op": insn.op_str, "depth": depth
                })
        tgt = rip_target(insn)
        if tgt is None:
            continue
        if tgt == MODULES or abs(tgt - MODULES) < 0x10:
            findings["modules_uses"].append({
                "fn": hex(va), "at": hex(insn.address), "tgt": hex(tgt),
                "mn": insn.mnemonic, "op": insn.op_str, "depth": depth
            })
        if insn.mnemonic == "lea":
            leas.append((insn.address, tgt, insn.op_str, insn))
        if insn.mnemonic.startswith("mov") and insn.operands and insn.operands[0].type == X86_OP_MEM:
            stores.append((insn.address, tgt, insn.op_str, insn))

    # dual consecutive BSS/DATA qword stores (potential Register dual-store)
    bss = [(a, t, o, i) for a, t, o, i in stores if region(t) in ("BSS", "DATA")]
    for i in range(len(bss) - 1):
        a1, t1, o1, _ = bss[i]
        a2, t2, o2, _ = bss[i + 1]
        if abs(a2 - a1) <= 0x20 and region(t1) == region(t2):
            # pair of nearby stores
            gap = abs(t2 - t1)
            if gap in (8, 0x10, 0x18, 0x20) or (0 < gap <= 0x40):
                findings["dual_ptr_stores"].append({
                    "fn": hex(va), "depth": depth,
                    "s1": {"at": hex(a1), "tgt": hex(t1), "op": o1},
                    "s2": {"at": hex(a2), "tgt": hex(t2), "op": o2},
                    "tgt_gap": gap,
                })

    # LEA of something then soon store that reg to BSS? (pass structure ptr)
    # Heuristic: lea dest_reg, [rip+X]; within 0x40 bytes mov [rip+BSS], dest_reg
    for la, lt, lo, li in leas:
        if region(lt) not in ("CONST", "DATA", "BSS", "?"):
            continue
        # find which reg was written
        if not li.operands: continue
        dest = li.operands[0]
        if dest.type != X86_OP_REG: continue
        dreg = dest.reg
        for sa, st, so, si in stores:
            if not (0 < sa - la <= 0x60): continue
            if region(st) not in ("BSS", "DATA"): continue
            # check if store src is dreg
            if len(si.operands) >= 2 and si.operands[1].type == X86_OP_REG and si.operands[1].reg == dreg:
                findings["lea_then_store_reg"].append({
                    "fn": hex(va), "depth": depth,
                    "lea_at": hex(la), "lea_tgt": hex(lt), "lea_reg": region(lt),
                    "store_at": hex(sa), "store_tgt": hex(st), "store_reg": region(st),
                })

    if imm_hits or any(t == MODULES for _, t, _, _ in leas) or len(bss) >= 4:
        findings["interesting_fns"].append({
            "fn": hex(va), "depth": depth, "n_insns": len(insns),
            "n_bss": len(bss), "imm": imm_hits[:5],
            "parent": hex(parent) if parent else None,
        })

    calls = collect_calls(insns)[:MAX_CALLS_PER_FN]
    for at, c in calls:
        call_edges.append((va, at, c))
        # prune obvious allocator / stub noise at deep levels still explore but mark
        analyze(c, depth + 1, parent=va)

print("=== Deep BFS from MAIN_INIT / IL2CPP_INIT / STUB ===")
for entry in (MAIN_INIT, IL2CPP_INIT, STUB):
    analyze(entry, 0)
print(f"visited_fns={len(visited)} call_edges={len(call_edges)}")
print(f"typecount_imm_hits={len(findings['typecount_imm'])}")
for h in findings["typecount_imm"][:30]:
    print("  IMM", h)
print(f"modules_uses={len(findings['modules_uses'])}")
for h in findings["modules_uses"][:20]:
    print("  MOD", h)
print(f"dual_ptr_stores={len(findings['dual_ptr_stores'])}")
for h in findings["dual_ptr_stores"][:40]:
    print("  DUAL", h)
print(f"lea_then_store={len(findings['lea_then_store_reg'])}")
for h in findings["lea_then_store_reg"][:40]:
    print("  LEA->ST", h)

# depth histogram
hist = collections.Counter(visited.values())
print("depth_hist", dict(sorted(hist.items())))

# ---------- 2) Full-binary search for LE64 0x78FA in file-backed segments ----------
print("\n=== Full file LE64/LE32 typecount scan ===")
pat8 = struct.pack("<Q", TYPECOUNT)
pat4 = struct.pack("<I", TYPECOUNT)
hits8 = []
idx = 0
while True:
    j = blob.find(pat8, idx)
    if j < 0: break
    hits8.append(j)
    idx = j + 1
hits4 = []
idx = 0
# only in non-text? scan all but report region
while True:
    j = blob.find(pat4, idx)
    if j < 0: break
    hits4.append(j)
    idx = j + 1
print(f"LE64_0x78FA count={len(hits8)} offs={[hex(h) for h in hits8[:20]]}")
print(f"LE32_0x78FA count={len(hits4)}")
# classify LE32 by region
by_reg = collections.Counter()
for h in hits4:
    by_reg[region(h)] += 1
print("LE32 by region", dict(by_reg))
# For each LE64 hit, dump qword neighborhood as potential MetaReg field
for h in hits8:
    # read surrounding 0x80 as potential struct
    base = max(0, h - 0x70)
    qs = []
    for off in range(base, min(len(blob), h + 0x40), 8):
        qs.append((off, struct.unpack_from("<Q", blob, off)[0]))
    print(f"\nLE64 hit fileoff/va={hex(h)} region={region(h)}")
    for off, v in qs:
        mark = " <<<" if off == h else ""
        print(f"  [{hex(off)}] {v:#018x}{mark}")

# ---------- 3) Search code for mov imm32 0x78FA encodings ----------
print("\n=== Capstone walk TEXT for imm==0x78FA (full) ===")
imm_full = []
for ta, tb in TEXT_RANGES:
    va = ta
    while va < tb:
        end = min(va + 0x20000, tb)
        off_s = va_to_off(va)
        chunk = blob[off_s:va_to_off(end) or off_s]
        last = va
        for insn in md.disasm(chunk, va):
            last = insn.address + insn.size
            for op in insn.operands or []:
                if op.type == X86_OP_IMM and (op.imm & 0xFFFFFFFF) == TYPECOUNT:
                    imm_full.append((insn.address, insn.mnemonic, insn.op_str, op.imm))
        va = end if last <= va else (last if last < end else end)
print(f"full_text imm0x78FA={len(imm_full)}")
for h in imm_full[:50]:
    print(" ", h)

# ---------- 4) After MODULES lea at 0x2E9E93: dump 200 insns & track r12 uses ----------
print("\n=== Trail after MODULES LEA 0x2E9E93 ===")
off = va_to_off(0x2E9E93)
for insn in list(md.disasm(blob[off:off+0x400], 0x2E9E93))[:120]:
    tgt = rip_target(insn)
    extra = f" ; ->{hex(tgt)}[{region(tgt)}]" if tgt else ""
    print(f"  {insn.address:08x}: {insn.mnemonic:8s} {insn.op_str}{extra}")

# ---------- 5) Find functions that store TWO pointer-looking values from args (Register pattern) ----------
# Heuristic: fn prologue; early mov [rip+X], rdi/rsi/rdx (first 3 args) — classic Register(code, meta, opts)
print("\n=== Scan TEXT for early arg-store to BSS (Register signature) ===")
register_cands = []
for ta, tb in TEXT_RANGES:
    # sample every potential function: look for push rbp; mov rbp,rsp OR endbr/aligned after int3
    va = ta
    while va < tb:
        end = min(va + 0x10000, tb)
        off_s = va_to_off(va)
        chunk = blob[off_s:va_to_off(end) or off_s]
        # find candidate fn starts: 55 48 89 e5 or 4883ec
        i = 0
        while i < len(chunk) - 8:
            # look for mov [rip+disp], rdi/rsi/rdx  (48 89 3D / 48 89 35 / 48 89 15)
            b0 = chunk[i]
            # 48 89 3D xx xx xx xx  = mov [rip+d], rdi
            # 48 89 35 = rsi; 48 89 15 = rdx; 48 89 05 = rax
            if chunk[i:i+3] in (b"\x48\x89\x3d", b"\x48\x89\x35", b"\x48\x89\x15"):
                disp = struct.unpack_from("<i", chunk, i+3)[0]
                insn_end = va + i + 7
                tgt = insn_end + disp
                if region(tgt) in ("BSS", "DATA"):
                    which = {b"\x48\x89\x3d": "rdi", b"\x48\x89\x35": "rsi", b"\x48\x89\x15": "rdx"}[chunk[i:i+3]]
                    register_cands.append((va + i, tgt, which))
            i += 1
        va = end

# Cluster: same function-ish window (±0x80) having both rdi and rsi stores to BSS
print(f"raw arg-stores to BSS/DATA: {len(register_cands)}")
# group by approximate function (align to 0x40 of address)
from collections import defaultdict
clusters = defaultdict(list)
for at, tgt, which in register_cands:
    clusters[at & ~0x7F].append((at, tgt, which))

dual_arg = []
for k, items in clusters.items():
    regs = {w for _, _, w in items}
    if "rdi" in regs and "rsi" in regs:
        dual_arg.append((k, items))
print(f"clusters with BOTH rdi+rsi BSS stores: {len(dual_arg)}")
for k, items in dual_arg[:30]:
    print(f"  cluster@{hex(k)}:")
    for at, tgt, w in sorted(items):
        print(f"    {hex(at)} mov [,{w}] -> {hex(tgt)} [{region(tgt)}]")
    # disasm context
    start = min(at for at,_,_ in items) - 0x20
    start = max(start, 0)
    off = va_to_off(start)
    if off:
        print("    context:")
        for insn in list(md.disasm(blob[off:off+0xA0], start))[:25]:
            tgt = rip_target(insn)
            ex = f" ;->{hex(tgt)}" if tgt else ""
            print(f"      {insn.address:08x}: {insn.mnemonic:8s} {insn.op_str}{ex}")

# ---------- 6) Export summary JSON ----------
summary = {
    "visited_fns": len(visited),
    "max_depth_reached": max(visited.values()) if visited else 0,
    "depth_hist": dict(sorted(hist.items())),
    "typecount_imm_in_bfs": len(findings["typecount_imm"]),
    "typecount_imm_full_text": len(imm_full),
    "le64_78FA_file": len(hits8),
    "le32_78FA_by_region": dict(by_reg),
    "dual_ptr_stores_bfs": len(findings["dual_ptr_stores"]),
    "lea_then_store": len(findings["lea_then_store_reg"]),
    "modules_uses": findings["modules_uses"],
    "dual_arg_register_clusters": len(dual_arg),
    "dual_arg_detail": [
        {"cluster": hex(k), "stores": [{"at": hex(a), "tgt": hex(t), "reg": w} for a, t, w in items]}
        for k, items in dual_arg[:50]
    ],
    "typecount_imm_samples": findings["typecount_imm"][:20],
    "imm_full_samples": [{"at": hex(a), "mn": m, "op": o, "imm": hex(i)} for a, m, o, i in imm_full[:30]],
    "lea_then_store_samples": findings["lea_then_store_reg"][:30],
    "dual_ptr_samples": findings["dual_ptr_stores"][:30],
}
out = Path("/tmp/tpm_a25_deep_summary.json")
out.write_text(json.dumps(summary, indent=2))
print(f"\nWrote {out}")
