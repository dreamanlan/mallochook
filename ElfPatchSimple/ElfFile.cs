using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

namespace ElfPatch
{
    public class ElfFile
    {
        public void Load(string file)
        {
            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            try {
                m_is_loaded = false;
                var buffer = br.ReadBytes((int)fs.Length);
                uint pos = 0;
                m_old_file_data = buffer;
                m_ident = ReadElfIdent(buffer, 0);
                if (m_ident.IsElf()) {
                    pos += elf_ident.SizeOfStruct;
                    if (m_ident.e_class == (byte)enum_elfclass.ELFCLASS32) {
                        Load32(buffer, pos);
                        m_is_loaded = true;
                    } else if (m_ident.e_class == (byte)enum_elfclass.ELFCLASS64) {
                        Load64(buffer, pos);
                        m_is_loaded = true;
                    }
                }
            } finally {
                br.Close();
                fs.Close();
                fs.Dispose();
            }
        }
        public void AddInitArrayCall(uint entry)
        {
            m_InitArrays.Add(entry);
        }
        public void Save(string file, uint newSectionSize)
        {
            if (!m_is_loaded)
                return;

            byte[] buffer = null;
            if (m_ident.e_class == (uint)enum_elfclass.ELFCLASS32) {
                buffer = Save32(newSectionSize);
            } else {
                buffer = Save64(newSectionSize);
            }

            if (null != buffer) {
                FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(fs);
                try {
                    bw.Write(buffer);
                } finally {
                    bw.Close();
                    fs.Close();
                    fs.Dispose();
                }
            }
        }

        private void Load32(byte[] buffer, uint pos)
        {
            m_elf32_hdr = ReadElf32Header(buffer, pos);
            m_max_vaddr_32 = 0;
            m_max_offset_32 = 0;
            m_elf32_phdrs.Clear();
            pos = m_elf32_hdr.e_phoff;
            for (int i = 0; i < m_elf32_hdr.e_phnum; ++i) {
                var phdr = ReadElf32Phdr(buffer, pos);
                m_elf32_phdrs.Add(phdr);
                pos += m_elf32_hdr.e_phentsize;

                var v = phdr.p_vaddr + phdr.p_memsz;
                var o = phdr.p_offset + phdr.p_filesz;
                if (v > m_max_vaddr_32)
                    m_max_vaddr_32 = v;
                if (o > m_max_offset_32)
                    m_max_offset_32 = o;
            }
            
            m_elf32_section_infos.Clear();
            m_elf32_dyns.Clear();
            m_elf32_init_array.Clear();
            pos = m_elf32_hdr.e_shoff;
            for (int i = 0; i < m_elf32_hdr.e_shnum; ++i) {
                var shdr = ReadElf32Shdr(buffer, pos);
                byte[] data = null;
                if (shdr.sh_type != (uint)enum_sht.SHT_NULL && shdr.sh_type != (uint)enum_sht.SHT_NOBITS) {
                    data = new byte[shdr.sh_size];
                    Array.Copy(buffer, shdr.sh_offset, data, 0, shdr.sh_size);
                }
                m_elf32_section_infos.Add(new elf32_section_info { shdr = shdr, data = data });
                pos += m_elf32_hdr.e_shentsize;

                if (m_elf32_hdr.e_shstrndx > 0 && m_elf32_hdr.e_shstrndx == i) {
                    m_sh_strs = ParseStrings(buffer, (int)shdr.sh_offset, (int)shdr.sh_size);
                    m_sh_str_next_pos = shdr.sh_size;
                } else if (shdr.sh_type == (int)enum_sht.SHT_DYNAMIC) {
                    m_elf32_dyn_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var dyn = ReadElf32Dyn(buffer, shdr.sh_offset + dpos);
                        if (dyn.d_tag != (uint)enum_dt.DT_NULL) {
                            m_elf32_dyns.Add(dyn);
                        }
                    }
                } else if (shdr.sh_type == (int)enum_sht.SHT_INIT_ARRAY) {
                    m_elf32_init_array_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += sizeof(uint)) {
                        var proc = ReadUint(buffer, shdr.sh_offset + dpos);
                        m_elf32_init_array.Add(proc);
                    }
                }

