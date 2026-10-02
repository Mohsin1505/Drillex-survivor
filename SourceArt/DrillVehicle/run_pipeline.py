"""Full rebuild: model -> UV/materials/bake -> LOD/export. Runs on a Blender timer and
writes progress to _pipeline_status.json so long bakes don't block the caller."""
import bpy
import json
import runpy
import time
import traceback

D = r"D:\Drillex\SourceArt\DrillVehicle"
STATUS = D + r"\_pipeline_status.json"


def _status(**kw):
    with open(STATUS, "w") as f:
        json.dump(kw, f, indent=1)


def _run():
    t = time.time()
    try:
        _status(stage="build")
        runpy.run_path(D + r"\build_drill_vehicle.py", run_name="drl")["build"]()
        _status(stage="bake")
        bake = runpy.run_path(D + r"\texture_bake.py", run_name="tb")["run"](do_bake=True)
        _status(stage="export", bake=bake)
        exp = runpy.run_path(D + r"\export_fbx.py", run_name="ex")["run"]()
        _status(stage="done", bake=bake, export=exp, seconds=round(time.time() - t))
    except Exception:
        _status(stage="error", error=traceback.format_exc())
    return None


def start():
    _status(stage="queued")
    bpy.app.timers.register(_run, first_interval=0.5)
