bl_info = {
    "name": "FBX Shape Normal Patch",
    "author": "GitHub Copilot",
    "version": (1, 1, 0),
    "blender": (5, 1, 0),
    "location": "View3D > Sidebar > FBX ShapeNorm",
    "description": "Monkey patch FBX exporter to write epsilon normals for selected shape keys",
    "category": "Import-Export",
}

import json

import bpy
from bpy.props import StringProperty
from bpy.types import AddonPreferences, Operator, Panel

EPSILON = 1.1e-6
_original_fbx_data_mesh_shapes_elements = None
_fbx_bin_module = None


def _get_addon_prefs(context=None):
    ctx = context or bpy.context
    addons = ctx.preferences.addons
    addon = addons.get(__package__)
    if addon is None:
        return None
    return addon.preferences


def _load_selected_shape_keys(context=None):
    prefs = _get_addon_prefs(context)
    if prefs is None or not prefs.selected_shape_keys_by_object_json:
        return {}
    try:
        data = json.loads(prefs.selected_shape_keys_by_object_json)
    except Exception:
        return {}
    if not isinstance(data, dict):
        return {}

    out = {}
    for obj_name, shape_names in data.items():
        if not isinstance(obj_name, str) or not isinstance(shape_names, list):
            continue
        filtered = {shape_name for shape_name in shape_names if isinstance(shape_name, str)}
        if filtered:
            out[obj_name] = filtered
    return out


def _save_selected_shape_keys(selected, context=None):
    prefs = _get_addon_prefs(context)
    if prefs is None:
        return

    serializable = {
        obj_name: sorted(shape_names)
        for obj_name, shape_names in selected.items()
        if isinstance(obj_name, str) and shape_names
    }
    prefs.selected_shape_keys_by_object_json = json.dumps(serializable, ensure_ascii=True)


def _active_shape_keys(context):
    obj = context.object
    if obj is None or obj.type != 'MESH' or obj.data is None:
        return None, []
    shape_keys = getattr(obj.data, "shape_keys", None)
    if shape_keys is None:
        return obj.name, []
    key_blocks = shape_keys.key_blocks
    if len(key_blocks) <= 1:
        return obj.name, []
    return obj.name, [key.name for key in key_blocks[1:]]


def _patched_fbx_data_mesh_shapes_elements(root, me_obj, me, scene_data, fbx_me_tmpl, fbx_me_props):
    m = _fbx_bin_module
    if m is None:
        return

    if me not in scene_data.data_deformers_shape:
        return

    write_normals = True
    selected_shape_keys = _load_selected_shape_keys()
    object_name = me_obj.bdata.name
    selected_for_object = selected_shape_keys.get(object_name, set())

    _me_key, shape_key, shapes = scene_data.data_deformers_shape[me]

    channels = []

    vertices = me.vertices
    for shape, (channel_key, geom_key, shape_verts_co, shape_verts_nors, shape_verts_idx) in shapes.items():
        if shape.vertex_group and shape.vertex_group in me_obj.bdata.vertex_groups:
            shape_verts_weights = m.np.zeros(len(shape_verts_idx), dtype=m.np.float64)
            mv_shape_verts_weights = shape_verts_weights.data
            mv_shape_verts_idx = shape_verts_idx.data
            vg_idx = me_obj.bdata.vertex_groups[shape.vertex_group].index
            for sk_idx, v_idx in enumerate(mv_shape_verts_idx):
                for vg in vertices[v_idx].groups:
                    if vg.group == vg_idx:
                        mv_shape_verts_weights[sk_idx] = vg.weight
                        break
            shape_verts_weights *= 100.0
        else:
            shape_verts_weights = m.np.full(len(shape_verts_idx), 100.0, dtype=m.np.float64)
        channels.append((channel_key, shape, shape_verts_weights))

        geom = m.elem_data_single_int64(root, b"Geometry", m.get_fbx_uuid_from_key(geom_key))
        geom.add_string(m.fbx_name_class(shape.name.encode(), b"Geometry"))
        geom.add_string(b"Shape")

        tmpl = m.elem_props_template_init(scene_data.templates, b"Geometry")
        props = m.elem_properties(geom)
        m.elem_props_template_finalize(tmpl, props)

        m.elem_data_single_int32(geom, b"Version", m.FBX_GEOMETRY_SHAPE_VERSION)

        m.elem_data_single_int32_array(geom, b"Indexes", shape_verts_idx)
        m.elem_data_single_float64_array(geom, b"Vertices", shape_verts_co)
        if write_normals:
            shape_verts_nors = shape_verts_nors.copy()

            if shape.name in selected_for_object:
                shape_verts_nors.fill(EPSILON)

            requires_unity_workaround = (m.np.abs(shape_verts_nors) < EPSILON).all()
            if requires_unity_workaround:
                shape_verts_nors[0][0] = EPSILON

            m.elem_data_single_float64_array(geom, b"Normals", shape_verts_nors)

    m.fbx_data_bindpose_element(root, me_obj, me, scene_data)

    fbx_shape = m.elem_data_single_int64(root, b"Deformer", m.get_fbx_uuid_from_key(shape_key))
    fbx_shape.add_string(m.fbx_name_class(me.name.encode(), b"Deformer"))
    fbx_shape.add_string(b"BlendShape")

    m.elem_data_single_int32(fbx_shape, b"Version", m.FBX_DEFORMER_SHAPE_VERSION)

    for channel_key, shape, shape_verts_weights in channels:
        fbx_channel = m.elem_data_single_int64(root, b"Deformer", m.get_fbx_uuid_from_key(channel_key))
        fbx_channel.add_string(m.fbx_name_class(shape.name.encode(), b"SubDeformer"))
        fbx_channel.add_string(b"BlendShapeChannel")

        m.elem_data_single_int32(fbx_channel, b"Version", m.FBX_DEFORMER_SHAPECHANNEL_VERSION)
        m.elem_data_single_float64(fbx_channel, b"DeformPercent", shape.value * 100.0)
        m.elem_data_single_float64_array(fbx_channel, b"FullWeights", shape_verts_weights)

        m.elem_props_template_set(
            fbx_me_tmpl,
            fbx_me_props,
            "p_number",
            shape.name.encode(),
            shape.value * 100.0,
            animatable=True,
        )


