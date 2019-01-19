using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MonoPatch
{
    public class MonoFile
    {
        public ModuleDefinition ModuleDefinition
        {
            get { return m_ModuleDefinition; }
        }
        public void Load(string file, bool readSymbols)
        {
            try {
                m_ModuleDefinition = ModuleDefinition.ReadModule(file, new ReaderParameters { ReadSymbols = readSymbols });
                if (null != m_ModuleDefinition) {
                    var resolver = m_ModuleDefinition.AssemblyResolver as BaseAssemblyResolver;
                    if (null != resolver) {
                        resolver.AddSearchDirectory(Path.GetDirectoryName(file));
                    }
                    m_ObjectArrayTypeRef = m_ModuleDefinition.ImportReference(typeof(object[]));
                    m_ObjectTypeRef = m_ModuleDefinition.ImportReference(typeof(object));
                } else {
                    ScriptProcessor.ErrorTxts.Add(string.Format("Can't read module from '{0}'", file));
                }
            } catch (Exception ex) {
                ScriptProcessor.ErrorTxts.Add(string.Format("load from '{0}', exception:{1}\n{2}", file, ex.Message, ex.StackTrace));
            }
        }
        public void InjectPrologue(MethodReference methodRef)
        {
            if (null != methodRef) {
                bool haveReturnValue = 0 != string.Compare(methodRef.ReturnType.FullName, "System.Void", true);
                int paramCount = methodRef.Parameters.Count;
                if (paramCount == 2) {
                    if (methodRef.Parameters[0].ParameterType.FullName == "System.Int32") {
                        InjectMemoryLog(methodRef, haveReturnValue);
                    } else {
                        InjectCallHook(methodRef, haveReturnValue);
                    }
                } else {
                    InjectReturnHook(methodRef, haveReturnValue);
                }
            }
        }
        public void Save(string file, bool writeSymbols)
        {
            try {
                if (File.Exists(file)) {
                    File.Delete(file);
                }
                m_ModuleDefinition.Write(file, new WriterParameters { WriteSymbols = writeSymbols });
            } catch (Exception ex) {
                ScriptProcessor.ErrorTxts.Add(string.Format("save to '{0}', exception:{0}{1}\n{2}", file, ex.Message, ex.StackTrace));
            }
        }

        private void InjectMemoryLog(MethodReference methodRef, bool haveReturnValue)
        {
            try {
                foreach (var typeDef in m_ModuleDefinition.Types) {
                    if (ScriptProcessor.DontInjectTypes.Contains(typeDef.FullName))
                        continue;
                    if (!ScriptProcessor.CheckType(typeDef, methodRef))
                        continue;
                    foreach (var methodDef in typeDef.Methods) {
                        var fullName = methodDef.FullName;
                        if (fullName == methodRef.FullName)
                            continue;
                        if (ScriptProcessor.DontInjectMethods.Contains(fullName))
                            continue;
                        if (!ScriptProcessor.CheckMethod(methodDef, methodRef))
                            continue;
                        var body = methodDef.Body;
                        if (HaveNew(methodDef)) {
                            string tag = CalcTag(methodDef);
                            
                            var ilProcessor = body.GetILProcessor();
                            var insertPoint = body.Instructions[0];
                            if (insertPoint.OpCode == OpCodes.Nop) {
                                insertPoint = body.Instructions[1];
                            } else {
                                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Nop));
                            }
                            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_0));
                            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldstr, tag));
                            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Call, methodRef));
                            if (haveReturnValue) {
                                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Pop));
                            }
                            //退出前再调用一次
                            insertPoint = FindRet(body, null);
                            while (null != insertPoint) {
                                insertPoint.OpCode = OpCodes.Nop;
                                insertPoint.Operand = null;
                                var newRet = ilProcessor.Create(OpCodes.Ret);
                                ilProcessor.InsertAfter(insertPoint, newRet);
                                insertPoint = newRet;

                                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_1));
                                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldstr, tag));
                                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Call, methodRef));
                                if (haveReturnValue) {
                                    ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Pop));
                                }
                                insertPoint = FindRet(body, insertPoint);
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                ScriptProcessor.ErrorTxts.Add(string.Format("InjectPrologue({0}), exception:{1}\n{2}", methodRef.FullName, ex.Message, ex.StackTrace));
            }
        }
        private void InjectCallHook(MethodReference methodRef, bool haveReturnValue)
        {
            try {
                foreach (var typeDef in m_ModuleDefinition.Types) {
                    if (ScriptProcessor.DontInjectTypes.Contains(typeDef.FullName))
                        continue;
                    if (!ScriptProcessor.CheckType(typeDef, methodRef))
                        continue;
                    foreach (var methodDef in typeDef.Methods) {
                        var fullName = methodDef.FullName;
                        if (fullName == methodRef.FullName)
                            continue;
                        if (ScriptProcessor.DontInjectMethods.Contains(fullName))
                            continue;
                        if (!ScriptProcessor.CheckMethod(methodDef, methodRef))
                            continue;
                        var body = methodDef.Body;
                        body.InitLocals = true;

                        var ilProcessor = body.GetILProcessor();
                        var insertPoint = body.Instructions[0];
                        if (insertPoint.OpCode == OpCodes.Nop) {
                            insertPoint = body.Instructions[1];
                        } else {
                            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Nop));
                        }
                        body.Variables.Add(new VariableDefinition(m_ObjectArrayTypeRef));
                        int arrLocalIndex = body.Variables.Count - 1;
                        int ct = methodDef.Parameters.Count;
                        AddCallHook(arrLocalIndex, ct, methodDef, methodRef, haveReturnValue, insertPoint, ilProcessor);
                    }
                }
            } catch (Exception ex) {
                ScriptProcessor.ErrorTxts.Add(string.Format("InjectPrologue({0}), exception:{1}\n{2}", methodRef.FullName, ex.Message, ex.StackTrace));
            }
        }
        private void InjectReturnHook(MethodReference methodRef, bool haveReturnValue)
        {
            try {
                foreach (var typeDef in m_ModuleDefinition.Types) {
                    if (ScriptProcessor.DontInjectTypes.Contains(typeDef.FullName))
                        continue;
                    if (!ScriptProcessor.CheckType(typeDef, methodRef))
                        continue;
                    foreach (var methodDef in typeDef.Methods) {
                        var fullName = methodDef.FullName;
                        if (fullName == methodRef.FullName)
                            continue;
                        if (ScriptProcessor.DontInjectMethods.Contains(fullName))
                            continue;
                        if (!ScriptProcessor.CheckMethod(methodDef, methodRef))
                            continue;
                        var body = methodDef.Body;
                        body.InitLocals = true;

                        var ilProcessor = body.GetILProcessor();
                        body.Variables.Add(new VariableDefinition(m_ObjectArrayTypeRef));
                        int arrLocalIndex = body.Variables.Count - 1;
                        int ct = methodDef.Parameters.Count;

                        var insertPoint = FindRet(body, null);
                        while (null != insertPoint) {
                            insertPoint.OpCode = OpCodes.Nop;
                            insertPoint.Operand = null;
                            var newRet = ilProcessor.Create(OpCodes.Ret);
                            ilProcessor.InsertAfter(insertPoint, newRet);
                            insertPoint = newRet;

                            AddReturnHook(arrLocalIndex, ct, methodDef, methodRef, haveReturnValue, insertPoint, ilProcessor);
                            insertPoint = FindRet(body, insertPoint);
                        }
                    }
                }
            } catch (Exception ex) {
                ScriptProcessor.ErrorTxts.Add(string.Format("InjectPrologue({0}), exception:{1}\n{2}", methodRef.FullName, ex.Message, ex.StackTrace));
            }
        }

        private void AddCallHook(int arrLocalIndex, int paramCount, MethodDefinition methodDef, MethodReference methodRef, bool haveReturnValue, Instruction insertPoint, ILProcessor ilProcessor)
        {
            AddLdc(paramCount, insertPoint, ilProcessor);
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Newarr, m_ObjectTypeRef));
            AddStloc(arrLocalIndex, insertPoint, ilProcessor);
            for (int i = 0; i < paramCount; ++i) {
                AddLdloc(arrLocalIndex, insertPoint, ilProcessor);
                AddLdc(i, insertPoint, ilProcessor);
                AddLdarg(methodDef.IsStatic ? i : i + 1, insertPoint, ilProcessor);
                if (methodDef.Parameters[i].ParameterType.IsValueType) {
                    //box
                    ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Box, methodDef.Parameters[i].ParameterType));
                }
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stelem_Ref));
            }
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldstr, CalcTag(methodDef)));
            AddLdloc(arrLocalIndex, insertPoint, ilProcessor);
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Call, methodRef));
            if (haveReturnValue) {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Pop));
            }
        }
        private void AddReturnHook(int arrLocalIndex, int paramCount, MethodDefinition methodDef, MethodReference methodRef, bool haveReturnValue, Instruction insertPoint, ILProcessor ilProcessor)
        {
            bool funcHaveReturn = 0 != string.Compare(methodDef.ReturnType.FullName, "System.Void", true);
            if (funcHaveReturn) {
                if (methodDef.ReturnType.IsValueType) {
                    ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Box, methodDef.ReturnType));
                }
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Dup));
            } else {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldnull));
            }
            AddLdc(paramCount, insertPoint, ilProcessor);
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Newarr, m_ObjectTypeRef));
            AddStloc(arrLocalIndex, insertPoint, ilProcessor);
            for (int i = 0; i < paramCount; ++i) {
                AddLdloc(arrLocalIndex, insertPoint, ilProcessor);
                AddLdc(i, insertPoint, ilProcessor);
                AddLdarg(methodDef.IsStatic ? i : i + 1, insertPoint, ilProcessor);
                if (methodDef.Parameters[i].ParameterType.IsValueType) {
                    //box
                    ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Box, methodDef.Parameters[i].ParameterType));
                }
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stelem_Ref));
            }
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldstr, CalcTag(methodDef)));
            AddLdloc(arrLocalIndex, insertPoint, ilProcessor);
            ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Call, methodRef));
            if (haveReturnValue) {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Pop));
            }
        }
        private Instruction FindRet(MethodBody body, Instruction lastInst)
        {
            int startIx = 0;
            if (null != lastInst) {
                startIx = body.Instructions.IndexOf(lastInst);
                if (startIx >= 0)
                    ++startIx;
                else
                    startIx = 0;
            }
            for (; startIx < body.Instructions.Count; ++startIx) {
                if (body.Instructions[startIx].OpCode == OpCodes.Ret) {
                    return body.Instructions[startIx];
                }
            }
            return null;
        }

        private void AddLdc(int ct, Instruction insertPoint, ILProcessor ilProcessor)
        {
            if (ct >= 0 && ct <= 8) {
                switch (ct) {
                    case 0:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_0));
                        break;
                    case 1:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_1));
                        break;
                    case 2:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_2));
                        break;
                    case 3:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_3));
                        break;
                    case 4:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_4));
                        break;
                    case 5:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_5));
                        break;
                    case 6:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_6));
                        break;
                    case 7:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_7));
                        break;
                    case 8:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_8));
                        break;
                }
            } else if (ct < 128) {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4_S, (sbyte)ct));
            } else {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldc_I4, ct));
            }
        }
        private void AddLdloc(int ct, Instruction insertPoint, ILProcessor ilProcessor)
        {
            if (ct >= 0 && ct <= 3) {
                switch (ct) {
                    case 0:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldloc_0));
                        break;
                    case 1:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldloc_1));
                        break;
                    case 2:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldloc_2));
                        break;
                    case 3:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldloc_3));
                        break;
                }
            } else if (ct < 256) {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldloc_S, (byte)ct));
            } else {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldloc, ct));
            }
        }
        private void AddLdarg(int ct, Instruction insertPoint, ILProcessor ilProcessor)
        {
            if (ct >= 0 && ct <= 3) {
                switch (ct) {
                    case 0:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_0));
                        break;
                    case 1:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_1));
                        break;
                    case 2:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_2));
                        break;
                    case 3:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_3));
                        break;
                }
            } else if (ct < 256) {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg_S, (byte)ct));
            } else {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Ldarg, ct));
            }
        }
        private void AddStloc(int ct, Instruction insertPoint, ILProcessor ilProcessor)
        {
            if (ct >= 0 && ct <= 3) {
                switch (ct) {
                    case 0:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stloc_0));
                        break;
                    case 1:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stloc_1));
                        break;
                    case 2:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stloc_2));
                        break;
                    case 3:
                        ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stloc_3));
                        break;
                }
            } else if (ct < 256) {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stloc_S, (byte)ct));
            } else {
                ilProcessor.InsertBefore(insertPoint, ilProcessor.Create(OpCodes.Stloc, ct));
            }
        }
        private bool HaveNew(MethodDefinition method)
        {
            var body = method.Body;
            if (null != body) {
                foreach (var il in body.Instructions) {
                    if (il.OpCode == OpCodes.Newarr) {
                        return true;
                    } else if (il.OpCode == OpCodes.Newobj) {
                        var typeRef = il.Operand as TypeReference;
                        var typeDef = il.Operand as TypeDefinition;
                        var methodRef = il.Operand as MethodReference;
                        var methodDef = il.Operand as MethodDefinition;
                        if (null != typeRef && !typeRef.IsValueType) {
                            return true;
                        } else if (null != typeDef && !typeDef.IsValueType) {
                            return true;
                        } else if (null != methodRef && !methodRef.DeclaringType.IsValueType) {
                            return true;
                        } else if (null != methodDef && !methodDef.DeclaringType.IsValueType) {
                            return true;
                        }
                    } else if (il.OpCode == OpCodes.Call || il.OpCode == OpCodes.Calli || il.OpCode == OpCodes.Callvirt) {
                        var methodRef = il.Operand as MethodReference;
                        var methodDef = il.Operand as MethodDefinition;
                        if (null != methodRef) {
                            var fn = methodRef.FullName;
                            if (ScriptProcessor.IsTreatAsNew(fn))
                                return true;
                        } else if (null != methodDef) {
                            var fn = methodDef.FullName;
                            if (ScriptProcessor.IsTreatAsNew(fn))
                                return true;
                        }
                    }
                }
            }
            return false;
        }
        private string CalcTag(MethodDefinition methodDef)
        {
            string fullName = methodDef.FullName;
            string retFullName = methodDef.ReturnType.FullName;
            string tag = fullName;
            int ix = fullName.IndexOf(retFullName);
            if (ix >= 0) {
                tag = fullName.Substring(ix + retFullName.Length).Trim();
            } else {
                ix = fullName.IndexOf(' ');
                if (ix >= 0) {
                    tag = fullName.Substring(ix).Trim();
                }
            }
            return tag;
        }

        private ModuleDefinition m_ModuleDefinition = null;
        private TypeReference m_ObjectArrayTypeRef = null;
        private TypeReference m_ObjectTypeRef = null;
    }
}
