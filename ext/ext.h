// ext.h

#pragma once

using namespace System;

namespace ext {

	public ref class KanjiCode {
	public:
		enum class Type { ASCII, JIS, EUC, SJIS, UTF16, UTF8 };
		static Type judge(array<char>^ str, int length);
		static const int ESC = 0x1b;

		static int ISkanji(int code);
		static int ISkana(int code);
	};
}
