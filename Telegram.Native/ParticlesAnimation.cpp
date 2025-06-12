#include "pch.h"
#include "ParticlesAnimation.h"
#if __has_include("ParticlesAnimation.g.cpp")
#include "ParticlesAnimation.g.cpp"
#endif

#include <random>
#include <algorithm>

#define IS_MOBILE false

namespace winrt::Telegram::Native::implementation
{
    inline int alpha_blend(int pixel, int sa, int sr, int sg, int sb)
    {
        if (sa == 0) return pixel;
        if (pixel == 0) return (sa << 24) | (sr << 16) | (sg << 8) | sb;

        // Alpha blend
        int destPixel = pixel;
        int da = ((destPixel >> 24) & 0xff);
        int dr = ((destPixel >> 16) & 0xff);
        int dg = ((destPixel >> 8) & 0xff);
        int db = ((destPixel) & 0xff);

        destPixel = ((sa + (((da * (255 - sa)) * 0x8081) >> 23)) << 24) |
            ((sr + (((dr * (255 - sa)) * 0x8081) >> 23)) << 16) |
            ((sg + (((dg * (255 - sa)) * 0x8081) >> 23)) << 8) |
            ((sb + (((db * (255 - sa)) * 0x8081) >> 23)));

        return destPixel;
    }

    inline Color premultiply_color(uint8_t r, uint8_t g, uint8_t b, uint8_t opacity)
    {
        // Use bit shifts for faster division (255 ≈ 256)
        uint32_t pr = (r * opacity) >> 8;
        uint32_t pg = (g * opacity) >> 8;
        uint32_t pb = (b * opacity) >> 8;

        return Color(opacity, pr, pg, pb);
    }

    // Bounds checking helper
    inline bool is_valid_pixel(int x, int y, int width, int height)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Safe pixel write with bounds check
    inline void set_pixel(int32_t* pixels, int x, int y, int width, int height, int sa, int sr, int sg, int sb)
    {
        if (is_valid_pixel(x, y, width, height))
        {
            if (sa == 255)
            {
                pixels[y * width + x] = (sa << 24) | (sr << 16) | (sg << 8) | sb;
            }
            else
            {
                int32_t* pixel = &pixels[y * width + x];
                *pixel = alpha_blend(*pixel, sa, sr, sg, sb);
            }
        }
    }

    // Safe pixel write with alpha blending
    inline void set_pixel_alpha(int32_t* pixels, int x, int y, int width, int height, int sa, int sr, int sg, int sb, uint8_t alpha)
    {
        if (is_valid_pixel(x, y, width, height))
        {
            uint32_t pa = (sa * alpha) >> 8;
            uint32_t pr = (sr * alpha) >> 8;
            uint32_t pg = (sg * alpha) >> 8;
            uint32_t pb = (sb * alpha) >> 8;

            int32_t* pixel = &pixels[y * width + x];
            *pixel = alpha_blend(*pixel, pa, pr, pg, pb);
        }
    }

