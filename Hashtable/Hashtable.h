#ifndef __HASHTABLE_H__
#define __HASHTABLE_H__

unsigned int GetPrime(unsigned int code);
unsigned int ToIndex(unsigned int hashSize, unsigned int code, unsigned int& incr);
bool IsPrime(unsigned int val);

//用于编译时查找大于等于指定值的素数的代码（实在没别的办法了，所以用了这种比较晦涩的技术，请不要怨我！）
namespace HashtableUtility
{
	template<bool v, int V,
		int K1 = 0, int K2 = 0, int K3 = 0, int K4 = 0, int K5 = 0, int K6 = 0, int K7 = 0, int K8 = 0, int K9 = 0, int K10 = 0, int K11 = 0, int K12 = 0,
		int K13 = 0, int K14 = 0, int K15 = 0, int K16 = 0, int K17 = 0, int K18 = 0, int K19 = 0, int K20 = 0, int K21 = 0, int K22 = 0, int K23 = 0, int K24 = 0,
		int K25 = 0, int K26 = 0, int K27 = 0, int K28 = 0, int K29 = 0, int K30 = 0, int K31 = 0, int K32 = 0, int K33 = 0, int K34 = 0, int K35 = 0, int K36 = 0,
		int K37 = 0, int K38 = 0, int K39 = 0, int K40 = 0, int K41 = 0, int K42 = 0, int K43 = 0, int K44 = 0, int K45 = 0, int K46 = 0, int K47 = 0, int K48 = 0,
		int K49 = 0, int K50 = 0, int K51 = 0, int K52 = 0, int K53 = 0, int K54 = 0, int K55 = 0, int K56 = 0, int K57 = 0, int K58 = 0, int K59 = 0, int K60 = 0,
		int K61 = 0, int K62 = 0, int K63 = 0, int K64 = 0, int K65 = 0, int K66 = 0, int K67 = 0, int K68 = 0, int K69 = 0, int K70 = 0, int K71 = 0, int K72 = 0>
	class FindImpl
	{
	public:
		static const int Value = K1;
	};
	template<int V,
		int K1, int K2, int K3, int K4, int K5, int K6, int K7, int K8, int K9, int K10, int K11, int K12,
		int K13, int K14, int K15, int K16, int K17, int K18, int K19, int K20, int K21, int K22, int K23, int K24,
		int K25, int K26, int K27, int K28, int K29, int K30, int K31, int K32, int K33, int K34, int K35, int K36,
		int K37, int K38, int K39, int K40, int K41, int K42, int K43, int K44, int K45, int K46, int K47, int K48,
		int K49, int K50, int K51, int K52, int K53, int K54, int K55, int K56, int K57, int K58, int K59, int K60,
		int K61, int K62, int K63, int K64, int K65, int K66, int K67, int K68, int K69, int K70, int K71, int K72>
	class FindImpl<false, V, K1, K2, K3, K4, K5, K6, K7, K8, K9, K10, K11, K12, K13, K14, K15, K16, K17, K18, K19, K20, K21, K22, K23, K24, K25, K26, K27, K28, K29, K30, K31, K32, K33, K34, K35, K36, K37, K38, K39, K40, K41, K42, K43, K44, K45, K46, K47, K48, K49, K50, K51, K52, K53, K54, K55, K56, K57, K58, K59, K60, K61, K62, K63, K64, K65, K66, K67, K68, K69, K70, K71, K72>
	{
	public:
		static const int Value = FindImpl<(V <= K2), V, K2, K3, K4, K5, K6, K7, K8, K9, K10, K11, K12, K13, K14, K15, K16, K17, K18, K19, K20, K21, K22, K23, K24, K25, K26, K27, K28, K29, K30, K31, K32, K33, K34, K35, K36, K37, K38, K39, K40, K41, K42, K43, K44, K45, K46, K47, K48, K49, K50, K51, K52, K53, K54, K55, K56, K57, K58, K59, K60, K61, K62, K63, K64, K65, K66, K67, K68, K69, K70, K71, K72>::Value;
	};
	template<int V,
		int K1 = 0, int K2 = 0, int K3 = 0, int K4 = 0, int K5 = 0, int K6 = 0, int K7 = 0, int K8 = 0, int K9 = 0, int K10 = 0, int K11 = 0, int K12 = 0,
		int K13 = 0, int K14 = 0, int K15 = 0, int K16 = 0, int K17 = 0, int K18 = 0, int K19 = 0, int K20 = 0, int K21 = 0, int K22 = 0, int K23 = 0, int K24 = 0,
		int K25 = 0, int K26 = 0, int K27 = 0, int K28 = 0, int K29 = 0, int K30 = 0, int K31 = 0, int K32 = 0, int K33 = 0, int K34 = 0, int K35 = 0, int K36 = 0,
		int K37 = 0, int K38 = 0, int K39 = 0, int K40 = 0, int K41 = 0, int K42 = 0, int K43 = 0, int K44 = 0, int K45 = 0, int K46 = 0, int K47 = 0, int K48 = 0,
		int K49 = 0, int K50 = 0, int K51 = 0, int K52 = 0, int K53 = 0, int K54 = 0, int K55 = 0, int K56 = 0, int K57 = 0, int K58 = 0, int K59 = 0, int K60 = 0,
		int K61 = 0, int K62 = 0, int K63 = 0, int K64 = 0, int K65 = 0, int K66 = 0, int K67 = 0, int K68 = 0, int K69 = 0, int K70 = 0, int K71 = 0, int K72 = 0>
	class Find
	{
	public:
		static const int Value = FindImpl<(V <= K1), V, K1, K2, K3, K4, K5, K6, K7, K8, K9, K10, K11, K12, K13, K14, K15, K16, K17, K18, K19, K20, K21, K22, K23, K24, K25, K26, K27, K28, K29, K30, K31, K32, K33, K34, K35, K36, K37, K38, K39, K40, K41, K42, K43, K44, K45, K46, K47, K48, K49, K50, K51, K52, K53, K54, K55, K56, K57, K58, K59, K60, K61, K62, K63, K64, K65, K66, K67, K68, K69, K70, K71, K72>::Value;
	};
	template<int v>
	class Prime
	{
	public:
		static const int Value = Find<v,
			3, 7, 11, 0x11, 0x17, 0x1d, 0x25, 0x2f, 0x3b, 0x47, 0x59, 0x6b, 0x83, 0xa3, 0xc5, 0xef,
			0x125, 0x161, 0x1af, 0x209, 0x277, 0x2f9, 0x397, 0x44f, 0x52f, 0x63d, 0x78b, 0x91d, 0xaf1, 0xd2b, 0xfd1, 0x12fd,
			0x16cf, 0x1b65, 0x20e3, 0x2777, 0x2f6f, 0x38ff, 0x446f, 0x521f, 0x628d, 0x7655, 0x8e01, 0xaa6b, 0xcc89, 0xf583, 0x126a7, 0x1619b,
			0x1a857, 0x1fd3b, 0x26315, 0x2dd67, 0x3701b, 0x42023, 0x4f361, 0x5f0ed, 0x72125, 0x88e31, 0xa443b, 0xc51eb, 0xec8c1, 0x11bdbf, 0x154a3f, 0x198c4f,
			0x1ea867, 0x24ca19, 0x2c25c1, 0x34fa1b, 0x3f928f, 0x4c4987, 0x5b8b6f, 0x6dda89
		>::Value;
	};
	enum
	{
		IDTS_EMPTY = 0,
		IDTS_USED = 1,
		IDTS_REMOVED = 2,
	};
	static const unsigned int INVALID_HASH_INDEX = 0xFFFFFFFF;
	static const unsigned int MAX_HASH_INDEX_VALUE = 0x7FFFFFFF;
	static const unsigned int INVALID_ID = 0;
}

