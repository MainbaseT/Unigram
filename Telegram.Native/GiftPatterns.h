#pragma once

#include "GiftPatterns.g.h"

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.UI.Composition.h>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::UI::Composition;

namespace winrt::Telegram::Native::implementation
{
    inline static float2 ComputeRenderSize(float width, float height, float rasterizationScale)
    {
        auto scale = (int)(rasterizationScale * 100);
        auto dpi = 0.25f * rasterizationScale;
        auto factor = 1.0f / rasterizationScale;

        return float2(width * dpi * factor, height * dpi * factor);
    }

    inline static float2 ComputeRenderPhysicalSize(float width, float height, float rasterizationScale)
    {
        auto scale = (int)(rasterizationScale * 100);
        auto dpi = 0.25f * rasterizationScale;

        return float2(width * dpi, height * dpi);
    }

    struct GiftPatterns : GiftPatternsT<GiftPatterns>
    {
        GiftPatterns(ICompositionSurface surface)
            : m_surface(surface)
            , m_renderSize(0, 0)
            , m_renderPhysicalSize(0, 0)
            , m_naturalSize(0, 0)
            , m_patterns(winrt::single_threaded_vector<GiftPattern>())
        {

        }

        GiftPatterns(ICompositionSurface surface, float width, float height, float rasterizationScale, IVector<GiftPattern> patterns)
            : m_surface(surface)
            , m_renderSize(ComputeRenderSize(width, height, rasterizationScale))
            , m_renderPhysicalSize(ComputeRenderPhysicalSize(width, height, rasterizationScale))
            , m_naturalSize(width, height)
            , m_patterns(patterns)
        {

        }

        ICompositionSurface Surface();
        float2 RenderSize();
        float2 RenderPhysicalSize();
        float2 NaturalSize();
        IVector<GiftPattern> Patterns();

    private:
        ICompositionSurface m_surface;
        float2 m_renderSize;
        float2 m_renderPhysicalSize;
        float2 m_naturalSize;
        IVector<GiftPattern> m_patterns;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct GiftPatterns : GiftPatternsT<GiftPatterns, implementation::GiftPatterns>
    {
    };
}