    // 1px diameter circles (0.5px radius)
    inline void draw_circle_1px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Single pixel
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
    }

    inline void draw_circle_1px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.25px effective diameter - center + light edges
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_circle_1px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.5px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_circle_1px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_circle_1px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective diameter with anti-aliasing
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Outer ring with anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_circle_1px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective diameter with anti-aliasing
        // Solid center
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Anti-aliased outer ring
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx - 2, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 2, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 2, width, height, sa, sr, sg, sb, 128);

        // Corner anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy - 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 2, cy - 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 2, cy + 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 2, cy + 2, width, height, sa, sr, sg, sb, 64);
    }

    // 2px diameter circles (1px radius)
    inline void draw_circle_2px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Classic 2px diameter pattern
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_circle_2px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Light anti-aliasing
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_circle_2px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 3px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Diagonal anti-aliasing
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 160);
    }

    inline void draw_circle_2px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Outer ring with light anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_circle_2px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 5px effective diameter with good anti-aliasing
        // Solid center 3x3
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Anti-aliased outer ring
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx - 2, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 2, cy + 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 2, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 2, cy + 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 1, cy - 2, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy - 2, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 1, cy + 2, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy + 2, width, height, sa, sr, sg, sb, 160);

        // Corner fade
        set_pixel_alpha(pixels, cx - 2, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 2, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy + 2, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_circle_2px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 8px effective diameter with full anti-aliasing
        // Solid center 5x5
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx * dx + dy * dy <= 4)
                { // Within 2px radius
                    set_pixel(pixels, cx + dx, cy + dy, width, height, sa, sr, sg, sb);
                }
            }
        }

        // Anti-aliased outer ring (3px radius)
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 180);

        set_pixel_alpha(pixels, cx - 3, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 3, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 3, width, height, sa, sr, sg, sb, 128);

        set_pixel_alpha(pixels, cx - 3, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 3, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 3, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 3, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 2, cy - 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy - 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 2, cy + 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy + 3, width, height, sa, sr, sg, sb, 96);

        // Outer corner fade
        set_pixel_alpha(pixels, cx - 3, cy - 3, width, height, sa, sr, sg, sb, 48);
        set_pixel_alpha(pixels, cx + 3, cy - 3, width, height, sa, sr, sg, sb, 48);
        set_pixel_alpha(pixels, cx - 3, cy + 3, width, height, sa, sr, sg, sb, 48);
        set_pixel_alpha(pixels, cx + 3, cy + 3, width, height, sa, sr, sg, sb, 48);
    }

    // Dispatch function for easy usage
    inline void draw_circle_scaled(int32_t* pixels, int width, int height, Particle* particle, Color color, int scale, double rasterizationScale)
    {
        int cx = particle->X;
        int cy = particle->Y;
        float radius = particle->Radius;

        if (radius == 0.5)
        {
            switch (scale)
            {
            case 100: draw_circle_1px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_circle_1px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_circle_1px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_circle_1px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_circle_1px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_circle_1px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
        else if (radius == 1)
        {
            switch (scale)
            {
            case 100: draw_circle_2px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_circle_2px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_circle_2px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_circle_2px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_circle_2px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_circle_2px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
    }

    void ParticlesAnimation::RenderSync(IBuffer bitmap)
    {
        auto add = 0.04;
        auto pixels = (int32_t*)bitmap.data();

        std::fill_n(pixels, m_width * m_height, m_background);

        for (int i = 0, length = m_particles.size(); i < length; ++i)
        {
            auto dot = &m_particles[i];
            auto addOpacity = dot->Adding ? add : -add;

            dot->Opacity += addOpacity;
            // if(dot.mOpacity <= 0) dot.mOpacity = dot.opacity;

            // const easedOpacity = easing(dot.mOpacity);
            auto easedOpacity = (byte)(std::clamp(dot->Opacity, 0., 1.) * 255);
            auto color = premultiply_color(m_foreground.R, m_foreground.G, m_foreground.B, easedOpacity);

            draw_circle_scaled(pixels, m_width, m_height, dot, color, m_scalePercent, m_rasterizationScale);

            if (dot->Opacity <= 0)
            {
                dot->Adding = true;
                m_particles[i] = GenerateParticle(dot->Adding);
            }
            else if (dot->Opacity >= 1)
            {
                dot->Adding = false;
            }
        }
    }

    inline double min(double x, double y)
    {
        return x > y ? y : x;
    }

    void ParticlesAnimation::Prepare()
    {
        auto w = m_width * (1 / m_rasterizationScale);
        auto h = m_height * (1 / m_rasterizationScale);

        auto count = round(w * h / (35 * (IS_MOBILE ? 2 : 1)));
        count *= m_text ? 4 : 1;
        count = min(/*!liteMode.isAvailable('chat_spoilers') ? 400 :*/ IS_MOBILE ? 1000 : 2200, count);

        for (int i = 0; i < count; ++i)
        {
            m_particles.push_back(GenerateParticle(-1));
        }
    }

    inline double NextDouble()
    {
        static std::random_device rd;  // Will be used to obtain a seed for the random number engine
        static std::mt19937 gen(rd()); // Standard mersenne_twister_engine seeded with rd()
        static std::uniform_real_distribution<> dis(0.0, 1.0);
        return dis(gen);
    }

    Particle ParticlesAnimation::GenerateParticle(int32_t type)
    {
        auto x = floor(NextDouble() * m_width);
        auto y = floor(NextDouble() * m_height);
        auto opacity = type == 1 ? 0 : NextDouble();
        auto radius = (NextDouble() >= .8 ? 1 : 0.5);
        auto adding = type == -1
            ? NextDouble() >= .5
            : type;
        return Particle(
            (float)x,
            (float)y,
            (float)radius,
            opacity,
            adding);
    }
}
