using System;
using com.fpnn.rtm;
using UnityEngine;

namespace PVP.Game
{
    public class RTMPVPQuestProcessor : RTMQuestProcessor
    {
        public override void SessionClosed(int ClosedByErrorCode)
        {
            lock (this)
                Debug.LogError($"RTM: Session closed by error code: {ClosedByErrorCode}");
        }

        public override bool ReloginWillStart(int lastErrorCode, int retriedCount)
        {
            lock (this)
                Debug.LogWarning($"RTM: ReLogin will start. Last error code is {lastErrorCode}, total reLogin count is {retriedCount}.");
            return true;
        }

        public override void ReloginCompleted(bool successful, bool retryAgain, int errorCode, int retriedCount)
        {
            lock (this)
            {
                if (successful)
                    Debug.Log("RTM: ReLogin Completed. ReLogin succeeded, total reLogin count is " + retriedCount);
                else
                    Debug.LogError($"RTM: Relogin failed, error code: {errorCode}, will"
                        + (retryAgain ? "" : " not") + $" retry again. Total relogin count is {retriedCount}.");
            }
        }

        public override void Kickout()
        {
            lock (this)
                Debug.Log("RTM: Received kickout.");
        }

        public override void KickoutRoom(long roomId)
        {
            lock (this)
                Debug.Log($"RTM: Kickout from room {roomId}");
        }

        //----------------[ Message Interfaces ]-----------------//

        #region PushMessage

        public override void PushMessage(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgP2P, message, 2);
            }
        }

        public override void PushGroupMessage(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgGroup, message, 2);
            }
        }

        public override void PushRoomMessage(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgRoom, message, 2);
            }
        }

        public override void PushBroadcastMessage(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgBroadcast, message, 2);
            }
        }

        #endregion

        #region Chat

        public override void PushChat(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg(Chat_Msg_Type.MsgP2P, message, 1);
            }
        }
        public override void PushGroupChat(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgGroup, message, 1);
            }
        }
        public override void PushRoomChat(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgRoom, message, 1);
            }
        }
        public override void PushBroadcastChat(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgBroadcast, message, 1);
            }
        }

        #endregion

        #region Cmd

        public override void PushCmd(RTMMessage message)
        {
            lock (this)
            {
                Debug.Log($"RTM: Receive push cmd: from {message.fromUid}, " +
                          $"mid: {message.messageId}, attrs: {message.attrs}, " +
                          $"message {message.stringMessage}.");
            }
        }
        public override void PushGroupCmd(RTMMessage message)
        {
            lock (this)
            {
                Debug.Log($"RTM: Receive push group cmd: from {message.fromUid}, " +
                          $"in group {message.toId}, mid: {message.messageId}, attrs: {message.attrs}, " +
                          $"message {message.stringMessage}.");
            }
        }
        public override void PushRoomCmd(RTMMessage message)
        {
            lock (this)
            {
                Debug.Log($"RTM: Receive push room cmd: from {message.fromUid}, " +
                          $"in room {message.toId}, mid: {message.messageId}, attrs: {message.attrs}, " +
                          $"message {message.stringMessage}.");
            }
        }
        public override void PushBroadcastCmd(RTMMessage message)
        {
            lock (this)
            {
                Debug.Log($"RTM: Receive push broadcast cmd: from {message.fromUid}, " +
                          $"mid: {message.messageId}, attrs: {message.attrs}, " +
                          $"message {message.stringMessage}.");
            }
        }

        #endregion

        #region Files

        public override void PushFile(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgP2P, message, 1);
            }
        }
        public override void PushGroupFile(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgGroup, message, 1);
            }
        }
        public override void PushRoomFile(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgRoom, message, 1);
            }
        }
        public override void PushBroadcastFile(RTMMessage message)
        {
            lock (this)
            {
                RTMMgr.Inst.AddMsg( Chat_Msg_Type.MsgBroadcast, message, 1);
            }
        }

        #endregion
    }
}
