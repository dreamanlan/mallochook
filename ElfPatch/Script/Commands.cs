using System;
using System.Collections.Generic;
using System.IO;
using ElfPatch;

namespace Calculator
{
    //---------------------------------------------------------------------------------------------------------------
    internal class BeginCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            string info = operands[0].AsString;
            ScriptProcessor.Begin(info);
            return 0;
        }
    }
    internal class EndCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            string info = operands[0].AsString;
            ScriptProcessor.End(info);
            return 0;
        }
    }
    internal class BeginFileCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            if (operands.Count > 2) {
                string file = operands[0].AsString;
                uint size = operands[1].Get<uint>();
                string info = operands[2].AsString;
                ScriptProcessor.BeginFile(file, size, info);
            }
            return 0;
        }
    }
    internal class AddDllCallCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            if (operands.Count > 2) {
                string so = operands[0].AsString;
                string func = operands[1].AsString;
                string arg = operands[2].AsString;
                ScriptProcessor.AddDllCall(so, func, arg);
            }
            return 0;
        }
    }
    internal class AddInitArrayCallCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            if (operands.Count > 0) {
                uint entry = operands[0].Get<uint>();
                ScriptProcessor.AddInitArrayCall(entry);
            }
            return 0;
        }
    }
    internal class EndFileCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            if (operands.Count > 0) {
                string file = operands[0].AsString;
                ScriptProcessor.EndFile(file);
            }
            return 0;
        }
    }
    internal class GetFileListCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            return CalculatorValue.FromObject(ScriptProcessor.GetFileList());
        }
    }
    //---------------------------------------------------------------------------------------------------------------
    internal class LogCommand : SimpleExpressionBase
    {
        protected override CalculatorValue OnCalc(IList<CalculatorValue> operands)
        {
            if (operands.Count > 0) {
                string format = operands[0].AsString;
                List<object> vargs = new List<object>();
                for(int i = 1; i < operands.Count; ++i) {
                    var opd = operands[i].Get<object>();
                    vargs.Add(opd);
                }
                ScriptProcessor.ErrorTxts.Add(string.Format(format, vargs.ToArray()));
            }
            return 0;
        }
    }
    //---------------------------------------------------------------------------------------------------------------
}
