import org.objectweb.asm.*;
import org.objectweb.asm.tree.*;
import java.io.*;
import java.nio.file.*;

/** 피처폰 원본은 new String(byte[]) / getBytes() 를 기본 문자셋(EUC-KR)에 의존한다.
 *  안드로이드 기본값은 UTF-8 이라 한글이 깨지므로, 해당 호출을 com.seoultycoon.Cp 의 정적 도우미(EUC-KR 고정)로 바꾼다. */
public class PatchCharset {
    public static void main(String[] a) throws Exception {
        int n = 0;
        try (DirectoryStream<Path> ds = Files.newDirectoryStream(Paths.get(a[0]), "*.class")) {
            for (Path p : ds) {
                ClassNode cn = new ClassNode();
                new ClassReader(Files.readAllBytes(p)).accept(cn, 0);
                int changed = 0;
                for (MethodNode m : cn.methods) {
                    boolean again = true;
                    while (again) {
                        again = false;
                        for (AbstractInsnNode in = m.instructions.getFirst(); in != null; in = in.getNext()) {
                            if (in instanceof MethodInsnNode) {
                                MethodInsnNode mi = (MethodInsnNode) in;
                                if (mi.getOpcode() == Opcodes.INVOKESTATIC && mi.owner.equals("java/lang/System") && mi.name.equals("currentTimeMillis")) {
                                    m.instructions.set(mi, new MethodInsnNode(Opcodes.INVOKESTATIC, "com/seoultycoon/Cp", "now", "()J", false)); changed++; again = true; break;
                                }
                                if (mi.getOpcode() == Opcodes.INVOKESTATIC && mi.owner.equals("java/lang/Thread") && mi.name.equals("sleep") && mi.desc.equals("(J)V")) {
                                    m.instructions.set(mi, new MethodInsnNode(Opcodes.INVOKESTATIC, "com/seoultycoon/Cp", "sleep", "(J)V", false)); changed++; again = true; break;
                                }
                                if (mi.owner.equals("java/lang/String") && mi.name.equals("getBytes") && mi.desc.equals("()[B") && mi.getOpcode() == Opcodes.INVOKEVIRTUAL) {
                                    m.instructions.set(mi, new MethodInsnNode(Opcodes.INVOKESTATIC, "com/seoultycoon/Cp", "bytes", "(Ljava/lang/String;)[B", false));
                                    changed++; again = true; break;
                                }
                                if (mi.owner.equals("java/lang/String") && mi.name.equals("<init>") && (mi.desc.equals("([B)V") || mi.desc.equals("([BII)V"))) {
                                    // 짝이 되는 NEW java/lang/String 을 거꾸로 찾는다
                                    int depth = 0; AbstractInsnNode q = mi.getPrevious(); TypeInsnNode nw = null;
                                    for (; q != null; q = q.getPrevious()) {
                                        if (q instanceof MethodInsnNode) { MethodInsnNode x = (MethodInsnNode) q; if (x.owner.equals("java/lang/String") && x.name.equals("<init>")) depth++; }
                                        if (q.getOpcode() == Opcodes.NEW && ((TypeInsnNode) q).desc.equals("java/lang/String")) { if (depth == 0) { nw = (TypeInsnNode) q; break; } depth--; }
                                    }
                                    if (nw == null || nw.getNext().getOpcode() != Opcodes.DUP) throw new RuntimeException("unexpected pattern in " + cn.name + "." + m.name);
                                    AbstractInsnNode dup = nw.getNext();
                                    m.instructions.remove(dup); m.instructions.remove(nw);
                                    String d = mi.desc.equals("([B)V") ? "([B)Ljava/lang/String;" : "([BII)Ljava/lang/String;";
                                    m.instructions.set(mi, new MethodInsnNode(Opcodes.INVOKESTATIC, "com/seoultycoon/Cp", "str", d, false));
                                    changed++; again = true; break;
                                }
                            }
                        }
                    }
                }
                if (changed > 0) {
                    ClassWriter cw = new ClassWriter(0);   // 프레임/맥스 재계산 없음(원본은 구버전 클래스)
                    cn.accept(cw);
                    Files.write(p, cw.toByteArray());
                    System.out.println("patched " + cn.name + " x" + changed); n += changed;
                }
            }
        }
        System.out.println("total " + n);
    }
}
