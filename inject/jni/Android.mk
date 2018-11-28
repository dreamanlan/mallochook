LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)
LOCAL_SRC_FILES := inject.c
LOCAL_SRC_FILES += shellcode.s
LOCAL_C_INCLUDES := /$(JNI_H_INCLUDE)
LOCAL_SHARED_LIBRARIES := libutils
LOCAL_PRELINK_MODULE := false
LOCAL_LDLIBS := -L . -ldl -llog 
LOCAL_MODULE := inject
include $(BUILD_EXECUTABLE)