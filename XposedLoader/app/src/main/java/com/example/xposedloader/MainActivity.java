package com.example.xposedloader;

import androidx.annotation.RequiresApi;
import androidx.appcompat.app.AppCompatActivity;

import android.os.Build;
import android.os.Bundle;
import android.content.pm.PackageManager;
import android.os.FileUtils;
import android.util.Log;

import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.Charset;
import java.util.Enumeration;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

public class MainActivity extends AppCompatActivity {

    @RequiresApi(api = Build.VERSION_CODES.Q)
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        android.content.pm.ApplicationInfo ai = null;
        try {
            ai = getPackageManager().getApplicationInfo(getPackageName(), 0);
            ZipFile zf = new ZipFile(ai.sourceDir, Charset.defaultCharset());
            Enumeration<? extends ZipEntry> entries = zf.entries();
            while(entries.hasMoreElements()){
                ZipEntry ent = entries.nextElement();
                Log.i("xposedloader", "zip entry: "+ent.getName());
            }
            Log.i("xposedloader", "files dir: "+getFilesDir());
            Log.i("xposedloader", "data dir: "+getDataDir());
            ZipEntry entry = zf.getEntry("lib/armeabi-v7a/libMallocHook.so");
            InputStream in = zf.getInputStream(entry);
            OutputStream out = new FileOutputStream(getFilesDir()+"/libMallocHook.so");
            FileUtils.copy(in,out);
            in.close();
            out.flush();
            out.close();
        } catch (PackageManager.NameNotFoundException e) {
            e.printStackTrace();
        } catch (IOException e) {
            e.printStackTrace();
        }
        //System.loadLibrary("MallocHook");
    }
}
