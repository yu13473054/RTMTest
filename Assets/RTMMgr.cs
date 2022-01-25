using System;
using System.Collections;
using System.Collections.Generic;
using com.fpnn;
using com.fpnn.rtm;
using UnityEngine;
using UnityEngine.Networking;
using FileInfo = com.fpnn.rtm.FileInfo;
using Object = UnityEngine.Object;

namespace PVP.Game
{
    //历史消息结构体
    public class HistoryStruct
    {
        public long uid;
        public Action<RTMMsgInfo> historyCallback;
        public Action<long> succBack;
        public int reqCount;
        public List<RTMMsgInfo> rspList;

        public HistoryStruct(long uid, int count, Action<RTMMsgInfo> cb, Action<long> succBack)
        {
            this.uid = uid;
            this.reqCount = count;
            this.historyCallback = cb;
            this.succBack = succBack;
            rspList = new List<RTMMsgInfo>();
        }
    }

    public class RTMMgr
    {
        private RTMMgr()
        {
        }

        private static readonly RTMMgr _inst = new RTMMgr();
        public static RTMMgr Inst => _inst;

        private const string RTMENDPOINT = "rtm-nx-front.ilivedata.com:13321";
        private const string RTCENDPOINT = "rtc-nx-front.ilivedata.com:13702";
        private const long PID = 80000217;

        private RTMClient _client;
        private long _uid;
        private Action<RTMMsgInfo> _pushCallback;
        private int _audioType;
        private long _audioTypeId;
        private string _attrs_str;
        private Action<long, string> _speech2TextCB;
        private HashSet<long> _set;

        private Dictionary<long, bool> _roomDic;
        private Dictionary<long, HistoryStruct> _historyDic;
        private RTMPVPQuestProcessor _rtmpvpQuestProcessor;

        private readonly Queue<RTMMsgInfo> _msgQueue = new Queue<RTMMsgInfo>();
        private readonly Queue<Action> _callBackQueue = new Queue<Action>();
        private readonly object lockObj = new object();
        
        //SDK初始化
        public void Init()
        {
            _historyDic = new Dictionary<long, HistoryStruct>();
            _roomDic = new Dictionary<long, bool>();
            com.fpnn.common.ErrorRecorder errorRecorder = new PVPErrorRecorder();
            ClientEngine.Init(new com.fpnn.Config()
            {
                errorRecorder = new PVPErrorRecorder()
            });
            RTMControlCenter.Init(new RTMConfig()
            {
                defaultErrorRecorder = errorRecorder
            });
            _rtmpvpQuestProcessor = new RTMPVPQuestProcessor();

            //初始化录音相关
#if UNITY_ANDROID || UNITY_IOS
            AudioRecorderNative.Instance.Init("zh-CN", new PVPAudioRecorder());
#endif
            _set = new HashSet<long>();
        }

        public void Release()
        {
            _pushCallback = null;
            _speech2TextCB = null;
            _client = null;
            _historyDic.Clear();
            _historyDic = null;
            _msgQueue.Clear();
            _roomDic.Clear();
#if UNITY_EDITOR
            RTCEngine.Stop();
            RTMControlCenter.Close();
            ClientEngine.Close();
#endif
        }

