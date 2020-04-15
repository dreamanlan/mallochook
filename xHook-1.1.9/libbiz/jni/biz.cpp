#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <unwind.h>
#include <jni.h>
#include <dlfcn.h>
#include <pthread.h>
//#include <jni.h>
#include <android/log.h>
#include "xhook.h"
#include "Hashtable.h"

namespace
{
	struct BacktraceState
	{
		void** current;
		void** end;
	};

	static _Unwind_Reason_Code unwind_callback(struct _Unwind_Context* context, void* arg)
	{
		BacktraceState* state = static_cast<BacktraceState*>(arg);
		uintptr_t pc = _Unwind_GetIP(context);
		if (pc) {
			if (state->current == state->end) {
				return _URC_END_OF_STACK;
			}
			else {
				*state->current++ = reinterpret_cast<void*>(pc);
			}
		}
		return _URC_NO_REASON;
	}

	static size_t capture_backtrace(void** buffer, size_t max)
	{
		BacktraceState state = { buffer, buffer + max };
		_Unwind_Backtrace(unwind_callback, &state);

		return state.current - buffer;
	}

	static void dump_backtrace(const char* title, const char* prefix, void** buffer, size_t count)
	{
		for (size_t idx = 0; idx < count; ++idx) {
			const void* addr = buffer[idx];
			const void* raddr = 0;
			const char* symbol = "";
			const char* file = "";

			Dl_info info;
			if (dladdr(addr, &info)) {
				raddr = (const void*)((const char*)addr - (const char*)info.dli_fbase);
				if(info.dli_sname)
				    symbol = info.dli_sname;
				if(info.dli_fname)
				    file = info.dli_fname;
			}

			__android_log_print(ANDROID_LOG_INFO, title, "%s #%d:%p %p %s|%s", prefix, idx, addr, raddr, symbol, file);
		}
	}

	static void backtrace_tologcat(const char* title, const char* prefix)
	{
		const size_t max = 64;
		void* buffer[max];
		dump_backtrace(title, prefix, buffer, capture_backtrace(buffer, max));
	}
}

static void *my_malloc(size_t);
static void* (*old_malloc_ptr)(size_t);
static void my_free(void*);
static void (*old_free_ptr)(void*);
static uint32_t min_log_size = 1024*1024;
static bool g_CanLog = false;
static bool g_Refresh = true;
static pthread_mutexattr_t g_MutexAttr;
static pthread_mutex_t g_Mutex;
static pthread_mutex_t g_ReEntryMutex;

typedef HashtableT<uint32_t, uint32_t, 1024> ThreadHashtable;
typedef HashsetT<uint64_t, 1024*1024> AllocHashtable;
static ThreadHashtable g_ReEntryHash;
static AllocHashtable g_AllocHash;

static int hook()
{
	int r1 = xhook_register("^/data/.*\\.so$", "malloc", (void*)my_malloc, (void**)&old_malloc_ptr);
	if(r1){
		__android_log_print(ANDROID_LOG_INFO, "inject", "register malloc hook failed:%d !", r1);
	}
	int r2 = xhook_register("^/data/.*\\.so$", "free", (void*)my_free, (void**)&old_free_ptr);
	if(r2){
		__android_log_print(ANDROID_LOG_INFO, "inject", "register free hook failed:%d !", r2);
	}
	int r3 = xhook_refresh(1);
	if(r3){
		__android_log_print(ANDROID_LOG_INFO, "inject", "malloc/free hook failed:%d !", r3);
	}
	if(!r1 && !r2 && !r3){
		__android_log_print(ANDROID_LOG_INFO, "inject", "malloc/free hook finish.");
	}
	return 0;
}

static bool BeginLogger(void)
{
	bool ret = false;
	pthread_mutex_lock(&g_ReEntryMutex);
	pthread_t tid = pthread_self();
	uint32_t v = g_ReEntryHash.Get(tid);
	if(!v){
		g_ReEntryHash.Add(tid, 1);
		ret = true;
	}
	pthread_mutex_unlock(&g_ReEntryMutex);
	return ret;
}

static void EndLogger(void) 
{
	pthread_mutex_lock(&g_ReEntryMutex);
	pthread_t tid = pthread_self();
	g_ReEntryHash.Remove(tid);
	pthread_mutex_unlock(&g_ReEntryMutex);
}

static void* my_malloc(size_t size)
{	
	void* p = (*old_malloc_ptr)(size);
	if(g_Refresh){
		if(!xhook_refresh(1)){
			g_Refresh = false;
		}
	}
	if(g_CanLog){
		if(BeginLogger()){
			pthread_mutex_lock(&g_Mutex);
			if (size >= min_log_size){
				g_AllocHash.Insert((uint64_t)p);
				
				char prefix[256];
				snprintf(prefix, 255, "mymalloc[addr:%p size:%u]", p, size);
				prefix[255] = 0;
				//__android_log_print(ANDROID_LOG_INFO, "mymalloc", "%s", prefix);
				backtrace_tologcat("mymalloc", prefix);
			}
			pthread_mutex_unlock(&g_Mutex);
			EndLogger();
		}
	}
	return p;
}

static void my_free(void* ptr)
{
	if(g_CanLog){
		if(BeginLogger()){
			pthread_mutex_lock(&g_Mutex);
			bool exist = g_AllocHash.Exist((uint64_t)ptr);
			g_AllocHash.Remove((uint64_t)ptr);
			if(exist){
				__android_log_print(ANDROID_LOG_INFO, "mymalloc", "myfree[addr:%p]", ptr);
			}
			pthread_mutex_unlock(&g_Mutex);
			EndLogger();
		}
	}
	(*old_free_ptr)(ptr);
}

extern "C" {
	int InstallHook(const char* p)
	{
		g_CanLog = false;
		if(p && p[0]){
			min_log_size = (uint32_t)atoi(p);
		}
		__android_log_print(ANDROID_LOG_INFO, "inject", "InstallHook\n");
		printf("InstallHook\n");
		pthread_mutexattr_init(&g_MutexAttr);
        pthread_mutexattr_settype(&g_MutexAttr, PTHREAD_MUTEX_RECURSIVE);
		pthread_mutex_init(&g_Mutex, &g_MutexAttr);
		pthread_mutex_init(&g_ReEntryMutex, &g_MutexAttr);
		hook();
		__android_log_print(ANDROID_LOG_INFO, "inject", "InstallHook finish.\n");
		printf("InstallHook finish.\n");
		g_CanLog = true;
		return 0;
	}
}

JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM *vm, void *reserved) {
    __android_log_print(ANDROID_LOG_INFO, "inject", "JNI_OnLoad enter");

    JNIEnv *env = NULL;
    if (vm->GetEnv((void **) &env, JNI_VERSION_1_6) == JNI_OK) {
        __android_log_print(ANDROID_LOG_INFO, "inject", "GetEnv OK");

        InstallHook("256");

        __android_log_print(ANDROID_LOG_INFO, "inject", "JNI_OnLoad leave");
        return JNI_VERSION_1_6;
    }
}
