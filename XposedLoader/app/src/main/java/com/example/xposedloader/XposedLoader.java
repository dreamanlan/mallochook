package com.example.xposedloader;

import android.util.Log;

import java.util.ArrayList;
import java.util.Arrays;

import de.robv.android.xposed.IXposedHookLoadPackage;
import de.robv.android.xposed.XC_MethodHook;
import de.robv.android.xposed.XposedBridge;
import de.robv.android.xposed.XposedHelpers;
import de.robv.android.xposed.callbacks.XC_LoadPackage;

import static de.robv.android.xposed.XposedHelpers.findAndHookConstructor;
import static de.robv.android.xposed.XposedHelpers.findAndHookMethod;


public class XposedLoader implements IXposedHookLoadPackage {
    public static final String TAG = "XposedLoader";
    public static final String[] PackageNames = new String[]{"com.example.xposedloader", "com.tencent.qs"};
    //public static final String HOOK_SO = "/data/data/com.example.xposedloader/files/libMallocHook.so";
    public static final String HOOK_SO = "/data/data/com.tencent.qs/lib/libMallocHook.so";

    private static boolean s_Hooked = false;

    @Override
    public void handleLoadPackage(XC_LoadPackage.LoadPackageParam param) throws Throwable {

        Log.e(TAG, param.packageName);
        boolean find = false;
        for (String s : PackageNames) {
            if (s.equals(param.packageName)) {
                find = true;
                break;
            }
        }
        if (!find)
            return;
        Log.e(TAG, "found package: " + param.packageName);
        s_Hooked = false;
        //findAndHookMethod(Runtime.class, "nativeLoad", String.class, ClassLoader.class,
        //findAndHookMethod(System.class, "loadLibrary", String.class,
        final ClassLoader hookCl = param.classLoader;
        findAndHookMethod("com.unity3d.player.UnityPlayer", hookCl, "start",
                new XC_MethodHook() {
                    @Override
                    protected void afterHookedMethod(MethodHookParam param) throws Throwable {
                        if (s_Hooked)
                            return;
                        try {
                            if (android.os.Build.VERSION.SDK_INT <= 19) {
                                Log.e(TAG, "load for android <= 4.4");
                                s_Hooked = true;
                                System.load(HOOK_SO);
                            } else {
                                Log.e(TAG, "load for android > 4.4");
                                s_Hooked = true;
                                XposedHelpers.callStaticMethod(Runtime.class, "nativeLoad", HOOK_SO, hookCl);
                                //System.loadLibrary("MallocHook");
                            }
                        } catch (Exception ex) {
                            Log.e(TAG, "exception:" + ex.getMessage());
                        }
                        Log.e(TAG, "java layer done.");
                    }
                });

    }
}