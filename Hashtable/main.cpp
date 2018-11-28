// Hashtable.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include "Hashtable.h"

HashtableT<unsigned int, unsigned int, 1024 * 1024> g_Hashtable;
HashsetT<unsigned int, 1024 * 1024> g_Hashset;

int _tmain(int argc, _TCHAR* argv[])
{
	g_Hashtable.Add(1, 2);
	g_Hashtable.Add(2, 3);
	unsigned int v1 = g_Hashtable.Get(2);
	g_Hashtable.Remove(2);
	v1 = g_Hashtable.Get(2);

	bool v2 = g_Hashset.Exist(1024);
	g_Hashset.Insert(1024);
	v2 = g_Hashset.Exist(1024);
	g_Hashset.Remove(1024);
	v2 = g_Hashset.Exist(1024);

	return 0;
}

