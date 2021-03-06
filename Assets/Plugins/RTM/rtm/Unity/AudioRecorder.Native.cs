using System.Collections;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using AOT;
using UnityEngine.Assertions;

namespace com.fpnn.rtm
{
    public class AudioRecorderNative : Singleton<AudioRecorderNative>
    {
        public interface IAudioRecorderListener
        {
            void RecordStart();
            void RecordEnd();
            void OnRecord(RTMAudioData audioData);
            void OnVolumn(double db);
            void PlayEnd();
        }

        static internal IAudioRecorderListener audioRecorderListener;
        static internal string language;
        static volatile bool cancelRecord = false;

        delegate void VolumnCallbackDelegate(float volumn);
        [MonoPInvokeCallback(typeof(VolumnCallbackDelegate))]
        private static void VolumnCallback(float volumn)
        {
            if (audioRecorderListener != null)
            {
#if (UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX)
                float minValue = -60;
                float range = 60;
                float outRange = 100;
                if (volumn < minValue)
                    volumn = minValue;

                volumn = (volumn + range) / range * outRange;
#endif
                audioRecorderListener.OnVolumn(volumn);
            }
        }

        delegate void StartRecordCallbackDelegate();
        [MonoPInvokeCallback(typeof(StartRecordCallbackDelegate))]
        private static void StartRecordCallback()
        {
            if (audioRecorderListener != null)
                audioRecorderListener.RecordStart();
        }

        delegate void StopRecordCallbackDelegate(IntPtr data, int length, long time);
        [MonoPInvokeCallback(typeof(StopRecordCallbackDelegate))]
        private static void StopRecordCallback(IntPtr data, int length, long time)
        {
            if (audioRecorderListener != null)
            {
                audioRecorderListener.RecordEnd();
                if (cancelRecord)
                {
                    cancelRecord = false;
                    return;
                }
                byte[] payload = new byte[length];
                Marshal.Copy(data, payload, 0, length);

                RTMAudioData audioData = new RTMAudioData(payload, language, time);
                audioRecorderListener.OnRecord(audioData);
           }
        }

        delegate void PlayFinishCallbackDelegate();
        [MonoPInvokeCallback(typeof(PlayFinishCallbackDelegate))]
        private static void PlayFinishCallback()
        {
            if (audioRecorderListener != null)
                audioRecorderListener.PlayEnd();
        }

#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void startRecord(VolumnCallbackDelegate callback, StartRecordCallbackDelegate startCallback);

        [DllImport("__Internal")]
        private static extern void stopRecord(StopRecordCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void startPlay(byte[] data, int length, PlayFinishCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void stopPlay();
#elif UNITY_ANDROID

#else
        [DllImport("RTMNative")]
        private static extern void startRecord(VolumnCallbackDelegate callback, StartRecordCallbackDelegate startCallback);

        [DllImport("RTMNative")]
        private static extern void stopRecord(StopRecordCallbackDelegate callback);

        [DllImport("RTMNative")]
        private static extern void startPlay(byte[] data, int length, PlayFinishCallbackDelegate callback);

        [DllImport("RTMNative")]
        private static extern void stopPlay();

        [DllImport("RTMNative")]
        private static extern void playWithPath(byte[] data, int length, PlayFinishCallbackDelegate callback);
#endif

#if UNITY_ANDROID
        class AudioRecordAndroidProxy : AndroidJavaProxy
        {
            public AudioRecordAndroidProxy() : base("com.NetForUnity.IAudioAction")
            {
            }

            public void startRecord(bool success, string errorMsg)
            {
                if (AudioRecorderNative.audioRecorderListener != null && success)
                    AudioRecorderNative.audioRecorderListener.RecordStart();
            }

            public void stopRecord()
            {
                if (AudioRecorderNative.audioRecorderListener != null)
                    AudioRecorderNative.audioRecorderListener.RecordEnd();
            }

            public void broadFinish()
            {
                if (AudioRecorderNative.audioRecorderListener != null)
                    AudioRecorderNative.audioRecorderListener.PlayEnd();
            }

            public void listenVolume(double db)
            {
                if (AudioRecorderNative.audioRecorderListener != null)
                    AudioRecorderNative.audioRecorderListener.OnVolumn(db);
            }
        }
        static AndroidJavaObject AudioRecord = null;
#endif
        public void Init(string language, IAudioRecorderListener listener)
        {
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
            Assert.IsTrue(false, "windows is not supported for now");
#endif
            AudioRecorderNative.language = language;
            audioRecorderListener = listener;
#if UNITY_ANDROID
            AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject appconatext = jc.GetStatic<AndroidJavaObject>("currentActivity");
            if (AudioRecord == null)
            {
                AndroidJavaClass playerClass = new AndroidJavaClass("com.NetForUnity.RTMAudio");
                AudioRecord = playerClass.CallStatic<AndroidJavaObject>("getInstance");
            }
            AudioRecord.Call("init", appconatext, language, new AudioRecordAndroidProxy());
#else

#endif
        }

        public void StartRecord()
        {
#if UNITY_ANDROID
            if (AudioRecord != null)
                AudioRecord.Call("startRecord");
#else
            startRecord(VolumnCallback, StartRecordCallback);
#endif
        }

        public void StopRecord()
        {
#if UNITY_ANDROID
            AndroidJavaObject audio = AudioRecord.Call<AndroidJavaObject>("stopRecord");
            int duration = audio.Get<int>("duration");
            byte[] audioData = audio.Get<byte[]>("audioData");
            //byte[] audioData = (byte[])(Array)audio.Get<sbyte[]>("audioData");
            if (cancelRecord)
            {
                cancelRecord = false;
                return;
            }
            if (audioRecorderListener != null)
            {
                RTMAudioData data = new RTMAudioData(audioData, language, duration);
                audioRecorderListener.OnRecord(data);
            }
#else
            stopRecord(StopRecordCallback);
#endif
        }

        public void CancelRecord()
        {
            cancelRecord = true;
            StopRecord();
        }

        public void Play(RTMAudioData data)
        {
#if UNITY_ANDROID
            if (AudioRecord != null)
                AudioRecord.Call("broadAudio", AudioConvert.ConvertToWav(data.Audio));
#else
            startPlay(data.Audio, data.Audio.Length, PlayFinishCallback);
#endif
        }

        public void StopPlay()
        {
#if UNITY_ANDROID
            if (AudioRecord != null)
                AudioRecord.Call("stopAudio");
#else
            stopPlay();
#endif
        }
    }
}