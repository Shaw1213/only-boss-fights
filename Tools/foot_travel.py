"""
Foot-locked timing for a walk cycle (a cleaned DreamLayer sprite-sheet ZIP, cycle kept in place).

A character walking on the spot slides its planted foot backward; in the game the body moves forward at
the same speed, so the foot appears to stay put on the floor. That only works if each frame lasts in
proportion to how far the planted foot slides before the next frame. DreamLayer's frame_durations_ms are
near uniform (620-660 ms) whatever the foot does, so frames where the foot barely moves (two feet-together
poses in a row) stall the body and the foot skates.

For each pair of consecutive frames (wrapping, for a loop) this takes the foot on the floor in the second
frame (the boot touching the floor row) and finds where it was in the first: shifted forward by the travel,
and possibly still in the air (a heel strike lands between the two frames), so the search also lifts it.
The planted foot is a rigid shape, so the best overlap gives the travel. Durations are then made
proportional to that travel (keeping the authored total, with a floor so no frame vanishes), and the total
travel per cycle is written out so the game can derive the ground speed at which the cycle plays at its
authored rate. A match below --min-score (a heel strike seen toe-first, say) is replaced by the mean of the
confident ones, and listed under "estimated".

Writes <zip name>.foot_travel.json next to the ZIP:
  travel_px        per frame: planted-foot travel to the next frame, in sheet pixels
  durations_ms     foot-locked durations (sum = the authored total)
  total_travel_px  per cycle
Needs feet on one baseline (clean_sprite_zip.py --snap-feet) and no baked-in drift (--remove-drift).
--order plays a subset / reordering as the loop (1-based frame numbers, e.g. 8,2,3,4,5,6,7 to drop a duplicate
feet-together frame); it is recorded in the JSON as "order", and the timing is for that sequence.

Usage:
  python -I Tools/foot_travel.py WALK.zip [--order 8,2,3,4,5,6,7] [--boot 0.12] [--max-shift 0.3] [--min-share 0.25]
"""
import argparse
import io
import json
import os
import zipfile

import numpy as np
from PIL import Image


def load(zip_path):
    with zipfile.ZipFile(zip_path) as z:
        atlas = json.loads(z.read("atlas.json"))
        names = sorted(n for n in z.namelist() if n.startswith("frames/") and n.endswith(".png"))
        frames = [np.array(Image.open(io.BytesIO(z.read(n))).convert("RGBA")) for n in names]
    return atlas, names, frames


def components(mask):
    """8-connected components of a small mask, as a label image (0 = background). Plain numpy flood fill."""
    labels = np.zeros(mask.shape, np.int32)
    n = 0
    for y, x in zip(*np.nonzero(mask)):
        if labels[y, x]:
            continue
        n += 1
        stack = [(y, x)]
        labels[y, x] = n
        while stack:
            cy, cx = stack.pop()
            for ny in range(max(0, cy - 1), min(mask.shape[0], cy + 2)):
                for nx in range(max(0, cx - 1), min(mask.shape[1], cx + 2)):
                    if mask[ny, nx] and not labels[ny, nx]:
                        labels[ny, nx] = n
                        stack.append((ny, nx))
    return labels, n


def planted_feet(alpha, floor, boot_rows):
    """Masks (boot_rows tall, full width) of each boot touching the floor row (the bottom 3 rows)."""
    region = alpha[floor - boot_rows + 1:floor + 1]
    labels, n = components(region)
    feet = []
    for i in range(1, n + 1):
        blob = labels == i
        if blob[-3:].any() and blob.sum() > 40:
            feet.append(blob)
    return feet