//哈希表的基础功能（素数获取, 整数hash计算，冲突处理等）
template<class SlotT, int SizeV>
class HashtableBasicT
{
	typedef typename SlotT::KeyType KeyT;
public:
	static const unsigned int MAX_COUNT = HashtableUtility::Prime<SizeV>::Value;
public:
	//增加一个表项
	unsigned int		PrepareAddIndex(const KeyT& key);
	//读取信息
	unsigned int		Find(const KeyT& key) const;
	//删除表项
	unsigned int		Remove(const KeyT& key);
	//清除所有数据
	void				Cleanup(void);
public:
	SlotT				m_Slots[MAX_COUNT];
};

template<class SlotT, int SizeV>
inline unsigned int HashtableBasicT<SlotT, SizeV>::PrepareAddIndex(const KeyT& key)
{
	unsigned int count = MAX_COUNT;
	if (0 == count)
		return HashtableUtility::INVALID_HASH_INDEX;
	unsigned int incr = 1;
	unsigned int c = ToIndex(count, key.GetHashCode(), incr);
	for (unsigned int i = 0; i < count; ++i, c = (c + incr) % count)
	{
		SlotT& slot = m_Slots[c];
		if (slot.GetStatus() == HashtableUtility::IDTS_USED)
		{
			continue;
		}
		slot.SetStatus(HashtableUtility::IDTS_USED);
		return c;
	}
	return HashtableUtility::INVALID_HASH_INDEX;
}

