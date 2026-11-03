package com.xce.io;
import java.io.*;
public class XFile {
    static java.io.File dir = new java.io.File(System.getProperty("xfile.dir", "/tmp/xfile"));
    RandomAccessFile f;
    public XFile(String n, int mode) throws IOException { dir.mkdirs(); f = new RandomAccessFile(new File(dir, n), mode == 1 ? "r" : "rw"); if (mode == 2) f.setLength(0); }
    public static boolean exists(String n) { return new File(dir, n).exists(); }
    public static int filesize(String n) { return (int) new File(dir, n).length(); }
    public int read(byte[] b, int o, int l) throws IOException { return f.read(b, o, l); }
    public int write(byte[] b, int o, int l) throws IOException { f.write(b, o, l); return l; }
    public void close() throws IOException { f.close(); }
}
