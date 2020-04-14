#include <unistd.h>
#include <stdio.h>
#include <stdlib.h>

#include "inject.h"

int main( int argc, char **argv ) {
#ifndef DEBUG
    libinject_log(NULL);
#endif
    
	pid_t target_pid;
	const char* path = "libMallocHook.so";
	for(int i=1;i<argc;++i){
		const char* p = argv[i];
		if(0==strcmp(p,"-pid") && i+1<argc){
			++i;
			target_pid = atoi(argv[i]);
		}else if(0==strcmp(p,"-name") && i+1<argc){
			++i;
			target_pid = libinject_find_pid_of(argv[i]);
		}else if(0==strcmp(p,"-so") && i+1<argc){
			++i;
			path = argv[i];
		}else if(p[0]!='-'){
			target_pid = atoi(p);
		}else{
			puts("[Usage](def so=libMallocHook.so)");
			puts("\tinjectnew process_id");
			puts("\tinjectnew -pid process_id");
			puts("\tinjectnew -name com.xxx.xxx");
			puts("\tinjectnew -pid process_id -so xxx.so");
			puts("\tinjectnew -name com.xxx.xxx -so xxx.so");
			return -1;
		}
	}
	printf("pid:%d so:%s\n", target_pid, path);
	int ret = libinject_inject(target_pid, path);
	printf("inject result:%d\n",ret);
}
