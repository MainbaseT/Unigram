#ifndef VOIP_VIDEO_OUTPUT_H
#define VOIP_VIDEO_OUTPUT_H

#include "pch.h"

#include <stddef.h>
#include <memory>
#include <mutex>

#include "api/video/i420_buffer.h"
#include "libyuv.h"

#include "api/video/video_frame.h"
#include "api/video/video_source_interface.h"
#include "media/base/video_adapter.h"
#include "media/base/video_broadcaster.h"

#include <winrt/Microsoft.Graphics.Canvas.h>
#include <winrt/Microsoft.Graphics.Canvas.Effects.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.System.h>

#include <winrt/Windows.UI.Xaml.h>

#include <Microsoft.Graphics.Canvas.h>
#include <Microsoft.Graphics.Canvas.native.h>

#include <d3d11.h>

#include "FrameReceivedEventArgs.h"

namespace ABI
{
    using namespace Microsoft::Graphics::Canvas;
}

using namespace winrt::Microsoft::Graphics::Canvas;
using namespace winrt::Microsoft::Graphics::Canvas::Effects;
using namespace winrt::Microsoft::Graphics::Canvas::UI::Composition;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::UI::Composition;

struct VoipVideoOutput : public rtc::VideoSinkInterface<webrtc::VideoFrame>
{
    std::atomic<bool> m_disposed{ false };
    std::atomic<bool> m_deviceLost{ false };
    std::atomic<bool> m_mirrored{ false };

    mutable std::recursive_mutex m_deviceMutex;
    mutable winrt::slim_mutex m_frameMutex;

    std::atomic<bool> m_resourcesValid{ false };

    PixelShaderEffect m_shader{ nullptr };
    CanvasBitmap m_bitmapY{ nullptr };
    CanvasBitmap m_bitmapU{ nullptr };
    CanvasBitmap m_bitmapV{ nullptr };

    winrt::com_ptr<winrt::Telegram::Native::Calls::implementation::FrameReceivedEventArgs> m_frameReceivedArgs;
    winrt::event<winrt::Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipVideoOutputSink,
        winrt::Telegram::Native::Calls::FrameReceivedEventArgs>> m_frameReceivedEventSource;

    CanvasDevice m_canvasDevice;
    CompositionSurfaceBrush m_brush{ nullptr };
    CompositionDrawingSurface m_surface{ nullptr };

    std::atomic<bool> m_pendingAsync{ false };

public:
    void ReleaseShader()
    {
        // Should be called under m_deviceMutex
        try
        {
            if (m_shader)
            {
                m_shader.Close();
                m_shader = nullptr;
            }
        }
        catch (...) {}

        try
        {
            if (m_bitmapY)
            {
                m_bitmapY.Close();
                m_bitmapY = nullptr;
            }
        }
        catch (...) {}

        try
        {
            if (m_bitmapU)
            {
                m_bitmapU.Close();
                m_bitmapU = nullptr;
            }
        }
        catch (...) {}

        try
        {
            if (m_bitmapV)
            {
                m_bitmapV.Close();
                m_bitmapV = nullptr;
            }
        }
        catch (...) {}

        m_resourcesValid = false;
    }

private:
    void UpdateBrushSurface()
    {
        m_pendingAsync = false;

        if (m_disposed.load())
        {
            return;
        }

        try
        {
            std::lock_guard<std::recursive_mutex> deviceGuard(m_deviceMutex);
            if (!m_disposed.load() && m_brush && m_surface)
            {
                m_brush.Surface(m_surface);
            }
        }
        catch (...)
        {
            // Ignore exceptions in async callback
        }
    }

