package com.skt.m;
public class AudioSystem {
    static int vol = 5;
    public static int getMaxVolume(String t) { return 10; }
    public static int getVolume(String t) { return vol; }
    public static void setVolume(String t, int v) { vol = v; }
    public static AudioClip getAudioClip(String t) {
        return new AudioClip() { public void open(byte[] b, int o, int l) {} public void play() {} public void loop() {} public void stop() {} public void close() {} };
    }
}
