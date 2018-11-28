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
        public void Load(string file, uint newSectionSize)
        {
            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            try {
                m_is_loaded = false;
                var buffer = br.ReadBytes((int)fs.Length);
                uint pos = 0;
                m_old_file_data = buffer;
                m_new_section_size = newSectionSize;
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
        public void AddDllCall(string so, string func, string arg)
        {
            m_DllCalls.Add(new DllCallInfo { SoName = so, FuncName = func, Arg = arg });
        }
        public void AddInitArrayCall(uint entry)
        {
            m_InitArrays.Add(entry);
        }
        public void Save(string file)
        {
            if (!m_is_loaded)
                return;

            uint newSectionSize = m_new_section_size;

            byte[] buffer = null;
            if (m_ident.e_class == (uint)enum_elfclass.ELFCLASS32) {
                buffer = Save32(newSectionSize);
            } else if (m_ident.e_class == (uint)enum_elfclass.ELFCLASS64) {
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
            
            m_elf32_shdrs.Clear();
            m_elf32_dyns.Clear();
            m_elf32_init_array.Clear();
            m_elf32_fini_array.Clear();
            m_movable_datas_32.Clear();
            pos = m_elf32_hdr.e_shoff;
            for (int i = 0; i < m_elf32_hdr.e_shnum; ++i) {
                var shdr = ReadElf32Shdr(buffer, pos);
                m_elf32_shdrs.Add(shdr);
                pos += m_elf32_hdr.e_shentsize;

                if (m_elf32_hdr.e_shstrndx > 0 && m_elf32_hdr.e_shstrndx == i) {
                    m_sh_strs = ParseStrings(buffer, (int)shdr.sh_offset, (int)shdr.sh_size);
                    m_sh_str_next_pos = shdr.sh_size;                    
                } else if (shdr.sh_type == (int)enum_sht.SHT_DYNAMIC) {
                    m_elf32_dyn_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var dyn = ReadElf32Dyn(buffer, shdr.sh_offset + dpos);
                        if (dyn.d_tag != (uint)enum_dt.DT_NULL) {
                            if (dyn.d_tag == (uint)enum_dt.DT_PLTREL) {
                                m_elf32_rel_plt_type = dyn.d_val;
                            } else if (dyn.d_tag == (uint)enum_dt.DT_PLTGOT) {
                                m_elf32_plt_got = dyn.d_ptr;
                            }
                            m_elf32_dyns.Add(dyn);
                        }
                    }
                } else if (shdr.sh_type == (int)enum_sht.SHT_INIT_ARRAY) {
                    m_elf32_init_array_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += sizeof(uint)) {
                        var data = ReadUint(buffer, shdr.sh_offset + dpos);
                        m_elf32_init_array.Add(data);
                    }
                } else if (shdr.sh_type == (int)enum_sht.SHT_FINI_ARRAY) {
                    m_elf32_fini_array_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += sizeof(uint)) {
                        var data = ReadUint(buffer, shdr.sh_offset + dpos);
                        m_elf32_fini_array.Add(data);
                    }
                }

                var v = shdr.sh_addr + shdr.sh_size;
                var o = shdr.sh_offset + shdr.sh_size;
                if (v > m_max_vaddr_32)
                    m_max_vaddr_32 = v;
                if (o > m_max_offset_32)
                    m_max_offset_32 = o;
            }
            uint interpName = 0;
            m_sh_strs.TryGetValue(".interp", out interpName);
            uint dynstrName = 0;
            m_sh_strs.TryGetValue(".dynstr", out dynstrName);
            uint reldynName = 0;
            m_sh_strs.TryGetValue(".rel.dyn", out reldynName);
            uint relpltName = 0;
            m_sh_strs.TryGetValue(".rel.plt", out relpltName);

            uint pltName = 0;
            if (!m_sh_strs.TryGetValue(".plt", out pltName)) {
                pltName = relpltName + 4;
            }
            uint gotName = 0;
            m_sh_strs.TryGetValue(".got", out gotName);
            uint strtabName = 0;
            m_sh_strs.TryGetValue(".strtab", out strtabName);

            int dynsymIndex = -1;
            int hashIndex = -1;
            int dynstrIndex = -1;
            int reldynIndex = -1;
            int relpltIndex = -1;
            
            m_bucket.Clear();
            m_chain.Clear();

            m_elf32_dyn_syms.Clear();
            m_elf32_sta_syms.Clear();
            m_elf32_dyn_rels.Clear();
            m_elf32_plt_rels.Clear();
            for (int i = 0; i < m_elf32_shdrs.Count; ++i) {
                var shdr = m_elf32_shdrs[i];                                
                if (shdr.sh_type == (uint)enum_sht.SHT_DYNSYM) {
                    dynsymIndex = i;
					
                    m_elf32_dyn_sym_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {					
                        var sym = ReadElf32Sym(buffer, shdr.sh_offset + dpos);
                        m_elf32_dyn_syms.Add(sym);
                    }
                } else if (shdr.sh_name == dynstrName && shdr.sh_type == (uint)enum_sht.SHT_STRTAB) {
                    dynstrIndex = i;
					
                    m_elf32_dyn_str_shdr = shdr;
                    m_dyn_strs = ParseStrings(buffer, (int)shdr.sh_offset, (int)shdr.sh_size);
                    m_dyn_str_next_pos = shdr.sh_size;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_HASH) {
                    hashIndex = i;
					
                    m_elf32_hash_shdr = shdr;
                    uint dpos = shdr.sh_offset;
                    uint nbucket = ReadUint(buffer, dpos);
                    dpos += sizeof(uint);
                    uint nchain = ReadUint(buffer, dpos);
                    dpos += sizeof(uint);
                    for (int ix = 0; ix < nbucket; ++ix) {
                        var val = ReadUint(buffer, dpos);
                        m_bucket.Add((int)val);
                        dpos += sizeof(uint);
                    }
                    for (int ix = 0; ix < nchain; ++ix) {
                        var val = ReadUint(buffer, dpos);
                        m_chain.Add((int)val);
                        dpos += sizeof(uint);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_SYMTAB) {
                    m_elf32_sta_sym_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var sym = ReadElf32Sym(buffer, shdr.sh_offset + dpos);
                        m_elf32_sta_syms.Add(sym);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == reldynName) {
                    reldynIndex = i;

                    m_elf32_dyn_rel_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var rel = ReadElf32Rel(buffer, shdr.sh_offset + dpos);
                        m_elf32_dyn_rels.Add(rel);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == relpltName) {
                    relpltIndex = i;

                    m_elf32_plt_rel_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += shdr.sh_entsize) {
                        var rel = ReadElf32Rel(buffer, shdr.sh_offset + dpos);
                        m_elf32_plt_rels.Add(rel);
                    }
                } else if (shdr.sh_type == (uint)enum_sht.SHT_PROGBITS && shdr.sh_name == pltName) {
                    m_elf32_plt_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_PROGBITS && shdr.sh_name == gotName) {
                    m_elf32_got_shdr = shdr;
                    for (uint dpos = 0; dpos < shdr.sh_size; dpos += sizeof(uint)) {
                        var val = ReadUint(buffer, shdr.sh_offset + dpos);
                        m_elf32_gots.Add(val);
                    }
                } else if (shdr.sh_name == strtabName && shdr.sh_type == (uint)enum_sht.SHT_STRTAB) {
                    m_sta_strs = ParseStrings(buffer, (int)shdr.sh_offset, (int)shdr.sh_size);
                    m_sta_str_next_pos = shdr.sh_size;
                } else if (dynsymIndex < 0 || hashIndex < 0 || dynstrIndex < 0 || reldynIndex < 0 || relpltIndex < 0) {
                    if (shdr.sh_name == interpName && shdr.sh_type == (uint)enum_sht.SHT_PROGBITS
                        || shdr.sh_type == (uint)enum_sht.SHT_NOTE
                        || shdr.sh_type == (uint)enum_sht.SHT_GNU_VERSYM
                        || shdr.sh_type == (uint)enum_sht.SHT_GNU_VERDEF
                        || shdr.sh_type == (uint)enum_sht.SHT_GNU_VERNEED) {
                        movable_data_info_32 info = new movable_data_info_32();
                        info.shdr = shdr;
                        var data = new byte[shdr.sh_size];
                        Array.Copy(buffer, shdr.sh_offset, data, 0, shdr.sh_size);
                        info.data = data;
                        m_movable_datas_32.Add(info);
                    } else if (shdr.sh_type != (uint)enum_sht.SHT_NULL) {
                        ScriptProcessor.ErrorTxts.Add("Can't add segment !");
                    }
                }
            }
        }
        private byte[] Save32(uint newSectionSize)
        {
            uint dlopenPlt = FindPlt32("dlopen");
            uint dlsymPlt = FindPlt32("dlsym");
            uint dlclosePlt = FindPlt32("dlclose");
            uint dlerrorPlt = FindPlt32("dlerror");
                        
            //.plt与.got之间以及很多内部函数里面，使用了代码到数据的相对偏移访问got表，所以这些数据都不可以移动。
            //所以目前的修改实际上只能增加段与节，并不能利用现有的elf文件的动态链接机制。
            //目前的修改策略：
            //1、将.interp、.dynsym、.dynstr、.hash节（及中间不重要的note节）移动到新加的段里，为修改phdr提供空间（一般够加很多个段了，每个段需要0x20个字节，实验发现phdr不能挪走）。
            //2、增加一个新段与一个新节用于写入代码或数据。
            //3、增加一个init array入口，用于调用写入的代码。
            //4、修改由移动引发的重定位数据。

            uint vaddr = (m_max_vaddr_32 + 0x1000) / 0x1000 * 0x1000;
            uint offset = (m_max_offset_32 + 0x1000) / 0x1000 * 0x1000;
            
            uint textName = 0;
            m_sh_strs.TryGetValue(".text", out textName);
            uint interpName = 0;
            m_sh_strs.TryGetValue(".interp", out interpName);
            uint dynstrName = 0;
            m_sh_strs.TryGetValue(".dynstr", out dynstrName);
            uint reldynName = 0;
            m_sh_strs.TryGetValue(".rel.dyn", out reldynName);
            uint relpltName = 0;
            m_sh_strs.TryGetValue(".rel.plt", out relpltName);
            //phdr与dyn段移到文件尾，再加一个新的LOAD段，把字符串表挪过来
            int phdrIndex = -1;
            int interpIndex = -1;
            int noteIndex = -1;
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
                } else if (type == (uint)enum_pt.PT_INTERP || type == (uint)enum_pt.PT_NOTE) {
                    for (int ix = 0; ix < m_movable_datas_32.Count; ++ix) {
                        var shdr = m_movable_datas_32[ix].shdr;
                        if (shdr.sh_name == interpName && shdr.sh_type == (uint)enum_sht.SHT_PROGBITS && phdr.p_offset == shdr.sh_offset && phdr.p_filesz == shdr.sh_size) {
                            interpIndex = i;
                        } else if (shdr.sh_type == (uint)enum_sht.SHT_NOTE && phdr.p_offset == shdr.sh_offset && phdr.p_filesz == shdr.sh_size) {
                            noteIndex = i;
                        }
                    }
                }
            }

            int lastTextIndex = -1;
            for (int i = 0; i < m_elf32_shdrs.Count; ++i) {
                if (textName == m_elf32_shdrs[i].sh_name) {
                    lastTextIndex = i;
                }
            }

            int newInitIndex = m_elf32_init_array.Count;
            foreach (var entry in m_InitArrays) {
                m_elf32_init_array.Add(vaddr + entry);
            }

            m_elf32_hdr.e_phnum += 2;
            ++m_elf32_hdr.e_shnum;

            m_elf32_init_array_shdr.sh_size += (uint)m_InitArrays.Count * sizeof(uint);
            m_elf32_dyn_rel_shdr.sh_size += (uint)m_InitArrays.Count * m_elf32_dyn_rel_shdr.sh_entsize;
            var oldInitArrayAddr = m_elf32_init_array_shdr.sh_addr;
            var oldFiniArrayAddr = m_elf32_fini_array_shdr.sh_addr;
            
            elf32_shdr newSection = m_elf32_shdrs[lastTextIndex];
            newSection.sh_addr = vaddr;
            newSection.sh_offset = offset;
            newSection.sh_size = newSectionSize;
            newSection.Align();

            elf32_phdr newSegment1 = m_elf32_phdrs[lastLoadIndex];
            newSegment1.p_vaddr = vaddr;
            newSegment1.p_paddr = vaddr;
            newSegment1.p_offset = offset;
            newSegment1.p_filesz = newSectionSize;
            newSegment1.p_memsz = newSectionSize;
            newSegment1.p_flags = (uint)enum_pf.PF_R | (uint)enum_pf.PF_X;
            newSegment1.Align();

            elf32_phdr newSegment2 = m_elf32_phdrs[lastLoadIndex];
            newSegment2.p_vaddr = vaddr + newSectionSize;
            newSegment2.p_paddr = vaddr + newSectionSize;
            newSegment2.p_offset = offset + newSectionSize;
            newSegment2.p_flags = (uint)enum_pf.PF_R | (uint)enum_pf.PF_W;
            newSegment2.Align();

            //计算移动后的虚地址与文件偏移，还是按照在原来文件里的节的顺序处理（原始的顺序是递增的，移动后保证不了所有节仍然是递增的，这里保证移动过的节之间是递增的，未移动的节之间也是递增的）
            elf32_shdr lastShdr = newSection;
            lastShdr.sh_addr = newSegment2.p_vaddr;
            lastShdr.sh_offset = newSegment2.p_offset;
            lastShdr.sh_size = 0;
            for (int i = 0; i < m_elf32_shdrs.Count; ++i) {
                //注意：这里只使用原始表里shdr的type与顺序，修改都是使用独立的变量记录的
                var shdr = m_elf32_shdrs[i];
                if (shdr.sh_type == (uint)enum_sht.SHT_DYNSYM) {
                    //dynsym节
                    shdr = m_elf32_dyn_sym_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size; 
                    shdr.sh_offset = lastShdr.sh_offset+lastShdr.sh_size; 
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_dyn_sym_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_STRTAB && shdr.sh_name == dynstrName) {
                    //dynstr节
                    shdr = m_elf32_dyn_str_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_dyn_str_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_HASH) {
                    shdr = m_elf32_hash_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_hash_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == reldynName) {
                    shdr = m_elf32_dyn_rel_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_dyn_rel_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == relpltName) {
                    shdr = m_elf32_plt_rel_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_plt_rel_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_DYNAMIC) {
                    shdr = m_elf32_dyn_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_dyn_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_INIT_ARRAY) {
                    shdr = m_elf32_init_array_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_init_array_shdr = shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_FINI_ARRAY) {
                    shdr = m_elf32_fini_array_shdr;

                    shdr.sh_addr = lastShdr.sh_addr + lastShdr.sh_size;
                    shdr.sh_offset = lastShdr.sh_offset + lastShdr.sh_size;
                    shdr.Align();
                    lastShdr = shdr;

                    m_elf32_fini_array_shdr = shdr;
                }
            }
                        
            //dynamic段
            if (dynIndex >= 0) {
                var phdr = m_elf32_phdrs[dynIndex];
                phdr.p_vaddr = m_elf32_dyn_shdr.sh_addr;
                phdr.p_paddr = phdr.p_vaddr;
                phdr.p_offset = m_elf32_dyn_shdr.sh_offset;

                if (phdr.p_align != m_elf32_dyn_shdr.sh_addralign) {
                    ScriptProcessor.ErrorTxts.Add(string.Format("dynamic segement align {0} != dynamic section align {1}, use section align, maybe something wrong !", phdr.p_align, m_elf32_dyn_shdr.sh_addralign));
                    phdr.p_align = m_elf32_dyn_shdr.sh_addralign;
                }

                m_elf32_phdrs[dynIndex] = phdr;
            }
            
            //每个新加的init_array需要添加一个rel
            int baseIx = newInitIndex;
            for (int i = 0; i < m_InitArrays.Count; ++i) {
                var rel = new elf32_rel();
                rel.r_offset = m_elf32_init_array_shdr.sh_addr + (uint)(baseIx + i) * sizeof(uint);
                rel.r_info = ELF32_R_INFO(0, (uint)enum_arm32_reloc.R_ARM_RELATIVE);
                m_elf32_dyn_rels.Add(rel);
            }

            //注意，这里只能对挪过来的部分进行重定位(新加的并没有旧的对应的地址，如果执行会把旧的合法地址定位到新增加的内容上！)
            //init_array
            for (int i = 0; i < baseIx; ++i) {
                var oa = oldInitArrayAddr + (uint)(i * sizeof(uint));
                var na = m_elf32_init_array_shdr.sh_addr + (uint)(i * sizeof(uint));
                Relocate32(oa, na);
            }
            //fini_array
            for (uint o = 0; o < m_elf32_fini_array_shdr.sh_size; o += sizeof(uint)) {
                var oa = oldFiniArrayAddr + o;
                var na = m_elf32_fini_array_shdr.sh_addr + o;
                Relocate32(oa, na);
            }
			
            //文件大小确定
            uint newOffset = lastShdr.sh_offset + lastShdr.sh_size;
            uint shdrSize = (uint)(m_elf32_hdr.e_shentsize * m_elf32_hdr.e_shnum);
            m_elf32_hdr.e_shoff = newOffset;
            uint fullSize = newOffset + shdrSize;

            newSegment2.p_filesz = fullSize - newSegment2.p_offset;
            newSegment2.p_memsz = newSegment2.p_filesz;
            newSegment2.p_flags = (uint)enum_pf.PF_R | (uint)enum_pf.PF_W;
            newSegment2.Align();
                        
            //phdr段、INTERP与NOTE段
            if (phdrIndex >= 0) {
                var phdr = m_elf32_phdrs[phdrIndex];
                phdr.p_filesz += m_elf32_hdr.e_phentsize * (uint)2;
                phdr.p_memsz += m_elf32_hdr.e_phentsize * (uint)2;
                m_elf32_phdrs[phdrIndex] = phdr;

                elf32_shdr lastMovableShdr = new elf32_shdr();
                for (int i = 0; i < m_movable_datas_32.Count; ++i) {
                    var shdr = m_movable_datas_32[i].shdr;
                    if (i == 0) {
                        shdr.sh_addr = phdr.p_vaddr + phdr.p_memsz;
                        shdr.sh_offset = phdr.p_offset + phdr.p_filesz;
                        shdr.Align();
                    } else {
                        shdr.sh_addr = lastMovableShdr.sh_addr + lastMovableShdr.sh_size;
                        shdr.sh_offset = lastMovableShdr.sh_offset + lastMovableShdr.sh_size;
                        shdr.Align();
                    }
                    m_movable_datas_32[i].shdr = shdr;
                    lastMovableShdr = shdr;

                    if (shdr.sh_name == interpName && interpIndex >= 0) {
                        var temp = m_elf32_phdrs[interpIndex];
                        temp.p_vaddr = shdr.sh_addr;
                        temp.p_paddr = temp.p_vaddr;
                        temp.p_offset = shdr.sh_offset;
                        m_elf32_phdrs[interpIndex] = temp;
                    } else if (shdr.sh_type == (uint)enum_sht.SHT_NOTE && noteIndex >= 0) {
                        var temp = m_elf32_phdrs[noteIndex];
                        temp.p_vaddr = shdr.sh_addr;
                        temp.p_paddr = temp.p_vaddr;
                        temp.p_offset = shdr.sh_offset;
                        m_elf32_phdrs[noteIndex] = temp;
                    }
                }
            }

            //修正dynamic节里的数据
            for (int i = 0; i < m_elf32_dyns.Count; ++i) {
                var dyn = m_elf32_dyns[i];
                if (dyn.d_tag == (uint)enum_dt.DT_STRTAB) {
                    dyn.d_ptr = m_elf32_dyn_str_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_STRSZ) {
                    dyn.d_val = m_elf32_dyn_str_shdr.sh_size;
                } else if (dyn.d_tag == (uint)enum_dt.DT_SYMTAB) {
                    dyn.d_ptr = m_elf32_dyn_sym_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_SYMENT) {
                    dyn.d_val = m_elf32_dyn_sym_shdr.sh_entsize;
                } else if (dyn.d_tag == (uint)enum_dt.DT_ARM_SYMTABSZ) {
                    dyn.d_val = m_elf32_dyn_sym_shdr.sh_size;
                } else if (dyn.d_tag == (uint)enum_dt.DT_HASH) {
                    dyn.d_ptr = m_elf32_hash_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_REL) {
                    dyn.d_ptr = m_elf32_dyn_rel_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_RELSZ) {
                    dyn.d_val = m_elf32_dyn_rel_shdr.sh_size;
                } else if (dyn.d_tag == (uint)enum_dt.DT_JMPREL) {
                    dyn.d_ptr = m_elf32_plt_rel_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_PLTRELSZ) {
                    dyn.d_val = m_elf32_plt_rel_shdr.sh_size;
                } else if (dyn.d_tag == (uint)enum_dt.DT_INIT_ARRAY) {
                    dyn.d_ptr = m_elf32_init_array_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_INIT_ARRAYSZ) {
                    dyn.d_val = m_elf32_init_array_shdr.sh_size;
                } else if (dyn.d_tag == (uint)enum_dt.DT_FINI_ARRAY) {
                    dyn.d_ptr = m_elf32_fini_array_shdr.sh_addr;
                } else if (dyn.d_tag == (uint)enum_dt.DT_FINI_ARRAYSZ) {
                    dyn.d_val = m_elf32_fini_array_shdr.sh_size;
                } else {
                    for (int ix = 0; ix < m_movable_datas_32.Count; ++ix) {
                        var movable = m_movable_datas_32[ix].shdr;
                        if (dyn.d_tag==(uint)enum_dt.DT_VERSYM && movable.sh_type==(uint)enum_sht.SHT_GNU_VERSYM) {
                            dyn.d_ptr = movable.sh_addr;
                        } else if (dyn.d_tag == (uint)enum_dt.DT_VERDEF && movable.sh_type == (uint)enum_sht.SHT_GNU_VERDEF) {
                            dyn.d_ptr = movable.sh_addr;
                        } else if (dyn.d_tag == (uint)enum_dt.DT_VERNEED && movable.sh_type == (uint)enum_sht.SHT_GNU_VERNEED) {
                            dyn.d_ptr = movable.sh_addr;
                        }
                    }
                }
                m_elf32_dyns[i] = dyn;
            }

            //将修改的section头写入原来的头列表里
            for (int i = 0; i < m_elf32_shdrs.Count; ++i) {
                var shdr = m_elf32_shdrs[i];
                uint oa = shdr.sh_addr;
                if (shdr.sh_type == (uint)enum_sht.SHT_DYNSYM) {
                    shdr = m_elf32_dyn_sym_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_STRTAB && shdr.sh_name == dynstrName) {
                    shdr = m_elf32_dyn_str_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_HASH) {
                    shdr = m_elf32_hash_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == reldynName) {
                    shdr = m_elf32_dyn_rel_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_REL && shdr.sh_name == relpltName) {
                    shdr = m_elf32_plt_rel_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_DYNAMIC) {
                    shdr = m_elf32_dyn_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_INIT_ARRAY) {
                    shdr = m_elf32_init_array_shdr;
                } else if (shdr.sh_type == (uint)enum_sht.SHT_FINI_ARRAY) {
                    shdr = m_elf32_fini_array_shdr;
                } else {
                    for (int ix = 0; ix < m_movable_datas_32.Count; ++ix) {
                        var movable = m_movable_datas_32[ix].shdr;
                        if (shdr.sh_name == movable.sh_name && shdr.sh_type == movable.sh_type) {
                            shdr = movable;
                        }
                    }
                }
                m_elf32_shdrs[i] = shdr;
                uint na = shdr.sh_addr;
                Relocate32(oa, na);
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
            WriteElf32Phdr(buffer, pos, newSegment1);
            pos += m_elf32_hdr.e_phentsize;
            WriteElf32Phdr(buffer, pos, newSegment2);
            pos += m_elf32_hdr.e_phentsize;
            //.interp或可能的.note
            for (int i = 0; i < m_movable_datas_32.Count; ++i) {
                var shdr = m_movable_datas_32[i].shdr;
                var data = m_movable_datas_32[i].data;
                pos = shdr.sh_offset;
                Array.Copy(data, 0, buffer, pos, data.Length);
            }
            //写入新加的segment
            pos = newSection.sh_offset;
            if (dlopenPlt > 0 && dlsymPlt > 0 && m_DllCalls.Count > 0) {
                foreach (var dllCall in m_DllCalls) {
                    var bytes = GenShellCodeTemplate();
                    uint code = CalcBlCode(vaddr + c_offset_dlopen, dlopenPlt);
                    WriteUint(bytes, c_offset_dlopen, code);
                    code = CalcBlCode(vaddr + c_offset_dlsym, dlsymPlt);
                    WriteUint(bytes, c_offset_dlsym, code);
                    if (dlclosePlt > 0) {
                        code = CalcBlCode(vaddr + c_offset_dlclose, dlclosePlt);
                        WriteUint(bytes, c_offset_dlclose, code);
                    } else {
                        //1EFF2FE1 bx lr
                        WriteUint(bytes, c_offset_dlclose, 0xe12fff1e);
                    }
                    if (dlerrorPlt > 0) {
                        code = CalcBlCode(vaddr + c_offset_dlerror_1, dlerrorPlt);
                        WriteUint(bytes, c_offset_dlerror_1, code);
                        code = CalcBlCode(vaddr + c_offset_dlerror_2, dlerrorPlt);
                        WriteUint(bytes, c_offset_dlerror_2, code);
                    } else {
                        //00F020E3 nop
                        //1EFF2FE1 bx lr
                        WriteUint(bytes, c_offset_dlerror_1, 0xe320f000);
                        WriteUint(bytes, c_offset_dlerror_2, 0xe12fff1e);
                    }

                    if (dllCall.SoName.Length <= c_max_sopath) {
                        var nameBytes = Encoding.ASCII.GetBytes(dllCall.SoName);
                        Array.Copy(nameBytes, 0, bytes, c_offset_sopath, nameBytes.Length);
                        bytes[c_offset_sopath + nameBytes.Length] = 0;
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("sopath '{0}' length > {1} !", dllCall.SoName, c_max_sopath));
                    }

                    if (dllCall.FuncName.Length <= c_max_func) {
                        var nameBytes = Encoding.ASCII.GetBytes(dllCall.FuncName);
                        Array.Copy(nameBytes, 0, bytes, c_offset_func, nameBytes.Length);
                        bytes[c_offset_func + nameBytes.Length] = 0;
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("func '{0}' length > {1} !", dllCall.FuncName, c_max_func));
                    }

                    if (dllCall.Arg.Length <= c_max_arg) {
                        var nameBytes = Encoding.ASCII.GetBytes(dllCall.Arg);
                        Array.Copy(nameBytes, 0, bytes, c_offset_arg, nameBytes.Length);
                        bytes[c_offset_arg + nameBytes.Length] = 0;
                    } else {
                        ScriptProcessor.ErrorTxts.Add(string.Format("arg '{0}' length > {1} !", dllCall.Arg, c_max_arg));
                    }

                    Array.Copy(bytes, 0, buffer, pos, bytes.Length);
                    pos += (uint)bytes.Length;
                }
            } else {
                ScriptProcessor.ErrorTxts.Add(string.Format("target file must import：dlopen {0:X} dlsym {1:X} dlclose {2:X} dlerror {3:X} !", dlopenPlt, dlsymPlt, dlclosePlt, dlerrorPlt));
                //留空，供手工修改
                for (int i = 0; i < newSection.sh_size / sizeof(uint); ++i) {
                    WriteUint(buffer, (uint)(pos + i * sizeof(uint)), i % 2 == 0 ? 0xe92d40f0 : 0xe8bd80f0);
                }
            }
            //写入挪过来的dynstr、hash、dynamic、init_array、fini_array
            pos = m_elf32_dyn_str_shdr.sh_offset;
            foreach (var pair in m_dyn_strs) {
                var bytes = Encoding.ASCII.GetBytes(pair.Key);
                var o = pair.Value;
                Array.Copy(bytes, 0, buffer, pos + o, bytes.Length);
                buffer[pos + o + bytes.Length] = 0;
            }
            pos = m_elf32_hash_shdr.sh_offset;
            WriteUint(buffer, pos, (uint)m_bucket.Count);
            pos += sizeof(uint);
            WriteUint(buffer, pos, (uint)m_chain.Count);
            pos += sizeof(uint);
            foreach (var val in m_bucket) {
                WriteUint(buffer, pos, (uint)val);
                pos += sizeof(uint);
            }
            foreach (var val in m_chain) {
                WriteUint(buffer, pos, (uint)val);
                pos += sizeof(uint);
            }
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
            pos = m_elf32_fini_array_shdr.sh_offset;
            foreach (var val in m_elf32_fini_array) {
                WriteUint(buffer, pos, val);
                pos += sizeof(uint);
            }
            //写入与重定位影响相关的数据
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
            for (int i = 0; i < m_elf32_shdrs.Count; ++i) {
                var shdr = m_elf32_shdrs[i];
                WriteElf32Shdr(buffer, pos, shdr);
                pos += m_elf32_hdr.e_shentsize;
            }
            WriteElf32Shdr(buffer, pos, newSection);
            return buffer;
        }
        
        private void Load64(byte[] buffer, uint pos)
        {
        }
        private byte[] Save64(uint newSectionSize)
        {
            return null;
        }
        
        private uint FindPlt32(string funcName)
        {
            bool isDyn;
            uint name = FindName(funcName, out isDyn);
            if (name > 0 && isDyn) {
                int symIndex = FindSym32(name, isDyn);
                var rel = FindRel32(symIndex, isDyn);
                if (rel.r_offset > 0 && m_elf32_plt_got > 0) {
                    uint pltIndex = (rel.r_offset - m_elf32_plt_got) / sizeof(uint) - 3;
                    return m_elf32_plt_shdr.sh_addr + 0x14 + pltIndex * 0x0c;
                }
            }
            return 0;
        }
        private elf32_rel FindRel32(int symIndex, bool isDyn)
        {
            if (isDyn) {
                foreach (var rel in m_elf32_plt_rels) {
                    var index = ELF32_R_SYM(rel.r_info);
                    if (index == symIndex)
                        return rel;
                }
            } else {
                foreach (var rel in m_elf32_dyn_rels) {
                    var index = ELF32_R_SYM(rel.r_info);
                    if (index == symIndex)
                        return rel;                    
                }
            }
            return new elf32_rel();
        }
        private elf32_rel FindRel32(uint address, out bool isPlt)
        {
            isPlt = false;
            foreach (var rel in m_elf32_plt_rels) {
                if (address == rel.r_offset) {
                    isPlt = true;
                    return rel;
                }
            }
            foreach (var rel in m_elf32_dyn_rels) {
                if (address == rel.r_offset)
                    return rel;
            }
            return new elf32_rel();
        }
        private int FindSym32(uint name, bool isDyn)
        {
            if (isDyn) {
                for (int i = 0; i < m_elf32_dyn_syms.Count;++i ) {
                    var sym = m_elf32_dyn_syms[i];
                    if (sym.st_name == name)
                        return i;
                }
            } else {
                for (int i = 0; i < m_elf32_sta_syms.Count; ++i) {
                    var sym = m_elf32_sta_syms[i];
                    if (sym.st_name == name)
                        return i;
                }
            }
            return 0;
        }
        private uint FindName(string funcName, out bool isDyn)
        {
            //先找动态链接的名字
            uint index = 0;
            isDyn = false;
            if (m_dyn_strs.TryGetValue(funcName, out index)) {
                isDyn = true;
                return index;
            }
            //再找静态符号名
            if (m_sta_strs.TryGetValue(funcName, out index)) {
                return index;
            }
            //再考虑遍历查找（可能名字是另一个名字的后缀部分）
            foreach (var pair in m_dyn_strs) {
                var key = pair.Key;
                var val = pair.Value;
                int ix = key.IndexOf(funcName);
                if (ix > 0 && ix + funcName.Length == key.Length) {
                    isDyn = true;
                    return val + (uint)ix;
                }
            }
            foreach (var pair in m_sta_strs) {
                var key = pair.Key;
                var val = pair.Value;
                int ix = key.IndexOf(funcName);
                if (ix > 0 && ix + funcName.Length == key.Length)
                    return val + (uint)ix;
            }
            return index;
        }
        private void Relocate32(uint oldAddress, uint newAddress)
        {
            if (oldAddress == 0 || newAddress == 0)
                return;
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

        private uint m_max_vaddr_32 = 0;
        private uint m_max_offset_32 = 0;

        private class movable_data_info_32
        {
            internal elf32_shdr shdr;
            internal byte[] data;
        }

        private bool m_is_loaded = false;
        private byte[] m_old_file_data = null;
        private Dictionary<string, uint> m_sh_strs = null;
        private uint m_sh_str_next_pos = 0;
        private Dictionary<string, uint> m_sta_strs = null;
        private uint m_sta_str_next_pos = 0;
        private Dictionary<string, uint> m_dyn_strs = null;
        private uint m_dyn_str_next_pos = 0;
        private uint m_new_section_size = 0x1000;
        private elf_ident m_ident;
        
        private List<int> m_bucket = new List<int>();
        private List<int> m_chain = new List<int>();

        private elf32_hdr m_elf32_hdr;
        private List<elf32_phdr> m_elf32_phdrs = new List<elf32_phdr>();
        private List<elf32_shdr> m_elf32_shdrs = new List<elf32_shdr>();
        private elf32_shdr m_elf32_dyn_shdr;
        private elf32_shdr m_elf32_init_array_shdr;
        private elf32_shdr m_elf32_fini_array_shdr;
        private List<elf32_dyn> m_elf32_dyns = new List<elf32_dyn>();
        private List<uint> m_elf32_init_array = new List<uint>();
        private List<uint> m_elf32_fini_array = new List<uint>();
        private List<movable_data_info_32> m_movable_datas_32 = new List<movable_data_info_32>();

        private elf32_shdr m_elf32_dyn_str_shdr;
        private elf32_shdr m_elf32_hash_shdr;
        private uint m_elf32_rel_plt_type;

        private elf32_shdr m_elf32_dyn_sym_shdr;
        private elf32_shdr m_elf32_sta_sym_shdr;
        private elf32_shdr m_elf32_dyn_rel_shdr;
        private elf32_shdr m_elf32_plt_rel_shdr;
        private List<elf32_sym> m_elf32_dyn_syms = new List<elf32_sym>();
        private List<elf32_sym> m_elf32_sta_syms = new List<elf32_sym>();
        private List<elf32_rel> m_elf32_dyn_rels = new List<elf32_rel>();
        private List<elf32_rel> m_elf32_plt_rels = new List<elf32_rel>();

        private elf32_shdr m_elf32_plt_shdr;
        private elf32_shdr m_elf32_got_shdr;
        private uint m_elf32_plt_got = 0;
        private List<uint> m_elf32_gots = new List<uint>();
        
        private class DllCallInfo
        {
            internal string SoName;
            internal string FuncName;
            internal string Arg;
        }

        private List<DllCallInfo> m_DllCalls = new List<DllCallInfo>();
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
        
        private static uint CalcBlCode(uint src, uint target)
        {
            uint offset = (target - src - 0x08) >> 2;
            return 0xeb000000 + (offset & 0x00ffffff);
        }
        private static byte[] GenShellCodeTemplate()
        {
            byte[] bytes = new byte[c_ShellCode.Length / 2];
            for (int i = 0; i < c_ShellCode.Length - 1; i += 2) {
                int ix = i / 2;
                string b = c_ShellCode.Substring(i, 2);
                bytes[ix] = byte.Parse(b, System.Globalization.NumberStyles.AllowHexSpecifier);
            }
            return bytes;
        }
        //hexedit拷出来的格式，这样保存比较方便更换
        private const string c_ShellCode = "10402DE90210A0E348008FE2FEFFFFEB0040A0E1000054E30C00000A70108FE20400A0E1FEFFFFEB000050E30300000A0010A0E17C008FE21040BDE800F081E2FEFFFFEB0400A0E11040BDE8FEFFFFEA1040BDE8FEFFFFEA2F646174612F6C6F63616C2F746D702F6C69624D616C6C6F63486F6F6B2E736F2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E00000000496E7374616C6C486F6F6B2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E00000000313032342E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E2E00000000";
        private const uint c_offset_dlopen = 0x0c;
        private const uint c_offset_dlsym = 0x24;
        private const uint c_offset_dlclose = 0x4c;
        private const uint c_offset_dlerror_1 = 0x40;
        private const uint c_offset_dlerror_2 = 0x54;
        private const uint c_offset_sopath = 0x58;
        private const uint c_offset_func = 0x94;
        private const uint c_offset_arg = 0xb8;

        private const uint c_max_sopath = 56;
        private const uint c_max_func = 32;
        private const uint c_max_arg = 32;
        
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
        PT_GNU_EH_FRAME = 0x6474e550,
        PT_GNU_STACK = 0x6474e551,
        PT_GNU_RELRO = 0x6474e552,
        PT_HIOS = 0x6fffffff,
        PT_LOPROC = 0x70000000,
        PT_ARM_ARCHEXT = 0x70000000,
        PT_ARM_EXIDX = 0x70000001,
        PT_ARM_UNWIND = 0x70000001,
        PT_HIPROC = 0x7fffffff,
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
        DT_RELACOUNT = 0x6FFFFFF9,
        DT_RELCOUNT = 0x6FFFFFFA,
        DT_FLAGS_1 = 0x6FFFFFFB,
        DT_VERDEF = 0x6FFFFFFC,
        DT_VERDEFNUM = 0x6FFFFFFD,
        DT_VERNEED = 0x6FFFFFFE,
        DT_VERNEEDNUM = 0x6FFFFFFF,
        DT_VERSYM = 0x6FFFFFF0,
        DT_LOOS = 0x6000000d,
        DT_HIOS = 0x6ffff000,
        DT_LOPROC = 0x70000000,
        DT_ARM_RESERVED1 = 0x70000000,
        DT_ARM_SYMTABSZ = 0x70000001,
        DT_ARM_PREEMPTMAP = 0x70000002,
        DT_ARM_RESERVED2 = 0x70000003,
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
        SHT_LOOS = 0x60000000,
        SHT_GNU_ATTRIBUTES = 0x6FFFFFF5,
        SHT_GNU_HASH = 0x6FFFFFF6,
        SHT_GNU_LIBLIST = 0x6FFFFFF7,
        SHT_CHECKSUM = 0x6FFFFFF8,
        SHT_LOSUNW = 0x6FFFFFFA,
        SHT_SUNW_MOVE = 0x6FFFFFFA,
        SHT_SUNW_COMDAT = 0x6FFFFFFB,
        SHT_SUNW_SYMINFO = 0x6FFFFFFC,
        SHT_GNU_VERDEF = 0x6FFFFFFD,
        SHT_GNU_VERNEED = 0x6FFFFFFE,
        SHT_GNU_VERSYM = 0x6FFFFFFF,
        SHT_HISUNW = 0x6FFFFFFF,
        SHT_HIOS = 0x6FFFFFFF,
        SHT_LOPROC = 0x70000000,
        SHT_ARM_EXIDX = 0x70000001,
        SHT_ARM_PREEMPTMAP = 0x70000002,
        SHT_ARM_ATTRIBUTES = 0x70000003,
        SHT_ARM_DEBUGOVERLAY = 0x70000004,
        SHT_ARM_OVERLAYSECTION = 0x70000005,
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