        public void Login(long uid, string token, Action<RTMMsgInfo> pushCallback, Action succCallBack = null,
            Action faildCallback = null)
        {
            _uid = uid;
            _pushCallback = pushCallback;
            _client = new RTMClient(RTMENDPOINT, RTCENDPOINT, PID, uid, _rtmpvpQuestProcessor);
            
            void ObjAction(long projectId, long c_uid, bool successful, int errorCode)
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (successful)
                        {
                            Debug.Log("RTM: login success.");
                            succCallBack?.Invoke();
                        }
                        else
                        {
                            Debug.LogError("RTM: login failed, error code: " + errorCode);
                            faildCallback?.Invoke();
                            _client = null;
                        }
                        
                    });
                }
            }
            bool flag = _client.Login(ObjAction, token);
            Debug.Log("login    "+flag);
        }

        public void EnterRoom(long roomId)
        {
            if (_client == null) return;
            if (_roomDic.ContainsKey(roomId))
                return;
            _roomDic.Add(roomId, true);
            void ObjAction(int code)
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (code != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            _roomDic.Remove(roomId);
                            Debug.LogError("RTM：enter room failed. errorCode = " + code);
                        }
                    });
                }
            }
            _client.EnterRoom(ObjAction, roomId);
        }

        public void LeaveRoom(long roomId)
        {
            if (_client == null) return;
            if(!_roomDic.ContainsKey(roomId))
                return;
            _roomDic.Remove(roomId);
            
            void ObjAction(int code)
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (code != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            _roomDic.Add(roomId, true); //失败后，再保存回来
                            Debug.LogError("RTM：leave room failed. errorCode = " + code);
                        }
                    });
                }
            }
            _client.LeaveRoom(ObjAction, roomId);
        }

        //获取房间对应的人数
        public void GetRoomMemberCount(long roomId, Action<int> cb)
        {
            if (_client == null) return;
            _set.Clear();
            _set.Add(roomId);
            _client.GetRoomMemberCount((rstDic, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode == com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            rstDic.TryGetValue(roomId, out var num);
                            cb?.Invoke(num);
                        }
                        else
                        {
                            Debug.LogError("RTM: GetRoomMemberCount failed, error code: " + errorCode);
                        }
                        
                    });
                }
            }, _set);
        }

        //查询房间并加入
        public void SearchRoomAndEnter(int max_channel, int up_limit, long mainType, Action<long> call_back)
        {
            if (_client == null) return;
            _set.Clear();
            for (long i = 1; i <= max_channel; i++)
            {
                long v = mainType | i;
                _set.Add(v);
            }
            _client.GetRoomMemberCount((rstDic, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode == com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            foreach (var pair in rstDic)
                            {
                                if (pair.Value < up_limit)
                                {
                                    EnterRoom(pair.Key);
                                    call_back?.Invoke(pair.Key);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Debug.LogError("RTM: GetRoomMemberCount failed, error code: " + errorCode);
                            call_back?.Invoke(0);
                        }
                    });
                }
            }, _set);
        }

        /// <summary>
        /// 发送文本消息
        /// </summary>
        /// <param name="type">0：P2P；1：Group；2：Room</param>
        /// <param name="typeId">对应类型所需要的id</param>
        public void SendMessageAsync(int type, long typeId, Action<long, int> cb = null, string textStr = null, byte[] message = null,
            byte[] attrs = null, byte mtype = 52)
        {
            if (_client == null) return;
            if (mtype < 51)
                Debug.LogError("<RTMMgr> mType 必须在51到127之间！");
            string attr_str = "";
            if (attrs != null)
                attr_str = Convert.ToBase64String(attrs);

            void ObjAction(long id, int code)
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (code != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            Debug.Log("RTM：Send chat message failed. errorCode = " + code);
                            cb?.Invoke(-1, -1);
                        }
                        else
                            cb?.Invoke(id, code);
                    });
                }
            }

            Chat_Msg_Type msgType = (Chat_Msg_Type) type;
            switch (msgType)
            {
                case Chat_Msg_Type.MsgP2P:
                    if (message == null)
                        _client.SendMessage(ObjAction, typeId, mtype, textStr, attr_str);
                    else
                        _client.SendMessage(ObjAction, typeId, mtype, message, attr_str);
                    break;
                case Chat_Msg_Type.MsgGroup:
                    if (message == null)
                        _client.SendGroupMessage(ObjAction, typeId, mtype, textStr, attr_str);
                    else
                        _client.SendGroupMessage(ObjAction, typeId, mtype, message, attr_str);
                    break;
                case Chat_Msg_Type.MsgRoom:
                    EnterRoom(typeId);
                    if (message == null)
                        _client.SendRoomMessage(ObjAction, typeId, mtype, textStr, attr_str);
                    else
                        _client.SendRoomMessage(ObjAction, typeId, mtype, message, attr_str);
                    break;
            }
        }
        public long SendMessage(int type, long typeId, byte mtype, string textStr = null, byte[] message = null,
            byte[] attrs = null)
        {
            if (_client == null) return -1;
            if (mtype < 51)
                Debug.LogError("<RTMMgr> mType 必须在51到127之间！");
            string attr_str = "";
            if (attrs != null)
                attr_str = Convert.ToBase64String(attrs);

            Chat_Msg_Type msgType = (Chat_Msg_Type) type;
            int errorCode = -1;
            long messageId = -1;
            switch (msgType)
            {
                case Chat_Msg_Type.MsgP2P:
                    if (message == null)
                        errorCode = _client.SendMessage(out messageId, typeId, mtype, textStr, attr_str);
                    else
                        errorCode = _client.SendMessage(out messageId, typeId, mtype, message, attr_str);
                    break;
                case Chat_Msg_Type.MsgGroup:
                    if (message == null)
                        errorCode = _client.SendGroupMessage(out messageId, typeId, mtype, textStr, attr_str);
                    else
                        errorCode = _client.SendGroupMessage(out messageId, typeId, mtype, message, attr_str);
                    break;
                case Chat_Msg_Type.MsgRoom:
                    EnterRoom(typeId);
                    if (message == null)
                        errorCode = _client.SendRoomMessage(out messageId, typeId, mtype, textStr, attr_str);
                    else
                        errorCode = _client.SendRoomMessage(out messageId, typeId, mtype, message, attr_str);
                    break;
            }

            if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            {
                Debug.Log("RTM：Send chat message failed. errorCode = " + errorCode);
            }

            return messageId;
        }

        /// <summary>
        /// 发送文本消息
        /// </summary>
        /// <param name="type">0：P2P；1：Group；2：Room</param>
        /// <param name="typeId">对应类型所需要的id</param>
        public void SendChat(int type, long typeId, string textStr, byte[] attrs, Action<long, int> cb = null)
        {
            if (_client == null) return;
            string attr_str = "";
            if (attrs != null)
                attr_str = Convert.ToBase64String(attrs);

            void ObjAction(long id, int errorCode)
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            Debug.Log("RTM：Send chat message failed. errorCode = " + errorCode);
                            cb?.Invoke(-1, -1);
                        }
                        else
                            cb?.Invoke(id, errorCode);
                    });
                }
            }
            Chat_Msg_Type msgType = (Chat_Msg_Type) type;
            switch (msgType)
            {
                case Chat_Msg_Type.MsgP2P:
                    _client.SendChat(ObjAction, typeId, textStr, attr_str);
                    break;
                case Chat_Msg_Type.MsgGroup:
                    _client.SendGroupChat(ObjAction, typeId, textStr, attr_str);
                    break;
                case Chat_Msg_Type.MsgRoom:
                    EnterRoom(typeId);
                    _client.SendRoomChat(ObjAction, typeId, textStr, attr_str);
                    break;
            }
        }
        
        //文本审核
        public void TextAudit(string text, Action<int, string> callBack)
        {
            if (_client == null) return;
            _client.TextCheck((result, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            Debug.Log("RTM: TextCheck in sync failed, error " + errorCode);
                            callBack(errorCode, string.Empty); //检查出错
                        }
                        else
                        {
                            int rst = result.result == 0 ? -1 : -2; // -1 表示通过，用负数防止和errorCode重合. 	0:通过，1:建议审核，2:不通过
                            callBack(rst, result.text);
                            //result.tags 敏感类型
                            //result.wlist 筛查出来的敏感内容
                        }
                    });
                }
            }, text);
        }

        //图片审核
        public void ImageAudit(byte[] imageContent, Action<int> callBack)
        {
            if (_client == null) return;
            
            _client.ImageCheck((result, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            Debug.Log("RTM: ImageCheck in sync failed, error " + errorCode);
                            callBack(errorCode); //检查出错
                        }
                        else
                        {
                            int rst = result.result == 0 ? -1 : -2; // -1 表示通过，用负数防止和errorCode重合
                            callBack(rst);
                            //result.tags 敏感类型
                        }
                    });
                }
            }, imageContent);
        }

        private IEnumerator CoPlayAudio(AudioSource source, string url, string lang, int duration)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            _audioDownloadCo = null;
            if(!string.IsNullOrEmpty(request.error))
                yield break;
            byte[] audioArray = request.downloadHandler.data;
            PlayAudio(source, audioArray, lang, duration);
        }

        private Coroutine _audioDownloadCo;
        public void PlayAudio(AudioSource source, string url, string lang, int duration)
        {
            if (string.IsNullOrEmpty(url))
                return;
            // if(_audioDownloadCo != null)
            //     Main.Instance.StopCoroutine(_audioDownloadCo);
            // _audioDownloadCo = Main.Instance.StartCoroutine(CoPlayAudio(source, url, lang, duration));
        }

        public void PlayAudio(AudioSource audioSource, byte[] audioArray, string lang, int duration)
        {
            if(audioArray == null)
                return;
            
            // create the RTMAudioData instance
            RTMAudioData audioData = new RTMAudioData(audioArray, new FileInfo()
            {
                duration = duration,
                language = lang
            });
            
            AudioRecorderNative.Instance.Play(audioData);
            
            // //卸载老资源
            // if (audioSource.clip)
            //     Object.Destroy(audioSource.clip);
            // //播放新资源
            // audioSource.clip = AudioClip.Create("testSound", audioData.LengthSamples, 1, audioData.Frequency, false);
            // audioSource.clip.SetData(audioData.PcmData, 0);
            // audioSource.Play();
        }

        public void StopPlay()
        {
            AudioRecorderNative.Instance.StopPlay();
        }

        //开始录音
        public void StartRecord()
        {
#if UNITY_ANDROID || UNITY_IOS
            Debug.Log("RTM: StartRecord");
            AudioRecorderNative.Instance.StartRecord();
#endif
        }

        /// <summary>
        /// 停止录音
        /// </summary>
        /// <param name="type">0：P2P；1：Group；2：Room</param>
        /// <param name="typeId">对应类型所需要的id</param>
        public void StopRecord(int type, long typeId, byte[] attrs)
        {
            if (_client == null) return;
            _audioType = type;
            _audioTypeId = typeId;
            if(attrs != null)
                _attrs_str = Convert.ToBase64String(attrs);
            else
            {
                _attrs_str = "";
            }
#if UNITY_ANDROID || UNITY_IOS
            Debug.Log("RTM: StopRecord");
            AudioRecorderNative.Instance.StopRecord();
#else
            ////////桌面测试逻辑////////
            Chat_Msg_Type msgType = (Chat_Msg_Type) _audioType;
            int errorCode = -1;
            long messageId = -1;
            switch (msgType)
            {
                case Chat_Msg_Type.MsgP2P:
                    errorCode = _client.SendChat(out messageId, _audioTypeId, "", _attrs_str);
                    break;
                case Chat_Msg_Type.MsgGroup:
                    errorCode = _client.SendGroupChat(out messageId, _audioTypeId, "", _attrs_str);
                    break;
                case Chat_Msg_Type.MsgRoom:
                    EnterRoom(_audioTypeId);
                    errorCode = _client.SendRoomChat(out messageId, _audioTypeId, "", _attrs_str);
                    break;
            }

            if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            {
                Debug.Log("RTM：Send audio failed. errorCode = " + errorCode);
                return;
            }

            //转发到lua层
            RTMMsgInfo info = new RTMMsgInfo()
            {
                type = _audioType,
                typeValue = _audioTypeId,
                messageType = 1,
                messageId = messageId,
                attrs = Convert.FromBase64String(_attrs_str),
                fromUid = _uid,
                isRTMAudio = true,
                language = Lan2Str(LanMgr.Instance.CurLanguageType),
                duration = Random.Range(5, 30),
                audioData = null
            };
            _pushCallback(info);
            ////////桌面测试逻辑////////
#endif
        }

        /// <summary>
        /// 停止录音, 并转成文字
        /// </summary>
        /// <param name="type">0：P2P；1：Group；2：Room</param>
        /// <param name="typeId">对应类型所需要的id</param>
        public void StopRecordAndToText(Action<long, string> callback)
        {
            if (_client == null) return;
            _speech2TextCB = callback;
#if UNITY_ANDROID || UNITY_IOS
            AudioRecorderNative.Instance.StopRecord();
#else
            ////////桌面测试逻辑////////
            Main.Instance.StartCoroutine(Speech2Text_Test(_speech2TextCB));
            _speech2TextCB = null;
            ////////桌面测试逻辑////////
#endif
        }

