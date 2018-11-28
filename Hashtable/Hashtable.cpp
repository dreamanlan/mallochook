#include "Hashtable.h"
#include <math.h>

unsigned int GetPrime(unsigned int code)
{
	static unsigned int primes[] =
	{
		3, 7, 11, 0x11, 0x17, 0x1d, 0x25, 0x2f, 0x3b, 0x47, 0x59, 0x6b, 0x83, 0xa3, 0xc5, 0xef,
		0x125, 0x161, 0x1af, 0x209, 0x277, 0x2f9, 0x397, 0x44f, 0x52f, 0x63d, 0x78b, 0x91d, 0xaf1, 0xd2b, 0xfd1, 0x12fd,
		0x16cf, 0x1b65, 0x20e3, 0x2777, 0x2f6f, 0x38ff, 0x446f, 0x521f, 0x628d, 0x7655, 0x8e01, 0xaa6b, 0xcc89, 0xf583, 0x126a7, 0x1619b,
		0x1a857, 0x1fd3b, 0x26315, 0x2dd67, 0x3701b, 0x42023, 0x4f361, 0x5f0ed, 0x72125, 0x88e31, 0xa443b, 0xc51eb, 0xec8c1, 0x11bdbf, 0x154a3f, 0x198c4f,
		0x1ea867, 0x24ca19, 0x2c25c1, 0x34fa1b, 0x3f928f, 0x4c4987, 0x5b8b6f, 0x6dda89
	};
	int l = 0;
	int h = sizeof(primes) / sizeof(unsigned int) - 1;
	if (primes[h] > code)
	{
		while (l < h)
		{
			int m = (l + h) / 2;
			unsigned int prime = primes[m];
			if (prime < code)
				l = m + 1;
			else if (prime == code)
				return prime;
			else
				h = m;
		}
		return primes[h];
	}
	else
	{
		for (unsigned int i = (code | 1); i < HashtableUtility::MAX_HASH_INDEX_VALUE; i += 2)
		{
			if (IsPrime(i))
			{
				return i;
			}
		}
	}
	return code;
}

unsigned int ToIndex(unsigned int hashSize, unsigned int code, unsigned int& incr)
{
	if (0 == hashSize || 1 == hashSize)
		return HashtableUtility::INVALID_HASH_INDEX;
	unsigned int num = (unsigned int)(code & HashtableUtility::MAX_HASH_INDEX_VALUE);
	incr = 1 + ((unsigned int)(((num >> 5) + 1) % (hashSize - 1)));
	return num % hashSize;
}

bool IsPrime(unsigned int val)
{
	if ((val & 1) == 0)
	{
		return (val == 2);
	}
	unsigned int num = (unsigned int)sqrt((double)val);
	for (unsigned int i = 3; i <= num; i += 2)
	{
		if ((val % i) == 0)
		{
			return false;
		}
	}
	return true;
}