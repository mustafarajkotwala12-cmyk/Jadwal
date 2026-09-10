#include <stdio.h>
#include <dlfcn.h>
#include <limits.h>
#include <mach-o/dyld.h>
#include <libgen.h>
#include <stdlib.h>
#include <string.h>

typedef int (*hostfxr_main_fn)(const int argc, const char** argv);

int main(int argc, const char* argv[]) {
    char path[PATH_MAX];
    uint32_t size = sizeof(path);
    if (_NSGetExecutablePath(path, &size) != 0) {
        fprintf(stderr, "Error: Could not determine executable path.\n");
        return 1;
    }

    char* dir = dirname(path);

    char fxr_path[PATH_MAX];
    snprintf(fxr_path, sizeof(fxr_path), "%s/libhostfxr.dylib", dir);

    void* lib = dlopen(fxr_path, RTLD_NOW);
    if (!lib) {
        fprintf(stderr, "Error loading %s: %s\n", fxr_path, dlerror());
        return 1;
    }

    hostfxr_main_fn hostfxr_main = (hostfxr_main_fn)dlsym(lib, "hostfxr_main");
    if (!hostfxr_main) {
        fprintf(stderr, "Error locating hostfxr_main: %s\n", dlerror());
        return 1;
    }

    char dll_path[PATH_MAX];
    snprintf(dll_path, sizeof(dll_path), "%s/Jadwal.App.dll", dir);

    const char** new_argv = malloc(sizeof(char*) * (argc + 3));
    if (!new_argv) return 1;

    new_argv[0] = "exec";
    new_argv[1] = dll_path;
    for (int i = 1; i < argc; i++) {
        new_argv[i + 1] = argv[i];
    }
    new_argv[argc + 1] = NULL;

    int rc = hostfxr_main(argc + 1, new_argv);
    free(new_argv);
    return rc;
}