template<class SlotT, int SizeV>
inline unsigned int HashtableBasicT<SlotT, SizeV>::Find(const KeyT& key) const
{
	unsigned int count = MAX_COUNT;
	if (0 == count)
		return HashtableUtility::INVALID_HASH_INDEX;
	unsigned int incr = 1;
	unsigned int c = ToIndex(count, key.GetHashCode(), incr);
	for (unsigned int i = 0; i < count; ++i, c = (c + incr) % count)
	{
		unsigned int v = m_Slots[c].GetStatus();
		if (v == HashtableUtility::IDTS_EMPTY)
		{
			return HashtableUtility::INVALID_HASH_INDEX;
		}
		else if (v == HashtableUtility::IDTS_REMOVED)
		{
			continue;
		}

		if (key.KeyEqual(c))
		{
			return c;
		}
	}
	return HashtableUtility::INVALID_HASH_INDEX;
}

template<class SlotT, int SizeV>
inline unsigned int HashtableBasicT<SlotT, SizeV>::Remove(const KeyT& key)
{
	unsigned int c = Find(key);
	if (HashtableUtility::INVALID_HASH_INDEX == c)
		return HashtableUtility::INVALID_HASH_INDEX;
	SlotT& slot = m_Slots[c];
	slot.Cleanup();
	slot.SetStatus(HashtableUtility::IDTS_REMOVED);
	return c;
}

template<class SlotT, int SizeV>
inline void HashtableBasicT<SlotT, SizeV>::Cleanup(void)
{
	unsigned int count = MAX_COUNT;
	for (unsigned int i = 0; i < count; ++i)
	{
		SlotT& slot = m_Slots[i];
		slot.Cleanup();
	}
}

//基础数值类型用作hash键时的HashtableT的第三个模板参数
template<typename KeyT>
class DefKeyWorkerT
{
public:
	static unsigned int GetHashCode(const KeyT& key)
	{
		return (unsigned int)key;
	}
	static int Equal(const KeyT& key1, const KeyT& key2)
	{
		return key1 == key2;
	}
	static void Clean(KeyT& key)
	{
		key = (KeyT)HashtableUtility::INVALID_ID;
	}
};

template<typename KeyT>
class DefKeyWorkerT<KeyT*>
{
public:
	static unsigned int GetHashCode(const KeyT* key)
	{
		unsigned int val = 0;
		val = *reinterpret_cast<const unsigned int*>(key);
		return val;
	}
	static int Equal(const KeyT* key1, const KeyT* key2)
	{
		return key1 == key2;
	}
	static void Clean(KeyT*& key)
	{
		key = 0;
	}
};

//HashtableT的第四个模板参数的默认实现。
template<typename ValT>
class DefValueWorkerT
{
public:
	static void Clean(ValT& val)
	{
		val = ValT();
	}
};

template<typename ValT>
class DefValueWorkerT<ValT*>
{
public:
	static void Clean(ValT*& val)
	{
		val = 0;
	}
};

//HashtableT的第四个模板参数的实现，用于以INVALID_ID表示无效值的情形。
template<typename ValT, ValT InvalidVal = HashtableUtility::INVALID_ID>
class IntegerValueWorkerT
{
public:
	static void Clean(ValT& val)
	{
		val = InvalidVal;
	}
};

template<typename KeyT, typename ValT, int SizeV, typename KeyWorkerT = DefKeyWorkerT<KeyT>, typename ValueWorkerT = DefValueWorkerT<ValT> >
class HashtableT
{
	typedef HashtableT<KeyT, ValT, SizeV, KeyWorkerT, ValueWorkerT> HashtableType;
	friend class SlotKey;
	class SlotKey
	{
	public:
		int KeyEqual(unsigned int index) const
		{
			return KeyWorkerT::Equal(m_Key, m_pHashtable->m_Hash.m_Slots[index].m_ID);
		}
		unsigned int GetHashCode(void) const
		{
			return KeyWorkerT::GetHashCode(m_Key);
		}
	public:
		SlotKey(const HashtableType* ptr, const KeyT& key) :m_pHashtable(ptr), m_Key(key)
		{}
	private://禁用拷贝与赋值，此类使用引用包装，只用于受限场合
		SlotKey(const SlotKey&);
		SlotKey& operator= (const SlotKey&);
	private:
		const KeyT&				m_Key;
		const HashtableType*	m_pHashtable;
	};
	class Slot
	{
	public:
		typedef SlotKey KeyType;
	public:
		unsigned int GetHashCode(void) const
		{
			return KeyWorkerT::GetHashCode(m_ID);
		}
		void SetStatus(unsigned int status)
		{
			m_Status = status;
		}
		unsigned int GetStatus(void) const
		{
			return m_Status;
		}
		void Cleanup(void)
		{
			Clear();
		}
	public:
		Slot(void)
		{
			Clear();
		}
	private:
		void Clear(void)
		{
			m_Status = HashtableUtility::IDTS_EMPTY;
			KeyWorkerT::Clean(m_ID);
			ValueWorkerT::Clean(m_Value);
		}
	public:
		unsigned int	m_Status;
		KeyT	m_ID;
		ValT	m_Value;
	};
	typedef HashtableBasicT<Slot, SizeV> HashtableBasicType;
public:
	//增加一个表项
	inline bool				Add(const KeyT& id, const ValT& val)
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Find(key);
		if (index != HashtableUtility::INVALID_HASH_INDEX)
			return false;
		index = m_Hash.PrepareAddIndex(key);
		if (index == HashtableUtility::INVALID_HASH_INDEX || index >= HashtableBasicType::MAX_COUNT)
			return false;
		Slot& slot = m_Hash.m_Slots[index];
		slot.m_ID = id;
		slot.m_Value = val;
		return true;
	}
	//读取信息
	inline const ValT&		Get(const KeyT& id) const
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Find(key);
		if (index == HashtableUtility::INVALID_HASH_INDEX || index >= HashtableBasicType::MAX_COUNT)
			return GetInvalidValueRef();
		const Slot& slot = m_Hash.m_Slots[index];
		return slot.m_Value;
	}
	//读取信息
	inline ValT&			Get(const KeyT& id)
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Find(key);
		if (index == HashtableUtility::INVALID_HASH_INDEX || index >= HashtableBasicType::MAX_COUNT)
			return GetInvalidValueRef();
		Slot& slot = m_Hash.m_Slots[index];
		return slot.m_Value;
	}
	//删除表项
	inline void	Remove(const KeyT& id)
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Remove(key);
	}
