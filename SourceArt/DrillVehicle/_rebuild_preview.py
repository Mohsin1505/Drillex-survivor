"""Dev helper: rebuild the vehicle and render the review sheet."""
import runpy
import bpy

D = r"D:\Drillex\SourceArt\DrillVehicle"


def run(res=512, views=None):
    ns = runpy.run_path(D + r"\build_drill_vehicle.py", run_name="drl")
    ns["build"]()
    pv = runpy.run_path(D + r"\preview_render.py", run_name="pv")
    paths = pv["render"](D + r"\_preview", res, views)
    tris = 0
    dg = bpy.context.evaluated_depsgraph_get()
    for o in bpy.data.objects:
        if o.type == 'MESH':
            m = o.evaluated_get(dg).to_mesh()
            m.calc_loop_triangles()
            tris += len(m.loop_triangles)
            o.evaluated_get(dg).to_mesh_clear()
    return {"paths": paths, "tris": tris}
