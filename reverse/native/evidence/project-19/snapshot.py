"""Read-only EC snapshot + diff. usage: snapshot.py <label> [prev_label]"""
import sys, json, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2]))
from probe_ec import read_addresses
D = pathlib.Path(__file__).parent
addrs = list(range(0x400, 0x500)) + list(range(0x700, 0x800))
def take():
    r = read_addresses(addrs)
    return {x["address"]: x["byte"] for x in r["reads"] if x["transport_ok"] and x["returned_bytes"] == 4}
a = take(); b = take()
label = sys.argv[1]
stable = {k: v for k, v in a.items() if b.get(k) == v}
(D / f"{label}.json").write_text(json.dumps(stable))
print(label, "stable bytes:", len(stable), "/", len(addrs))
if len(sys.argv) > 2:
    p = json.loads((D / f"{sys.argv[2]}.json").read_text())
    for k in sorted(stable, key=lambda s: int(s, 16)):
        if k in p and p[k] != stable[k]:
            print(f"  {k}: {p[k]:#04x} -> {stable[k]:#04x}")
