package com.xce.lcdui;
/** 피처폰 IME 대신: 이름 입력이 필요하면 안드로이드 입력창을 띄워 글자를 채워 넣는다. */
public class TextComponentHandler {
    public interface Prompter { void prompt(TextComponent tc); }
    public static volatile Prompter prompter;
    static final TextComponentHandler h = new TextComponentHandler();
    TextComponent tc;
    public static TextComponentHandler getTextComponentHandler() { return h; }
    public void setTextComponent(TextComponent t) { tc = t; if (t != null && prompter != null) prompter.prompt(t); }
    public void clear() {}
    public boolean keyPressed(int k) {
        if (tc != null && k == 8) tc.delete();
        else if (tc != null && (k >= 32 && k < 127)) tc.insert((char) k);
        return true;
    }
    public boolean keyReleased(int k) { return true; }
}
