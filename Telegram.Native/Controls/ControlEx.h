#pragma once

#include "Controls/ControlEx.g.h"

#include "FrameworkElementEx.h"

namespace winrt::Telegram::Native::Controls::implementation
{
    struct ControlEx : FrameworkElementEx<ControlEx, ControlExT<ControlEx>>
    {
        ControlEx() = default;
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct ControlEx : ControlExT<ControlEx, implementation::ControlEx>
    {
    };
}
