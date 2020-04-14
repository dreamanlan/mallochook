LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_SRC_FILES:= \
    inject.cpp \
    inject_main.cpp

LOCAL_MODULE := injectnew
LOCAL_CFLAGS += -DDEBUG
LOCAL_LDLIBS := -ldl -llog

include $(BUILD_EXECUTABLE)
