package com.skt.m;
import com.seoultycoon.Sound;
public class AudioSystem {
    public static int getMaxVolume(String t) { return 10; }
    public static int getVolume(String t) { return Sound.volume; }
    public static void setVolume(String t, int v) { Sound.volume = v; }
    public static AudioClip getAudioClip(String t) { return new Sound(); }
}
