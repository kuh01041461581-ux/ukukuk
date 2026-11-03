package com.xce.lcdui;
public class XDisplay { public static volatile int refreshCount; public static void refresh(int a, int b, int c, int d) { refreshCount++; } }
