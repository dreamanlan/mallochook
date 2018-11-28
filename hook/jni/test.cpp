#include <stdio.h>
#include <stdlib.h>
#include <iomanip>
#include <unwind.h>
#include <dlfcn.h>
#include <android/log.h>
#include <pthread.h>
#include "inlineHook.h"

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
			const char* symbol = "";

			Dl_info info;
			if (dladdr(addr, &info) && info.dli_sname) {
				symbol = info.dli_sname;
			}

			__android_log_print(ANDROID_LOG_INFO, title, "%s #%d:%p %s", prefix, idx, addr, symbol);
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
static uint32_t g_CanLog = 0;

static int hook()
{
	int r = 0;
	if (registerInlineHook((uint32_t)malloc, (uint32_t)my_malloc, (uint32_t **)&old_malloc_ptr) != ELE7EN_OK) {
		__android_log_print(ANDROID_LOG_INFO, "mymalloc", "register malloc hook failed!");
		r = -1;
	}
	if (inlineHook((uint32_t)malloc) != ELE7EN_OK) {
		__android_log_print(ANDROID_LOG_INFO, "mymalloc", "malloc hook failed!");
		r = -1;
	}
	if (registerInlineHook((uint32_t)free, (uint32_t)my_free, (uint32_t **)&old_free_ptr) != ELE7EN_OK) {
		__android_log_print(ANDROID_LOG_INFO, "mymalloc", "register free hook failed!");
		r = -1;
	}
	if (inlineHook((uint32_t)free) != ELE7EN_OK) {
		__android_log_print(ANDROID_LOG_INFO, "mymalloc", "free hook failed!");
		r = -1;
	}
	__android_log_print(ANDROID_LOG_INFO, "mymalloc", "malloc/free hook finish.");
	return 0;
}

static int unHook()
{
	int r = 0;
	if (inlineUnHook((uint32_t)malloc) != ELE7EN_OK) {
		r = -1;
	}
	if (inlineUnHook((uint32_t)free) != ELE7EN_OK) {
		r = -1;
	}
	return r;
}

static void* my_malloc(size_t size)
{
	void* p = (*old_malloc_ptr)(size);
	if(g_CanLog && size>=min_log_size){
		char prefix[256];
		snprintf(prefix, 255, "mymalloc[addr:%p size:%u]", p, size);
		prefix[255] = 0;
		//__android_log_print(ANDROID_LOG_INFO, "mymalloc", "%s", prefix);
		backtrace_tologcat("mymalloc", prefix);
	}
	return p;
}

static void my_free(void* ptr)
{
	if(g_CanLog){
		__android_log_print(ANDROID_LOG_INFO, "mymalloc", "myfree[addr:%p]", ptr);
	}
	(*old_free_ptr)(ptr);
}

static int local_install_hook(const char* p)
{
	g_CanLog = 0;
	if(p && p[0]){
		min_log_size = (uint32_t)atoi(p);
	}
	printf("InstallHook\n");
	hook();
	printf("InstallHook finish.\n");
	g_CanLog = 1;
	return 0;
}

static int local_uninstall_hook(void)
{
	g_CanLog = 0;
	printf("UninstallHook\n");
	unHook();
	printf("UninstallHook finish.\n");
	return 0;
}

void testnew(void)
{
	char* p = new char[10240];
	p[0]='a';
	p[1]=0;
	printf("%s\n",p);
	delete[] p;
}

void testmalloc(void)
{
	void* p = malloc(20480);
	char* pp=(char*)p;
	pp[0]='b';
	pp[1]=0;
	printf("%s\n",pp);
	free(p);
}

void testdl(void)
{
	typedef void (*TestPtr)(void);
	void* handle = dlopen("/data/local/tmp/libtest2.so", RTLD_GLOBAL);
	if(handle){
		void* ptr = dlsym(handle, "test");
		if(ptr){
			TestPtr fptr = (TestPtr)ptr;
			fptr();
		}else{
			printf("test dlsym error:%s\n", dlerror());
			dlclose(handle);
		}
	}else{
		printf("test dlopen error:%s\n", dlerror());
	}
}

void* test(void* p)
{
		for(int i=0;i<1024;++i){
			testnew();
			testmalloc();
		}
		return p;
}

int main(int argc, char** argv)
{
	const int c_thread_num = 255;
	if(argc<=1){
		testdl();
		pthread_t tid[c_thread_num];
		for(int i=0;i<c_thread_num;++i){
			pthread_create(&tid[i], 0, test, 0);
		}
		for(int i=0;i<c_thread_num;++i){
			pthread_join(tid[i], 0);
		}
	}else if(argc==2){
		int num = atoi(argv[1]);
		if(num<100000)
			num=100000;
		for(int i=0;i<num;++i){
			testnew();
			testmalloc();
		}
	}else{
		int num = atoi(argv[1]);
		local_install_hook(argv[2]);
		for(int i=0;i<num;++i){
			testnew();
			testmalloc();
		}
		local_uninstall_hook();
	}
	return 0;
}