    bool CheckAndHandleDeviceLost()
    {
        std::lock_guard<std::recursive_mutex> guard(m_deviceMutex);

        if (m_disposed.load())
        {
            return false;
        }

        try
        {
            if (m_canvasDevice && m_canvasDevice.IsDeviceLost())
            {
                m_deviceLost = true;
                m_resourcesValid = false;

                ReleaseShader();

                m_canvasDevice = CanvasDevice();
                if (m_brush)
                {
                    auto compositor = m_brush.Compositor();
                    auto compositionGraphicsDevice = CanvasComposition::CreateCompositionGraphicsDevice(compositor, m_canvasDevice);

                    if (m_surface)
                    {
                        try
                        {
                            m_surface.Close();
                        }
                        catch (...) {}
                    }

                    m_surface = compositionGraphicsDevice.CreateDrawingSurface(
                        { 0, 0 },
                        DirectXPixelFormat::B8G8R8A8UIntNormalized,
                        DirectXAlphaMode::Premultiplied);

                    // Safely queue surface update
                    if (!m_disposed.load() && !m_pendingAsync.load())
                    {
                        m_pendingAsync = true;
                        m_brush.DispatcherQueue().TryEnqueue({ this, &VoipVideoOutput::UpdateBrushSurface });
                    }
                }

                m_deviceLost = false;
                m_resourcesValid = true;
                return true;  // Device was recreated
            }
        }
        catch (...)
        {
            m_deviceLost = true;
            m_resourcesValid = false;
            return false;
        }

        return m_resourcesValid.load();
    }

public:
    VoipVideoOutput(SpriteVisual visual, bool mirrored)
    {
        try
        {
            m_frameReceivedArgs = winrt::make_self<winrt::Telegram::Native::Calls::implementation::FrameReceivedEventArgs>(0, 0);
            m_mirrored = mirrored;

            m_canvasDevice = CanvasDevice();
            auto compositor = visual.Compositor();
            auto compositionGraphicsDevice = CanvasComposition::CreateCompositionGraphicsDevice(compositor, m_canvasDevice);

            m_surface = compositionGraphicsDevice.CreateDrawingSurface(
                { 0, 0 },
                DirectXPixelFormat::B8G8R8A8UIntNormalized,
                DirectXAlphaMode::Premultiplied);

            m_brush = compositor.CreateSurfaceBrush(m_surface);
            m_brush.HorizontalAlignmentRatio(.5);
            m_brush.VerticalAlignmentRatio(.5);
            m_brush.Stretch(winrt::Windows::UI::Composition::CompositionStretch::Uniform);

            visual.Brush(m_brush);
            m_resourcesValid = true;
        }
        catch (...)
        {
            m_disposed = true;
            throw;
        }
    }

    ~VoipVideoOutput()
    {
        m_disposed = true;

        //// Wait for pending async operations to complete
        //while (m_pendingAsyncOps.load() > 0)
        //{
        //    std::this_thread::sleep_for(std::chrono::milliseconds(1));
        //}

        std::lock_guard<std::recursive_mutex> deviceGuard(m_deviceMutex);
        winrt::slim_lock_guard const frameGuard(m_frameMutex);

        try
        {
            ReleaseShader();
        }
        catch (...) {}

        try
        {
            if (m_surface)
            {
                m_surface.Close();
                m_surface = nullptr;
            }
        }
        catch (...) {}

        try
        {
            if (m_brush)
            {
                m_brush.Close();
                m_brush = nullptr;
            }
        }
        catch (...) {}
    }

    std::atomic<int32_t> m_pixelWidth{ 0 };
    std::atomic<int32_t> m_pixelHeight{ 0 };

