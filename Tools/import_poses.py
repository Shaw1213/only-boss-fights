"""
Cut out, normalize and copy DreamLayer pose images into the Unity project, with a provenance sidecar.

Each pose is scaled so a rigid "ruler" feature matches the master's at the character's target art size
(see normalize_pose.py): the hero's head (eye line to chin; his sword length drifts between poses), the
knight's helm height (his sword is foreshortened in the master). Edit the POSES table below, then run:
  python -I Tools/import_poses.py [name ...]      (no names = all)
"""
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PY = sys.executable

# Per character: target standing body height (px) of the master, the master's body height and ruler length.
CHARACTERS = {
    "Hero":   {"target_height": 800, "master_height": 1696, "master_ruler": 89.0},   # eye line to chin, by hand
    "Knight": {"target_height": 960, "master_height": 1920, "master_ruler": 235.0},  # helm height, dome to chin, by hand
    # The King's edits came back on the reference's 1600 px canvas, but not all at its scale: in game the strike
    # visibly grew. Each pose's ruler is the master's 100 times how much bigger it's drawn than the idle, measured on
    # rigid parts (helm and crown, knee medallions, sabatons, belt gems: template matching over scale and rotation)
    # and checked side by side. 900 px keeps the widest poses under the 1024 texture cap.
    "King":   {"target_height": 900, "master_height": 1392, "master_ruler": 100.0},  # king-ref-t4 cut, body height
    # The Unknown One (2026-10-11): measured the same way as the King from the start (helm + crown, crown, chest eye,
    # knee medallion, sabaton; template matching over scale and rotation, then checked side by side). 900 px keeps the
    # widest pose (phase 2, tentacles spread) under the 1024 texture cap.
    "Final":  {"target_height": 900, "master_height": 1784, "master_ruler": 100.0},  # final-ref-b cut, body height
}

