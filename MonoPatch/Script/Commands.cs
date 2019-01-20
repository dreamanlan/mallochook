using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoPatch;

namespace Calculator
{
    //---------------------------------------------------------------------------------------------------------------
    internal class BeginCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string info = operands[0] as string;
                if (null != info) {
                    ScriptProcessor.Begin(info);
                }
            }
            return 0;
        }
    }
    internal class EndCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string info = operands[0] as string;
                if (null != info) {
                    ScriptProcessor.End(info);
                }
            }
            return 0;
        }
    }
    internal class ListTypesCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string module = operands[0] as string;
                string type = string.Empty;
                if (operands.Count > 1) {
                    type = operands[1] as string;
                }
                string method = string.Empty;
                if (operands.Count > 2) {
                    method = operands[2] as string;
                }
                if (!string.IsNullOrEmpty(module)) {
                    var md = ModuleDefinition.ReadModule(module);
                    if (null != md) {
                        var list = new List<TypeDefinition>();
                        foreach (var td in md.Types) {
                            if (td.FullName.IndexOf(type, StringComparison.CurrentCultureIgnoreCase) >= 0) {
                                list.Add(td);
                            }
                        }
                        return list;
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("listtypes can't read module '{0}'", module));
                    }
                }
            }
            return new List<TypeDefinition>();
        }
    }
    internal class ListMethodsCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string module = operands[0] as string;
                string type = string.Empty;
                if (operands.Count > 1) {
                    type = operands[1] as string;
                }
                string method = string.Empty;
                if (operands.Count > 2) {
                    method = operands[2] as string;
                }
                if (!string.IsNullOrEmpty(module)) {
                    var md = ModuleDefinition.ReadModule(module);
                    if (null != md) {
                        var list = new List<MethodDefinition>();
                        foreach (var td in md.Types) {
                            if (td.FullName.IndexOf(type, StringComparison.CurrentCultureIgnoreCase) >= 0) {
                                foreach (var m in td.Methods) {
                                    if (m.Name.IndexOf(method, StringComparison.CurrentCultureIgnoreCase) >= 0) {
                                        list.Add(m);
                                    }
                                }
                            }
                        }
                        return list;
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("listmethods can't read module '{0}'", module));
                    }
                }
            }
            return new List<MethodDefinition>();
        }
    }
    internal class UseSymbolsCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                bool enabled = (bool)Convert.ChangeType(operands[0], typeof(bool));
                ScriptProcessor.UseSymbols = enabled;
            }
            return ScriptProcessor.UseSymbols;
        }
    }
    internal class SetCheckTypeCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 1) {
                string hookFunc = operands[0] as string;
                string checkFunc = operands[1] as string;
                ScriptProcessor.SetCheckTypeFunc(hookFunc, checkFunc);
                return checkFunc;
            }
            return string.Empty;
        }
    }
    internal class SetCheckMethodCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 1) {
                string hookFunc = operands[0] as string;
                string checkFunc = operands[1] as string;
                ScriptProcessor.SetCheckMethodFunc(hookFunc, checkFunc);
                return checkFunc;
            }
            return string.Empty;
        }
    }
    internal class GetModuleFileNameCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            var fileName = ScriptProcessor.GetModuleDefinition().FileName;
            return fileName;
        }
    }
    internal class GetModuleNameCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            var name = ScriptProcessor.GetModuleDefinition().Name;
            return name;
        }
    }
    internal class DontInjectCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 1) {
                string module = operands[0] as string;
                string type = operands[1] as string;
                string method = string.Empty;
                if (operands.Count > 2) {
                    method = operands[2] as string;
                }
                if (!string.IsNullOrEmpty(module) && !string.IsNullOrEmpty(type)) {
                    TypeDefinition td = null;
                    TypeReference tr;
                    if (ScriptProcessor.GetModuleDefinition().TryGetTypeReference(type, out tr)) {
                        try {
                            td = tr.Resolve();
                        } catch (Exception ex) {
                            ScriptProcessor.ErrorTxts.Add(string.Format("dontinject '{0}/{1}.{2}' exception:{3}\n{4}", module, type, method, ex.Message, ex.StackTrace));
                        }
                    } else {
                        var md = ModuleDefinition.ReadModule(module);
                        if (null != md) {
                            td = md.GetType(type);
                        } else {
                            ScriptProcessor.ErrorTxts.Add(string.Format("dontinject can't read module '{0}'", module));
                        }
                    }
                    if (null != td) {
                        if (string.IsNullOrEmpty(method)) {
                            var fullName = td.FullName;
                            if (!ScriptProcessor.DontInjectTypes.Contains(fullName)) {
                                ScriptProcessor.DontInjectTypes.Add(fullName);
                            }
                        } else {
                            bool handled = false;
                            foreach (var m in td.Methods) {
                                var name = m.Name;
                                var fullName = m.FullName;
                                if (name == method || 0 == string.Compare(fullName, method, true)) {
                                    handled = true;
                                    if (!ScriptProcessor.DontInjectMethods.Contains(fullName)) {
                                        ScriptProcessor.DontInjectMethods.Add(fullName);
                                    }
                                }
                            }
                            if (!handled) {
                                ScriptProcessor.ErrorTxts.Add(string.Format("dontinject can't find method '{0}' from {1}", method, type));
                            }
                        }
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("dontinject can't find type '{0}' from {1}", type, module));
                    }
                }
            }
            return 0;
        }
    }
    internal class DontTreatAsNewCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string r = operands[0] as string;
                var regex = new Regex(r, RegexOptions.Compiled);
                ScriptProcessor.DontTreatAsNew.Add(regex);
            }
            return 0;
        }
    }
    internal class TreatAsNewCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string r = operands[0] as string;
                var regex = new Regex(r, RegexOptions.Compiled);
                ScriptProcessor.TreatAsNew.Add(regex);
            }
            return 0;
        }
    }
    internal class BeginFileCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 1) {
                string file = operands[0] as string;
                string info = operands[1] as string;
                if (!string.IsNullOrEmpty(file) && null != info) {
                    ScriptProcessor.BeginFile(file, info);
                }
            }
            return 0;
        }
    }
    internal class LoadAssemblyCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string assembly = operands[0] as string;
                if (!string.IsNullOrEmpty(assembly)) {
                    try {
                        var assem = System.Reflection.Assembly.LoadFile(assembly);
                        return assem;
                    } catch (Exception ex) {
                        ScriptProcessor.ErrorTxts.Add(string.Format("loadassembly '{0}' exception:{1}\n{2}", assembly, ex.Message, ex.StackTrace));
                    }
                }
            }
            return null;
        }
    }
    internal class LoadTypeCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 1) {
                var assembly = operands[0] as System.Reflection.Assembly;
                string typeStr = operands[1] as string;
                if (null != assembly && !string.IsNullOrEmpty(typeStr)) {
                    try {
                        //var types = assembly.GetTypes();
                        var type = assembly.GetType(typeStr, true);
                        return type;
                    } catch (TypeLoadException ex) {
                        ScriptProcessor.ErrorTxts.Add(string.Format("loadtype '{0}' exception:{1}\n{2}", typeStr, ex.Message, ex.StackTrace));
                    } catch (Exception ex) {
                        ScriptProcessor.ErrorTxts.Add(string.Format("loadtype '{0}' exception:{1}\n{2}", typeStr, ex.Message, ex.StackTrace));
                    }
                }
            }
            return null;
        }
    }
    internal class InjectCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 2) {
                string module = operands[0] as string;
                string type = operands[1] as string;
                string method = operands[2] as string;
                if (!string.IsNullOrEmpty(module) && !string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(method)) {
                    TypeDefinition td = null;
                    TypeReference tr;
                    if (ScriptProcessor.GetModuleDefinition().TryGetTypeReference(type, out tr)) {
                        try {
                            td = tr.Resolve();
                        } catch (Exception ex) {
                            ScriptProcessor.ErrorTxts.Add(string.Format("inject '{0}.{1}' exception:{2}\n{3}", type, method, ex.Message, ex.StackTrace));
                        }
                    } else {
                        var md = ModuleDefinition.ReadModule(module);
                        if (null != md) {
                            td = md.GetType(type);
                        } else {
                            ScriptProcessor.ErrorTxts.Add(string.Format("inject can't read module '{0}'", module));
                        }
                    }
                    if (null != td) {
                        bool handled = false;
                        foreach (var m in td.Methods) {
                            if (m.Name == method) {
                                bool memoryLog = 2 == m.Parameters.Count && 0 == string.Compare(m.Parameters[0].ParameterType.FullName, "System.Int32", true) && 0 == string.Compare(m.Parameters[1].ParameterType.FullName, "System.String", true);
                                bool callHook = 2 == m.Parameters.Count && 0 == string.Compare(m.Parameters[0].ParameterType.FullName, "System.String", true) && 0 == string.Compare(m.Parameters[1].ParameterType.FullName, "System.Object[]", true);
                                bool returnHook = 3 == m.Parameters.Count && 0 == string.Compare(m.Parameters[0].ParameterType.FullName, "System.Object", true) && 0 == string.Compare(m.Parameters[1].ParameterType.FullName, "System.String", true) && 0 == string.Compare(m.Parameters[2].ParameterType.FullName, "System.Object[]", true);
                                if (memoryLog || callHook || returnHook) {
                                    handled = true;
                                    try {
                                        var mr = new MethodReference(m.Name, m.ReturnType, m.DeclaringType);
                                        foreach (var p in m.Parameters) {
                                            mr.Parameters.Add(p);
                                        }
                                        var methodRef = ScriptProcessor.GetModuleDefinition().ImportReference(mr);
                                        ScriptProcessor.InjectPrologue(methodRef);
                                    } catch (Exception ex) {
                                        ScriptProcessor.ErrorTxts.Add(string.Format("inject '{0}.{1}' exception:{2}\n{3}", type, method, ex.Message, ex.StackTrace));
                                    }
                                    break;
                                }
                            }
                        }
                        if (!handled) {
                            ScriptProcessor.ErrorTxts.Add(string.Format("inject can't find method '{0}' from {1}", method, type));
                        }
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("inject can't find type '{0}' from {1}", type, module));
                    }
                }
            } else if (operands.Count == 2) {
                var type = operands[0] as Type;
                string method = operands[1] as string;
                if (null != type && !string.IsNullOrEmpty(method)) {
                    var m = type.GetMethod(method, new Type[] { typeof(string) });
                    if (null != m) {
                        try {
                            var methodRef = ScriptProcessor.GetModuleDefinition().ImportReference(m);
                            ScriptProcessor.InjectPrologue(methodRef);
                        } catch (Exception ex) {
                            ScriptProcessor.ErrorTxts.Add(string.Format("inject '{0}.{1}' exception:{2}\n{3}", type.FullName, method, ex.Message, ex.StackTrace));
                        }
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("inject can't find method '{0}' from {1}", method, type.FullName));
                    }
                }
            }
            return 0;
        }
    }
    internal class EndFileCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string file = operands[0] as string;
                if (!string.IsNullOrEmpty(file)) {
                    ScriptProcessor.EndFile(file);
                }
            }
            return 0;
        }
    }
    internal class GetFileListCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            return ScriptProcessor.GetFileList();
        }
    }
    //---------------------------------------------------------------------------------------------------------------
    internal class LogCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                string format = operands[0] as string;
                if (!string.IsNullOrEmpty(format)) {
                    List<object> vargs = new List<object>();
                    vargs.AddRange(operands);
                    vargs.RemoveAt(0);
                    ScriptProcessor.ErrorTxts.Add(string.Format(format, vargs.ToArray()));
                }
            }
            return 0;
        }
    }
    //---------------------------------------------------------------------------------------------------------------
}
