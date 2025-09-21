#include "pch.h"
#include "GiftPatterns.h"
#if __has_include("GiftPatterns.g.cpp")
#include "GiftPatterns.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    float2 GiftPatterns::Size()
    {
        return m_size;
    }

    IVector<GiftPattern> GiftPatterns::Patterns()
    {
        return m_patterns;
    }
}
