// Ghidra headless 스크립트: Thumb 함수 시작(push {..,lr})을 찾아 함수로 만들고 전부 디컴파일해 C 로 저장
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.lang.*;
import ghidra.program.model.mem.*;
import java.io.*;
import java.math.BigInteger;

public class DecompAll extends GhidraScript {
    public void run() throws Exception {
        Program p = currentProgram;
        Address base = toAddr(0x1000000L);
        Address end = toAddr(0x1000000L + 0x82700L);
        Register tmode = p.getLanguage().getRegister("TMode");
        p.getProgramContext().setValue(tmode, base, toAddr(0x1000000L + 0xb0000L), BigInteger.ONE);
        Memory m = p.getMemory();
        int n = 0;
        for (long a = 0x1000000L + 0x100; a < 0x1000000L + 0x82640L; a += 2) {
            Address ad = toAddr(a);
            int hw = m.getShort(ad) & 0xffff;
            boolean push = (hw & 0xff00) == 0xb500;               // push {rlist, lr}
            if (!push) continue;
            if (getFunctionAt(ad) != null) continue;
            disassemble(ad);
            createFunction(ad, null);
            n++;
        }
        println("functions created: " + n);
        DecompInterface di = new DecompInterface();
        di.openProgram(p);
        File out = new File(System.getProperty("user.dir"), "st2_decomp.c");
        PrintWriter w = new PrintWriter(new FileWriter(out));
        FunctionIterator it = p.getFunctionManager().getFunctions(true);
        int done = 0;
        while (it.hasNext()) {
            Function f = it.next();
            DecompileResults r = di.decompileFunction(f, 20, monitor);
            if (r.decompileCompleted()) {
                w.println("// " + f.getEntryPoint().toString().replace("0", "0x").substring(0) + "  file=" + Long.toHexString(f.getEntryPoint().getOffset() - 0x1000000L));
                w.println(r.getDecompiledFunction().getC());
            }
            done++;
        }
        w.close();
        println("decompiled " + done + " -> " + out);
    }
}