private:
	HashtableBasicT<Slot, SizeV> m_Hash;
public:
	static inline KeyT&	GetInvalidKeyRef(void)
	{
		static KeyT s_Key;
		KeyWorkerT::Clean(s_Key);
		return s_Key;
	}
	static inline ValT&	GetInvalidValueRef(void)
	{
		static ValT s_Val;
		ValueWorkerT::Clean(s_Val);
		return s_Val;
	}
};

template<typename KeyT, int SizeV, typename KeyWorkerT = DefKeyWorkerT<KeyT> >
class HashsetT
{
	typedef HashsetT<KeyT, SizeV, KeyWorkerT> HashsetType;
	friend class SlotKey;
	class SlotKey
	{
	public:
		int KeyEqual(unsigned int index) const
		{
			return KeyWorkerT::Equal(m_Key, m_pHashset->m_Hash.m_Slots[index].m_ID);
		}
		unsigned int GetHashCode(void) const
		{
			return KeyWorkerT::GetHashCode(m_Key);
		}
	public:
		SlotKey(const HashsetType* ptr, const KeyT& key) :m_pHashset(ptr), m_Key(key)
		{}
	private://禁用拷贝与赋值，此类使用引用包装，只用于受限场合
		SlotKey(const SlotKey&);
		SlotKey& operator= (const SlotKey&);
	private:
		const KeyT&				m_Key;
		const HashsetType*		m_pHashset;
	};
	class Slot
	{
	public:
		typedef SlotKey KeyType;
	public:
		virtual unsigned int GetHashCode(void) const
		{
			return KeyWorkerT::GetHashCode(m_ID);
		}
		virtual void SetStatus(unsigned int status)
		{
			m_Status = status;
		}
		virtual unsigned int GetStatus(void) const
		{
			return m_Status;
		}
		virtual void Cleanup(void)
		{
			Clear();
		}
	public:
		Slot(void)
		{
			Clear();
		}
	private:
		void Clear(void)
		{
			m_Status = HashtableUtility::IDTS_EMPTY;
			KeyWorkerT::Clean(m_ID);
		}
	public:
		unsigned int	m_Status;
		KeyT	m_ID;
	};
	typedef HashtableBasicT<Slot, SizeV> HashtableBasicType;
public:
	//增加一个表项
	inline bool				Insert(const KeyT& id)
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Find(key);
		if (index != HashtableUtility::INVALID_HASH_INDEX)
			return false;
		index = m_Hash.PrepareAddIndex(key);
		if (index == HashtableUtility::INVALID_HASH_INDEX || index >= HashtableBasicType::MAX_COUNT)
			return false;
		Slot& slot = m_Hash.m_Slots[index];
		slot.m_ID = id;
		return true;
	}
	//读取信息
	inline bool				Exist(const KeyT& id) const
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Find(key);
		if (index == HashtableUtility::INVALID_HASH_INDEX || index >= HashtableBasicType::MAX_COUNT)
			return false;
		return true;
	}
	//删除表项
	inline void				Remove(const KeyT& id)
	{
		SlotKey key(this, id);
		unsigned int index = m_Hash.Remove(key);
	}
private:
	HashtableBasicT<Slot, SizeV> m_Hash;
public:
	static inline KeyT&	GetInvalidKeyRef(void)
	{
		static KeyT s_Key;
		KeyWorkerT::Clean(s_Key);
		return s_Key;
	}
};

#endif