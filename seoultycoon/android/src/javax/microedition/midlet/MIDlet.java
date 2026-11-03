package javax.microedition.midlet;
public abstract class MIDlet {
    protected abstract void startApp() throws Exception;
    protected abstract void pauseApp();
    protected abstract void destroyApp(boolean b) throws Exception;
}
