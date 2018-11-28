using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace ElfPatch
{
    public partial class MainForm : Form
    {
        public ToolStripProgressBar ProgressBar
        {
            get
            {
                return progressBar;
            }
        }
        public ToolStripStatusLabel StatusBar
        {
            get
            {
                return statusLabel;
            }
        }
        public TextBox ResultCtrl
        {
            get
            {
                return resultCtrl;
            }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            resultCtrl.Text = @"";
            ScriptProcessor.Init();
        }

        private void addBtn_Click(object sender, EventArgs e)
        {
            Dictionary<string, bool> existFiles = new Dictionary<string, bool>();
            foreach (string s in assemblyList.Items)
            {
                existFiles[s] = true;
            }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = "so";
            ofd.Filter = "so�ļ�|*.so|�����ļ�|*.*||";
            ofd.CheckPathExists = true;
            ofd.CheckFileExists = true;
            ofd.Multiselect = true;
            ofd.Title = "��ָ��Ҫ��ӵ�elf so�����ļ�";
            if (DialogResult.OK == ofd.ShowDialog())
            {
                foreach (string s in ofd.FileNames)
                {
                    if (!existFiles.ContainsKey(s))
                        assemblyList.Items.Add(s);
                }
                if (ofd.FileNames.Length > 0 && exportDir.Text.Trim().Length <= 0)
                {
                    string as0 = ofd.FileNames[0];
                    string path = Path.GetDirectoryName(as0);
                    exportDir.Text = Path.GetDirectoryName(path);
                }
                statusLabel.Text = "OK.";
            }
            ofd.Dispose();
        }

        private void clearBtn_Click(object sender, EventArgs e)
        {
            assemblyList.Items.Clear();
            statusLabel.Text = "OK.";
        }

        private void folderBtn_Click(object sender, EventArgs e)
        {
            string path = exportDir.Text.Trim();
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "��ָ��һ�����Ŀ¼��ע�ⲻҪʹ��ԭ�ļ�����Ŀ¼������Ḳ��ԭ�ļ�����";
            fbd.ShowNewFolderButton = true;
            fbd.SelectedPath = path;
            if (DialogResult.OK == fbd.ShowDialog())
            {
                exportDir.Text = fbd.SelectedPath;
                statusLabel.Text = "OK.";
            }
            else
            {
                return;
            }
        }

        private void execScript_Click(object sender, EventArgs e)
        {
            if (assemblyList.Items.Count <= 0)
                return;
            string path = exportDir.Text.Trim();
            if (path.Length <= 0) {
                MessageBox.Show("����ѡ��һ�����Ŀ¼��");
                return;
            }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = "scp";
            ofd.Filter = "�ű��ļ�|*.scp||";
            ofd.CheckPathExists = true;
            ofd.CheckFileExists = true;
            ofd.Multiselect = false;
            ofd.Title = "��ָ���ű��ļ�";
            if (DialogResult.OK == ofd.ShowDialog()) {
                string file = ofd.FileName;

                List<string> files = new List<string>();
                foreach (string s in assemblyList.Items) {
                    files.Add(s);
                }
                ScriptProcessor.Start(files, path, file);
            }
        }
    }
}