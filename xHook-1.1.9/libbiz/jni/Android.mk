LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)
LOCAL_MODULE            := MallocHook
LOCAL_C_INCLUDES				:= $(LOCAL_PATH)/../../../Hashtable $(LOCAL_PATH)/../../libxhook/jni
LOCAL_SRC_FILES         := biz.cpp \
	$(LOCAL_PATH)/../../../Hashtable/Hashtable.cpp	\
	$(LOCAL_PATH)/../../libxhook/jni/xhook.c \
  $(LOCAL_PATH)/../../libxhook/jni/xh_core.c \
  $(LOCAL_PATH)/../../libxhook/jni/xh_elf.c \
  $(LOCAL_PATH)/../../libxhook/jni/xh_jni.c \
  $(LOCAL_PATH)/../../libxhook/jni/xh_log.c \
  $(LOCAL_PATH)/../../libxhook/jni/xh_util.c \
  $(LOCAL_PATH)/../../libxhook/jni/xh_version.c
  
LOCAL_CFLAGS            := -Wall -Wextra
LOCAL_CONLYFLAGS        := -std=c11
LOCAL_CPPONLYFLAGS        := -std=c++11
LOCAL_LDLIBS            := -llog -ldl
include $(BUILD_SHARED_LIBRARY)
