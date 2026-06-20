#if defined(_WIN32)
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API
#endif

#include "fbxplugin.h"
#include "ufbx/ufbx.h"


EXPORT_API int DisableFBXShapeNormal(char *path, unsigned int pathLen,
                                     ShapeName *shapeNames,
                                     unsigned int shapeCount) {
  if (path == NULL || pathLen == 0) {
    return -1;
  }
  if (shapeNames == NULL || shapeCount == 0) {
    return -2;
  }

  ufbx_error error = {0};
  ufbx_load_opts opts = {0};
  ufbx_scene *scene = ufbx_load_file(path, &opts, &error);
  if (scene == NULL || error.type != UFBX_ERROR_NONE) {
    return -3;
  }

  ufbx_

  ufbx_quat q1 = {0};
  ufbx_quat_dot(q1, q1);

  return 0;
}