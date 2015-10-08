﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LumiSoft.Net;
using LumiSoft.Net.SDP;
using LumiSoft.Net.SIP;
using LumiSoft.Net.SIP.Debug;
using LumiSoft.Net.SIP.Stack;
using LumiSoft.Net.SIP.Message;
using LumiSoft.Net.Media;
using LumiSoft.Net.Media.Codec.Audio;
using LumiSoft.Net.RTP;
using LumiSoft.Net.RTP.Debug;
using LumiSoft.Net.STUN.Client;
using LumiSoft.Net.UPnP.NAT;

namespace LumiSoft.SIP.UA
{
    /// <summary>
    /// This class represents SIP call.
    /// </summary>
    public class SIP_Call
    {
        private object                    m_pLock                 = new object();
        private SIP_CallState             m_CallState             = SIP_CallState.Calling;
        private SIP_Stack                 m_pStack                = null;        
        private SIP_RequestSender         m_pInitialInviteSender  = null;
        private RTP_MultimediaSession     m_pRtpMultimediaSession = null;
        private DateTime                  m_StartTime;
        private SIP_Dialog_Invite         m_pDialog               = null;
        private SIP_Flow                  m_pFlow                 = null;
        private TimerEx                   m_pKeepAliveTimer       = null;
        private SDP_Message               m_pLocalSDP             = null;
        private SDP_Message               m_pRemoteSDP            = null;
        private Dictionary<string,object> m_pTags                 = null;
                
        /// <summary>
        /// Calling constructor.
        /// </summary>
        /// <param name="stack">Reference to SIP stack.</param>
        /// <param name="sender">Initial INVITE sender.</param>
        /// <param name="session">Call RTP multimedia session.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>stack</b>,<b>sender</b> or <b>session</b> is null reference.</exception>
        internal SIP_Call(SIP_Stack stack,SIP_RequestSender sender,RTP_MultimediaSession session)
        {
            if(stack == null){
                throw new ArgumentNullException("stack");
            }
            if(sender == null){
                throw new ArgumentNullException("sender");
            }
            if(session == null){
                throw new ArgumentNullException("session");
            }

            m_pStack                = stack;
            m_pInitialInviteSender  = sender;
            m_pRtpMultimediaSession = session;

            m_pTags = new Dictionary<string,object>();

            m_pInitialInviteSender.Completed += new EventHandler(delegate(object s,EventArgs e){
                m_pInitialInviteSender = null;

                if(this.State == SIP_CallState.Terminating){
                    SetState(SIP_CallState.Terminated);
                }
            });

            m_CallState = SIP_CallState.Calling;
        }

