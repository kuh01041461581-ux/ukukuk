// 지정한 주소(들)의 함수만 디컴파일. noreturn 추정을 모두 해제해 잘림을 방지. 사용: -postScript DecompOne.java 74b38 74f88
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.lang.*;
import java.io.*;
import java.math.BigInteger;

public class DecompOne extends GhidraScript {
    public void run() throws Exception {
        Program p = currentProgram;
        Register tmode = p.getLanguage().getRegister("TMode");
        p.getProgramContext().setValue(tmode, toAddr(0x1000000L), toAddr(0x10b0000L), BigInteger.ONE);
        for (long a = 0x1000000L + 0x100; a < 0x1000000L + 0x82640L; a += 2) {
            Address ad = toAddr(a);
            int hw = p.getMemory().getShort(ad) & 0xffff;
            if ((hw & 0xff00) == 0xb500 && getFunctionAt(ad) == null) { disassemble(ad); createFunction(ad, null); }
        }
        for (Function f : p.getFunctionManager().getFunctions(true)) f.setNoReturn(false);
        DecompInterface di = new DecompInterface(); di.openProgram(p);
        PrintWriter w = new PrintWriter(new FileWriter(System.getProperty("user.dir") + "/one_decomp.c"));
        for (String s : getScriptArgs()) {
            Address ad = toAddr(0x1000000L + Long.parseLong(s, 16));
            Function f = getFunctionAt(ad);
            if (f == null) { disassemble(ad); f = createFunction(ad, null); }
            if (f == null) { w.println("// no function at " + s); continue; }
            f.setNoReturn(false);
            DecompileResults r = di.decompileFunction(f, 60, monitor);
            w.println("// file=" + s); w.println(r.decompileCompleted() ? r.getDecompiledFunction().getC() : "FAILED " + r.getErrorMessage());
        }
        w.close();
    }
}