def find_travel(foot, prev_alpha, floor, boot_rows, max_shift, max_lift):
    """Where the planted foot was one frame earlier: (travel backward in px, lift in px, overlap share)."""
    total = foot.sum()
    best = (0, 0, -1.0)
    w = foot.shape[1]
    for lift in range(0, max_lift + 1):
        top = floor - boot_rows + 1 - lift
        if top < 0:
            break
        prev = prev_alpha[top:top + boot_rows]
        for s in range(-max_shift // 6, max_shift + 1):  # it came from further forward (+x); a little noise allowed
            if s >= 0:
                overlap = (foot[:, :w - s] & prev[:, s:]).sum()
            else:
                overlap = (foot[:, -s:] & prev[:, :w + s]).sum()
            score = overlap / total
            if score > best[2] + 1e-9 or (abs(score - best[2]) < 1e-9 and lift < best[1]):
                best = (s, lift, float(score))
    return best


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("zip")
    ap.add_argument("--boot", type=float, default=0.12, help="boot height (the region matched) as a fraction of the character's height")
    ap.add_argument("--max-shift", type=float, default=0.3, help="largest travel searched, as a fraction of the character's height")
    ap.add_argument("--max-lift", type=float, default=0.08, help="highest a landing foot is searched above the floor, as a fraction of the height")
    ap.add_argument("--min-share", type=float, default=0.25, help="shortest frame, as a fraction of the mean duration")
    ap.add_argument("--min-score", type=float, default=0.7, help="overlap share below which a match is replaced by the mean travel")
    ap.add_argument("--no-loop", action="store_true", help="last frame has no successor (keeps its authored share)")
    ap.add_argument("--order", help="frames to play, 1-based, comma-separated (default: all, in order)")
    args = ap.parse_args()

    atlas, names, frames = load(args.zip)
    authored = atlas.get("frame_durations_ms") or [atlas.get("duration_ms", 100)] * len(frames)
    order = [int(v) for v in args.order.split(",")] if args.order else list(range(1, len(frames) + 1))
    frames = [frames[i - 1] for i in order]
    authored = [authored[i - 1] for i in order]
    alphas = [f[..., 3] > 128 for f in frames]
    floor = max(int(np.where(a.any(1))[0].max()) for a in alphas)
    height = max(floor - int(np.where(a.any(1))[0].min()) + 1 for a in alphas)
    rows = max(8, int(round(height * args.boot)))
    max_shift = int(round(height * args.max_shift))
    max_lift = int(round(height * args.max_lift))

    n = len(frames)
    pairs = n if not args.no_loop else n - 1
    travel, scores, lifts = [], [], []
    for i in range(pairs):
        j = (i + 1) % n
        # Each boot on the floor in frame j, traced back into frame i; the best-matching one wins (in double
        # support the toe-off foot pivots on its toe and matches worse than the flat one).
        found = [find_travel(f, alphas[i], floor, rows, max_shift, max_lift) for f in planted_feet(alphas[j], floor, rows)]
        s, lift, score = max(found, key=lambda r: r[2]) if found else (0, 0, 0.0)
        travel.append(max(0, s))
        lifts.append(lift)
        scores.append(round(score, 3))

    # Unsure matches take the mean of the confident moving ones (a true hold, 0 px with a good match, stays 0).
    confident = [t for t, sc in zip(travel, scores) if sc >= args.min_score and t > 0]
    estimated = [i + 1 for i, sc in enumerate(scores) if sc < args.min_score]
    if confident:
        for i in range(len(travel)):
            if scores[i] < args.min_score:
                travel[i] = int(round(float(np.mean(confident))))

    total_ms = float(sum(authored))
    mean_travel = float(np.mean(travel)) if travel else 0.0
    shares = [max(t, args.min_share * mean_travel) for t in travel]
    if args.no_loop:
        shares.append(float(np.mean(shares)))  # the held last frame
    scale = total_ms / sum(shares) if sum(shares) > 0 else 1.0
    durations = [int(round(s * scale)) for s in shares]
    durations[-1] += int(round(total_ms)) - sum(durations)  # keep the total exact

    out = {
        "source_zip": os.path.basename(args.zip),
        "frames": n,
        "order": order,
        "floor_row": floor,
        "boot_rows": rows,
        "travel_px": travel,
        "landing_lift_px": lifts,
        "match_scores": scores,
        "estimated": estimated,
        "total_travel_px": int(sum(travel)),
        "authored_durations_ms": authored,
        "durations_ms": durations,
        "note": "durations proportional to planted-foot travel to the next frame (min share %.2f of the mean)" % args.min_share,
    }
    path = os.path.splitext(args.zip)[0] + ".foot_travel.json"
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    print(json.dumps({k: out[k] for k in ("travel_px", "match_scores", "estimated", "total_travel_px", "durations_ms")}))
    print(f"wrote {path}")


if __name__ == "__main__":
    main()
