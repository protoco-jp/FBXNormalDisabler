#if defined(_WIN32)
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API
#endif

EXPORT_API int AddTwoNumbers(char *path, unsigned int len);