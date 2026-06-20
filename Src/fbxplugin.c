#if defined(_WIN32)
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API
#endif

#include "ufbx/ufbx.h"

EXPORT_API int AddTwoNumbers(int a, int b) {
    ufbx_quat q1 = {0};
    ufbx_quat_dot(q1,q1);
    return a + b;
}   