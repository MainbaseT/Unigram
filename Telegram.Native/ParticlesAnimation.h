#pragma once

#include "ParticlesAnimation.g.h"

using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Xaml::Media::Imaging;

namespace winrt::Telegram::Native::implementation
{
    struct Particle
    {
        Particle(float x, float y, float radius, double opacity, bool adding)
            : X(x)
            , Y(y)
            , Radius(radius)
            , Opacity(opacity)
            , Adding(adding)
        {
        }

        float X;
        float Y;
        float Radius;
        double Opacity;
        bool Adding;
    };

    inline static double findClosestScale(double target)
    {
        static double rasterizationScales[6] = { 1.0, 1.25, 1.5, 2.0, 2.5, 4.0 };

        const double* closest = rasterizationScales;
        double minDiff = std::abs(*rasterizationScales - target);

        for (size_t i = 1; i < 6; ++i)
        {
            double diff = std::abs(rasterizationScales[i] - target);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = &rasterizationScales[i];
            }
        }

        return *closest;
    }

    inline int32_t premultiply_background(Color color)
    {
        // Use bit shifts for faster division (255 ≈ 256)
        uint32_t pr = (color.R * color.A) >> 8;
        uint32_t pg = (color.G * color.A) >> 8;
        uint32_t pb = (color.B * color.A) >> 8;

        return (color.A << 24) | (pr << 16) | (pg << 8) | pb;
    }

    struct ParticlesAnimation : ParticlesAnimationT<ParticlesAnimation>
    {
        ParticlesAnimation(int32_t width, int32_t height, double rasterizationScale, bool text, Color foreground, Color background)
            : m_width(width)
            , m_height(height)
            , m_scalePercent(findClosestScale(rasterizationScale) * 100)
            , m_rasterizationScale(rasterizationScale)
            , m_text(text)
            , m_foreground(foreground)
            , m_background(premultiply_background(background))
        {
            Prepare();
        }

        void RenderSync(IBuffer bitmap);

        int32_t PixelWidth()
        {
            return m_width;
        }

        int32_t PixelHeight()
        {
            return m_height;
        }

    private:
        void Prepare();
        Particle GenerateParticle(int32_t type);

        int32_t m_width;
        int32_t m_height;
        int32_t m_scalePercent;
        double m_rasterizationScale;
        bool m_text;

        Color m_foreground;
        int32_t m_background;

        std::vector<Particle> m_particles;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct ParticlesAnimation : ParticlesAnimationT<ParticlesAnimation, implementation::ParticlesAnimation>
    {
    };
}
