using System;
using System.Collections.Generic;
using System.IO;
using ElfPatch;

namespace Calculator
{
    //---------------------------------------------------------------------------------------------------------------
    internal class BeginCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            string info = operands[0] as string;
            ScriptProcessor.Begin(info);
            return 0;
        }
    }
    internal class EndCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            string info = operands[0] as string;
            ScriptProcessor.End(info);
            return 0;
        }
    }
    internal class BeginFileCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 2) {
                string file = operands[0] as string;
                uint size = (uint)Convert.ChangeType(operands[1], typeof(uint));
                string info = operands[2] as string;
                ScriptProcessor.BeginFile(file, size, info);
            }
            return 0;
        }
    }
    internal class AddDllCallCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 2) {
                string so = operands[0] as string;
                string func = operands[1] as string;
                string arg = operands[2] as string;
                ScriptProcessor.AddDllCall(so, func, arg);
            }
            return 0;
        }
    }
    internal class AddInitArrayCallCommand : SimpleExpressionBase
    {
        protected override object OnCalc(IList<object> operands)
        {
            if (operands.Count > 0) {
                uint entry = (uint)Convert.ChangeType(operands[0], typeof(uint));
                ScriptProcessor.AddInitArrayCall(entry);
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
                ScriptProcessor.EndFile(file);
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
                List<object> vargs = new List<object>();
                vargs.AddRange(operands);
                vargs.RemoveAt(0);
                ScriptProcessor.ErrorTxts.Add(string.Format(format, vargs.ToArray()));
            }
            return 0;
        }
    }
    //---------------------------------------------------------------------------------------------------------------
}