    void OnFrame(const webrtc::VideoFrame& frame) override
    {
        if (m_disposed.load())
        {
            return;
        }

        if (!CheckAndHandleDeviceLost())
        {
            return;
        }

        winrt::slim_lock_guard const frameGuard(m_frameMutex);

        if (m_disposed.load() || !m_surface || !m_resourcesValid.load())
        {
            return;
        }

        try
        {
            rtc::scoped_refptr<webrtc::I420BufferInterface> buffer(frame.video_frame_buffer()->ToI420());

            int32_t width = buffer->width();
            int32_t height = buffer->height();

            auto sizeY = buffer->StrideY() * height;
            auto sizeUV = sizeY / 2;

            if (m_bitmapY == nullptr ||
                m_bitmapY.SizeInPixels().Width != width ||
                m_bitmapY.SizeInPixels().Height != height)
            {
                std::lock_guard<std::recursive_mutex> deviceGuard(m_deviceMutex);

                if (m_disposed.load()) return;

                ReleaseShader();

                auto format = DirectXPixelFormat::R8UIntNormalized;

                auto yView = winrt::array_view<uint8_t const>(buffer->DataY(), sizeY);
                m_bitmapY = CanvasBitmap::CreateFromBytes(m_canvasDevice, yView, width, height, format);
                auto uView = winrt::array_view<uint8_t const>(buffer->DataU(), sizeUV);
                m_bitmapU = CanvasBitmap::CreateFromBytes(m_canvasDevice, uView, width / 2, height / 2, format);
                auto vView = winrt::array_view<uint8_t const>(buffer->DataV(), sizeUV);
                m_bitmapV = CanvasBitmap::CreateFromBytes(m_canvasDevice, vView, width / 2, height / 2, format);

                if (m_shader == nullptr)
                {
                    // TODO: Consider embedding shader or using safer file loading
                    FILE* file = _wfopen(L"Assets\\i420.bin", L"rb");
                    if (file)
                    {
                        fseek(file, 0, SEEK_END);
                        size_t length = ftell(file);
                        fseek(file, 0, SEEK_SET);

                        std::unique_ptr<uint8_t[]> shaderData(new uint8_t[length]);
                        if (fread(shaderData.get(), 1, length, file) == length)
                        {
                            auto shaderView = winrt::array_view<uint8_t const>(shaderData.get(), shaderData.get() + length);
                            m_shader = PixelShaderEffect(shaderView);
                            m_shader.Source1BorderMode(EffectBorderMode::Hard);
                            m_shader.Source2BorderMode(EffectBorderMode::Hard);
                            m_shader.Source3BorderMode(EffectBorderMode::Hard);
                        }
                        fclose(file);
                    }
                }

                if (m_shader)
                {
                    m_shader.Source1(m_bitmapY);
                    m_shader.Source2(m_bitmapU);
                    m_shader.Source3(m_bitmapV);

                    // This is needed to force BGRA rendering
                    std::unique_ptr<uint8_t[]> fill(new uint8_t[width * height * 4]);
                    std::fill_n(fill.get(), width * height * 4, 0xFF);
                    auto bgra = winrt::array_view<uint8_t const>(fill.get(), width * height * 4);
                    m_shader.Source4(CanvasBitmap::CreateFromBytes(m_canvasDevice, bgra, width, height, DirectXPixelFormat::R8G8B8A8UIntNormalized));
                }

                m_resourcesValid = true;
            }
            else
            {
                m_bitmapY.as<ABI::ICanvasBitmap>()->SetPixelBytes(sizeY, (BYTE*)buffer->DataY());
                m_bitmapU.as<ABI::ICanvasBitmap>()->SetPixelBytes(sizeUV, (BYTE*)buffer->DataU());
                m_bitmapV.as<ABI::ICanvasBitmap>()->SetPixelBytes(sizeUV, (BYTE*)buffer->DataV());
            }

            if (m_shader && m_surface)
            {
                float3x2 matrix;
                auto finalSize = winrt::Windows::Foundation::Size(width, height);

                switch (frame.rotation())
                {
                case webrtc::kVideoRotation_0:
                    matrix = float3x2::identity();
                    break;
                case webrtc::kVideoRotation_180:
                    matrix = make_float3x2_rotation(180 * (M_PI / 180), float2(width / 2, height / 2));
                    break;
                case webrtc::kVideoRotation_90:
                    finalSize = winrt::Windows::Foundation::Size(height, width);
                    matrix = make_float3x2_rotation(90 * (M_PI / 180), float2(height / 2, width / 2));
                    break;
                case webrtc::kVideoRotation_270:
                    finalSize = winrt::Windows::Foundation::Size(height, width);
                    matrix = make_float3x2_rotation(270 * (M_PI / 180), float2(height / 2, width / 2));
                    break;
                }

                float x = (finalSize.Width - width) / 2;
                float y = (finalSize.Height - height) / 2;

                m_pixelWidth = m_frameReceivedArgs->m_pixelWidth = finalSize.Width;
                m_pixelHeight = m_frameReceivedArgs->m_pixelHeight = finalSize.Height;
                m_frameReceivedEventSource(nullptr, *m_frameReceivedArgs);

                if (finalSize != m_surface.Size())
                {
                    CanvasComposition::Resize(m_surface, finalSize);
                }

                auto drawingSession = CanvasComposition::CreateDrawingSession(m_surface);
                if (drawingSession)
                {
                    drawingSession.Transform(matrix * make_float3x2_scale(m_mirrored ? -1 : 1, 1, float2(finalSize.Width / 2, finalSize.Height / 2)));
                    drawingSession.DrawImage(m_shader, x, y);
                    drawingSession.Close();
                }
            }
        }
        catch (...)
        {
            m_resourcesValid = false;
        }
    }
};

#endif // VOIP_VIDEO_OUTPUT_H
