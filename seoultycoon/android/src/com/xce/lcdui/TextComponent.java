package com.xce.lcdui;
public interface TextComponent {
    int getCaretPosition(); int getConstraints(); int getMaxSize(); int size();
    void insert(char c); void delete(); void replace(char c); void clear();
    void setCaretPosition(int p); void moveCursor(int d); void setCaretVisible(boolean v);
    void repaint(); void repaintIM();
}