def _install_patch():
    global _original_fbx_data_mesh_shapes_elements
    global _fbx_bin_module

    if _original_fbx_data_mesh_shapes_elements is not None:
        return

    try:
        import io_scene_fbx.export_fbx_bin as fbx_bin
    except Exception as ex:
        print("[FBX Shape Normal Patch] Failed to import io_scene_fbx.export_fbx_bin:", ex)
        return

    _fbx_bin_module = fbx_bin
    _original_fbx_data_mesh_shapes_elements = fbx_bin.fbx_data_mesh_shapes_elements
    fbx_bin.fbx_data_mesh_shapes_elements = _patched_fbx_data_mesh_shapes_elements
    print("[FBX Shape Normal Patch] FBX exporter monkey patch installed.")


def _remove_patch():
    global _original_fbx_data_mesh_shapes_elements
    global _fbx_bin_module

    if _fbx_bin_module is None or _original_fbx_data_mesh_shapes_elements is None:
        return

    _fbx_bin_module.fbx_data_mesh_shapes_elements = _original_fbx_data_mesh_shapes_elements
    _original_fbx_data_mesh_shapes_elements = None
    _fbx_bin_module = None
    print("[FBX Shape Normal Patch] FBX exporter monkey patch removed.")


class FBXSNORM_AddonPreferences(AddonPreferences):
    bl_idname = __package__

    selected_shape_keys_by_object_json: StringProperty(
        name="Selected Shape Keys By Object",
        default="{}",
    )

    def draw(self, _context):
        layout = self.layout
        layout.label(text="Shape key selection is controlled in the 3D View sidebar panel.")


class FBXSNORM_OT_toggle_shape_key(Operator):
    bl_idname = "fbx_snorm.toggle_shape_key"
    bl_label = "Toggle Shape Key"
    bl_description = "Toggle this shape key for epsilon normal export"
    bl_options = {'INTERNAL'}

    shape_name: StringProperty()

    def execute(self, context):
        obj_name, _names = _active_shape_keys(context)
        if obj_name is None:
            return {'CANCELLED'}

        selected = _load_selected_shape_keys(context)
        per_object = selected.setdefault(obj_name, set())
        if self.shape_name in per_object:
            per_object.remove(self.shape_name)
        else:
            per_object.add(self.shape_name)

        if not per_object:
            selected.pop(obj_name, None)

        _save_selected_shape_keys(selected, context)
        return {'FINISHED'}


class FBXSNORM_OT_select_all(Operator):
    bl_idname = "fbx_snorm.select_all"
    bl_label = "All"
    bl_description = "Enable all shape keys on the active mesh"

    def execute(self, context):
        obj_name, names = _active_shape_keys(context)
        if obj_name is None:
            return {'CANCELLED'}

        selected = _load_selected_shape_keys(context)
        selected[obj_name] = set(names)
        _save_selected_shape_keys(selected, context)
        return {'FINISHED'}


class FBXSNORM_OT_clear_all(Operator):
    bl_idname = "fbx_snorm.clear_all"
    bl_label = "None"
    bl_description = "Disable all shape keys on the active mesh"

    def execute(self, context):
        obj_name, names = _active_shape_keys(context)
        if obj_name is None:
            return {'CANCELLED'}

        selected = _load_selected_shape_keys(context)
        per_object = selected.setdefault(obj_name, set())
        for name in names:
            per_object.discard(name)
        if not per_object:
            selected.pop(obj_name, None)
        _save_selected_shape_keys(selected, context)
        return {'FINISHED'}


class FBXSNORM_PT_panel(Panel):
    bl_label = "FBX Shape Normal Patch"
    bl_idname = "FBXSNORM_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "FBX ShapeNorm"

    def draw(self, context):
        layout = self.layout
        obj_name, names = _active_shape_keys(context)

        if not names:
            layout.label(text="Select a mesh with relative shape keys.")
            return

        row = layout.row(align=True)
        row.operator("fbx_snorm.select_all", text="All", icon='CHECKBOX_HLT')
        row.operator("fbx_snorm.clear_all", text="None", icon='CHECKBOX_DEHLT')

        selected = _load_selected_shape_keys(context)
        selected_for_object = selected.get(obj_name, set())
        col = layout.column(align=True)
        for name in names:
            checked = name in selected_for_object
            icon = 'CHECKBOX_HLT' if checked else 'CHECKBOX_DEHLT'
            op = col.operator("fbx_snorm.toggle_shape_key", text=name, icon=icon, emboss=True)
            op.shape_name = name

        layout.separator()
        layout.label(text=f"Object: {obj_name}")
        layout.label(text=f"Enabled: {len([n for n in names if n in selected_for_object])}/{len(names)}")


classes = (
    FBXSNORM_AddonPreferences,
    FBXSNORM_OT_toggle_shape_key,
    FBXSNORM_OT_select_all,
    FBXSNORM_OT_clear_all,
    FBXSNORM_PT_panel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    _install_patch()


def unregister():
    _remove_patch()
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
