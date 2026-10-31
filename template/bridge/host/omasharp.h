#pragma once

#define OMASHARP_ABI 4

extern "C" {

struct omasharp_host {
    void (*notify)(const char *name, const char *json);
    void (*post)(long long token);
    void (*raised)(const char *name, const char *json);
    const char *args;
    void (*draw)(const char *name, const unsigned char *data, int len);
    double mono_advance;
    void (*resolve)(long long id, const char *text, int is_error);
};

struct omasharp_api {
    int version;
    char *(*app_info)();
    char *(*start)(const omasharp_host *host);
    char *(*get)(const char *name, int *is_error);
    char *(*set)(const char *name, const char *json, int *is_error);
    char *(*call)(const char *name, const char *args, int *is_error);
    void (*free)(char *p);
    void (*run_posted)(long long token);
    void (*shutdown)();
};

typedef const omasharp_api *(*omasharp_api_fn)();
}