#if UNITY_EDITOR
        private IEnumerator Speech2Text_Test(Action<long, string> callback)
        {
            yield return new WaitForSeconds(2);
            callback(0, "这是语音转换后的文本");
        }
#endif

        public float GetLoudness()
        {
            if (_client == null) return 0;
#if UNITY_ANDROID || UNITY_IOS
            return AudioRecorder.Instance.GetRelativeLoudness(0.0001f);
#else
            return Random.Range(0.0f, 1f);
#endif
        }

        //取消录音
        public void CancelRecord()
        {
            if (_client == null) return;
#if UNITY_ANDROID || UNITY_IOS
            AudioRecorderNative.Instance.CancelRecord();
#endif
            _speech2TextCB = null;
        }

        /// 发送文件消息，例如音频
        public void SendAudio(RTMAudioData audioData)
        {
            lock (lockObj)
            {
                if (_client == null) return;
                _callBackQueue.Enqueue(() =>
                {
                    if (_speech2TextCB != null) //语音转文字的相关接口
                    {
                        SpeechToText(0, audioData.Audio, audioData.Language, _speech2TextCB);
                        _speech2TextCB = null;
                        return;
                    }
        
                    void ObjAction(long messageId, int errorCode)
                    {
                        Debug.Log("sendAudio  11111  "+messageId);
                        lock (lockObj)
                        {
                            _callBackQueue.Enqueue(() =>
                            {
                                
                                if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
                                {
                                    Debug.Log("RTM：Send audio failed. errorCode = " + errorCode);
                                    return;
                                }
                    
                                Debug.Log("sendAudio  222222  "+messageId);
                                //转发到lua层
                                RTMMsgInfo info = new RTMMsgInfo()
                                {
                                    type = _audioType,
                                    typeValue = _audioTypeId,
                                    messageType = 1,
                                    messageId = messageId,
                                    // attrs = Convert.FromBase64String(_attrs_str),
                                    fromUid = _uid,
                                    isRTMAudio = true,
                                    language = audioData.Language,
                                    duration = (int) audioData.Duration,
                                    audioData = audioData.Audio
                                };
                                _pushCallback(info);
                            });
                        }
                    }
                    
                    Chat_Msg_Type msgType = (Chat_Msg_Type) _audioType;
                    switch (msgType)
                    {
                        case Chat_Msg_Type.MsgP2P:
                            _client.SendFile(ObjAction, _audioTypeId, audioData, _attrs_str);
                            break;
                        case Chat_Msg_Type.MsgGroup:
                            _client.SendGroupFile(ObjAction, _audioTypeId, audioData, _attrs_str);
                            break;
                        case Chat_Msg_Type.MsgRoom:
                            EnterRoom(_audioTypeId);
                            // _client.SendRoomFile(ObjAction, _audioTypeId, audioData, _attrs_str);
                            long msgID;
                            int errorCode = _client.SendRoomFile(out msgID, _audioTypeId, audioData, _attrs_str);
                            ObjAction(msgID, errorCode);
                            break;
                    }
                });
            }
        }

        public void Translate(long messageId, string text, int sourceLang, Action<long, string> cb)
        {
            if (_client == null) return;

            void ObjAction(TranslatedInfo info, int errorCode)
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            Debug.Log("RTM：Translate failed. errorCode = " + errorCode);
                        }
                        else
                            cb?.Invoke(messageId, info.targetText);
                    });
                }
            }
            // _client.Translate(ObjAction, text, Lan2Str(LanMgr.Instance.CurLanguageType), Lan2Str((LanguageType) sourceLang));
        }

        /// <summary>
        /// 语音转文字
        /// </summary>
        /// <param name="callback">成功回调</param>
        /// <param name="url">语音URL</param>
        /// <param name="lang">语音的语种</param>
        public void SpeechToText(long messageId, string url, string lang, Action<long, string> callback)
        {
            if (_client == null) return;
#if UNITY_ANDROID || UNITY_IOS
            _client.SpeechToText((resultText, resultLanguage, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode == com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            callback(messageId, resultText);
                        }
                        else
                        {
                            Debug.LogError("RTM：SpeechToText error: " + errorCode);
                        }
                    });
                }
            }, url, lang);
