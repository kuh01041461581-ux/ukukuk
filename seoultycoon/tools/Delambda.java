import org.objectweb.asm.*;
import org.objectweb.asm.tree.*;
import java.io.*;
import java.nio.file.*;
import java.util.*;

/** javac -source 8 람다(invokedynamic LambdaMetafactory)를 익명 클래스 형태로 풀어 dx(람다 미지원)가 처리하게 한다. 사용: Delambda <classes-dir> */
public class Delambda implements Opcodes {
    static int counter = 0;
    static Path root;
    public static void main(String[] a) throws Exception {
        root = Paths.get(a[0]); List<Path> files = new ArrayList<>();
        try (var s = Files.walk(root)) { s.filter(p -> p.toString().endsWith(".class")).forEach(files::add); }
        for (Path p : files) process(p);
    }
    static void process(Path p) throws Exception {
        ClassNode cn = new ClassNode(); new ClassReader(Files.readAllBytes(p)).accept(cn, ClassReader.SKIP_FRAMES);
        boolean changed = false; Set<String> privLambdas = new HashSet<>();
        for (MethodNode m : cn.methods) for (AbstractInsnNode in : m.instructions.toArray()) {
            if (!(in instanceof InvokeDynamicInsnNode)) continue;
            InvokeDynamicInsnNode id = (InvokeDynamicInsnNode) in;
            if (!id.bsm.getOwner().equals("java/lang/invoke/LambdaMetafactory")) continue;
            Type sam = (Type) id.bsmArgs[0]; Handle impl = (Handle) id.bsmArgs[1]; Type inst = (Type) id.bsmArgs[2];
            String iface = Type.getReturnType(id.desc).getInternalName();
            String gen = cn.name + "$$L" + (counter++);
            if (impl.getOwner().equals(cn.name)) privLambdas.add(impl.getName() + impl.getDesc());
            writeClass(gen, iface, id.name, sam, inst, Type.getArgumentTypes(id.desc), impl);
            m.instructions.set(in, new MethodInsnNode(INVOKESTATIC, gen, "create", id.desc, false));
            changed = true;
        }
        if (!changed) return;
        for (MethodNode m : cn.methods) if (privLambdas.contains(m.name + m.desc)) m.access &= ~ACC_PRIVATE;
        ClassWriter cw = new ClassWriter(ClassWriter.COMPUTE_MAXS); cn.accept(cw); Files.write(p, cw.toByteArray());
    }
    static void conv(MethodVisitor mv, Type from, Type to) {
        if (from.equals(to)) return;
        int fs = from.getSort(), ts = to.getSort();
        boolean fp = fs <= Type.DOUBLE, tp = ts <= Type.DOUBLE;
        if (ts == Type.VOID || fs == Type.VOID) return;
        if (!fp && !tp) { if (!to.getInternalName().equals("java/lang/Object")) mv.visitTypeInsn(CHECKCAST, to.getInternalName()); return; }
        if (fp && !tp) {                                           // 박싱
            String w = box(from); mv.visitMethodInsn(INVOKESTATIC, w, "valueOf", "(" + from.getDescriptor() + ")L" + w + ";", false); return; }
        if (!fp && tp) {                                           // 언박싱
            String w = box(to); mv.visitTypeInsn(CHECKCAST, w.equals("java/lang/Integer") || true ? (to.getSort() == Type.BOOLEAN ? "java/lang/Boolean" : to.getSort() == Type.CHAR ? "java/lang/Character" : "java/lang/Number") : w);
            if (to.getSort() == Type.BOOLEAN) mv.visitMethodInsn(INVOKEVIRTUAL, "java/lang/Boolean", "booleanValue", "()Z", false);
            else if (to.getSort() == Type.CHAR) mv.visitMethodInsn(INVOKEVIRTUAL, "java/lang/Character", "charValue", "()C", false);
            else mv.visitMethodInsn(INVOKEVIRTUAL, "java/lang/Number", to.getClassName() + "Value", "()" + to.getDescriptor(), false);
            return; }
        // 기본형 → 기본형 확장
        int f = kind(from), t = kind(to);
        if (f == t) return;
        if (f == 0 && t == 1) mv.visitInsn(I2L); else if (f == 0 && t == 2) mv.visitInsn(I2F); else if (f == 0 && t == 3) mv.visitInsn(I2D);
        else if (f == 1 && t == 2) mv.visitInsn(L2F); else if (f == 1 && t == 3) mv.visitInsn(L2D); else if (f == 2 && t == 3) mv.visitInsn(F2D);
    }
    static int kind(Type t) { switch (t.getSort()) { case Type.LONG: return 1; case Type.FLOAT: return 2; case Type.DOUBLE: return 3; default: return 0; } }
    static String box(Type t) {
        switch (t.getSort()) { case Type.BOOLEAN: return "java/lang/Boolean"; case Type.CHAR: return "java/lang/Character"; case Type.BYTE: return "java/lang/Byte"; case Type.SHORT: return "java/lang/Short";
            case Type.INT: return "java/lang/Integer"; case Type.LONG: return "java/lang/Long"; case Type.FLOAT: return "java/lang/Float"; default: return "java/lang/Double"; }
    }
    static void writeClass(String gen, String iface, String name, Type sam, Type inst, Type[] cap, Handle impl) throws Exception {
        ClassWriter cw = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        cw.visit(V1_8, ACC_FINAL | ACC_SYNTHETIC, gen, null, "java/lang/Object", new String[]{iface});
        StringBuilder cd = new StringBuilder("(");
        for (int i = 0; i < cap.length; i++) { cw.visitField(ACC_PRIVATE | ACC_FINAL, "c" + i, cap[i].getDescriptor(), null, null); cd.append(cap[i].getDescriptor()); }
        cd.append(")V");
        MethodVisitor mv = cw.visitMethod(0, "<init>", cd.toString(), null, null); mv.visitCode();
        mv.visitVarInsn(ALOAD, 0); mv.visitMethodInsn(INVOKESPECIAL, "java/lang/Object", "<init>", "()V", false);
        int slot = 1; for (int i = 0; i < cap.length; i++) { mv.visitVarInsn(ALOAD, 0); mv.visitVarInsn(cap[i].getOpcode(ILOAD), slot); mv.visitFieldInsn(PUTFIELD, gen, "c" + i, cap[i].getDescriptor()); slot += cap[i].getSize(); }
        mv.visitInsn(RETURN); mv.visitMaxs(0, 0); mv.visitEnd();
        String fd = cd.toString().replace(")V", ")L" + iface + ";");
        mv = cw.visitMethod(ACC_STATIC, "create", fd, null, null); mv.visitCode();
        mv.visitTypeInsn(NEW, gen); mv.visitInsn(DUP); slot = 0;
        for (Type c : cap) { mv.visitVarInsn(c.getOpcode(ILOAD), slot); slot += c.getSize(); }
        mv.visitMethodInsn(INVOKESPECIAL, gen, "<init>", cd.toString(), false); mv.visitInsn(ARETURN); mv.visitMaxs(0, 0); mv.visitEnd();
        // SAM 구현
        mv = cw.visitMethod(ACC_PUBLIC, name, sam.getDescriptor(), null, null); mv.visitCode();
        Type[] sp = sam.getArgumentTypes(), ip = inst.getArgumentTypes();
        Type[] implArgs = Type.getArgumentTypes(impl.getDesc()); int tag = impl.getTag();
        boolean isStatic = tag == H_INVOKESTATIC, isNew = tag == H_NEWINVOKESPECIAL;
        if (isNew) { mv.visitTypeInsn(NEW, impl.getOwner()); mv.visitInsn(DUP); }
        List<Type> pushed = new ArrayList<>();
        // 호출 대상(리시버) + 인자들: 캡처 값 먼저, 그다음 SAM 인자
        int total = implArgs.length + (isStatic || isNew ? 0 : 1), ci = 0, si = 0, sslot = 1;
        for (int k = 0; k < total; k++) {
            Type want = (k == 0 && !isStatic && !isNew) ? Type.getObjectType(impl.getOwner()) : implArgs[k - (isStatic || isNew ? 0 : 1)];
            if (ci < cap.length) { mv.visitVarInsn(ALOAD, 0); mv.visitFieldInsn(GETFIELD, gen, "c" + ci, cap[ci].getDescriptor()); conv(mv, cap[ci], want); ci++; }
            else { mv.visitVarInsn(sp[si].getOpcode(ILOAD), sslot); sslot += sp[si].getSize(); conv(mv, sp[si], ip[si]); conv(mv, ip[si], want); si++; }
        }
        int op = isStatic ? INVOKESTATIC : isNew ? INVOKESPECIAL : tag == H_INVOKEINTERFACE ? INVOKEINTERFACE : INVOKEVIRTUAL;
        boolean itf = impl.isInterface();
        mv.visitMethodInsn(op, impl.getOwner(), impl.getName(), impl.getDesc(), itf);
        Type implRet = isNew ? Type.getObjectType(impl.getOwner()) : Type.getReturnType(impl.getDesc()), samRet = sam.getReturnType();
        if (samRet.getSort() == Type.VOID) { if (implRet.getSort() != Type.VOID) mv.visitInsn(implRet.getSize() == 2 ? POP2 : POP); mv.visitInsn(RETURN); }
        else { conv(mv, implRet, samRet); mv.visitInsn(samRet.getOpcode(IRETURN)); }
        mv.visitMaxs(0, 0); mv.visitEnd(); cw.visitEnd();
        Files.write(root.resolve(gen + ".class"), cw.toByteArray());
    }
}
