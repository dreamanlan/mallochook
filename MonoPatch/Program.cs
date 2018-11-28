using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MonoPatch
{
    static class Program
    {
        public static MainForm MainForm
        {
            get { return s_MainForm; }
        }
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0) {
                string outputDir = string.Empty;
                bool useSymbols = false;
                List<string> files = new List<string>();
                string scpFile = "modify.scp";
                for (int i = 0; i < args.Length; ++i) {
                    if (0 == string.Compare(args[i], "-symbols", true)) {
                        useSymbols = true;
                    } else if (0 == string.Compare(args[i], "-out", true)) {
                        if (i < args.Length - 1) {
                            string arg = args[i + 1];
                            if (!arg.StartsWith("-")) {
                                outputDir = arg;
                                ++i;
                            }
                        }
                    } else if (0 == string.Compare(args[i], "-scp", true)) {
                        if (i < args.Length - 1) {
                            string arg = args[i + 1];
                            if (!arg.StartsWith("-")) {
                                string file = arg;
                                if (!File.Exists(file)) {
                                    Console.WriteLine("file path not found ! {0}", file);
                                } else {
                                    scpFile = file;
                                }
                                ++i;
                            }
                        }
                    } else if (0 == string.Compare(args[i], "-src", true)) {
                        if (i < args.Length - 1) {
                            string arg = args[i + 1];
                            if (!arg.StartsWith("-")) {
                                string file = arg;
                                if (!File.Exists(file)) {
                                    Console.WriteLine("file path not found ! {0}", file);
                                } else {
                                    files.Add(file);
                                }
                                ++i;
                            }
                        }
                    } else {
                        string file = args[i];
                        if (!File.Exists(file)) {
                            Console.WriteLine("file path not found ! {0}", file);
                        } else {
                            files.Add(file);
                        }
                    }
                }
                if (files.Count > 0) {
                    if (string.IsNullOrEmpty(outputDir)) {
                        string srcDir = Path.GetDirectoryName(files[0]);
                        outputDir = Path.GetDirectoryName(srcDir);
                    }
                    ScriptProcessor.Init();
                    ScriptProcessor.Start(files, outputDir, useSymbols, scpFile);
                }
                Environment.Exit(0);
            } else {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                s_MainForm = new MainForm();
                Application.Run(s_MainForm);
            }
        }

        private static MainForm s_MainForm;
    }
}