package com.skt.m;
public class Device {
    public static void setKeyToneEnabled(boolean b) {}
    public static void setBacklightEnabled(boolean b) {}
    public static void setColorMode(int m) {}
    public static void enableRestoreLCD(boolean b) {}
    public static void invokeWapBrowser(String u) { System.out.println("[wap] " + u); }
}
