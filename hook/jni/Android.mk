LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)
LOCAL_C_INCLUDES := $(LOCAL_PATH)/../../Hashtable
LOCAL_SRC_FILES := \
	main.cpp	\
	$(LOCAL_PATH)/../../../Hashtable/Hashtable.cpp	\
	inlineHook.c	\
	relocate.c	
LOCAL_LDLIBS := -L . -ldl -llog
LOCAL_MODULE := libMallocHook
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_SRC_FILES := \
	test2.cpp
LOCAL_LDLIBS := -L . -ldl -llog 
LOCAL_MODULE := libtest2
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_SRC_FILES := \
	test.cpp	\
	inlineHook.c	\
	relocate.c
LOCAL_LDLIBS := -L . -ldl -llog 
LOCAL_MODULE := test_so
include $(BUILD_EXECUTABLE)