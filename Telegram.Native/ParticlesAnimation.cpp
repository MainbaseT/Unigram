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

#pragma region Circle

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

#pragma endregion

#pragma region Plus

    // 1px diameter plus shapes (0.5px radius equivalent)
    inline void draw_plus_1px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Single pixel
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
    }

    inline void draw_plus_1px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.25px effective size - center + light arms
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_1px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.5px effective size
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_1px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2px effective size - solid plus
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_plus_1px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective size with anti-aliasing
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended arms with anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_plus_1px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective size with anti-aliasing
        // Solid center cross
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended arms
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Anti-aliased outer tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 128);
    }

    // 2px diameter plus shapes (1px radius equivalent)
    inline void draw_plus_2px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Classic 2px plus pattern
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_plus_2px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective size with light anti-aliasing
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Light anti-aliasing on arm tips
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_2px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 3px effective size
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Medium anti-aliasing on arm tips
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_2px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective size - thicker plus
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended solid arms
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Light anti-aliasing on outer tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_plus_2px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 5px effective size with good anti-aliasing
        // Solid center cross
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended solid arms
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Anti-aliased outer tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 160);
    }

    inline void draw_plus_2px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 8px effective size with full anti-aliasing
        // Solid center cross (3 pixels thick)
        for (int i = -1; i <= 1; i++)
        {
            // Horizontal arm
            set_pixel(pixels, cx - 3, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx - 2, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx - 1, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + 1, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + 2, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + 3, cy + i, width, height, sa, sr, sg, sb);

            // Vertical arm
            set_pixel(pixels, cx + i, cy - 3, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + i, cy - 2, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + i, cy + 2, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + i, cy + 3, width, height, sa, sr, sg, sb);
        }

        // Anti-aliased outer tips
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 128);

        // Side anti-aliasing for smoother edges
        set_pixel_alpha(pixels, cx - 4, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 4, cy + 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 4, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 4, cy + 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 1, cy - 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy - 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 1, cy + 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy + 4, width, height, sa, sr, sg, sb, 64);
    }

    // Dispatch function for easy usage
    inline void draw_plus_scaled(int32_t* pixels, int width, int height, Particle* particle, Color color, int scale, double rasterizationScale)
    {
        int cx = particle->X;
        int cy = particle->Y;
        float radius = particle->Radius;

        if (radius == 0.5)
        {
            switch (scale)
            {
            case 100: draw_plus_1px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_plus_1px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_plus_1px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_plus_1px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_plus_1px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_plus_1px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
        else if (radius == 1)
        {
            switch (scale)
            {
            case 100: draw_plus_2px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_plus_2px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_plus_2px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_plus_2px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_plus_2px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_plus_2px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
    }

#pragma endregion

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

            if (m_type == ParticlesType::Status)
            {
                draw_plus_scaled(pixels, m_width, m_height, dot, color, m_scalePercent, m_rasterizationScale);
            }
            else
            {
                draw_circle_scaled(pixels, m_width, m_height, dot, color, m_scalePercent, m_rasterizationScale);
            }

            if (dot->Opacity <= 0)
            {
                dot->Adding = true;
                m_particles[i] = GenerateParticle(dot->Adding, NextPoint(m_width, m_height));
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

    inline double max(double x, double y)
    {
        return x > y ? x : y;
    }

    constexpr float PI = 3.14159265358979323846f;

    std::vector<Point> ParticlesAnimation::NextPoints(int count, float width, float height, float noiseFactor)
    {
        std::random_device rd;
        std::mt19937 gen(rd());

        // Pre-calculate constants
        const float centerX = width * 0.5f;
        const float centerY = height * 0.5f;
        const float semiMajor = width * 0.5f;
        const float semiMinor = height * 0.5f;
        const float invTwoPi = 1.0f / (2.0f * PI);

        std::vector<Point> particles;
        particles.reserve(count);

        // Generate all random numbers at once for better cache performance
        std::uniform_real_distribution<float> uniformDist(0.0f, 1.0f);
        std::uniform_real_distribution<float> noiseDist(-noiseFactor, noiseFactor);

        for (int i = 0; i < count; ++i)
        {
            if (m_type == ParticlesType::Status)
            {
                // Generate angle and radius
                float angle = uniformDist(gen) * 2.0f * PI;
                float r = std::sqrt(uniformDist(gen)); // sqrt for uniform area distribution

                // Fast trigonometry
                float cosAngle = std::cos(angle);
                float sinAngle = std::sin(angle);

                // Calculate base position
                float baseX = r * semiMajor * cosAngle;
                float baseY = r * semiMinor * sinAngle;

                // Add noise
                float noiseX = noiseDist(gen) * semiMajor;
                float noiseY = noiseDist(gen) * semiMinor;

                // Final position
                float x = centerX + baseX + noiseX;
                float y = centerY + baseY + noiseY;

                // Clamp to bounds (branchless)
                x = std::max(0.0f, std::min(width, x));
                y = std::max(0.0f, std::min(height, y));

                particles.emplace_back(Point{ x, y });
            }
            else
            {
                particles.emplace_back(Point{ uniformDist(gen) * m_width, uniformDist(gen) * m_height });
            }
        }

        return particles;
    }

    Point ParticlesAnimation::NextPoint(float width, float height, float noiseFactor)
    {
        static std::random_device rd;
        static std::mt19937 gen(rd());

        if (m_type == ParticlesType::Status)
        {
            // Pre-calculate constants
            const float centerX = width * 0.5f;
            const float centerY = height * 0.5f;
            const float semiMajor = width * 0.5f;
            const float semiMinor = height * 0.5f;

            std::uniform_real_distribution<float> uniformDist(0.0f, 1.0f);
            std::uniform_real_distribution<float> noiseDist(-noiseFactor, noiseFactor);

            // Generate angle and radius
            float angle = uniformDist(gen) * 2.0f * PI;
            float r = std::sqrt(uniformDist(gen)); // sqrt for uniform area distribution

            // Fast trigonometry
            float cosAngle = std::cos(angle);
            float sinAngle = std::sin(angle);

            // Calculate base position
            float baseX = r * semiMajor * cosAngle;
            float baseY = r * semiMinor * sinAngle;

            // Add noise
            float noiseX = noiseDist(gen) * semiMajor;
            float noiseY = noiseDist(gen) * semiMinor;

            // Final position
            float x = centerX + baseX + noiseX;
            float y = centerY + baseY + noiseY;

            // Clamp to bounds
            x = std::max(0.0f, std::min(width, x));
            y = std::max(0.0f, std::min(height, y));

            return { x, y };
        }
        else
        {
            static std::uniform_real_distribution<float> dis(0.0, 1.0);
            return { dis(gen) * m_width, dis(gen) * m_height };
        }
    }

    inline double NextDouble()
    {
        static std::random_device rd;  // Will be used to obtain a seed for the random number engine
        static std::mt19937 gen(rd()); // Standard mersenne_twister_engine seeded with rd()
        static std::uniform_real_distribution<> dis(0.0, 1.0);
        return dis(gen);
    }

    void ParticlesAnimation::Prepare()
    {
        auto w = m_width * (1 / m_rasterizationScale);
        auto h = m_height * (1 / m_rasterizationScale);

        auto count = round(w * h / (35 * (IS_MOBILE ? 2 : 1)));
        count *= m_type == ParticlesType::Text ? 4 : 1;
        count = min(/*!liteMode.isAvailable('chat_spoilers') ? 400 :*/ IS_MOBILE ? 1000 : 2200, count);

        auto particles = NextPoints(count, m_width, m_height);
        m_particles.reserve(count);

        for (const auto& particle : particles)
        {
            m_particles.emplace_back(GenerateParticle(-1, particle));
        }
    }

    Particle ParticlesAnimation::GenerateParticle(int32_t type, const Point& position)
    {
        const auto threshold = m_type == ParticlesType::Status ? .2f : .8f;

        auto opacity = type == 1 ? 0 : NextDouble();
        auto radius = (NextDouble() >= threshold ? 1.f : 0.5f);
        auto adding = type == -1
            ? NextDouble() >= .5
            : type;

        auto padding = ceil(radius * m_rasterizationScale / 2);

        return Particle(
            max(padding, min(m_width - padding - 1, round(position.X))),
            max(padding, min(m_height - padding - 1, round(position.Y))),
            radius,
            opacity,
            adding);
    }
}
