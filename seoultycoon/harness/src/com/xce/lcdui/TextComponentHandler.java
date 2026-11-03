package com.xce.lcdui;
public class TextComponentHandler {
    static TextComponentHandler h = new TextComponentHandler();
    public static TextComponentHandler getTextComponentHandler() { return h; }
    public void setTextComponent(TextComponent t) {}
    public void clear() {}
    public boolean keyPressed(int k) { return false; }
    public boolean keyReleased(int k) { return false; }
}