#else
            ////////桌面测试逻辑////////
            Main.Instance.StartCoroutine(Speech2Text_Test((l, s) =>
            {
                callback(messageId, "语音转文字语音转文字语音转文字语音转文字语音转文字语音转文字语音转文字");
            }));
            ////////桌面测试逻辑////////
#endif
        }

        public void SpeechToText(long messageId, byte[] audioData, string lang, Action<long, string> callback)
        {
            if (_client == null) return;
            _client.SpeechToText((resultText, resultLanguage, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode == com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            callback(messageId, resultText);
                        }
                        else
                        {
                            Debug.LogError("RTM：SpeechToText error: " + errorCode);
                        }
                    });
                }
            }, audioData, lang);
        }

        public void Update()
        {
            lock (_rtmpvpQuestProcessor)
            {
                while (_msgQueue.Count > 0)
                {
                    RTMMsgInfo info = _msgQueue.Dequeue();
                    //转发到lua层
                    _pushCallback(info);
                }
            }
            
            lock (lockObj)
            {
                while (_callBackQueue.Count > 0)
                {
                    Action cb = _callBackQueue.Dequeue();
                    //转发到lua层
                    cb();
                }
            }
        }

        public void AddMsg(Chat_Msg_Type type, RTMMessage message, byte messageType)
        {
            if (_client == null) return;
            RTMMsgInfo info = new RTMMsgInfo()
            {
                type = (int) type,
                typeValue = message.toId,
                messageType = messageType,
                messageId = message.messageId,
                binaryMessage = message.binaryMessage,
                fromUid = message.fromUid,
                attrs = Convert.FromBase64String(message.attrs),
            };
            if (message.messageType == (byte) MessageType.AudioFile && message.fileInfo != null &&
                message.fileInfo.isRTMAudio)
            {
                info.isRTMAudio = true;
                info.language = message.fileInfo.language;
                info.duration = message.fileInfo.duration;
                info.fileUrl = message.fileInfo.url;
            }
            else
            {
                info.stringMessage = message.stringMessage;
            }
            _msgQueue.Enqueue(info);
        }


        //获取有未读消息的用户列表
        public void GetUnreadUsers(Action<List<long>> callback)
        {
            if (_client == null)
                return;
            _client.GetUnread((userList, groupList, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode == com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            callback?.Invoke(userList);
                        }
                        else
                        {
                            callback?.Invoke(new List<long>());
                            Debug.LogError("RTM：GetUnreadUsers error: " + errorCode);
                        }
                    });
                }
            });
        }

        //批量获取未读消息的数量
        public void GetUnreadCount(Action<long, int> unreadCountCB, List<long> uidList)
        {
            if (_client == null)
                return;
            _set.Clear();
            for (int i = 0; i < uidList.Count; i++)
            {
                _set.Add(uidList[i]);
            }

            _client.GetP2PUnread((dic, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode == com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            foreach (var pair in dic)
                            {
                                unreadCountCB(pair.Key, pair.Value);
                            }
                        }
                        else
                        {
                            Debug.LogError("RTM：GetUnreadCount error: " + errorCode);
                        }
                    });
                }
            }, _set);
        }

        //获取指定用户的未读消息列表
        public void GetP2PChat(long uid, int count, Action<RTMMsgInfo> historyCallback, Action<long> succBack)
        {
            if (_client == null)
                return;
            if (_historyDic.ContainsKey(uid)) //历史消息并未查询完
                return;
            _historyDic[uid] = new HistoryStruct(uid, count, historyCallback, succBack);
            GetP2PChatRecursive(uid, count, 0, 0); //开始查询
        }

        private void GetP2PChatRecursive(long uid, int count, long beginMesc, long endMesc)
        {
            _client.GetP2PChat((reqCount, reqLastId, reqBeginMsec, reqEndMsec, messages, errorCode) =>
            {
                lock (lockObj)
                {
                    _callBackQueue.Enqueue(() =>
                    {
                        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
                        {
                            Debug.LogError("RTM：GetP2PChat error: " + errorCode);
                            _historyDic.Remove(uid);
                            return;
                        }
        
                        HistoryStruct s = _historyDic[uid];
                        for (int i = 0; i < messages.Count; i++)
                        {
                            HistoryMessage message = messages[i];
                            RTMMsgInfo info = new RTMMsgInfo()
                            {
                                type = (int) Chat_Msg_Type.MsgP2P,
                                typeValue = message.toId,
                                messageType = 1,
                                messageId = message.messageId,
                                fromUid = message.fromUid,
                                attrs = Convert.FromBase64String(message.attrs),
                            };
                            if (message.messageType == (byte) MessageType.AudioFile && message.fileInfo != null &&
                                message.fileInfo.isRTMAudio)
                            {
                                info.isRTMAudio = true;
                                info.language = message.fileInfo.language;
                                info.duration = message.fileInfo.duration;
                                info.fileUrl = message.fileInfo.url;
                            }
                            else
                            {
                                info.stringMessage = message.stringMessage;
                            }
        
                            s.rspList.Add(info);
                        }

                        int leftCount = s.reqCount - reqCount;
                        if (reqCount == 20 && leftCount > 0) //还有剩余消息，继续获取
                        {
                            s.reqCount = leftCount;
                            GetP2PChatRecursive(s.uid, leftCount, 0, reqEndMsec - 1);
                        }
                        else
                        {
                            _historyDic.Remove(s.uid);
                            //转发到lua层
                            for (int i = s.rspList.Count - 1; i >= 0; i--)
                            {
                                s.historyCallback(s.rspList[i]);
                            }
                            //完毕了就下发通知
                            s.succBack(s.uid);
                        }
                    });
                }
            }, uid, true, count > 20 ? 20 : count, beginMesc, endMesc);
        }
    }

    class PVPErrorRecorder : com.fpnn.common.ErrorRecorder
    {
        public void RecordError(Exception e)
        {
            Debug.LogError("RTM: Exception: " + e);
        }
        public void RecordError(string message)
        {
            Debug.LogError("RTM: Error: " + message);
        }
        public void RecordError(string message, Exception e)
        {
            Debug.LogError("RTM: Error: " + message + ", RTM exception: " + e);
        }
    }
    
    class PVPAudioRecorder : AudioRecorderNative.IAudioRecorderListener
    {
        public void RecordStart() {
            Debug.Log("RTM: Recorder Start");
        }

        public void RecordEnd() {
            Debug.Log("RTM: Recorder End");
        }

        public void OnRecord(RTMAudioData audioData)
        {
            Debug.Log("RTM: Recorder Send");
            RTMMgr.Inst.SendAudio(audioData);
        }
        
        public void OnVolumn(double db)
        { 
            Debug.Log("RTM: Recorder OnVolume = " + db);
        }

        public void PlayEnd()
        { 
            Debug.Log("RTM: PlayEnd");
        }
    }
}
