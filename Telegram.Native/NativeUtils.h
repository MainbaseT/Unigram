#pragma once

#include "NativeUtils.g.h"

#include "DebugUtils.h"

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>

#include <winrt/Windows.UI.Text.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Core.Direct.h>

typedef void (*td_log_message_callback_ptr)(int verbosity_level, const char* message);

using PFN_td_set_log_message_callback = WINUSERAPI void(WINAPI*)(int max_verbosity_level, td_log_message_callback_ptr callback);
using PFN_RhGetCurrentObjSize = WINUSERAPI INT64(WINAPI*)();
using PFN_RhCollect = WINUSERAPI void(WINAPI*)(int generation, int mode);

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::UI::Text;
using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Media;
using namespace winrt::Windows::UI::Xaml::Core::Direct;

template<typename Func>
inline void post_to_threadpool(Func&& func)
{
    auto* heapFunc = new std::decay_t<Func>(std::forward<Func>(func));

    PTP_WORK work = CreateThreadpoolWork(
        [](PTP_CALLBACK_INSTANCE, PVOID context, PTP_WORK) {
            std::unique_ptr<std::decay_t<Func>> funcPtr(
                static_cast<std::decay_t<Func>*>(context)
            );
            (*funcPtr)();
        },
        heapFunc,
        nullptr
    );

    SubmitThreadpoolWork(work);
    CloseThreadpoolWork(work);
}

namespace winrt::Telegram::Native::implementation
{
    struct NativeUtils : NativeUtilsT<NativeUtils>
    {
    public:
        static bool FileExists(hstring path);

        static int64_t GetDirectorySize(hstring path);
        static int64_t GetDirectorySize(hstring path, hstring filter);
        static void CleanDirectory(hstring path, int days);
        static void Delete(hstring path);

        static uint32_t GetLastInputTime();

        static IXamlDirectObject AddRunToCollection(XamlDirect direct, IXamlDirectObject inlines, hstring text, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize, bool transparent);
        static IXamlDirectObject AddRunToCollection(XamlDirect direct, IXamlDirectObject inlines, hstring text, int32_t offset, int32_t length, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize, bool transparent);

        //[DefaultOverload]
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value);
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value, int32_t offset);
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value, int32_t offset, int32_t length);
        //static int32_t GetDirectionality(char16 value);

        static hstring GetCurrentCulture();
        static hstring GetKeyboardCulture();

        static hstring FormatTime(winrt::Windows::Foundation::DateTime value);
        static hstring FormatDate(winrt::Windows::Foundation::DateTime value, hstring format);
        static hstring FormatDate(int year, int month, int day, hstring format);

        static hstring FormatTime(int value);
        static hstring FormatDate(int value, hstring format);

        static bool IsFileReadable(hstring path);
        static bool IsFileReadable(hstring path, int64_t& fileSize, int64_t& fileTime);

        static bool IsMediaSupported();

        static void OverrideScaleForCurrentView(int32_t value);
        static int32_t GetScaleForCurrentView();

        static void SetFatalErrorCallback(FatalErrorCallback action);
        static void SetCollectCallback(CollectCallback action, bool disableGcCollect, bool disablePressure);
        static void LogMessageCallback(int verbosity_level, const char* message);
        static winrt::Telegram::Native::FatalError GetStowedException();
        static winrt::Telegram::Native::FatalError GetBackTrace(hstring type, hstring message);

        static hstring GetLogMessage(int64_t format, int64_t args);

        static void Crash();

        static bool Collect()
        {
            std::lock_guard const guard(s_collectLock);
            return s_collect;
        }

        static void Collect(bool value);

        static FatalErrorCallback Callback;
        static CollectCallback s_collectCallback;

        static PFN_RhGetCurrentObjSize s_RhGetCurrentObjSize;
        static PFN_RhCollect s_RhCollect;

    private:
        static winrt::Telegram::Native::FatalError GetStowedException2(STOWED_EXCEPTION_INFORMATION_V2* stowed);

        static uint64_t GetDirectorySizeInternal(const std::wstring& path, const std::wstring& filter, uint64_t size);
        static void CleanDirectoryInternal(const std::wstring& path, int days);
        static bool IsBrowsePath(const std::wstring& path);
        static ULONGLONG FileTimeToSeconds(FILETIME& ft);
        static bool IsFileReadableInternal(hstring path, int64_t* fileSize, int64_t* fileTime);

        static Application::Suspending_revoker s_suspending;
        static Application::Resuming_revoker s_resuming;

        static std::mutex s_collectLock;
        static bool s_collect;
        static bool s_suspended;

        static INT64 RhGetCurrentObjSize()
        {
            return 0x7FFFFFFFFFFFFFFF;
        }

        static void RhCollect(int generation, int mode)
        {
            post_to_threadpool([&]() { s_collectCallback(generation, mode); });
        }

        static void OnSuspending(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::ApplicationModel::SuspendingEventArgs const& e);
        static void OnResuming(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::Foundation::IInspectable const& e);
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct NativeUtils : NativeUtilsT<NativeUtils, implementation::NativeUtils>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