                if (shdr.sh_addr > 0) {
                    var v = shdr.sh_addr + shdr.sh_size;
                    var o = shdr.sh_offset + shdr.sh_size;
                    if (v > m_max_vaddr_32)
                        m_max_vaddr_32 = v;
                    if (o > m_max_offset_32)
                        m_max_offset_32 = o;
                }
            }
            uint reldynName = 0;
            m_sh_strs.TryGetValue(".rel.dyn", out reldynName);
            uint relpltName = 0;
            m_sh_strs.TryGetValue(".rel.plt", out relpltName);
            m_elf32_dyn_syms.Clear();
            m_elf32_sta_syms.Clear();
            m_elf32_dyn_rels.Clear();
            m_elf32_plt_rels.Clear();
            for (int i = 0; i < m_elf32_section_infos.Count; ++i) {
                var shdr = m_elf32_section_infos[i].shdr;
                if (shdr.sh_type == (uint)enum_sht.SHT_DYNSYM) {
                    m_elf32_dyn_sym_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var sym = ReadElf32Sym(buffer, shdr.sh_offset + dpos);
                        m_elf32_dyn_syms.Add(sym);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_SYMTAB) {
                    m_elf32_sta_sym_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var sym = ReadElf32Sym(buffer, shdr.sh_offset + dpos);
                        m_elf32_sta_syms.Add(sym);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == reldynName) {
                    m_elf32_dyn_rel_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var rel = ReadElf32Rel(buffer, shdr.sh_offset + dpos);
                        m_elf32_dyn_rels.Add(rel);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == relpltName) {
                    m_elf32_plt_rel_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var rel = ReadElf32Rel(buffer, shdr.sh_offset + dpos);
                        m_elf32_plt_rels.Add(rel);
                    }
                }
            }
            m_elf32_section_infos.Sort((a, b) => {
                if (a.shdr.sh_offset < b.shdr.sh_offset) {
                    return -1;
                } else if (a.shdr.sh_offset == b.shdr.sh_offset) {
                    if (a.shdr.sh_type == (uint)enum_sht.SHT_NOBITS)
                        return -1;
                    else if (b.shdr.sh_type == (uint)enum_sht.SHT_NOBITS)
                        return 1;
                    else
                        return 0;
                } else {
                    return 1;
                }
            });
        }
        private void Load64(byte[] buffer, uint pos)
        {
        }
        private byte[] Save32(uint newSectionSize)
        {
            ///*
            uint vaddr = (m_max_vaddr_32 + 0x1000) / 0x1000 * 0x1000;
            uint offset = (m_max_offset_32 + 0x1000) / 0x1000 * 0x1000;
            
            uint textName = 0;
            uint bssName = 0;
            m_sh_strs.TryGetValue(".text", out textName);
            m_sh_strs.TryGetValue(".bss", out bssName);

            int phdrIndex = -1;
            int dynIndex = -1;
            int lastLoadIndex = -1;
            for (int i = 0; i < m_elf32_phdrs.Count; ++i) {
                var phdr = m_elf32_phdrs[i];
                uint type = m_elf32_phdrs[i].p_type;
                if (type == (uint)enum_pt.PT_PHDR) {
                    phdrIndex = i;
                } else if (type == (uint)enum_pt.PT_DYNAMIC) {
                    dynIndex = i;
                } else if (type == (uint)enum_pt.PT_LOAD) {
                    lastLoadIndex = i;
                }
            }
            int bssIndex = -1;
            int lastTextIndex = -1;
            for (int i = 0; i < m_elf32_section_infos.Count; ++i) {
                if (bssName == m_elf32_section_infos[i].shdr.sh_name) {
                    bssIndex = i;
                } else if (textName == m_elf32_section_infos[i].shdr.sh_name) {
                    lastTextIndex = i;
                }
            }

            foreach (var entry in m_InitArrays) {
                m_elf32_init_array.Add(vaddr + entry);
            }

            ++m_elf32_hdr.e_shnum;
            m_elf32_init_array_shdr.sh_size += (uint)m_InitArrays.Count * sizeof(uint);

            //init_array节
            uint oldInitArrayAddr = m_elf32_init_array_shdr.sh_addr;
            m_elf32_init_array_shdr.sh_addr = vaddr + newSectionSize;
            m_elf32_init_array_shdr.sh_offset = offset + newSectionSize;
            m_elf32_init_array_shdr.Align();

            for (uint o = 0; o < m_elf32_init_array_shdr.sh_size; o += sizeof(uint)) {
                var oa = oldInitArrayAddr + o;
                var na = m_elf32_init_array_shdr.sh_addr + o;
                Relocate(oa, na);
            }

            //.bss后面的节往后挪           
            elf32_shdr lastShdr = new elf32_shdr();
            for (int i = bssIndex + 1; i < m_elf32_section_infos.Count; ++i) {
                var shdr = m_elf32_section_infos[i].shdr;
                if (i == bssIndex + 1) {
                    shdr.sh_addr = 0;
                    shdr.sh_offset = m_elf32_init_array_shdr.sh_offset + m_elf32_init_array_shdr.sh_size;
                    shdr.Align();
                } else {
                    shdr.sh_addr = 0;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                }
                m_elf32_section_infos[i].shdr = shdr;
                lastShdr = shdr;
            }

            //文件大小确定
            uint newOffset = lastShdr.sh_offset + lastShdr.sh_size;
            uint shdrSize = (uint)(m_elf32_hdr.e_shentsize * m_elf32_hdr.e_shnum);
            m_elf32_hdr.e_shoff = newOffset;
            uint fullSize = newOffset + shdrSize;

            elf32_phdr modifiedSegment = m_elf32_phdrs[lastLoadIndex];
            modifiedSegment.p_filesz = fullSize - modifiedSegment.p_offset;
            modifiedSegment.p_memsz = fullSize - modifiedSegment.p_offset;
            modifiedSegment.p_flags = (uint)enum_pf.PF_R | (uint)enum_pf.PF_W | (uint)enum_pf.PF_X;

            m_elf32_phdrs[lastLoadIndex] = modifiedSegment;
            
            elf32_shdr newSection = m_elf32_section_infos[lastTextIndex].shdr;
            newSection.sh_addr = vaddr;
            newSection.sh_offset = offset;
            newSection.sh_size = newSectionSize;
            newSection.Align();

            //修正dynamic节里的数据
            for (int i = 0; i < m_elf32_dyns.Count; ++i) {
                var dyn = m_elf32_dyns[i];
                if (dyn.d_tag == (uint)enum_dt.DT_INIT_ARRAY) {
                    dyn.d_ptr = m_elf32_init_array_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_INIT_ARRAYSZ) {
                    dyn.d_val = m_elf32_init_array_shdr.sh_size;
                }
                m_elf32_dyns[i] = dyn;
            }

            //将修改的section头写入原来的头列表里
            for (int i = 0; i < m_elf32_section_infos.Count; ++i) {
                var shdr = m_elf32_section_infos[i].shdr;
                uint oa = shdr.sh_addr;
                if (shdr.sh_type == (uint)enum_sht.SHT_INIT_ARRAY) {
                    shdr = m_elf32_init_array_shdr;
                }
                m_elf32_section_infos[i].shdr = shdr;
                uint na = shdr.sh_addr;
                Relocate(oa, na);
            }

            byte[] buffer = new byte[fullSize];            
            uint pos = 0;
            //写入旧的文件数据（先写旧的数据，防止旧的数据覆盖新的修改）
            Array.Copy(m_old_file_data, buffer, m_old_file_data.Length);
            //写入ident
            pos = 0;
            WriteElfIdent(buffer, pos, m_ident);
            pos += elf_ident.SizeOfStruct;
            //写入elf头
            WriteElf32Header(buffer, pos, m_elf32_hdr);            
            //写入segment hdrs
            pos = m_elf32_hdr.e_phoff;
            for (int i = 0; i < m_elf32_phdrs.Count; ++i) {
                var phdr = m_elf32_phdrs[i];
                WriteElf32Phdr(buffer, pos, phdr);
                pos += m_elf32_hdr.e_phentsize;
            }
            //清空.bss节的数据
            var bssShdr = m_elf32_section_infos[bssIndex].shdr;
            for (uint bpos = bssShdr.sh_offset; bpos < bssShdr.sh_offset + bssShdr.sh_size; ++bpos) {
                buffer[bpos] = 0;
            }
            //写入dynamic、init_array
            pos = m_elf32_dyn_shdr.sh_offset;
            foreach (var dyn in m_elf32_dyns) {
                WriteElf32Dyn(buffer, pos, dyn);
                pos += m_elf32_dyn_shdr.sh_entsize;
            }
            pos = m_elf32_init_array_shdr.sh_offset;
            foreach (var val in m_elf32_init_array) {
                WriteUint(buffer, pos, val);
                pos += sizeof(uint);
            }
            //写入.bss节后面节的数据
            for (int i = bssIndex + 1; i < m_elf32_section_infos.Count; ++i) {
                var shdr = m_elf32_section_infos[i].shdr;
                var data = m_elf32_section_infos[i].data;
                pos = shdr.sh_offset;
                Array.Copy(data, 0, buffer, pos, data.Length);

                //符号表有可能会整体移动，后续要重新写入数据
                if (shdr.sh_type == (uint)enum_sht.SHT_SYMTAB) {
                    m_elf32_sta_sym_shdr = shdr;
                }
            }
            //写入受重定位影响的数据
            if (m_elf32_dyn_syms.Count > 0) {
                var shdr = m_elf32_dyn_sym_shdr;
                pos = shdr.sh_offset;
                for (int i = 0; i < m_elf32_dyn_syms.Count; ++i) {
                    var sym = m_elf32_dyn_syms[i];
                    WriteElf32Sym(buffer, pos, sym);
                    pos += shdr.sh_entsize;
                }
            }
            if (m_elf32_sta_syms.Count > 0) {
                var shdr = m_elf32_sta_sym_shdr;
                pos = shdr.sh_offset;
                for (int i = 0; i < m_elf32_sta_syms.Count; ++i) {
                    var sym = m_elf32_sta_syms[i];
                    WriteElf32Sym(buffer, pos, sym);
                    pos += shdr.sh_entsize;
                }
            }
            if (m_elf32_dyn_rels.Count > 0) {
                var shdr = m_elf32_dyn_rel_shdr;
                pos = shdr.sh_offset;
                for (int i = 0; i < m_elf32_dyn_rels.Count; ++i) {
                    var rel = m_elf32_dyn_rels[i];
                    WriteElf32Rel(buffer, pos, rel);
                    pos += shdr.sh_entsize;
                }
            }
            if (m_elf32_plt_rels.Count > 0) {
                var shdr = m_elf32_plt_rel_shdr;
                pos = shdr.sh_offset;
                for (int i = 0; i < m_elf32_plt_rels.Count; ++i) {
                    var rel = m_elf32_plt_rels[i];
                    WriteElf32Rel(buffer, pos, rel);
                    pos += shdr.sh_entsize;
                }
            }
            //写入section hdrs
            pos = m_elf32_hdr.e_shoff;
            for (int i = 0; i < m_elf32_section_infos.Count; ++i) {
                var shdr = m_elf32_section_infos[i].shdr;
                WriteElf32Shdr(buffer, pos, shdr);
                pos += m_elf32_hdr.e_shentsize;
            }
            WriteElf32Shdr(buffer, pos, newSection);
            //*/
            return buffer;
        }
        private byte[] Save64(uint newSectionSize)
        {
            return null;
        }

        private void Relocate(uint oldAddress, uint newAddress)
        {
            if (m_elf32_dyn_syms.Count > 0) {
                for (int i = 0; i < m_elf32_dyn_syms.Count; ++i) {
                    var sym = m_elf32_dyn_syms[i];
                    if (sym.st_value == oldAddress) {
                        sym.st_value = newAddress;
                        m_elf32_dyn_syms[i] = sym;
                    }
                }
            }
            if (m_elf32_sta_syms.Count > 0) {
                for (int i = 0; i < m_elf32_sta_syms.Count; ++i) {
                    var sym = m_elf32_sta_syms[i];
                    if (sym.st_value == oldAddress) {
                        sym.st_value = newAddress;
                        m_elf32_sta_syms[i] = sym;
                    }
                }
            }
            if (m_elf32_dyn_rels.Count > 0) {
                for (int i = 0; i < m_elf32_dyn_rels.Count; ++i) {
                    var rel = m_elf32_dyn_rels[i];
                    if (rel.r_offset == oldAddress) {
                        rel.r_offset = newAddress;
                        m_elf32_dyn_rels[i] = rel;
                    }
                }
            }
            if (m_elf32_plt_rels.Count > 0) {
                for (int i = 0; i < m_elf32_plt_rels.Count; ++i) {
                    var rel = m_elf32_plt_rels[i];
                    if (rel.r_offset == oldAddress) {
                        rel.r_offset = newAddress;
                        m_elf32_plt_rels[i] = rel;
                    }
                }
            }
        }
        private Dictionary<string, uint> ParseStrings(byte[] buffer, int start, int size)
        {
            Dictionary<string, uint> dict = new Dictionary<string, uint>();
            int pos = 1;
            int endPos = pos;
            for (; endPos < size; ++endPos) {
                if (buffer[start + endPos] == '\0') {
                    string s = Encoding.ASCII.GetString(buffer, start + pos, endPos - pos);
                    dict[s] = (uint)pos;
                    pos = endPos + 1;
                }
            }
            return dict;
        }
        private uint CalcElfHash(string name)
        {
            var bytes = Encoding.ASCII.GetBytes(name);
            uint h = 0, g;

            for (int i = 0; i < bytes.Length; ++i) {
                var v = bytes[i];
                h = (h << 4) + v;
                g = h & 0xf0000000;
                h ^= g;
                h ^= g >> 24;
            }

            return h;
        }

        private class elf32_section_info
        {
            internal elf32_shdr shdr;
            internal byte[] data;
        }
        private class elf64_section_info
        {
            internal elf64_shdr shdr;
            internal byte[] data;
        }

        private bool m_is_loaded = false;
        private byte[] m_old_file_data = null;
        private Dictionary<string, uint> m_sh_strs = null;
        private uint m_sh_str_next_pos = 0;
        private elf_ident m_ident;

        private elf32_hdr m_elf32_hdr;
        private List<elf32_phdr> m_elf32_phdrs = new List<elf32_phdr>();
        private List<elf32_section_info> m_elf32_section_infos = new List<elf32_section_info>();
        private elf32_shdr m_elf32_dyn_shdr;
        private elf32_shdr m_elf32_init_array_shdr;
        private List<elf32_dyn> m_elf32_dyns = new List<elf32_dyn>();
        private List<uint> m_elf32_init_array = new List<uint>();
        private uint m_max_vaddr_32 = 0;
        private uint m_max_offset_32 = 0;

        private elf32_shdr m_elf32_dyn_sym_shdr;
        private elf32_shdr m_elf32_sta_sym_shdr;
        private elf32_shdr m_elf32_dyn_rel_shdr;
        private elf32_shdr m_elf32_plt_rel_shdr;
        private List<elf32_sym> m_elf32_dyn_syms = new List<elf32_sym>();
        private List<elf32_sym> m_elf32_sta_syms = new List<elf32_sym>();
        private List<elf32_rel> m_elf32_dyn_rels = new List<elf32_rel>();
        private List<elf32_rel> m_elf32_plt_rels = new List<elf32_rel>();

        private elf64_hdr m_elf64_hdr;
        private List<elf64_phdr> m_elf64_phdrs = new List<elf64_phdr>();
        private List<elf64_section_info> m_elf64_section_infos = new List<elf64_section_info>();
        private elf64_shdr m_elf64_dyn_shdr;
        private elf64_shdr m_elf64_init_array_shdr;
        private List<elf64_dyn> m_elf64_dyns = new List<elf64_dyn>();
        private List<ulong> m_elf64_init_array_dyns = new List<ulong>();
        private ulong m_max_vaddr_64 = 0;
        private ulong m_max_offset_64 = 0;

        private elf64_shdr m_elf64_dyn_sym_shdr;
        private elf64_shdr m_elf64_sta_sym_shdr;
        private elf64_shdr m_elf64_dyn_rel_shdr;
        private elf64_shdr m_elf64_plt_rel_shdr;
        private List<elf64_sym> m_elf64_dyn_syms = new List<elf64_sym>();
        private List<elf64_sym> m_elf64_sta_syms = new List<elf64_sym>();
        private List<elf64_rel> m_elf64_dyn_rels = new List<elf64_rel>();
        private List<elf64_rel> m_elf64_plt_rels = new List<elf64_rel>();

        private List<uint> m_InitArrays = new List<uint>();

        public static byte ELF_ST_BIND(byte x)
        {
            return (byte)(x >> 4);
        }
        public static byte ELF_ST_TYPE(byte x)
        {
            return (byte)(x & 0xf);
        }
        public static byte ELF_ST_INFO(byte bind, byte type)
        {
            return (byte)((bind << 4) + (type & 0xf));
        }
        public static uint ELF32_R_SYM(uint x)
        {
            return (x >> 8);
        }
        public static uint ELF32_R_TYPE(uint x)
        {
            return (x & 0xff);
        }
        public static uint ELF32_R_INFO(uint sym, uint type)
        {
            return (sym << 8) + (type & 0xff);
        }
        public static ulong ELF64_R_SYM(ulong i)
        {
            return (i >> 32);
        }
        public static ulong ELF64_R_TYPE(ulong i)
        {
            return (i & 0xffffffff);
        }
        public static ulong ELF64_R_INFO(ulong sym, ulong type)
        {
            return (sym << 32) + (type & 0xffffffff);
        }

        private static unsafe uint ReadUint(byte[] buffer, uint pos)
        {
            uint data = 0;
            fixed (byte* p = buffer) {
                uint* ptr = (uint*)(p + pos);
                data = *ptr;
            }
            return data;
        }
        private static unsafe ulong ReadUlong(byte[] buffer, uint pos)
        {
            ulong data = 0;
            fixed (byte* p = buffer) {
                ulong* ptr = (ulong*)(p + pos);
                data = *ptr;
            }
            return data;
        }
        private static unsafe elf_ident ReadElfIdent(byte[] buffer, uint pos)
        {
            elf_ident ident = new elf_ident();
            fixed (byte* p = buffer) {
                elf_ident* ptr = (elf_ident*)(p + pos);
                ident = *ptr;
            }
            return ident;
        }
        private static unsafe elf32_hdr ReadElf32Header(byte[] buffer, uint pos)
        {
            elf32_hdr elfHeader = new elf32_hdr();
            fixed (byte* p = buffer) {
                elf32_hdr* ptr = (elf32_hdr*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_hdr ReadElf64Header(byte[] buffer, uint pos)
        {
            elf64_hdr elfHeader = new elf64_hdr();
            fixed (byte* p = buffer) {
                elf64_hdr* ptr = (elf64_hdr*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf32_phdr ReadElf32Phdr(byte[] buffer, uint pos)
        {
            elf32_phdr elfHeader = new elf32_phdr();
            fixed (byte* p = buffer) {
                elf32_phdr* ptr = (elf32_phdr*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_phdr ReadElf64Phdr(byte[] buffer, uint pos)
        {
            elf64_phdr elfHeader = new elf64_phdr();
            fixed (byte* p = buffer) {
                elf64_phdr* ptr = (elf64_phdr*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf32_shdr ReadElf32Shdr(byte[] buffer, uint pos)
        {
            elf32_shdr elfHeader = new elf32_shdr();
            fixed (byte* p = buffer) {
                elf32_shdr* ptr = (elf32_shdr*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_shdr ReadElf64Shdr(byte[] buffer, uint pos)
        {
            elf64_shdr elfHeader = new elf64_shdr();
            fixed (byte* p = buffer) {
                elf64_shdr* ptr = (elf64_shdr*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf32_dyn ReadElf32Dyn(byte[] buffer, uint pos)
        {
            elf32_dyn elfHeader = new elf32_dyn();
            fixed (byte* p = buffer) {
                elf32_dyn* ptr = (elf32_dyn*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_dyn ReadElf64Dyn(byte[] buffer, uint pos)
        {
            elf64_dyn elfHeader = new elf64_dyn();
            fixed (byte* p = buffer) {
                elf64_dyn* ptr = (elf64_dyn*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf32_sym ReadElf32Sym(byte[] buffer, uint pos)
        {
            elf32_sym elfHeader = new elf32_sym();
            fixed (byte* p = buffer) {
                elf32_sym* ptr = (elf32_sym*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_sym ReadElf64Sym(byte[] buffer, uint pos)
        {
            elf64_sym elfHeader = new elf64_sym();
            fixed (byte* p = buffer) {
                elf64_sym* ptr = (elf64_sym*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf32_rel ReadElf32Rel(byte[] buffer, uint pos)
        {
            elf32_rel elfHeader = new elf32_rel();
            fixed (byte* p = buffer) {
                elf32_rel* ptr = (elf32_rel*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_rel ReadElf64Rel(byte[] buffer, uint pos)
        {
            elf64_rel elfHeader = new elf64_rel();
            fixed (byte* p = buffer) {
                elf64_rel* ptr = (elf64_rel*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf32_rela ReadElf32Rela(byte[] buffer, uint pos)
        {
            elf32_rela elfHeader = new elf32_rela();
            fixed (byte* p = buffer) {
                elf32_rela* ptr = (elf32_rela*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }
        private static unsafe elf64_rela ReadElf64Rela(byte[] buffer, uint pos)
        {
            elf64_rela elfHeader = new elf64_rela();
            fixed (byte* p = buffer) {
                elf64_rela* ptr = (elf64_rela*)(p + pos);
                elfHeader = *ptr;
            }
            return elfHeader;
        }

        private static unsafe void WriteUint(byte[] buffer, uint pos, uint data)
        {
            fixed (byte* p = buffer) {
                uint* ptr = (uint*)(p + pos);
                *ptr = data;
            }
        }
        private static unsafe void WriteUlong(byte[] buffer, uint pos, ulong data)
        {
            fixed (byte* p = buffer) {
                ulong* ptr = (ulong*)(p + pos);
                *ptr = data;
            }
        }
        private static unsafe void WriteElfIdent(byte[] buffer, uint pos, elf_ident ident)
        {
            fixed (byte* p = buffer) {
                elf_ident* ptr = (elf_ident*)(p + pos);
                *ptr = ident;
            }
        }
        private static unsafe void WriteElf32Header(byte[] buffer, uint pos, elf32_hdr header)
        {
            fixed (byte* p = buffer) {
                elf32_hdr* ptr = (elf32_hdr*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Header(byte[] buffer, uint pos, elf64_hdr header)
        {
            fixed (byte* p = buffer) {
                elf64_hdr* ptr = (elf64_hdr*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf32Phdr(byte[] buffer, uint pos, elf32_phdr header)
        {
            fixed (byte* p = buffer) {
                elf32_phdr* ptr = (elf32_phdr*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Phdr(byte[] buffer, uint pos, elf64_phdr header)
        {
            fixed (byte* p = buffer) {
                elf64_phdr* ptr = (elf64_phdr*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf32Shdr(byte[] buffer, uint pos, elf32_shdr header)
        {
            fixed (byte* p = buffer) {
                elf32_shdr* ptr = (elf32_shdr*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Shdr(byte[] buffer, uint pos, elf64_shdr header)
        {
            fixed (byte* p = buffer) {
                elf64_shdr* ptr = (elf64_shdr*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf32Dyn(byte[] buffer, uint pos, elf32_dyn header)
        {
            fixed (byte* p = buffer) {
                elf32_dyn* ptr = (elf32_dyn*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Dyn(byte[] buffer, uint pos, elf64_dyn header)
        {
            fixed (byte* p = buffer) {
                elf64_dyn* ptr = (elf64_dyn*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf32Sym(byte[] buffer, uint pos, elf32_sym header)
        {
            fixed (byte* p = buffer) {
                elf32_sym* ptr = (elf32_sym*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Sym(byte[] buffer, uint pos, elf64_sym header)
        {
            fixed (byte* p = buffer) {
                elf64_sym* ptr = (elf64_sym*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf32Rel(byte[] buffer, uint pos, elf32_rel header)
        {
            fixed (byte* p = buffer) {
                elf32_rel* ptr = (elf32_rel*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Rel(byte[] buffer, uint pos, elf64_rel header)
        {
            fixed (byte* p = buffer) {
                elf64_rel* ptr = (elf64_rel*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf32Rela(byte[] buffer, uint pos, elf32_rela header)
        {
            fixed (byte* p = buffer) {
                elf32_rela* ptr = (elf32_rela*)(p + pos);
                *ptr = header;
            }
        }
        private static unsafe void WriteElf64Rela(byte[] buffer, uint pos, elf64_rela header)
        {
            fixed (byte* p = buffer) {
                elf64_rela* ptr = (elf64_rela*)(p + pos);
                *ptr = header;
            }
        }

        public static readonly char[] ELFMAG = new char[] { '\x7f', 'E', 'L', 'F' };
    }

    public enum enum_pt : uint
    {
        PT_NULL = 0,
        PT_LOAD = 1,
        PT_DYNAMIC = 2,
        PT_INTERP = 3,
        PT_NOTE = 4,
        PT_SHLIB = 5,
        PT_PHDR = 6,
        PT_TLS = 7,
        PT_LOOS = 0x60000000,
        PT_HIOS = 0x6fffffff,
        PT_LOPROC = 0x70000000,
        PT_HIPROC = 0x7fffffff,
        PT_GNU_EH_FRAME = 0x6474e550,
        PT_GNU_STACK = (PT_LOOS + 0x474e551),
    }

    public enum enum_et : uint
    {
        ET_NONE = 0,
        ET_REL = 1,
        ET_EXEC = 2,
        ET_DYN = 3,
        ET_CORE = 4,
        ET_LOPROC = 0xff00,
        ET_HIPROC = 0xffff,
    }

    public enum enum_dt : uint
    {
        DT_NULL = 0,
        DT_NEEDED = 1,
        DT_PLTRELSZ = 2,
        DT_PLTGOT = 3,
        DT_HASH = 4,
        DT_STRTAB = 5,
        DT_SYMTAB = 6,
        DT_RELA = 7,
        DT_RELASZ = 8,
        DT_RELAENT = 9,
        DT_STRSZ = 10,
        DT_SYMENT = 11,
        DT_INIT = 12,
        DT_FINI = 13,
        DT_SONAME = 14,
        DT_RPATH = 15,
        DT_SYMBOLIC = 16,
        DT_REL = 17,
        DT_RELSZ = 18,
        DT_RELENT = 19,
        DT_PLTREL = 20,
        DT_DEBUG = 21,
        DT_TEXTREL = 22,
        DT_JMPREL = 23,
        DT_BIND_NOW = 24,
        DT_INIT_ARRAY = 25,
        DT_FINI_ARRAY = 26,
        DT_INIT_ARRAYSZ = 27,
        DT_FINI_ARRAYSZ = 28,
        DT_RUNPATH = 29,
        DT_FLAGS = 30,
        DT_ENCODING = 32,
        DT_PREINIT_ARRAY = 32,
        DT_PREINIT_ARRAYSZ = 33,
        DT_SYMTAB_SHNDX = 34,
        DT_NUM = 35,
        DT_LOOS = 0x6000000d,
        DT_HIOS = 0x6ffff000,
        DT_LOPROC = 0x70000000,
        DT_HIPROC = 0x7fffffff,
        DT_PROCNUM = 0x36,
    }

    public enum enum_arm32_reloc : uint
    {
        R_ARM_NONE = 0,
        R_ARM_PC24 = 1,
        R_ARM_ABS32 = 2,
        R_ARM_REL32 = 3,
        R_ARM_PC13 = 4,
        R_ARM_ABS16 = 5,
        R_ARM_ABS12 = 6,
        R_ARM_THM_ABS5 = 7,
        R_ARM_ABS8 = 8,
        R_ARM_SBREL32 = 9,
        R_ARM_THM_PC22 = 10,
        R_ARM_THM_PC8 = 11,
        R_ARM_AMP_VCALL9 = 12,
        R_ARM_SWI24 = 13,
        R_ARM_TLS_DESC = 13,
        R_ARM_THM_SWI8 = 14,
        R_ARM_XPC25 = 15,
        R_ARM_THM_XPC22 = 16,
        R_ARM_TLS_DTPMOD32 = 17,
        R_ARM_TLS_DTPOFF32 = 18,
        R_ARM_TLS_TPOFF32 = 19,
        R_ARM_COPY = 20,
        R_ARM_GLOB_DAT = 21,
        R_ARM_JUMP_SLOT = 22,
        R_ARM_RELATIVE = 23,
        R_ARM_GOTOFF = 24,
        R_ARM_GOTPC = 25,
        R_ARM_GOT32 = 26,
        R_ARM_PLT32 = 27,
        R_ARM_CALL = 28,
        R_ARM_JUMP24 = 29,
        R_ARM_THM_JUMP24 = 30,
        R_ARM_BASE_ABS = 31,
        R_ARM_ALU_PCREL_7_0 = 32,
        R_ARM_ALU_PCREL_15_8 = 33,
        R_ARM_ALU_PCREL_23_15 = 34,
        R_ARM_LDR_SBREL_11_0 = 35,
        R_ARM_ALU_SBREL_19_12 = 36,
        R_ARM_ALU_SBREL_27_20 = 37,
        R_ARM_TARGET1 = 38,
        R_ARM_SBREL31 = 39,
        R_ARM_V4BX = 40,
        R_ARM_TARGET2 = 41,
        R_ARM_PREL31 = 42,
        R_ARM_MOVW_ABS_NC = 43,
        R_ARM_MOVT_ABS = 44,
        R_ARM_MOVW_PREL_NC = 45,
        R_ARM_MOVT_PREL = 46,
        R_ARM_THM_MOVW_ABS_NC = 47,
        R_ARM_THM_MOVT_ABS = 48,
        R_ARM_THM_MOVW_PREL_NC = 49,
        R_ARM_THM_MOVT_PREL = 50,
        R_ARM_THM_JUMP19 = 51,
        R_ARM_THM_JUMP6 = 52,
        R_ARM_THM_ALU_PREL_11_0 = 53,
        R_ARM_THM_PC12 = 54,
        R_ARM_ABS32_NOI = 55,
        R_ARM_REL32_NOI = 56,
        R_ARM_ALU_PC_G0_NC = 57,
        R_ARM_ALU_PC_G0 = 58,
        R_ARM_ALU_PC_G1_NC = 59,
        R_ARM_ALU_PC_G1 = 60,
        R_ARM_ALU_PC_G2 = 61,
        R_ARM_LDR_PC_G1 = 62,
        R_ARM_LDR_PC_G2 = 63,
        R_ARM_LDRS_PC_G0 = 64,
        R_ARM_LDRS_PC_G1 = 65,
        R_ARM_LDRS_PC_G2 = 66,
        R_ARM_LDC_PC_G0 = 67,
        R_ARM_LDC_PC_G1 = 68,
        R_ARM_LDC_PC_G2 = 69,
        R_ARM_ALU_SB_G0_NC = 70,
        R_ARM_ALU_SB_G0 = 71,
        R_ARM_ALU_SB_G1_NC = 72,
        R_ARM_ALU_SB_G1 = 73,
        R_ARM_ALU_SB_G2 = 74,
        R_ARM_LDR_SB_G0 = 75,
        R_ARM_LDR_SB_G1 = 76,
        R_ARM_LDR_SB_G2 = 77,
        R_ARM_LDRS_SB_G0 = 78,
        R_ARM_LDRS_SB_G1 = 79,
        R_ARM_LDRS_SB_G2 = 80,
        R_ARM_LDC_SB_G0 = 81,
        R_ARM_LDC_SB_G1 = 82,
        R_ARM_LDC_SB_G2 = 83,
        R_ARM_MOVW_BREL_NC = 84,
        R_ARM_MOVT_BREL = 85,
        R_ARM_MOVW_BREL = 86,
        R_ARM_THM_MOVW_BREL_NC = 87,
        R_ARM_THM_MOVT_BREL = 88,
        R_ARM_THM_MOVW_BREL = 89,
        R_ARM_TLS_GOTDESC = 90,
        R_ARM_TLS_CALL = 91,
        R_ARM_TLS_DESCSEQ = 92,
        R_ARM_THM_TLS_CALL = 93,
        R_ARM_PLT32_ABS = 94,
        R_ARM_GOT_ABS = 95,
        R_ARM_GOT_PREL = 96,
        R_ARM_GOT_BREL12 = 97,
        R_ARM_GOTOFF12 = 98,
        R_ARM_GOTRELAX = 99,
        R_ARM_GNU_VTENTRY = 100,
        R_ARM_GNU_VTINHERIT = 101,
        R_ARM_THM_PC11 = 102,
        R_ARM_THM_PC9 = 103,
        R_ARM_TLS_GD32 = 104,
        R_ARM_TLS_LDM32 = 105,
        R_ARM_TLS_LDO32 = 106,
        R_ARM_TLS_IE32 = 107,
        R_ARM_TLS_LE32 = 108,
        R_ARM_TLS_LDO12 = 109,
        R_ARM_TLS_LE12 = 110,
        R_ARM_TLS_IE12GP = 111,
        R_ARM_ME_TOO = 128,
        R_ARM_THM_TLS_DESCSEQ = 129,
        R_ARM_THM_TLS_DESCSEQ16 = 129,
        R_ARM_THM_TLS_DESCSEQ32 = 130,
        R_ARM_THM_GOT_BREL12 = 131,
        R_ARM_IRELATIVE = 160,
        R_ARM_RXPC25 = 249,
        R_ARM_RSBREL32 = 250,
        R_ARM_THM_RPC22 = 251,
        R_ARM_RREL32 = 252,
        R_ARM_RABS22 = 253,
        R_ARM_RPC24 = 254,
        R_ARM_RBASE = 255,
        R_ARM_NUM = 256
    }

    public enum enum_arm64_reloc : ulong
    {
        R_AARCH64_NONE = 0,
        R_AARCH64_P32_ABS32 = 1,
        R_AARCH64_P32_COPY = 180,
        R_AARCH64_P32_GLOB_DAT = 181,
        R_AARCH64_P32_JUMP_SLOT = 182,
        R_AARCH64_P32_RELATIVE = 183,
        R_AARCH64_P32_TLS_DTPMOD = 184,
        R_AARCH64_P32_TLS_DTPREL = 185,
        R_AARCH64_P32_TLS_TPREL = 186,
        R_AARCH64_P32_TLSDESC = 187,
        R_AARCH64_P32_IRELATIVE = 188,
        R_AARCH64_ABS64 = 257,
        R_AARCH64_ABS32 = 258,
        R_AARCH64_ABS16 = 259,
        R_AARCH64_PREL64 = 260,
        R_AARCH64_PREL32 = 261,
        R_AARCH64_PREL16 = 262,
        R_AARCH64_MOVW_UABS_G0 = 263,
        R_AARCH64_MOVW_UABS_G0_NC = 264,
        R_AARCH64_MOVW_UABS_G1 = 265,
        R_AARCH64_MOVW_UABS_G1_NC = 266,
        R_AARCH64_MOVW_UABS_G2 = 267,
        R_AARCH64_MOVW_UABS_G2_NC = 268,
        R_AARCH64_MOVW_UABS_G3 = 269,
        R_AARCH64_MOVW_SABS_G0 = 270,
        R_AARCH64_MOVW_SABS_G1 = 271,
        R_AARCH64_MOVW_SABS_G2 = 272,
        R_AARCH64_LD_PREL_LO19 = 273,
        R_AARCH64_ADR_PREL_LO21 = 274,
        R_AARCH64_ADR_PREL_PG_HI21 = 275,
        R_AARCH64_ADR_PREL_PG_HI21_NC = 276,
        R_AARCH64_ADD_ABS_LO12_NC = 277,
        R_AARCH64_LDST8_ABS_LO12_NC = 278,
        R_AARCH64_TSTBR14 = 279,
        R_AARCH64_CONDBR19 = 280,
        R_AARCH64_JUMP26 = 282,
        R_AARCH64_CALL26 = 283,
        R_AARCH64_LDST16_ABS_LO12_NC = 284,
        R_AARCH64_LDST32_ABS_LO12_NC = 285,
        R_AARCH64_LDST64_ABS_LO12_NC = 286,
        R_AARCH64_MOVW_PREL_G0 = 287,
        R_AARCH64_MOVW_PREL_G0_NC = 288,
        R_AARCH64_MOVW_PREL_G1 = 289,
        R_AARCH64_MOVW_PREL_G1_NC = 290,
        R_AARCH64_MOVW_PREL_G2 = 291,
        R_AARCH64_MOVW_PREL_G2_NC = 292,
        R_AARCH64_MOVW_PREL_G3 = 293,
        R_AARCH64_LDST128_ABS_LO12_NC = 299,
        R_AARCH64_MOVW_GOTOFF_G0 = 300,
        R_AARCH64_MOVW_GOTOFF_G0_NC = 301,
        R_AARCH64_MOVW_GOTOFF_G1 = 302,
        R_AARCH64_MOVW_GOTOFF_G1_NC = 303,
        R_AARCH64_MOVW_GOTOFF_G2 = 304,
        R_AARCH64_MOVW_GOTOFF_G2_NC = 305,
        R_AARCH64_MOVW_GOTOFF_G3 = 306,
        R_AARCH64_GOTREL64 = 307,
        R_AARCH64_GOTREL32 = 308,
        R_AARCH64_GOT_LD_PREL19 = 309,
        R_AARCH64_LD64_GOTOFF_LO15 = 310,
        R_AARCH64_ADR_GOT_PAGE = 311,
        R_AARCH64_LD64_GOT_LO12_NC = 312,
        R_AARCH64_LD64_GOTPAGE_LO15 = 313,
        R_AARCH64_TLSGD_ADR_PREL21 = 512,
        R_AARCH64_TLSGD_ADR_PAGE21 = 513,
        R_AARCH64_TLSGD_ADD_LO12_NC = 514,
        R_AARCH64_TLSGD_MOVW_G1 = 515,
        R_AARCH64_TLSGD_MOVW_G0_NC = 516,
        R_AARCH64_TLSLD_ADR_PREL21 = 517,
        R_AARCH64_TLSLD_ADR_PAGE21 = 518,
        R_AARCH64_TLSLD_ADD_LO12_NC = 519,
        R_AARCH64_TLSLD_MOVW_G1 = 520,
        R_AARCH64_TLSLD_MOVW_G0_NC = 521,
        R_AARCH64_TLSLD_LD_PREL19 = 522,
        R_AARCH64_TLSLD_MOVW_DTPREL_G2 = 523,
        R_AARCH64_TLSLD_MOVW_DTPREL_G1 = 524,
        R_AARCH64_TLSLD_MOVW_DTPREL_G1_NC = 525,
        R_AARCH64_TLSLD_MOVW_DTPREL_G0 = 526,
        R_AARCH64_TLSLD_MOVW_DTPREL_G0_NC = 527,
        R_AARCH64_TLSLD_ADD_DTPREL_HI12 = 528,
        R_AARCH64_TLSLD_ADD_DTPREL_LO12 = 529,
        R_AARCH64_TLSLD_ADD_DTPREL_LO12_NC = 530,
        R_AARCH64_TLSLD_LDST8_DTPREL_LO12 = 531,
        R_AARCH64_TLSLD_LDST8_DTPREL_LO12_NC = 532,
        R_AARCH64_TLSLD_LDST16_DTPREL_LO12 = 533,
        R_AARCH64_TLSLD_LDST16_DTPREL_LO12_NC = 534,
        R_AARCH64_TLSLD_LDST32_DTPREL_LO12 = 535,
        R_AARCH64_TLSLD_LDST32_DTPREL_LO12_NC = 536,
        R_AARCH64_TLSLD_LDST64_DTPREL_LO12 = 537,
        R_AARCH64_TLSLD_LDST64_DTPREL_LO12_NC = 538,
        R_AARCH64_TLSIE_MOVW_GOTTPREL_G1 = 539,
        R_AARCH64_TLSIE_MOVW_GOTTPREL_G0_NC = 540,
        R_AARCH64_TLSIE_ADR_GOTTPREL_PAGE21 = 541,
        R_AARCH64_TLSIE_LD64_GOTTPREL_LO12_NC = 542,
        R_AARCH64_TLSIE_LD_GOTTPREL_PREL19 = 543,
        R_AARCH64_TLSLE_MOVW_TPREL_G2 = 544,
        R_AARCH64_TLSLE_MOVW_TPREL_G1 = 545,
        R_AARCH64_TLSLE_MOVW_TPREL_G1_NC = 546,
        R_AARCH64_TLSLE_MOVW_TPREL_G0 = 547,
        R_AARCH64_TLSLE_MOVW_TPREL_G0_NC = 548,
        R_AARCH64_TLSLE_ADD_TPREL_HI12 = 549,
        R_AARCH64_TLSLE_ADD_TPREL_LO12 = 550,
        R_AARCH64_TLSLE_ADD_TPREL_LO12_NC = 551,
        R_AARCH64_TLSLE_LDST8_TPREL_LO12 = 552,
        R_AARCH64_TLSLE_LDST8_TPREL_LO12_NC = 553,
        R_AARCH64_TLSLE_LDST16_TPREL_LO12 = 554,
        R_AARCH64_TLSLE_LDST16_TPREL_LO12_NC = 555,
        R_AARCH64_TLSLE_LDST32_TPREL_LO12 = 556,
        R_AARCH64_TLSLE_LDST32_TPREL_LO12_NC = 557,
        R_AARCH64_TLSLE_LDST64_TPREL_LO12 = 558,
        R_AARCH64_TLSLE_LDST64_TPREL_LO12_NC = 559,
        R_AARCH64_TLSDESC_LD_PREL19 = 560,
        R_AARCH64_TLSDESC_ADR_PREL21 = 561,
        R_AARCH64_TLSDESC_ADR_PAGE21 = 562,
        R_AARCH64_TLSDESC_LD64_LO12 = 563,
        R_AARCH64_TLSDESC_ADD_LO12 = 564,
        R_AARCH64_TLSDESC_OFF_G1 = 565,
        R_AARCH64_TLSDESC_OFF_G0_NC = 566,
        R_AARCH64_TLSDESC_LDR = 567,
        R_AARCH64_TLSDESC_ADD = 568,
        R_AARCH64_TLSDESC_CALL = 569,
        R_AARCH64_TLSLE_LDST128_TPREL_LO12 = 570,
        R_AARCH64_TLSLE_LDST128_TPREL_LO12_NC = 571,
        R_AARCH64_TLSLD_LDST128_DTPREL_LO12 = 572,
        R_AARCH64_TLSLD_LDST128_DTPREL_LO12_NC = 573,
        R_AARCH64_COPY = 1024,
        R_AARCH64_GLOB_DAT = 1025,
        R_AARCH64_JUMP_SLOT = 1026,
        R_AARCH64_RELATIVE = 1027,
        R_AARCH64_TLS_DTPMOD = 1028,
        R_AARCH64_TLS_DTPREL = 1029,
        R_AARCH64_TLS_TPREL = 1030,
        R_AARCH64_TLSDESC = 1031,
        R_AARCH64_IRELATIVE = 1032,
    }

    public enum enum_stb : byte
    {
        STB_LOCAL = 0,
        STB_GLOBAL = 1,
        STB_WEAK = 2,
    }

    public enum enum_stt : byte
    {
        STT_NOTYPE = 0,
        STT_OBJECT = 1,
        STT_FUNC = 2,
        STT_SECTION = 3,
        STT_FILE = 4,
        STT_COMMON = 5,
        STT_TLS = 6,
    }

    public enum enum_pf : uint
    {
        PF_R = 0x4,
        PF_W = 0x2,
        PF_X = 0x1,
    }

    public enum enum_sht : uint
    {
        SHT_NULL = 0,
        SHT_PROGBITS = 1,
        SHT_SYMTAB = 2,
        SHT_STRTAB = 3,
        SHT_RELA = 4,
        SHT_HASH = 5,
        SHT_DYNAMIC = 6,
        SHT_NOTE = 7,
        SHT_NOBITS = 8,
        SHT_REL = 9,
        SHT_SHLIB = 10,
        SHT_DYNSYM = 11,
        SHT_INIT_ARRAY = 14,
        SHT_FINI_ARRAY = 15,
        SHT_PREINIT_ARRAY = 16,
        SHT_GROUP = 17,
        SHT_SYMTAB_SHNDX = 18,
        SHT_NUM = 19,
        SHT_LOPROC = 0x70000000,
        SHT_HIPROC = 0x7fffffff,
        SHT_LOUSER = 0x80000000,
        SHT_HIUSER = 0xffffffff,
    }

    public enum enum_shf : uint
    {
        SHF_WRITE = 0x1,
        SHF_ALLOC = 0x2,
        SHF_EXECINSTR = 0x4,
        SHF_MASKPROC = 0xf0000000,
    }

    public enum enum_shn : ushort
    {
        SHN_UNDEF = 0,
        SHN_LORESERVE = 0xff00,
        SHN_LOPROC = 0xff00,
        SHN_HIPROC = 0xff1f,
        SHN_ABS = 0xfff1,
        SHN_COMMON = 0xfff2,
        SHN_HIRESERVE = 0xffff,
    }

    public enum enum_elfclass : byte
    {
        ELFCLASSNONE = 0,
        ELFCLASS32 = 1,
        ELFCLASS64 = 2,
        ELFCLASSNUM = 3,
    }

    public enum enum_elfdata : byte
    {
        ELFDATANONE = 0,
        ELFDATA2LSB = 1,
        ELFDATA2MSB = 2,
    }

    public enum enum_ev : byte
    {
        EV_NONE = 0,
        EV_CURRENT = 1,
        EV_NUM = 2,
    }

    public enum enum_elfosabi : byte
    {
        ELFOSABI_NONE = 0,
        ELFOSABI_LINUX = 3,
    }

    public enum enum_NT : uint
    {
        NT_PRSTATUS = 1,
        NT_PRFPREG = 2,
        NT_PRPSINFO = 3,
        NT_TASKSTRUCT = 4,
        NT_AUXV = 6,
        NT_PRXFPREG = 0x46e62b7f,
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = sizeof(uint) * 2)]
    public struct elf32_dyn
    {
        [FieldOffset(0)]
        public uint d_tag;
        [FieldOffset(sizeof(uint))]
        public uint d_val;
        [FieldOffset(sizeof(uint))]
        public uint d_ptr;

        public static unsafe uint SizeOfStruct
        {
            get
            {
                return (uint)sizeof(elf32_dyn);
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = sizeof(ulong) * 2)]
    public struct elf64_dyn
    {
        [FieldOffset(0)]
        public ulong d_tag;
        [FieldOffset(sizeof(ulong))]
        public ulong d_val;
        [FieldOffset(sizeof(ulong))]
        public ulong d_ptr;

        public static unsafe uint SizeOfStruct
        {
            get
            {
                return (uint)sizeof(elf64_dyn);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 2)]
    public struct elf32_rel
    {
        public uint r_offset;
        public uint r_info;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(ulong) * 2)]
    public struct elf64_rel
    {
        public ulong r_offset;
        public ulong r_info;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 3)]
    public struct elf32_rela
    {
        public uint r_offset;
        public uint r_info;
        public uint r_addend;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(ulong) * 3)]
    public struct elf64_rela
    {
        public ulong r_offset;
        public ulong r_info;
        public ulong r_addend;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 3 + sizeof(byte) * 2 + sizeof(ushort))]
    public struct elf32_sym
    {
        public uint st_name;
        public uint st_value;
        public uint st_size;
        public byte st_info;
        public byte st_other;
        public ushort st_shndx;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) + sizeof(byte) * 2 + sizeof(ushort) + sizeof(ulong) * 2)]
    public struct elf64_sym
    {
        public uint st_name;
        public byte st_info;
        public byte st_other;
        public ushort st_shndx;
        public ulong st_value;
        public ulong st_size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct elf_ident
    {
        public byte e_ident_0;
        public byte e_ident_1;
        public byte e_ident_2;
        public byte e_ident_3;
        public byte e_class;
        public byte e_data;
        public byte e_version;
        public byte e_pad_s;
        public ulong e_Pad;

        public bool IsElf()
        {
            if(e_ident_0==ElfFile.ELFMAG[0] && 
                e_ident_1==ElfFile.ELFMAG[1] && 
                e_ident_2==ElfFile.ELFMAG[2] &&
                e_ident_3 == ElfFile.ELFMAG[3]) {
                    return true;
            } else {
                return false;
            }
        }
        public static unsafe uint SizeOfStruct
        {
            get
            {
                return (uint)sizeof(elf_ident);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(ushort) * 2 + sizeof(uint) * 5 + sizeof(ushort) * 6)]
    public struct elf32_hdr
    {
        public ushort e_type;
        public ushort e_machine;
        public uint e_version;
        public uint e_entry;
        public uint e_phoff;
        public uint e_shoff;
        public uint e_flags;
        public ushort e_ehsize;
        public ushort e_phentsize;
        public ushort e_phnum;
        public ushort e_shentsize;
        public ushort e_shnum;
        public ushort e_shstrndx;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(ushort) * 2 + sizeof(uint) * 2 + sizeof(ulong) * 3 + sizeof(ushort) * 6)]
    public struct elf64_hdr
    {
        public ushort e_type;
        public ushort e_machine;
        public uint e_version;
        public ulong e_entry;
        public ulong e_phoff;
        public ulong e_shoff;
        public uint e_flags;
        public ushort e_ehsize;
        public ushort e_phentsize;
        public ushort e_phnum;
        public ushort e_shentsize;
        public ushort e_shnum;
        public ushort e_shstrndx;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 8)]
    public struct elf32_phdr
    {
        public uint p_type;
        public uint p_offset;
        public uint p_vaddr;
        public uint p_paddr;
        public uint p_filesz;
        public uint p_memsz;
        public uint p_flags;
        public uint p_align;

        public void Align()
        {
            p_vaddr = CalcAlign(p_vaddr);
            p_paddr = p_vaddr;
            p_offset = CalcAlign(p_offset);
        }
        public uint CalcAlign(uint val)
        {
            if (p_align <= 1 || val % p_align == 0)
                return val;
            else
                return val - val % p_align + p_align;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 2 + sizeof(ulong) * 6)]
    public struct elf64_phdr
    {
        public uint p_type;
        public uint p_flags;
        public ulong p_offset;
        public ulong p_vaddr;
        public ulong p_paddr;
        public ulong p_filesz;
        public ulong p_memsz;
        public ulong p_align;

        public void Align()
        {
            p_vaddr = CalcAlign(p_vaddr);
            p_paddr = p_vaddr;
            p_offset = CalcAlign(p_offset);
        }
        public ulong CalcAlign(ulong val)
        {
            if (p_align <= 1 || val % p_align == 0)
                return val;
            else
                return val - val % p_align + p_align;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 10)]
    public struct elf32_shdr
    {
        public uint sh_name;
        public uint sh_type;
        public uint sh_flags;
        public uint sh_addr;
        public uint sh_offset;
        public uint sh_size;
        public uint sh_link;
        public uint sh_info;
        public uint sh_addralign;
        public uint sh_entsize;

        public void Align()
        {
            sh_addr = CalcAlign(sh_addr);
            sh_offset = CalcAlign(sh_offset);
        }
        public uint CalcAlign(uint val)
        {
            if (sh_addralign <= 1 || val % sh_addralign == 0)
                return val;
            else
                return val - val % sh_addralign + sh_addralign;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 4 + sizeof(ulong) * 6)]
    public struct elf64_shdr
    {
        public uint sh_name;
        public uint sh_type;
        public ulong sh_flags;
        public ulong sh_addr;
        public ulong sh_offset;
        public ulong sh_size;
        public uint sh_link;
        public uint sh_info;
        public ulong sh_addralign;
        public ulong sh_entsize;

        public void Align()
        {
            sh_addr = CalcAlign(sh_addr);
            sh_offset = CalcAlign(sh_offset);
        }
        public ulong CalcAlign(ulong val)
        {
            if (sh_addralign <= 1 || val % sh_addralign == 0)
                return val;
            else
                return val - val % sh_addralign + sh_addralign;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 3)]
    public struct elf32_note
    {
        public uint n_namesz;
        public uint n_descsz;
        public uint n_type;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(uint) * 3)]
    public struct elf64_note
    {
        public uint n_namesz;
        public uint n_descsz;
        public uint n_type;
    }
}