# name: (character, raw image, ruler px in the cut image, execution_id, idempotency key, note)
POSES = {
    "hero_idle":           ("Hero",   "ArtRaw/hero/hero-side.png", 120.0, "8bea9134-a324-446c-b799-e2a1f51e3168", "dlj-hero-side-t1", "combat-ready stance, the step cycle's own reference, as the idle (so idle and walk match)"),
    "hero_stand":          ("Hero",   "ArtRaw/cast/hero-master.png", 89.0, "c91cdf98-2289-4485-94f9-0ae481a31509", "dlj-cast-sheet-002", "hero master standing (lobby / cutscenes)"),
    "hero_slash":          ("Hero",   "ArtRaw/hero/hero-slash-001.png", 122.0, "2a251be8-91bd-4fd5-a463-f2621efb0691", "dlj-hero-slash-001", "light slash, active frame"),
    "knight_idle":         ("Knight", "ArtRaw/cast/knight-master.png", 235.0, "c91cdf98-2289-4485-94f9-0ae481a31509", "dlj-cast-sheet-002", "knight master (cast sheet crop) as the idle pose"),
    "knight_cleave_windup": ("Knight", "ArtRaw/knight/knight-cleave-windup-001.png", 192.0, "3b76b9a1-f09a-40ac-9f4d-2a8dcb3bc28c", "dlj-knight-cleave-windup-001", "overhead cleave windup"),
    "hero_charge":         ("Hero",   "ArtRaw/hero/hero-charge.png", 121.0, "ffb3d22c-3f4f-47af-a870-0785f50719cd", "dlj-hero-charge-t6", "charging a heavy (low coiled stance, glowing blade)"),
    "knight_cleave_strike": ("Knight", "ArtRaw/knight/knight-cleave-strike.png", 270.0, "1934660d-9c8a-4686-890c-eab9e1b1c856", "dlj-knight-cleave-strike-t1", "overhead cleave at impact"),
    "knight_lunge":        ("Knight", "ArtRaw/knight/knight-lunge.png", 265.0, "bc4202b1-9540-4e85-8995-4157ea651520", "dlj-knight-lunge-t1", "lunge thrust, sword level"),
    "hero_windup": ("Hero", "ArtRaw/hero/hero-windup.png", 117.0, "ebfcd1d4-9056-4da3-beb8-f9ffd48b10bb", "dlj-hero-windup-t2", "light-attack windup (anticipation)"),
    "hero_slash2": ("Hero", "ArtRaw/hero/hero-slash2.png", 117.0, "bb82b8b8-8e35-4042-9bb2-9d3674794485", "dlj-hero-slash2-t1", "second slash (rising), alternates in the combo"),
    "hero_block": ("Hero", "ArtRaw/hero/hero-block.png", 117.0, "f632a443-1ca7-4179-971c-7d9fdc4503aa", "dlj-hero-block-t2", "guard / parry (chin hidden by the blade: batch-norm ruler)"),
    "hero_dash": ("Hero", "ArtRaw/hero/hero-dash.png", 140.0, "767e1799-28a1-4a19-9a4c-14665c9bcdc5", "dlj-hero-dash-t1", "dash"),
    "hero_hurt": ("Hero", "ArtRaw/hero/hero-hurt.png", 105.0, "04ff8f1c-8b21-41f4-8c08-68f7b9fe1d4b", "dlj-hero-hurt-t2", "hurt recoil"),
    "hero_dead": ("Hero", "ArtRaw/hero/hero-dead.png", 146.0, "0a75230f-e126-43e1-af2b-fc9565cec0d2", "dlj-hero-dead-t2", "defeated (came back facing left: mirrored; ruler from ear size, head bowed)"),
    "knight_recovery": ("Knight", "ArtRaw/knight/knight-recovery.png", 277.0, "2402d171-f1ff-46aa-9d73-23599bdca2c8", "dlj-knight-recovery-t3", "recovery / punish window (facing left: mirrored; ruler from body height vs. the standing idle)"),
    "knight_staggered": ("Knight", "ArtRaw/knight/knight-staggered.png", 285.0, "c84d09df-dfd8-4c0f-ac87-824895235c77", "dlj-knight-staggered-t1", "staggered (facing left: mirrored; ruler from shin length)"),
    "knight_defeated": ("Knight", "ArtRaw/knight/knight-defeated.png", 305.0, "82d143b3-99ee-415e-b86a-3bab7cf159f6", "dlj-knight-defeated-t1", "defeated (facing left: mirrored; ruler from kneeling height)"),
    "knight_lunge_windup": ("Knight", "ArtRaw/knight/knight-lunge-windup-r2.png", 198.0, "cb177591-4ac7-4e4e-bc9f-19efd6181324", "dlj-knight-lunge-windup-r2-t1", "lunge windup (coiled, sword level at the hip); ruler from the upright helm compared at art scale"),
    "knight_hurt":         ("Knight", "ArtRaw/knight/knight-hurt-r2.png", 247.0, "6ffcd5ff-9469-4d30-bc86-fd69286a77cd", "dlj-knight-hurt-r2-t1", "hurt recoil; ruler = helm + shin average"),
    "hero_lobby_idle": ("Hero", "ArtRaw/hero/hero-lobby-idle.png", 105.0, "c4039084-4c64-4395-8aeb-355438bcb21c", "dlj-hero-lobby-idle-10100354-t1", "lobby: relaxed, sword sheathed on his back (the walk sheet is generated from it); ruler eye line to chin"),
    "knight_phase2":       ("Knight", "ArtRaw/knight/knight-phase2.png", 176.0, "8b6d7fd6-aff0-4d3f-893a-ced90bc0a2fa", "dlj-knight-phase2-10100155-t2", "phase-2 transformation: same idle, armor cracked and scorched (world art batch); ruler from body height vs. the standing idle (knight-ref draws a bigger helm than the master)"),
    "knight_slam_windup": ("Knight", "ArtRaw/knight/knight-slam-windup.png", 138.0, "54600c2a-28c0-49ef-ae3b-a2db2702425a", "dlj-knight-slam-windup-10100248-t1", "ground slam windup (asked for a reversed grip, got an overhead raise); ruler = helm x 0.8 (knight-ref draws a bigger helm than the master)"),
    "knight_slam": ("Knight", "ArtRaw/knight/knight-slam-r2.png", 196.0, "6ec1f886-5e21-4099-952c-a1e081f2483c", "dlj-knight-slam-r2-10100257-t1", "ground slam impact, sword driven into the floor (retry: the first had two swords); ruler = bowed helm x 0.8"),
    "knight_flame_windup": ("Knight", "ArtRaw/knight/knight-flame-windup.png", 192.0, "6a9445cf-add9-4b4d-ade1-ceeffb500443", "dlj-knight-flame-windup-10100249-t1", "Flame Wave windup: the cleave windup with the blade on fire (same framing, same ruler)"),
    "knight_flame_swing": ("Knight", "ArtRaw/knight/knight-flame-swing.png", 270.0, "78e8a481-d2d1-4000-8c02-25e09ed4ec9a", "dlj-knight-flame-swing-10100250-t1", "Flame Wave release: the cleave strike with the blade on fire (same framing, same ruler)"),
    # The King (2026-10-11): every pose an edit of the approved form reference (king-ref-t4.png, itself a chain of edits
    # from the knight master: no cape, then bulkier and jeweled, then chains/sword/background fixed).
    "king_idle": ("King", "ArtRaw/king/king-ref-t4.png", 100.0, "948773c4-f8c0-489a-bd8c-5223ceb075c2", "dlj-king-ref-10102350-t4", "the approved King form reference as the idle"),
    "king_cleave_windup": ("King", "ArtRaw/king/king-cleave-windup.png", 103.0, "fbd49656-9741-4e5a-88a0-b45b1e58f50a", "dlj-king-cleave-windup-10102355-t1", "Royal Cleave windup (drawn 3% big)"),
    "king_cleave_strike": ("King", "ArtRaw/king/king-cleave-strike.png", 122.0, "746e72ea-8510-474b-99d6-8f26926a4492", "dlj-king-cleave-strike-10110002-t1", "Royal Cleave impact (drawn floor shadow removed; drawn 22% big: helm, crown, sabatons)"),
    "king_cast": ("King", "ArtRaw/king/king-cast.png", 106.0, "1d0c1c35-feef-406a-9d35-a9b585ec9c05", "dlj-king-cast-10102355-t1", "Frost Lance / Volley cast, palm thrust at the hero (drawn 6% big)"),
    "king_floor_cast": ("King", "ArtRaw/king/king-floor-cast.png", 112.0, "13d8e8b5-6dd8-4370-ad8f-ba6405fd9b13", "dlj-king-floor-cast-10110002-t1", "Glacier Rise / Twin Rise / Procession: the sword planted in the floor (drawn 12% big: helm, knees, sabatons)"),
    "king_hurt": ("King", "ArtRaw/king/king-hurt.png", 110.0, "65e34644-20e9-467c-a798-acccf7cb17b3", "dlj-king-hurt-10110002-t2", "hurt / staggered: reeling, knocked off balance (drawn 10% big: knee medallions)"),
    "king_defeated": ("King", "ArtRaw/king/king-defeated.png", 114.0, "ec8cc4c2-acbc-40dd-8889-9520c707d22f", "dlj-king-defeated-10110002-t1", "defeated, kneeling (drawn 14% big: 85% of his standing height before, about 73% now)"),
    "king_phase2": ("King", "ArtRaw/king/king-phase2.png", 100.0, "2a8870c8-50f4-415d-9cee-95a26d802d71", "dlj-king-phase2-10110002-t1", "phase change: frost climbing his armor"),
    # The Unknown One (2026-10-11): every pose an edit of the approved reference final-ref-b.png (itself an edit of the
    # King's reference). Two came back with drawn floor shadows / effects, erased before the cutout (*.fixed.png).
    "final_idle": ("Final", "ArtRaw/final/final-ref-b.png", 100.0, "87a2b397-8cf2-4578-97b2-6b658d98c450", "dlj-final-ref-b-10110300-t1", "the approved Unknown One reference (the King's body taken by the god, a clawed tentacle arm) as the idle"),
    "final_cleave_windup": ("Final", "ArtRaw/final/final-cleave-windup.png", 85.0, "b6803b57-362b-4955-8aa7-0370a060be65", "dlj-final-cleave-windup-10110307-t1", "Hollow Cleave / Bind Strike / slam windup: sword overhead (drawn 15% small: helm, crown, knee, boot; the tentacle arm drifted to normal size)"),
    "final_cleave_strike": ("Final", "ArtRaw/final/final-cleave-strike.fixed.png", 122.0, "aef3f5cc-927e-45ec-8929-3a143f97dc71", "dlj-final-cleave-strike-10110307-t1", "Hollow Cleave / Bind Strike impact (drawn floor shadow removed locally; drawn 22% big: crown, knee, boot)"),
    "final_lash": ("Final", "ArtRaw/final/final-lash-t1.png", 100.0, "508ca095-961b-462c-aaf6-1aaea084ab43", "dlj-final-lash-10110307-t1", "Tendril Lash and the phase-2 volley: the tentacle arm whips forward (t1, kept by Shawn over the t2 retry; a second tentacle arm hangs at his side)"),
    "final_floor_cast": ("Final", "ArtRaw/final/final-floor-cast.fixed.png", 99.0, "bd20d22d-8db7-42bd-9be6-527c0e3fd703", "dlj-final-floor-cast-10110307-t1", "Grasping Ground / Bind / Procession / Maw: the claw sunk into the floor (drawn shadow pool and red burst erased locally)"),
    "final_hurt": ("Final", "ArtRaw/final/final-hurt.png", 122.0, "7468d27c-ee22-4042-b684-fcc585c2faec", "dlj-final-hurt-10110307-t1", "hurt / staggered: reeling, the claw thrown up (drawn 22% big: helm, crown, chest eye)"),
    "final_defeated": ("Final", "ArtRaw/final/final-defeated.png", 120.0, "1046cfce-38ec-4073-9742-c3b6d78819dd", "dlj-final-defeated-10110307-t1", "defeated, kneeling (drawn ~20% big: knee, crown, side by side)"),
    "final_leap": ("Final", "ArtRaw/final/final-leap.png", 110.0, "ccba67c6-0aa1-4e6f-be9c-74a201e69389", "dlj-final-leap-10110307-t1", "Leap Smash / Heaven Fall airborne (drawn ~10% big: shins and boots side by side; helm hidden)"),
    "final_smash": ("Final", "ArtRaw/final/final-smash.png", 121.0, "e2b7516e-3c8e-48e1-b1f0-fd1815a81a1e", "dlj-final-smash-10110307-t1", "the landing crouch (also the take-off crouch and the slams' impact; drawn 21% big: helm + crown)"),
    "final_phase2": ("Final", "ArtRaw/final/final-phase2.png", 101.0, "e2a9d7bc-2fe1-483b-bf27-504dda4e9300", "dlj-final-phase2-10110307-t1", "phase change: the god wakes, the chest eye opens, tentacles burst out, gold cracks"),
}

