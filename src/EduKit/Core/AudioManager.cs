using System;
using System.Runtime.InteropServices;

namespace EduKit.Core
{
    /// <summary>系统主音量控制（Windows 7+ Core Audio API），失败时自动降级为媒体键。</summary>
    public static class AudioManager
    {
        private const int ClsctxAll = 23;
        private const int ERender = 0;
        private const int EConsole = 0;
        private const int EMultimedia = 1;

        private static IAudioEndpointVolume _volume;

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MmDeviceEnumeratorComObject
        {
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeNotify(IntPtr notify);

            [PreserveSig]
            int UnregisterControlChangeNotify(IntPtr notify);

            [PreserveSig]
            int GetChannelCount(out int count);

            [PreserveSig]
            int SetMasterVolumeLevel(float levelDb, IntPtr context);

            [PreserveSig]
            int SetMasterVolumeLevelScalar(float level, IntPtr context);

            [PreserveSig]
            int GetMasterVolumeLevel(out float levelDb);

            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float level);

            [PreserveSig]
            int SetChannelVolumeLevel(int channel, float levelDb, IntPtr context);

            [PreserveSig]
            int SetChannelVolumeLevelScalar(int channel, float level, IntPtr context);

            [PreserveSig]
            int GetChannelVolumeLevel(int channel, out float levelDb);

            [PreserveSig]
            int GetChannelVolumeLevelScalar(int channel, out float level);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr context);

            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        public static bool IsAvailable
        {
            get
            {
                EnsureVolume();
                return _volume != null;
            }
        }

        private static bool EnsureVolume()
        {
            if (_volume != null)
            {
                return true;
            }

            try
            {
                var enumerator = (IMMDeviceEnumerator)new MmDeviceEnumeratorComObject();
                IMMDevice device;
                var hr = enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out device);
                if (hr != 0 || device == null)
                {
                    hr = enumerator.GetDefaultAudioEndpoint(ERender, EMultimedia, out device);
                }

                if (hr != 0 || device == null)
                {
                    return false;
                }

                var iid = typeof(IAudioEndpointVolume).GUID;
                object volumeObject;
                hr = device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out volumeObject);
                if (hr != 0 || volumeObject == null)
                {
                    return false;
                }

                _volume = (IAudioEndpointVolume)volumeObject;
                return true;
            }
            catch
            {
                _volume = null;
                return false;
            }
        }

        /// <summary>获取主音量百分比（0–100），不可用时返回 -1。</summary>
        public static int GetVolume()
        {
            if (!EnsureVolume())
            {
                return -1;
            }

            try
            {
                float scalar;
                if (_volume.GetMasterVolumeLevelScalar(out scalar) == 0)
                {
                    return (int)Math.Round(scalar * 100);
                }
            }
            catch
            {
                _volume = null;
            }

            return -1;
        }

        public static bool SetVolume(int percent)
        {
            if (!EnsureVolume())
            {
                return false;
            }

            try
            {
                return _volume.SetMasterVolumeLevelScalar(Math.Max(0f, Math.Min(1f, percent / 100f)), IntPtr.Zero) == 0;
            }
            catch
            {
                _volume = null;
                return false;
            }
        }

        public static bool? GetMuted()
        {
            if (!EnsureVolume())
            {
                return null;
            }

            try
            {
                bool muted;
                if (_volume.GetMute(out muted) == 0)
                {
                    return muted;
                }
            }
            catch
            {
                _volume = null;
            }

            return null;
        }

        public static bool SetMuted(bool muted)
        {
            if (!EnsureVolume())
            {
                return false;
            }

            try
            {
                return _volume.SetMute(muted, IntPtr.Zero) == 0;
            }
            catch
            {
                _volume = null;
                return false;
            }
        }

        public static void VolumeUp()
        {
            if (GetVolume() < 0)
            {
                Native.MediaKey(Native.VkVolumeUp);
            }
            else
            {
                SetVolume(GetVolume() + 2);
            }
        }

        public static void VolumeDown()
        {
            if (GetVolume() < 0)
            {
                Native.MediaKey(Native.VkVolumeDown);
            }
            else
            {
                SetVolume(GetVolume() - 2);
            }
        }

        public static void ToggleMute()
        {
            var muted = GetMuted();
            if (muted == null)
            {
                Native.MediaKey(Native.VkVolumeMute);
            }
            else
            {
                SetMuted(!muted.Value);
            }
        }
    }
}
