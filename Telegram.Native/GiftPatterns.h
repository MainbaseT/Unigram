#pragma once

#include "GiftPatterns.g.h"

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::implementation
{
    struct GiftPatterns : GiftPatternsT<GiftPatterns>
    {
        GiftPatterns(float width, float height, IVector<GiftPattern> patterns)
            : m_size(width, height)
            , m_patterns(patterns)
        {

        }

        float2 Size();
        IVector<GiftPattern> Patterns();

    private:
        float2 m_size;
        IVector<GiftPattern> m_patterns;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct GiftPatterns : GiftPatternsT<GiftPatterns, implementation::GiftPatterns>
    {
    };
}
