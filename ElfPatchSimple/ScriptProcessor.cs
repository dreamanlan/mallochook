using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Calculator;

namespace ElfPatch
{
    public static class ScriptProcessor
    {
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
            s_Calculator.Register("beginfile", new ExpressionFactoryHelper<BeginFileCommand>());
            s_Calculator.Register("addinitarraycall", new ExpressionFactoryHelper<AddInitArrayCallCommand>());
            s_Calculator.Register("endfile", new ExpressionFactoryHelper<EndFileCommand>());
            s_Calculator.Register("getfilelist", new ExpressionFactoryHelper<GetFileListCommand>());
            s_Calculator.Register("log", new ExpressionFactoryHelper<LogCommand>());
        }
        public static void Start(IList<string> files, string outputPath, string scpFile)
        {
            s_FileList.Clear();
            s_FileList.AddRange(files);
            s_OutputPath = outputPath;
            
            s_Calculator.LoadDsl(scpFile);
            s_Calculator.Calc("main");
        }
        public static IList<string> GetFileList()
        {
            return s_FileList;
        }
        public static void Begin(string info)
        {
            s_CurNum = 0;
            s_TotalNum = s_FileList.Count;
            Program.MainForm.ProgressBar.Value = 0;
            Program.MainForm.StatusBar.Text = info;

            ErrorTxts.Clear();
            s_ElfFiles.Clear();
        }
        public static void End(string info)
        {
            Program.MainForm.StatusBar.Text = info;
            Program.MainForm.ResultCtrl.Text = string.Join("\r\n", ErrorTxts.ToArray());
            ErrorTxts.Clear();
        }
        public static void BeginFile(string file, string info)
        {
            Program.MainForm.StatusBar.Text = info;

            if (!s_ElfFiles.ContainsKey(file)) {
                var elfFile = new ElfFile();
                s_ElfFiles[file] = elfFile;

                s_CurFile = elfFile;

                elfFile.Load(file);
            }
        }
        public static void AddInitArrayCall(uint entry)
        {
            if (null != s_CurFile) {
                s_CurFile.AddInitArrayCall(entry);
            }
        }
        public static void EndFile(string file, uint size)
        {
            if (null != s_CurFile) {
                var outFile = Path.Combine(s_OutputPath, Path.GetFileName(file));
                s_CurFile.Save(outFile, size);
            }

            s_CurNum++;
            Program.MainForm.ProgressBar.Value = s_CurNum * 100 / s_TotalNum;
        }

        private static DslCalculator s_Calculator = new DslCalculator();
        private static ElfFile s_CurFile = null;
        private static List<string> s_ErrorTxts = new List<string>();

        private static Dictionary<string, ElfFile> s_ElfFiles = new Dictionary<string, ElfFile>();
        private static List<string> s_FileList = new List<string>();
        private static string s_OutputPath = string.Empty;
        private static int s_CurNum = 0;
        private static int s_TotalNum = 0;
    }
}
