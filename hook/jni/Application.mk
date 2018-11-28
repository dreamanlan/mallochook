APP_STL := gnustl_static
APP_CPPFLAGS := -fexceptions -Wno-deprecated

APP_OPTIM := release

OPT_CFLAGS := -fno-exceptions -fno-rtti
OPT_CPPFLAGS := $(OPT_CLFAGS) -fPIC
APP_CFLAGS += $(OPT_CFLAGS)
APP_CPPFLAGS += $(OPT_CPPFLAGS)
LOCAL_CONLYFLAGS        := -std=c11
LOCAL_CPPONLYFLAGS      := -std=c++11
APP_ABI := armeabi-v7a 
APP_PLATFORM := android-19