# Extra clean_cutout.py arguments per pose. The King's pale frozen blade has broad near-white highlights that the
# enclosed-gap step took for background (holes in the blade): only remove enclosed gaps with a big solid core.
CUT_ARGS = {name: ["--gap-core", "16"] for name in POSES if name.startswith("king_")}
CUT_ARGS["king_cleave_strike"] = ["--gap-core", "16", "--floor-shadow"]
CUT_ARGS["king_floor_cast"] = ["--gap-core", "40"]
# Some Unknown One edits came back on a paper-grain background: background showing through tentacle loops and between
# the legs is enclosed and too noisy for the default gap tolerance. Dark armor, so no pale parts to protect.
for name in ("final_cleave_windup", "final_floor_cast", "final_smash", "final_phase2", "final_leap", "final_cleave_strike", "final_defeated"):
    CUT_ARGS[name] = ["--gap-tol", "24", "--gap-core", "2"]

# Body anchor (hip) column in the cut image, where the automatic hip-band centroid would be pulled sideways: the
# Unknown One's huge claw arm hangs through the hip band. Found by matching the idle's belt at each pose's scale (or by
# eye where the belt is hidden).
ANCHOR_X = {"final_idle": 646, "final_cleave_windup": 652, "final_cleave_strike": 360, "final_lash": 649,
            "final_floor_cast": 632, "final_hurt": 883, "final_defeated": 740, "final_leap": 770, "final_smash": 605,
            "final_phase2": 780}

