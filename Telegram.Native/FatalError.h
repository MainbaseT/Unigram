#pragma once

#include "FatalError.g.h"

namespace winrt::Telegram::Native::implementation
{
    struct FatalError : FatalErrorT<FatalError>
    {
        FatalError(hstring type, hstring message, hstring stackTrace, winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> frames)
            : m_type(type)
            , m_message(message)
            , m_stackTrace(stackTrace)
            , m_frames(frames)
        {

        }

        hstring Type()
        {
            return m_type;
        }

        hstring Message()
        {
            return m_message;
        }

        hstring StackTrace()
        {
            return m_stackTrace;
        }

        winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> Frames()
        {
            return m_frames;
        }

    private:
        hstring m_type;
        hstring m_message;
        hstring m_stackTrace;
        winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> m_frames;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct FatalError : FatalErrorT<FatalError, implementation::FatalError>
    {
    };
}
