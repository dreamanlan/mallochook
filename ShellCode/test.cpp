#include <stdio.h>
#include <stdlib.h>
#include <iomanip>

extern "C" void* dlopen(const char*, int);
extern "C" void dlclose(void*);
extern "C" void* dlsym(void*,const char*); 
extern "C" const char* dlerror(void);

void testdl(void)
{
	typedef void (*HookPtr)(const char*);
	void* handle = dlopen(
"/data/local/tmp/libMallocHook.so........................"
	, 2);
	if(handle){
		void* ptr = dlsym(handle, 
"InstallHook....................."
);
		if(ptr){
			HookPtr fptr = (HookPtr)ptr;
			fptr(
"1024............................"
);
		}else{
			dlerror();
			dlclose(handle);
		}
	}else{
		dlerror();
	}
}

int main(int argc, char** argv)
{
	testdl();
	return 0;
}