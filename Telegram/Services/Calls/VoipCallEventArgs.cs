//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Native.Calls;

namespace Telegram.Services.Calls
{
    public record VoipCallAudioLevelUpdatedEventArgs(float AudioLevel);

    public record VoipCallConnectionStateChangedEventArgs(VoipConnectionState State);

    public record VoipCallMediaStateChangedEventArgs(VoipAudioState Audio, VoipVideoState Video, bool IsScreenSharing);

    public record VoipCallRemoteBatteryLevelIsLowChangedEventArgs(bool IsLow);

    public record VoipCallSignalBarsUpdatedEventArgs(int Count);

    public record VoipCallStateChangedEventArgs(VoipState State, VoipReadyState ReadyState);
}
