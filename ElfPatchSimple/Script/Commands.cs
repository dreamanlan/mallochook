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
            string file = operands[0].AsString;
            string info = operands[1].AsString;
            ScriptProcessor.BeginFile(file, info);
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
            if (operands.Count > 1) {
                string file = operands[0].AsString;
                uint size = operands[1].Get<uint>();
                ScriptProcessor.EndFile(file, size);
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
