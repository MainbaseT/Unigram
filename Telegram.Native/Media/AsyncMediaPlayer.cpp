#include "pch.h"
#include "AsyncMediaPlayer.h"
#if __has_include("Media/AsyncMediaPlayer.g.cpp")
#include "Media/AsyncMediaPlayer.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayer::AsyncMediaPlayer(bool createGraphicsContext, bool debug, winrt::Windows::Foundation::Collections::IVector<hstring> options)
        : m_debug(debug)
        , m_dispatcherQueue(DispatcherQueue::GetForCurrentThread())
        , m_bufferingEventArgs(0.0f)
        , m_positionChangedEventArgs(0.0)
        , m_durationChangedEventArgs(0.0)
    {
        auto argc = 1 + static_cast<int>(options.Size());

        std::vector<std::string> argsStorage;
        argsStorage.reserve(argc);

        std::vector<const char*> argv;
        argv.reserve(argc);

        for (const auto& opt : options)
        {
            argsStorage.push_back(winrt::to_string(opt));
            argv.push_back(argsStorage.back().c_str());
        }

        argv.push_back("--aout=winstore");

        m_instance = libvlc_new(argc, argv.data());

        if (debug)
        {
            libvlc_log_set(m_instance, &LogCallback, this);
        }

        if (createGraphicsContext)
        {
            m_context = AsyncMediaPlayerSwapChain(true);
        }

        m_player = libvlc_media_player_new(m_instance);

        libvlc_event_manager_t* em = libvlc_media_player_event_manager(m_player);
        libvlc_event_attach(em, libvlc_MediaPlayerESSelected, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerVout, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerBuffering, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerEndReached, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerTimeChanged, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerLengthChanged, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerPlaying, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerPaused, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerStopped, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerAudioVolume, &EventCallback, this);
        libvlc_event_attach(em, libvlc_MediaPlayerEncounteredError, &EventCallback, this);

        m_defaultAudioRenderDeviceChanged = MediaDevice::DefaultAudioRenderDeviceChanged({ this, &AsyncMediaPlayer::OnDefaultAudioRenderDeviceChanged });
    }

    AsyncMediaPlayer::~AsyncMediaPlayer()
    {
        {
            std::lock_guard<std::mutex> lock(close_lock_);
            if (closed_) return;
        }

        Close();
    }

    void AsyncMediaPlayer::OnDefaultAudioRenderDeviceChanged(winrt::Windows::Foundation::IInspectable const& sender, DefaultAudioRenderDeviceChangedEventArgs const& args)
    {
        if (AudioDeviceRole::Default == args.Role())
        {
            Set(libvlc_audio_output_set, winrt::to_string(args.Id()).c_str());
        }
    }

    AsyncMediaPlayerSwapChain AsyncMediaPlayer::Context()
    {
        return m_context;
    }

    void AsyncMediaPlayer::Play(winrt::Windows::Foundation::Uri uri)
    {
        Write([this, uri]() {
            // #define CLOCK_FREQ         INT64_C(1000000)
            // #define DEFAULT_PTS_DELAY (3*CLOCK_FREQ/10)
            // INT64_C(1000) * var_InheritInteger(access, "network-caching")

            auto path = winrt::to_string(uri.AbsoluteUri());
            auto media = libvlc_media_new_location(m_instance, path.c_str());
            libvlc_media_add_option(media, ":network-caching=300");

            libvlc_media_player_set_media(m_player, media);
            libvlc_media_player_play(m_player);
            libvlc_media_release(media);
            }, true);
    }

    void AsyncMediaPlayer::Play()
    {
        Set(libvlc_media_player_play);
    }

    void AsyncMediaPlayer::Stop()
    {
        Set(libvlc_media_player_stop);
    }

    void AsyncMediaPlayer::Pause(bool pause)
    {
        Set(libvlc_media_player_set_pause, pause);
    }

    void AsyncMediaPlayer::Close()
    {
        std::lock_guard<std::mutex> lock(close_lock_);
        closed_ = true;
        work_queue_.clear();

        MediaDevice::DefaultAudioRenderDeviceChanged(m_defaultAudioRenderDeviceChanged);

        libvlc_event_manager_t* em = libvlc_media_player_event_manager(m_player);
        libvlc_event_detach(em, libvlc_MediaPlayerESSelected, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerVout, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerBuffering, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerEndReached, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerTimeChanged, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerLengthChanged, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerPlaying, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerPaused, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerStopped, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerAudioVolume, &EventCallback, this);
        libvlc_event_detach(em, libvlc_MediaPlayerEncounteredError, &EventCallback, this);

        libvlc_media_player_set_pause(m_player, true);

        if (m_debug)
        {
            libvlc_log_unset(m_instance);
        }

        if (m_context)
        {
            m_context.Destroy();
        }

        {
            std::lock_guard<std::mutex> lock(work_lock_);
            MediaPlayerCleanupManager::Close(m_instance, m_player, std::move(*work_thread_));
        }
    }

    AsyncMediaPlayerState AsyncMediaPlayer::State()
    {
        return (AsyncMediaPlayerState)Get(libvlc_media_player_get_state);
    }

    bool AsyncMediaPlayer::IsPlaying()
    {
        return Get(libvlc_media_player_is_playing);
    }

    bool AsyncMediaPlayer::CanPause()
    {
        return Get(libvlc_media_player_can_pause);
    }

    bool AsyncMediaPlayer::Mute()
    {
        return Get(libvlc_audio_get_mute);
    }

    void AsyncMediaPlayer::Mute(bool value)
    {
        Set(libvlc_audio_set_mute, value);
    }

    double AsyncMediaPlayer::Duration()
    {
        return Get(libvlc_media_player_get_length) / 1000.0;
    }

    double AsyncMediaPlayer::Position()
    {
        return Get(libvlc_media_player_get_time) / 1000.0;
    }

    void AsyncMediaPlayer::Position(double value)
    {
        auto time = static_cast<libvlc_time_t>(value * 1000);
        Set(libvlc_media_player_set_time, time);
    }

    void AsyncMediaPlayer::Seek(double value, bool relative)
    {
        auto time = static_cast<libvlc_time_t>(value * 1000);

        if (relative)
        {
            Write([this, time] { libvlc_media_player_set_time(m_player, libvlc_media_player_get_time(m_player) + time); });
        }
        else
        {
            Set(libvlc_media_player_set_time, time);
        }
    }

    float AsyncMediaPlayer::Rate()
    {
        return Get(libvlc_media_player_get_rate);
    }

    void AsyncMediaPlayer::Rate(float value)
    {
        Set(libvlc_media_player_set_rate, value);
    }

    int AsyncMediaPlayer::Volume()
    {
        return Get(libvlc_audio_get_volume);
    }

    void AsyncMediaPlayer::Volume(int value)
    {
        Set(libvlc_audio_set_volume, value);
    }

    void AsyncMediaPlayer::LogCallback(void* data, int level, const libvlc_log_t* ctx, const char* fmt, va_list args)
    {
        AsyncMediaPlayer* instance = static_cast<AsyncMediaPlayer*>(data);
        instance->HandleLog(level, ctx, fmt, args);
    }

    void AsyncMediaPlayer::HandleLog(int level, const libvlc_log_t* ctx, const char* fmt, va_list args)
    {
        int byteLength = vsnprintf(nullptr, 0, fmt, args) + 1;
        if (byteLength <= 1)
            return;

        char* buffer = new char[byteLength];
        vsprintf_s(buffer, byteLength, fmt, args);
        hstring message = winrt::to_hstring(std::string(buffer, byteLength - 1));
        delete[] buffer;

        const char* module;
        const char* file;
        unsigned int line = 0;
        libvlc_log_get_context(ctx, &module, &file, &line);

        // TODO:
        m_log(*this, AsyncMediaPlayerLogEventArgs((AsyncMediaPlayerLogLevel)level, message, winrt::to_hstring(module), winrt::to_hstring(file), line));
    }

    void AsyncMediaPlayer::EventCallback(const libvlc_event_t* event, void* user_data)
    {
        AsyncMediaPlayer* instance = static_cast<AsyncMediaPlayer*>(user_data);
        instance->HandleEvent(event);
    }

    void AsyncMediaPlayer::HandleEvent(const libvlc_event_t* event)
    {
        switch (event->type)
        {
        case libvlc_MediaPlayerESSelected:
        {
            auto trackId = event->u.media_player_es_changed.i_id;
            auto trackType = event->u.media_player_es_changed.i_type;
            TryEnqueue([weakThis{ get_weak() }, trackId, trackType]() {
                if (auto strongThis = weakThis.get())
                {
                    int width = 0;
                    int height = 0;

                    if (trackType == libvlc_track_video)
                    {
                        strongThis->GetVideoTrackInfo(trackId, width, height);
                    }

                    return strongThis->m_streamSelected(*strongThis, AsyncMediaPlayerStreamSelectedEventArgs(trackId, (AsyncMediaPlayerStreamType)trackType, width, height));
                }
                });
        }
        break;
        case libvlc_MediaPlayerVout:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_vout(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerBuffering:
        {
            auto cache = event->u.media_player_buffering.new_cache;
            TryEnqueue([weakThis{ get_weak() }, cache]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_bufferingEventArgs.Cache(cache);
                    return strongThis->m_buffering(*strongThis, strongThis->m_bufferingEventArgs);
                }
                });
        }
        break;
        case libvlc_MediaPlayerEndReached:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_endReached(*strongThis, nullptr);
                }
                });
            break;

        case libvlc_MediaPlayerTimeChanged:
        {
            auto position = event->u.media_player_time_changed.new_time / 1000.0;
            TryEnqueue([weakThis{ get_weak() }, position]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_positionChangedEventArgs.Position(position);
                    return strongThis->m_positionChanged(*strongThis, strongThis->m_positionChangedEventArgs);
                }
                });
        }
        break;
        case libvlc_MediaPlayerLengthChanged:
        {
            auto duration = event->u.media_player_length_changed.new_length / 1000.0;
            TryEnqueue([weakThis{ get_weak() }, duration]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_durationChangedEventArgs.Duration(duration);
                    return strongThis->m_durationChanged(*strongThis, strongThis->m_durationChangedEventArgs);
                }
                });
        }
        break;
        case libvlc_MediaPlayerPlaying:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_playing(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerPaused:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_paused(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerStopped:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_stopped(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerAudioVolume:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_volumeChanged(*strongThis, nullptr);
                }
                });
            break;

        case libvlc_MediaPlayerEncounteredError:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->m_encounteredError(*strongThis, nullptr);
                }
                });
            break;
        }
    }

    void AsyncMediaPlayer::TryEnqueue(DispatcherQueueHandler action)
    {
        {
            std::lock_guard<std::mutex> lock(close_lock_);
            if (closed_) return;
        }

        if (m_dispatcherQueue)
        {
            m_dispatcherQueue.TryEnqueue(action);
        }
        else
        {
            //ThreadPool.QueueUserWorkItem(state = > action());
        }
    }

    void AsyncMediaPlayer::GetVideoTrackInfo(int32_t trackId, int32_t& width, int32_t& height)
    {
        Read<int>([this, trackId, &width, &height]() {
            libvlc_media_t* media = libvlc_media_player_get_media(m_player);
            if (media)
            {
                libvlc_media_track_t** tracks;
                unsigned int trackCount = libvlc_media_tracks_get(media, &tracks);

                for (unsigned int i = 0; i < trackCount; ++i)
                {
                    if (tracks[i]->i_id == trackId && tracks[i]->i_type == libvlc_track_video)
                    {
                        auto* video = tracks[i]->video;
                        if (video)
                        {
                            width = video->i_width;
                            height = video->i_height;
                        }
                        break;
                    }
                }

                libvlc_media_tracks_release(tracks, trackCount);
                libvlc_media_release(media);
            }

            return 0;
            });
    }

    winrt::event_token AsyncMediaPlayer::Vout(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_vout.add(value);
    }

    void AsyncMediaPlayer::Vout(winrt::event_token const& token)
    {
        m_vout.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::StreamSelected(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerStreamSelectedEventArgs> const& value)
    {
        return m_streamSelected.add(value);
    }

    void AsyncMediaPlayer::StreamSelected(winrt::event_token const& token)
    {
        m_streamSelected.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::EndReached(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_endReached.add(value);
    }

    void AsyncMediaPlayer::EndReached(winrt::event_token const& token)
    {
        m_endReached.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Buffering(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerBufferingEventArgs> const& value)
    {
        return m_buffering.add(value);
    }

    void AsyncMediaPlayer::Buffering(winrt::event_token const& token)
    {
        m_buffering.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::PositionChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerPositionChangedEventArgs> const& value)
    {
        return m_positionChanged.add(value);
    }
    void AsyncMediaPlayer::PositionChanged(winrt::event_token const& token)
    {
        m_positionChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::DurationChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerDurationChangedEventArgs> const& value)
    {
        return m_durationChanged.add(value);
    }

    void AsyncMediaPlayer::DurationChanged(winrt::event_token const& token)
    {
        m_durationChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Playing(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_playing.add(value);
    }

    void AsyncMediaPlayer::Playing(winrt::event_token const& token)
    {
        m_playing.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Paused(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_paused.add(value);
    }

    void AsyncMediaPlayer::Paused(winrt::event_token const& token)
    {
        m_paused.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Stopped(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_stopped.add(value);
    }

    void AsyncMediaPlayer::Stopped(winrt::event_token const& token)
    {
        m_stopped.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::VolumeChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_volumeChanged.add(value);
    }

    void AsyncMediaPlayer::VolumeChanged(winrt::event_token const& token)
    {
        m_volumeChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::EncounteredError(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_encounteredError.add(value);
    }

    void AsyncMediaPlayer::EncounteredError(winrt::event_token const& token)
    {
        m_encounteredError.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Log(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerLogEventArgs> const& value)
    {
        return m_log.add(value);
    }

    void AsyncMediaPlayer::Log(winrt::event_token const& token)
    {
        m_log.remove(token);
    }
}