# Poses DreamLayer returned facing left (despite "facing right" in the prompt): mirrored on import.
MIRRORED = {"hero_dead", "knight_recovery", "knight_staggered", "knight_defeated"}


def run(args):
    out = subprocess.run([PY, "-I"] + args, cwd=ROOT, capture_output=True, text=True)
    if out.returncode != 0:
        raise RuntimeError(out.stderr)
    return json.loads(out.stdout)


def main():
    names = sys.argv[1:] or list(POSES)
    for name in names:
        char, raw, ruler, execution_id, key, note = POSES[name]
        c = CHARACTERS[char]
        cut = os.path.splitext(raw)[0] + ".cut.png"
        cut_report = run(["Tools/clean_cutout.py", raw, "--out", cut, "--no-preview"] + CUT_ARGS.get(name, []))
        if name in MIRRORED:
            from PIL import Image, ImageOps
            ImageOps.mirror(Image.open(os.path.join(ROOT, cut))).save(os.path.join(ROOT, cut))
        dest_dir = f"Assets/Art/DreamLayer/Characters/{char}"
        os.makedirs(os.path.join(ROOT, dest_dir), exist_ok=True)
        dest = f"{dest_dir}/{name}.png"
        args = ["Tools/normalize_pose.py", cut, "--out", dest,
                "--ref-scale", str(c["target_height"] / c["master_height"]),
                "--blade-ref", str(c["master_ruler"]), "--blade", str(ruler), "--measure-teal"]
        if name in ANCHOR_X:
            args += ["--anchor-x", str(ANCHOR_X[name])]
        norm = run(args)
        sidecar = {
            "asset": dest, "source": raw, "operation": "image_to_image (edit)", "execution_id": execution_id,
            "idempotency_key": key, "note": note, "mirrored": name in MIRRORED,
            "pipeline": {
                "cutout": "Tools/clean_cutout.py (local, 0 credits)",
                "normalize": "Tools/normalize_pose.py: ruler scale, hip-band anchor, feet on the bottom edge",
                "ruler_px": ruler, "teal_blade_px": norm.get("teal_blade_px"), "scale": norm["scale"], "anchor_x": norm["anchor_x"],
                "output_size": norm["output_size"], "enclosed_gaps_removed": cut_report["enclosed_gap_pixels_removed"],
            },
        }
        with open(os.path.join(ROOT, dest_dir, f"{name}.dreamlayer.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(sidecar, f, indent=2)
            f.write("\n")
        blade = norm.get("teal_blade_px")
        blade_note = f"  teal blade {blade * norm['scale']:.0f} px at art scale" if blade and char == "Hero" else ""
        print(f"{name:22s} scale {norm['scale']:.4f}  ruler {ruler}{blade_note}  -> {dest} {norm['output_size']}")


if __name__ == "__main__":
    main()
