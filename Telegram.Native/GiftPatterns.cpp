#include "pch.h"
#include "GiftPatterns.h"
#if __has_include("GiftPatterns.g.cpp")
#include "GiftPatterns.g.cpp"
#endif

#include <winrt/Windows.UI.Xaml.Media.h>

using namespace winrt::Windows::UI::Xaml::Media;

namespace winrt::Telegram::Native::implementation
{
    ICompositionSurface GiftPatterns::Surface()
    {
        return m_surface;
    }

    float2 GiftPatterns::RenderSize()
    {
        if (auto loadedImageSurface = m_surface.try_as<LoadedImageSurface>())
        {
            return loadedImageSurface.DecodedSize();
        }

        return m_renderSize;
    }

    float2 GiftPatterns::RenderPhysicalSize()
    {
        if (auto loadedImageSurface = m_surface.try_as<LoadedImageSurface>())
        {
            return loadedImageSurface.DecodedPhysicalSize();
        }

        return m_renderPhysicalSize;
    }

    float2 GiftPatterns::NaturalSize()
    {
        if (auto loadedImageSurface = m_surface.try_as<LoadedImageSurface>())
        {
            return loadedImageSurface.NaturalSize();
        }

        return m_naturalSize;
    }

    IVector<GiftPattern> GiftPatterns::Patterns()
    {
        return m_patterns;
    }
}
