using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

namespace SymbolMatch
{
    class AllocInfo
    {
        internal ulong Size = 0;
        internal List<int> Numbers = new List<int>();
        internal List<string> Symbols = new List<string>();
    }
    class Program
    {
        static void Main(string[] args)
        {
            var lines = File.ReadAllLines(args[0]);
            for (int i = 1; i < args.Length; ++i) {
                ReadSymbol(args[i]);
            }
            var addr2numbers = new Dictionary<ulong, AllocInfo>();
            var numberedLines = new SortedList<int, string>();
            var keys = s_Symbols.Keys;
            for (int i = 0; i < lines.Length; ++i) {
                var line = lines[i];
                var m = s_StackRegex.Match(line);
                if (m.Success) {
                    var val = m.Value;
                    if (m.Groups.Count > 3) {
                        var allocAddrStr = m.Groups[1].Value;
                        var sizeStr = m.Groups[2].Value;
                        var addrStr = m.Groups[3].Value;
                        if (!string.IsNullOrEmpty(addrStr)) {
                            ulong allocAddr = ulong.Parse(allocAddrStr, NumberStyles.AllowHexSpecifier);
                            ulong size = ulong.Parse(sizeStr);
                            ulong addr = ulong.Parse(addrStr, NumberStyles.AllowHexSpecifier);
                            ulong maddr = FindMatchedAddr(addr, keys);
                            bool find = false;
                            if (maddr > 0) {
                                string symbol;
                                if (s_Symbols.TryGetValue(maddr, out symbol)) {
                                    numberedLines.Add(i, line.Replace(val, val + " " + symbol));
                                    AddAddrAndNumber(allocAddr, size, i, symbol, addr2numbers);
                                    find = true;
                                }
                            }
                            if (!find) {
                                numberedLines.Add(i, line);
                                AddAddrAndNumber(allocAddr, size, i, string.Empty, addr2numbers);
                            }
                        }
                    }
                } else {
                    var m2 = s_FreeRegex.Match(line);
                    if (m2.Success) {
                        if (m2.Groups.Count > 1) {
                            var addrStr = m2.Groups[1].Value;
                            if (!string.IsNullOrEmpty(addrStr)) {
                                ulong addr = ulong.Parse(addrStr, NumberStyles.AllowHexSpecifier);
                                RemoveAddr(addr, addr2numbers, numberedLines);
                            }
                        }
                    }
                }
            }
            using (StreamWriter sw = new StreamWriter("stack_detail_info.txt", false)) {
                foreach (var pair in numberedLines) {
                    var line = pair.Value;
                    sw.WriteLine(line);
                }
                sw.Close();
            }
            ulong totalSize = 0;
            using (StreamWriter sw = new StreamWriter("alloc_size_symbol.txt", false)) {
                foreach (var pair in addr2numbers) {
                    var addr = pair.Key;
                    var info = pair.Value;
                    totalSize += info.Size;
                    sw.WriteLine("{0:x}\t{1}\t{2}", addr, info.Size, string.Join("|", info.Symbols));
                }
                sw.Close();
            }
            SortedDictionary<string, ulong> groupedAllocs = new SortedDictionary<string, ulong>();
            foreach (var pair in addr2numbers) {
                var addr = pair.Key;
                var info = pair.Value;
                string s = GetMaxCountSymbol(info.Symbols);
                ulong size;
                if (groupedAllocs.TryGetValue(s, out size)) {
                    groupedAllocs[s] = size + info.Size;
                } else {
                    groupedAllocs.Add(s, size);
                }
            }
            using (StreamWriter sw = new StreamWriter("alloc_size_group.txt", false)) {
                foreach (var pair in groupedAllocs) {
                    var sym = pair.Key;
                    var size = pair.Value;
                    sw.WriteLine("{0}\t{1}", sym, size);

                    Console.WriteLine("{0}  {1}", sym, size);
                }
                sw.Close();
            }
            Console.WriteLine("total size {0} in {1} allocs", totalSize, addr2numbers.Count);
        }
        private static string GetMaxCountSymbol(List<string> syms)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            foreach (string s in syms) {
                int ct;
                if (dict.TryGetValue(s, out ct)) {
                    dict[s] = ct + 1;
                } else {
                    dict.Add(s, 1);
                }
            }
            string sym = string.Empty;
            int maxV = 0;
            foreach (var pair in dict) {
                if (maxV < pair.Value) {
                    sym = pair.Key;
                    maxV = pair.Value;
                }
            }
            return sym;
        }
        private static void AddAddrAndNumber(ulong addr, ulong size, int number, string symbol, Dictionary<ulong, AllocInfo> addr2numbers)
        {
            AllocInfo info;
            if (!addr2numbers.TryGetValue(addr, out info)) {
                info = new AllocInfo { Size = size };
                addr2numbers.Add(addr, info);
            }
            info.Numbers.Add(number);
            info.Symbols.Add(symbol);
        }
        private static void RemoveAddr(ulong addr, Dictionary<ulong, AllocInfo> addr2numbers, SortedList<int, string> numberedLines)
        {
            AllocInfo info;
            if (addr2numbers.TryGetValue(addr, out info)) {
                foreach (var n in info.Numbers) {
                    numberedLines.Remove(n);
                }
                addr2numbers.Remove(addr);
            }
        }
        private static ulong FindMatchedAddr(ulong addr, IList<ulong> list)
        {
            int ct = list.Count;
            if (ct <= 0)
                return 0;
            int lower = 0;
            int upper = ct - 1;
            while (lower + 1 < upper) {
                int ix = (lower + upper) / 2;
                var taddr = list[ix];
                if (addr < taddr)
                    upper = ix;
                else if (addr == taddr)
                    return taddr;
                else
                    lower = ix;
            }
            var r = list[lower];
            if (addr < r || lower == upper)
                return 0;
            else
                return r;
        }
        private static void ReadSymbol(string file)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; ++i) {
                var fields = lines[i].Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length >= 2 && fields[1] != "Base" && fields[1] != "Address") {
                    var symbol = fields[0];
                    try {
                        int sp = symbol.LastIndexOfAny(s_PathSplits);
                        if (sp >= 0) {
                            symbol = symbol.Substring(sp + 1);
                        }
                        var addr = ulong.Parse(fields[1], NumberStyles.AllowHexSpecifier);
                        if (!s_Symbols.ContainsKey(addr)) {
                            s_Symbols.Add(addr, symbol);
                        }
                    } catch {
                    }
                }
            }
        }

        private static SortedList<ulong, string> s_Symbols = new SortedList<ulong, string>();
        private static Regex s_StackRegex = new Regex(@"mymalloc\[addr:0x([0-9a-f]+) size:([0-9]+)\] #[0-9]+:0x([0-9a-f]+)", RegexOptions.Compiled);
        private static Regex s_FreeRegex = new Regex(@"myfree\[addr:0x([0-9a-f]+)\]", RegexOptions.Compiled);
        private static char[] s_PathSplits = new char[] { '/', '\\' };
    }
}
