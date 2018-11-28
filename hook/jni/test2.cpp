#include <stdio.h>
#include <stdlib.h>
#include <iomanip>
#include <unwind.h>
#include <dlfcn.h>
#include <android/log.h>

void testnew(void)
{
	printf("test2 testnew\n");
	char* p = new char[1024];
	p[0]='a';
	p[1]=0;
	printf("%s\n",p);
	delete[] p;
}

void testmalloc(void)
{
	printf("test2 testmalloc\n");
	void* p = malloc(2048);
	char* pp=(char*)p;
	pp[0]='b';
	pp[1]=0;
	printf("%s\n",pp);
	free(p);
}

extern "C" {	
	void dummy(void)
	{
		typedef void (*HookPtr)(const char*);
		typedef void (*UnhookPtr)(void);
		void* handle = dlopen("/data/local/tmp/libMallocHook.so", RTLD_GLOBAL);
		if(handle){
			void* ptr = dlsym(handle, "InstallHook");
			if(ptr){
				HookPtr fptr = (HookPtr)ptr;
				fptr("1024");
			}else{
				printf("test2 dlsym error:%s\n", dlerror());
				dlclose(handle);
			}
		}else{
			printf("test2 dlopen error:%s\n", dlerror());
		}
	}	
	void test(void)
	{
		testnew();
		testmalloc();
	}
}