        /// <summary>
        /// Incoming call constructor.
        /// </summary>
        /// <param name="stack">Reference to SIP stack.</param>
        /// <param name="dialog">Reference SIP dialog.</param>
        /// <param name="session">Call RTP multimedia session.</param>
        /// <param name="localSDP">Local SDP.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>stack</b>,<b>dialog</b>,<b>session</b> or <b>localSDP</b> is null reference.</exception>
        internal SIP_Call(SIP_Stack stack,SIP_Dialog dialog,RTP_MultimediaSession session,SDP_Message localSDP)
        {
            if(stack == null){
                throw new ArgumentNullException("stack");
            }
            if(dialog == null){
                throw new ArgumentNullException("dialog");
            }
            if(session == null){
                throw new ArgumentNullException("session");
            }
            if(localSDP == null){
                throw new ArgumentNullException("localSDP");
            }

            m_pStack = stack;
            m_pDialog = (SIP_Dialog_Invite)dialog;
            m_pRtpMultimediaSession = session;
            m_pLocalSDP = localSDP;

            m_StartTime = DateTime.Now;
            m_pFlow = dialog.Flow;
            dialog.StateChanged += new EventHandler(m_pDialog_StateChanged);

            SetState(SIP_CallState.Active);

            // Start ping timer.
            m_pKeepAliveTimer = new TimerEx(40000);
            m_pKeepAliveTimer.Elapsed += new System.Timers.ElapsedEventHandler(m_pKeepAliveTimer_Elapsed);
            m_pKeepAliveTimer.Enabled = true;
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resource being used.
        /// </summary>
        public void Dispose()
        {
            lock(m_pLock){
                if(this.State == SIP_CallState.Disposed){
                    return;
                }
                SetState(SIP_CallState.Disposed);
                                
                // TODO: Clean up
                m_pStack = null;
                m_pLocalSDP = null;
                if(m_pDialog != null){
                    m_pDialog.Dispose();
                    m_pDialog = null;
                }
                m_pFlow = null;
                if(m_pKeepAliveTimer != null){
                    m_pKeepAliveTimer.Dispose();
                    m_pKeepAliveTimer = null;
                }

                this.StateChanged = null;
            }
        }

        #endregion

        #region method InitCalling

        /// <summary>
        /// Initializes call from Calling state to active..
        /// </summary>
        /// <param name="dialog">SIP dialog.</param>
        /// <param name="localSDP">Local SDP.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>dialog</b> or <b>localSDP</b> is null reference.</exception>
        internal void InitCalling(SIP_Dialog dialog,SDP_Message localSDP)
        {
            if(dialog == null){
                throw new ArgumentNullException("dialog");
            }
            if(localSDP == null){
                throw new ArgumentNullException("localSDP");
            }

            m_pDialog = (SIP_Dialog_Invite)dialog;
            m_pFlow = dialog.Flow;
            m_pLocalSDP = localSDP;
                        
            m_StartTime = DateTime.Now;
            dialog.StateChanged += new EventHandler(m_pDialog_StateChanged);
                
            SetState(SIP_CallState.Active);

            // Start ping timer.
            m_pKeepAliveTimer = new TimerEx(40000);
            m_pKeepAliveTimer.Elapsed += new System.Timers.ElapsedEventHandler(m_pKeepAliveTimer_Elapsed);
            m_pKeepAliveTimer.Enabled = true;
        }

        #endregion


        #region Events handling

        #region method m_pDialog_StateChanged

        /// <summary>
        /// Is called when SIP dialog state has changed.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pDialog_StateChanged(object sender,EventArgs e)
        {
            if(this.State == SIP_CallState.Disposed || this.State == SIP_CallState.Terminated){
                return;
            }
        
            if(m_pDialog.State == SIP_DialogState.Terminated){
                SetState(SIP_CallState.Terminated); 
            }
        }

        #endregion

        #region method m_pKeepAliveTimer_Elapsed

        /// <summary>
        /// Is called when ping timer triggers.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pKeepAliveTimer_Elapsed(object sender,System.Timers.ElapsedEventArgs e)
        {
            try{
                // Send ping request if any flow using object has not sent ping within 30 seconds.
                if(m_pFlow.LastPing.AddSeconds(30) < DateTime.Now){
                    m_pFlow.SendPing();
                }
            }
            catch{
            }
        }

        #endregion
        
        #endregion


        #region method Terminate
                
        /// <summary>
        /// Terminates call.
        /// </summary>
        /// <param name="reason">Call termination reason. This text is sent to remote-party.</param>
        /// <param name="sendBye">If true BYE request with <b>reason</b> text is sent remote-party.</param>
        public void Terminate(string reason,bool sendBye)
        {
            Terminate(reason);
        }

        /// <summary>
        /// Terminates a call.
        /// </summary>
        /// <param name="reason">Call termination reason. This text is sent to remote-party.</param>
        public void Terminate(string reason)
        {        
            lock(m_pLock){
                if(this.State == SIP_CallState.Disposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                if(this.State == SIP_CallState.Terminating || this.State == SIP_CallState.Terminated){
                    return;
                }
                else if(this.State == SIP_CallState.Active){
                    SetState(SIP_CallState.Terminating);
                    
                    m_pDialog.Terminate(reason,true);
                }                
                else if(this.State == SIP_CallState.Calling && m_pInitialInviteSender != null){
                    /* RFC 3261 15.
                        If we are caller and call is not active yet, we must do following actions:
                            *) Send CANCEL, set call Terminating flag.
                            *) If we get non 2xx final response, we are done. (Normally cancel causes '408 Request terminated')
                            *) If we get 2xx response (2xx sent by remote party before our CANCEL reached), we must send BYE to active dialog.
                    */

                    SetState(SIP_CallState.Terminating);

                    m_pInitialInviteSender.Cancel();                    
               }
            }
        }

        #endregion


        #region method SetState

        /// <summary>
        /// Set call state.
        /// </summary>
        /// <param name="state">New call state.</param>
        private void SetState(SIP_CallState state)
        {
            // Disposed call may not change state.
            if(this.State == SIP_CallState.Disposed){
                return;
            }

            m_CallState = state;
                        
            OnStateChanged(state);

            if(state == SIP_CallState.Terminated){
                Dispose();
            }
        }

        #endregion

                
        #region Properties implementation

        /// <summary>
        /// Gets current call state.
        /// </summary>
        public SIP_CallState State
        {
            get{ return m_CallState; }
        }

        /// <summary>
        /// Gets call RTP multimedia session.
        /// </summary>
        public RTP_MultimediaSession RtpMultimediaSession
        {
            get{ return m_pRtpMultimediaSession; }
        }

        /// <summary>
        /// Gets call start time.
        /// </summary>
        public DateTime StartTime
        {
            get{ return m_StartTime; }
        }

        /// <summary>
        /// Gets call dialog. Returns null if dialog not created yet.
        /// </summary>
        public SIP_Dialog_Invite Dialog
        {
            get{ return m_pDialog; }
        }

        /// <summary>
        /// Gets or sets current local SDP.
        /// </summary>
        public SDP_Message LocalSDP
        {
            get{ return m_pLocalSDP; }

            set{ m_pLocalSDP = value; }
        }

        /// <summary>
        /// Gets or sets current remote SDP.
        /// </summary>
        public SDP_Message RemoteSDP
        {
            get{ return m_pRemoteSDP; }

            set{ m_pRemoteSDP = value; }
        }

        /// <summary>
        /// Gets user data items collection.
        /// </summary>
        public Dictionary<string,object> Tags
        {
            get{ return m_pTags; }
        }

        #endregion

        #region Events implementation
        
        /// <summary>
        /// Is raised when call state has changed.
        /// </summary>
        public event EventHandler StateChanged = null;

        #region method OnStateChanged

        /// <summary>
        /// Raises <b>StateChanged</b> event.
        /// </summary>
        /// <param name="state">New call state.</param>
        private void OnStateChanged(SIP_CallState state)
        {
            if(this.StateChanged != null){
                this.StateChanged(this,new EventArgs());
            }
        }

        #endregion

        #endregion
    }
}
