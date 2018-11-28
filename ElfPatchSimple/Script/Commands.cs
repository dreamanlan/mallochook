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
            string file = operands[0] as string;
            string info = operands[1] as string;
            ScriptProcessor.BeginFile(file, info);
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
            if (operands.Count > 1) {
                string file = operands[0] as string;
                uint size = (uint)Convert.ChangeType(operands[1], typeof(uint));
                ScriptProcessor.EndFile(file, size);
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
