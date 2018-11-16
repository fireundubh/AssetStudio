using System;
using System.Runtime.InteropServices;
using System.Text;
using FMOD;

namespace AssetStudio
{
    public class AudioClipConverter
    {
        private AudioClip m_AudioClip;

        public AudioClipConverter(AudioClip audioClip)
        {
            this.m_AudioClip = audioClip;
        }

        public byte[] ConvertToWav()
        {
            var exinfo = new CREATESOUNDEXINFO();
            RESULT result = Factory.System_Create(out FMOD.System system);
            if (result != RESULT.OK)
            {
                return null;
            }

            result = system.init(1, INITFLAGS.NORMAL, IntPtr.Zero);
            if (result != RESULT.OK)
            {
                return null;
            }

            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = (uint) this.m_AudioClip.m_Size;
            result = system.createSound(this.m_AudioClip.m_AudioData, MODE.OPENMEMORY, ref exinfo, out Sound sound);
            if (result != RESULT.OK)
            {
                return null;
            }

            result = sound.getSubSound(0, out Sound subsound);
            if (result != RESULT.OK)
            {
                return null;
            }

            result = subsound.getFormat(out SOUND_TYPE type, out SOUND_FORMAT format, out int NumChannels, out int BitsPerSample);
            if (result != RESULT.OK)
            {
                return null;
            }
            result = subsound.getDefaults(out float frequency, out int priority);
            if (result != RESULT.OK)
            {
                return null;
            }

            var SampleRate = (int) frequency;
            result = subsound.getLength(out uint length, TIMEUNIT.PCMBYTES);
            if (result != RESULT.OK)
            {
                return null;
            }

            result = subsound.@lock(0, length, out IntPtr ptr1, out IntPtr ptr2, out uint len1, out uint len2);
            if (result != RESULT.OK)
            {
                return null;
            }

            var buffer = new byte[len1 + 44];
            //添加wav头
            Encoding.UTF8.GetBytes("RIFF").CopyTo(buffer, 0);
            BitConverter.GetBytes(len1 + 36).CopyTo(buffer, 4);
            Encoding.UTF8.GetBytes("WAVEfmt ").CopyTo(buffer, 8);
            BitConverter.GetBytes(16).CopyTo(buffer, 16);
            BitConverter.GetBytes((short) 1).CopyTo(buffer, 20);
            BitConverter.GetBytes((short) NumChannels).CopyTo(buffer, 22);
            BitConverter.GetBytes(SampleRate).CopyTo(buffer, 24);
            BitConverter.GetBytes(SampleRate * NumChannels * BitsPerSample / 8).CopyTo(buffer, 28);
            BitConverter.GetBytes((short) (NumChannels * BitsPerSample / 8)).CopyTo(buffer, 32);
            BitConverter.GetBytes((short) BitsPerSample).CopyTo(buffer, 34);
            Encoding.UTF8.GetBytes("data").CopyTo(buffer, 36);
            BitConverter.GetBytes(len1).CopyTo(buffer, 40);
            Marshal.Copy(ptr1, buffer, 44, (int) len1);

            result = subsound.unlock(ptr1, ptr2, len1, len2);
            if (result != RESULT.OK)
            {
                return null;
            }

            subsound.release();
            sound.release();
            system.release();
            return buffer;
        }

        public string GetExtensionName()
        {
            if (this.m_AudioClip.version[0] < 5)
            {
                switch (this.m_AudioClip.m_Type)
                {
                    case AudioType.ACC:
                        return ".m4a";
                    case AudioType.AIFF:
                        return ".aif";
                    case AudioType.IT:
                        return ".it";
                    case AudioType.MOD:
                        return ".mod";
                    case AudioType.MPEG:
                        return ".mp3";
                    case AudioType.OGGVORBIS:
                        return ".ogg";
                    case AudioType.S3M:
                        return ".s3m";
                    case AudioType.WAV:
                        return ".wav";
                    case AudioType.XM:
                        return ".xm";
                    case AudioType.XMA:
                        return ".wav";
                    case AudioType.VAG:
                        return ".vag";
                    case AudioType.AUDIOQUEUE:
                        return ".fsb";
                }
            }
            else
            {
                switch (this.m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                        return ".fsb";
                    case AudioCompressionFormat.Vorbis:
                        return ".fsb";
                    case AudioCompressionFormat.ADPCM:
                        return ".fsb";
                    case AudioCompressionFormat.MP3:
                        return ".fsb";
                    case AudioCompressionFormat.VAG:
                        return ".vag";
                    case AudioCompressionFormat.HEVAG:
                        return ".vag";
                    case AudioCompressionFormat.XMA:
                        return ".wav";
                    case AudioCompressionFormat.AAC:
                        return ".m4a";
                    case AudioCompressionFormat.GCADPCM:
                        return ".fsb";
                    case AudioCompressionFormat.ATRAC9:
                        return ".at9";
                }
            }

            return ".AudioClip";
        }

        public bool IsFMODSupport
        {
            get
            {
                if (this.m_AudioClip.version[0] < 5)
                {
                    switch (this.m_AudioClip.m_Type)
                    {
                        case AudioType.AIFF:
                        case AudioType.IT:
                        case AudioType.MOD:
                        case AudioType.S3M:
                        case AudioType.XM:
                        case AudioType.XMA:
                        case AudioType.VAG:
                        case AudioType.AUDIOQUEUE:
                            return true;
                        default:
                            return false;
                    }
                }

                switch (this.m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                    case AudioCompressionFormat.Vorbis:
                    case AudioCompressionFormat.ADPCM:
                    case AudioCompressionFormat.MP3:
                    case AudioCompressionFormat.VAG:
                    case AudioCompressionFormat.HEVAG:
                    case AudioCompressionFormat.XMA:
                    case AudioCompressionFormat.GCADPCM:
                    case AudioCompressionFormat.ATRAC9:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}