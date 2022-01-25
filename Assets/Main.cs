using System;
using System.Threading;
using UnityEngine;
using com.fpnn;
using com.fpnn.rtm;
using PVP.Game;

public class Main : MonoBehaviour
{
    public interface ITestCase
    {
        void Start(string endpoint, long pid, long uid, string token);
        void Stop();
    }

    Thread testThread;
    ITestCase tester;

    // Start is called before the first frame update
    void Start()
    {
        RTMMgr.Inst.Init();
        RTMMgr.Inst.Login(111501, "B0A527B3F071F1C1840EED940C18E85C", info =>
        {
            _audioDta = info.audioData;
            _dura = info.duration;
            Debug.Log("audio lang: "+info.language);
        });
    }

    void TestMain()
    {
        //-- Examples
        // tester = new Chat();
        // tester = new Data();
        // tester = new Files();
        // tester = new Friends();
        // tester = new Groups();
        // tester = new Histories();
        // tester = new Login();
        // tester = new Messages();
        // tester = new Rooms();
        // tester = new RTMSystem();
        // tester = new Users();
        //tester = new RTC();

        // tester.Start(rtmServerEndpoint, pid, uid, token);
    }

    public void SendTxt()
    {
        RTMMgr.Inst.SendChat((int)Chat_Msg_Type.MsgRoom, 123456, "111501发送了一条文本消息", null);
    }

    public void StartRecord()
    {
        RTMMgr.Inst.StartRecord();
        _startRecord = true;
        _time = 0;
    }

    public void StopPlay()
    {
        RTMMgr.Inst.StopPlay();
        Debug.Log("停止播放语音");
    }

    private bool _startRecord = false;
    private float _time;
    private void Update()
    {
        RTMMgr.Inst.Update();
        // if (_startRecord)
        // {
        //     _time += Time.deltaTime;
        //     if (_time > 0.2f)
        //     {
        //         _time -= 0.2f;
        //         Debug.Log("获取到的音量："+RTMMgr.Inst.GetLoudness());
        //     }
        // }
    }

    private byte[] _audioDta;
    private int _dura;
    public void StopRecord()
    {
        RTMMgr.Inst.StopRecord(3, 123456, null);
        _startRecord = false;
    }

    public void PlayAudio()
    {
        RTMMgr.Inst.PlayAudio(null, _audioDta, "zh-CN", _dura);
        Debug.Log("播放语音");
    }

    void SpeechCallback(long id, string resultText)
    {
        Debug.Log("转换文字：" + resultText);
    }
    public void SpeedhToTxt()
    {
        RTMMgr.Inst.SpeechToText(0, _audioDta, "zh-CN", SpeechCallback);
    }
}
