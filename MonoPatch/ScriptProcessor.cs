using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Calculator;

namespace MonoPatch
{
    public static class ScriptProcessor
    {
        public static bool UseSymbols
        {
            get { return s_UseSymbols; }
            set { s_UseSymbols = value; }
        }
        public static HashSet<string> DontInjectMethods
        {
            get { return s_DontInjectMethods; }
        }
        public static HashSet<string> DontInjectTypes
        {
            get { return s_DontInjectTypes; }
        }
        public static List<string> ErrorTxts
        {
            get { return s_ErrorTxts; }
        }
        public static void Init()
        {
            s_Calculator.Init();

            //◊¢≤·Gm√¸¡Ó
            s_Calculator.Register("begin", new ExpressionFactoryHelper<BeginCommand>());
            s_Calculator.Register("end", new ExpressionFactoryHelper<EndCommand>());
            s_Calculator.Register("listtypes", new ExpressionFactoryHelper<ListTypesCommand>());
            s_Calculator.Register("listmethods", new ExpressionFactoryHelper<ListMethodsCommand>());
            s_Calculator.Register("usesymbols", new ExpressionFactoryHelper<UseSymbolsCommand>());
            s_Calculator.Register("setchecktype", new ExpressionFactoryHelper<SetCheckTypeCommand>());
            s_Calculator.Register("setcheckmethod", new ExpressionFactoryHelper<SetCheckMethodCommand>());
            s_Calculator.Register("getmodulefilename", new ExpressionFactoryHelper<GetModuleFileNameCommand>());
            s_Calculator.Register("getmodulename", new ExpressionFactoryHelper<GetModuleNameCommand>());
            s_Calculator.Register("dontinject", new ExpressionFactoryHelper<DontInjectCommand>());
            s_Calculator.Register("beginfile", new ExpressionFactoryHelper<BeginFileCommand>());
            s_Calculator.Register("loadassembly", new ExpressionFactoryHelper<LoadAssemblyCommand>());
            s_Calculator.Register("loadtype", new ExpressionFactoryHelper<LoadTypeCommand>());
            s_Calculator.Register("inject", new ExpressionFactoryHelper<InjectCommand>());
            s_Calculator.Register("endfile", new ExpressionFactoryHelper<EndFileCommand>());
            s_Calculator.Register("getfilelist", new ExpressionFactoryHelper<GetFileListCommand>());
            s_Calculator.Register("log", new ExpressionFactoryHelper<LogCommand>());
        }
        public static void Start(IList<string> files, string outputPath, bool useSymbols, string scpFile)
        {
            s_CheckTypeFuncs.Clear();
            s_CheckMethodFuncs.Clear();
            s_DontInjectMethods.Clear();
            s_DontInjectTypes.Clear();

            s_FileList.Clear();
            s_FileList.AddRange(files);
            s_OutputPath = outputPath;
            s_UseSymbols = useSymbols;
            
            s_Calculator.Load(scpFile);
            s_Calculator.Calc("main");
        }
        public static IList<string> GetFileList()
        {
            return s_FileList;
        }
        public static void SetCheckTypeFunc(string hookFunc, string checkFunc)
        {
            s_CheckTypeFuncs[hookFunc] = checkFunc;
        }
        public static void SetCheckMethodFunc(string hookFunc, string checkFunc)
        {
            s_CheckMethodFuncs[hookFunc] = checkFunc;
        }
        public static void Begin(string info)
        {
            s_CurNum = 0;
            s_TotalNum = s_FileList.Count;

            if (null != Program.MainForm) {
                Program.MainForm.ProgressBar.Value = 0;
                Program.MainForm.StatusBar.Text = info;
            }

            ErrorTxts.Clear();
            s_MonoFiles.Clear();
        }
        public static void End(string info)
        {
            if (null != Program.MainForm) {
                Program.MainForm.StatusBar.Text = info;
                Program.MainForm.ResultCtrl.Text = string.Join("\r\n", ErrorTxts.ToArray());
            }

            foreach (string s in ErrorTxts) {
                Console.WriteLine(s);
            }
            ErrorTxts.Clear();
        }
        public static void BeginFile(string file, string info)
        {
            if (null != Program.MainForm) {
                Program.MainForm.StatusBar.Text = info;
            }

            if (!s_MonoFiles.ContainsKey(file)) {
                var monoFile = new MonoFile();
                s_MonoFiles[file] = monoFile;

                s_CurFile = monoFile;

                monoFile.Load(file, s_UseSymbols);
            }
        }
        public static Mono.Cecil.ModuleDefinition GetModuleDefinition()
        {
            if (null != s_CurFile) {
                return s_CurFile.ModuleDefinition;
            }
            return null;
        }
        public static void InjectPrologue(Mono.Cecil.MethodReference methodRef)
        {
            if (null != s_CurFile) {
                s_CurFile.InjectPrologue(methodRef);
            }
        }
        public static void EndFile(string file)
        {
            if (null != s_CurFile) {
                var outFile = Path.Combine(s_OutputPath, Path.GetFileName(file));
                s_CurFile.Save(outFile, s_UseSymbols);
            }

            s_CurNum++;
            if (null != Program.MainForm) {
                Program.MainForm.ProgressBar.Value = s_CurNum * 100 / s_TotalNum;
            }
        }
        public static bool CheckType(Mono.Cecil.TypeDefinition typeDef, Mono.Cecil.MethodReference methodRef)
        {
            string func;
            if (s_CheckTypeFuncs.TryGetValue(methodRef.Name, out func)) {
                var ret = s_Calculator.Calc(func, typeDef, methodRef);
                if (null != ret) {
                    return (bool)Convert.ChangeType(ret, typeof(bool));
                }
            }
            return true;
        }
        public static bool CheckMethod(Mono.Cecil.MethodDefinition methodDef, Mono.Cecil.MethodReference methodRef)
        {
            string func;
            if (s_CheckMethodFuncs.TryGetValue(methodRef.Name, out func)) {
                var ret = s_Calculator.Calc(func, methodDef, methodRef);
                if (null != ret) {
                    return (bool)Convert.ChangeType(ret, typeof(bool));
                }
            }
            return true;
        }

        private static DslCalculator s_Calculator = new DslCalculator();
        private static MonoFile s_CurFile = null;
        private static List<string> s_ErrorTxts = new List<string>();

        private static Dictionary<string, string> s_CheckTypeFuncs = new Dictionary<string, string>();
        private static Dictionary<string, string> s_CheckMethodFuncs = new Dictionary<string, string>();
        private static HashSet<string> s_DontInjectMethods = new HashSet<string>();
        private static HashSet<string> s_DontInjectTypes = new HashSet<string>();
        private static Dictionary<string, MonoFile> s_MonoFiles = new Dictionary<string, MonoFile>();
        private static List<string> s_FileList = new List<string>();
        private static string s_OutputPath = string.Empty;
        private static bool s_UseSymbols = false;
        private static int s_CurNum = 0;
        private static int s_TotalNum = 0;
    }
}
