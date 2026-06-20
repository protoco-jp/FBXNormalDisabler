#if defined(_WIN32)
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API
#endif

typedef struct {
  char name[64];
} ShapeName;

EXPORT_API int DisableFBXShapeNormal(
    char *path,
    unsigned int pathLen,
    ShapeName *shapeNames,
    unsigned int shapeCount
);