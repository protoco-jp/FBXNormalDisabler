#if defined(_WIN32)
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API
#endif

#include "ufbx/ufbx.h"

EXPORT_API int AddTwoNumbers(char *path, unsigned int len) {
    if(path == NULL || len == 0){
        return -1;
    }
    ufbx_quat q1 = {0};
    ufbx_quat_dot(q1,q1);
    return len;
}   