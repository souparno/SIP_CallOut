using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Net;

using LumiSoft.SIP.UA.Resources;

using LumiSoft.Net;
using LumiSoft.Net.SDP;
using LumiSoft.Net.SIP;
using LumiSoft.Net.SIP.Debug;
using LumiSoft.Net.SIP.Stack;
using LumiSoft.Net.SIP.Message;
using LumiSoft.Net.Media;
using LumiSoft.Net.Media.Codec;
using LumiSoft.Net.Media.Codec.Audio;
using LumiSoft.Net.RTP;
using LumiSoft.Net.RTP.Debug;
using LumiSoft.Net.STUN.Client;
using LumiSoft.Net.UPnP.NAT;

namespace LumiSoft.SIP.UA
{
    /// <summary>
    /// Application main UI.
    /// </summary>
    public class wfrm_Main : Form
    {
        private ToolStrip   m_pToolbar      = null;
        private Label       mt_From         = null;
        private TextBox     m_pFrom         = null;
        private Label       mt_To           = null;
        private ComboBox    m_pTo           = null;
        private Button      m_pToggleOnHold = null;
        private Button      m_pCall_HangUp  = null;
        private StatusStrip m_pStatusBar    = null;
            
        private bool                       m_IsClosing       = false;
        private bool                       m_IsDebug         = true;
        private SIP_Stack                  m_pStack          = null;
        private string                     m_StunServer      = "stun.iptel.org";
      //private string                     m_StunServer      = "stunserver.org";
      //private string                     m_StunServer      = "stun.counterpath.net";
        private UPnP_NAT_Client            m_pUPnP           = null;
        private int                        m_SipPort         = 7666;
        private int                        m_RtpBasePort     = 21240;
        private Dictionary<int,AudioCodec> m_pAudioCodecs    = null;
        private AudioOutDevice             m_pAudioOutDevice = null;
        private AudioInDevice              m_pAudioInDevice  = null;
        private SIP_Call                   m_pCall           = null;
        private WavePlayer                 m_pPlayer         = null;
        private Timer                      m_pTimerDuration  = null;
        private wfrm_IncomingCall          m_pIncomingCallUI = null;
        private string                     m_NatHandlingType = "";
        
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="isDebug">Specifies if debug windows showed.</param>
        public wfrm_Main(bool isDebug,string from,string[] to)
        {
            m_IsDebug = isDebug;

            InitUI();                                                       
            InitStack();

            if(!string.IsNullOrEmpty(from)){
                m_pFrom.Text = from;
            }
            if(to != null && to.Length > 0){
                m_pTo.Items.AddRange(to);
                m_pTo.SelectedIndex = 0;
            }
        }

        #region method InitUI

        /// <summary>
        /// Creates and intializes UI.
        /// </summary>
        private void InitUI()
        {
            this.ClientSize = new Size(400,110);
            this.MinimumSize = this.Size;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "SIP Call Out";
            this.MaximizeBox = false;
            this.Icon = ResManager.GetIcon("app.ico");
            this.Disposed += new EventHandler(wfrm_Main_Disposed);

            m_pToolbar = new ToolStrip();
            m_pToolbar.Size = new Size(70,25);
            m_pToolbar.Location = new Point(5,5);
            //--- Audio button
            ToolStripDropDownButton button_Audio = new ToolStripDropDownButton();
            button_Audio.Name = "audio";
            button_Audio.Image = ResManager.GetIcon("speaker.ico").ToBitmap();            
            foreach(AudioOutDevice device in AudioOut.Devices){
                ToolStripMenuItem item = new ToolStripMenuItem(device.Name);
                item.Checked = (button_Audio.DropDownItems.Count == 0);
                item.Tag = device;
                button_Audio.DropDownItems.Add(item);
            }
            button_Audio.DropDown.ItemClicked += new ToolStripItemClickedEventHandler(m_pToolbar_Audio_ItemClicked);
            m_pToolbar.Items.Add(button_Audio);
            //--- Microphone button
            ToolStripDropDownButton button_Mic = new ToolStripDropDownButton();
            button_Mic.Name = "mic";
            button_Mic.Image = ResManager.GetIcon("mic.ico").ToBitmap();
            foreach(AudioInDevice device in AudioIn.Devices){
                ToolStripMenuItem item = new ToolStripMenuItem(device.Name);
                item.Checked = (button_Mic.DropDownItems.Count == 0);
                item.Tag = device;
                button_Mic.DropDownItems.Add(item);
            }
            button_Mic.DropDown.ItemClicked += new ToolStripItemClickedEventHandler(m_pToolbar_Mic_ItemClicked);
            m_pToolbar.Items.Add(button_Mic);
            // Separator
            m_pToolbar.Items.Add(new ToolStripSeparator());
            // NAT
            ToolStripDropDownButton button_NAT = new ToolStripDropDownButton();
            button_NAT.Name = "nat";
            button_NAT.Image = ResManager.GetIcon("router.ico").ToBitmap();
            button_NAT.DropDown.ItemClicked += new ToolStripItemClickedEventHandler(m_pToolbar_NAT_DropDown_ItemClicked);
            m_pToolbar.Items.Add(button_NAT);

            mt_From = new Label();
            mt_From.Size = new Size(50,20);
            mt_From.Location = new Point(0,30);
            mt_From.TextAlign = ContentAlignment.MiddleRight;
            mt_From.Text = "From:";

            m_pFrom = new TextBox();
            m_pFrom.Size = new Size(220,20);
            m_pFrom.Location = new Point(55,30);
            m_pFrom.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            mt_To = new Label();
            mt_To.Size = new Size(50,20);
            mt_To.Location = new Point(0,55);
            mt_To.TextAlign = ContentAlignment.MiddleRight;
            mt_To.Text = "To:";

            m_pTo = new ComboBox();
            m_pTo.Size = new Size(220,20);
            m_pTo.Location = new Point(55,55);
            m_pTo.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            m_pToggleOnHold = new Button();
            m_pToggleOnHold.Size = new Size(50,45);
            m_pToggleOnHold.Location = new Point(285,30);
            m_pToggleOnHold.Anchor = AnchorStyles.Right;
            m_pToggleOnHold.Enabled = false;
            m_pToggleOnHold.Text = "Hold";
            m_pToggleOnHold.Click += new EventHandler(m_pToggleOnHold_Click);

            m_pCall_HangUp = new Button();
            m_pCall_HangUp.Size = new Size(45,45);
            m_pCall_HangUp.Location = new Point(340,30);
            m_pCall_HangUp.Anchor = AnchorStyles.Right;
            m_pCall_HangUp.Image = ResManager.GetIcon("call.ico",new Size(24,24)).ToBitmap();
            m_pCall_HangUp.Click += new EventHandler(m_pCall_HangUp_Click);
                        
            m_pStatusBar = new StatusStrip();
            //--- Text label
            ToolStripStatusLabel statusLabel_Text = new ToolStripStatusLabel();
            statusLabel_Text.Name = "text";
            statusLabel_Text.Size = new Size(200,20);
            statusLabel_Text.BorderSides = ToolStripStatusLabelBorderSides.All;
            statusLabel_Text.Spring = true;
            statusLabel_Text.TextAlign = ContentAlignment.MiddleLeft;
            m_pStatusBar.Items.Add(statusLabel_Text);
            //--- Duration label
            ToolStripStatusLabel statusLabel_Duration = new ToolStripStatusLabel();
            statusLabel_Duration.Name = "duration";
            statusLabel_Duration.AutoSize = false;
            statusLabel_Duration.Size = new Size(60,20);
            statusLabel_Duration.BorderSides = ToolStripStatusLabelBorderSides.All;
            m_pStatusBar.Items.Add(statusLabel_Duration);

            m_pTimerDuration = new Timer();
            m_pTimerDuration.Interval = 1000;
            m_pTimerDuration.Tick += new EventHandler(m_pTimerDuration_Tick);
            m_pTimerDuration.Enabled = true;

            this.Controls.Add(m_pToolbar);
            this.Controls.Add(mt_From);
            this.Controls.Add(m_pFrom);
            this.Controls.Add(mt_To);
            this.Controls.Add(m_pTo);
            this.Controls.Add(m_pToggleOnHold);
            this.Controls.Add(m_pCall_HangUp);
            this.Controls.Add(m_pStatusBar);
        }
                                                                
        #endregion

        
        #region Events handling

        #region method wfrm_Main_Disposed

        /// <summary>
        /// Is called when window has disposed(after closed).
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void wfrm_Main_Disposed(object sender,EventArgs e)
        {
            try{
                m_IsClosing = true;

                if(m_pCall != null){
                    m_pCall.Terminate("Hang up.");
                    
                    // Wait call to start terminating.
                    System.Threading.Thread.Sleep(200);
                }

                if(m_pPlayer != null){
                    m_pPlayer.Stop();
                }

                if(m_pTimerDuration != null){
                    m_pTimerDuration.Dispose();
                    m_pTimerDuration = null;
                }
                   
                if(m_pStack != null){
                    m_pStack.Dispose();
                    m_pStack = null;
                }
            }
            catch{
            }
        }

        #endregion

        #region method m_pToolbar_Audio_ItemClicked

        /// <summary>
        /// Is called when new audio-out device is selected.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pToolbar_Audio_ItemClicked(object sender,ToolStripItemClickedEventArgs e)
        {   
            try{
                foreach(ToolStripMenuItem item in ((ToolStripDropDownMenu)sender).Items){
                    if(item.Equals(e.ClickedItem)){
                        item.Checked = true;
                    }
                    else{
                        item.Checked = false;
                    }
                }

                m_pAudioOutDevice = (AudioOutDevice)e.ClickedItem.Tag;

                // Update active call audio-out device.
                if(m_pCall != null && m_pCall.LocalSDP != null){
                    foreach(SDP_MediaDescription media in m_pCall.LocalSDP.MediaDescriptions){
                        if(media.Tags.ContainsKey("rtp_audio_out")){
                            ((AudioOut_RTP)media.Tags["rtp_audio_out"]).AudioOutDevice = m_pAudioOutDevice;
                        }
                    }
                }
            }
            catch(Exception x){
                MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        #endregion

        #region method m_pToolbar_Mic_ItemClicked

        /// <summary>
        /// Is called when new audio-in device is selected.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pToolbar_Mic_ItemClicked(object sender,ToolStripItemClickedEventArgs e)
        {   
            try{
                foreach(ToolStripMenuItem item in ((ToolStripDropDownMenu)sender).Items){
                    if(item.Equals(e.ClickedItem)){
                        item.Checked = true;
                    }
                    else{
                        item.Checked = false;
                    }
                }

                m_pAudioInDevice = (AudioInDevice)e.ClickedItem.Tag;

                // Update active call audio-in device.
                if(m_pCall != null && m_pCall.LocalSDP != null){
                    foreach(SDP_MediaDescription media in m_pCall.LocalSDP.MediaDescriptions){
                        if(media.Tags.ContainsKey("rtp_audio_in")){
                            ((AudioIn_RTP)media.Tags["rtp_audio_in"]).AudioInDevice = m_pAudioInDevice;
                        }
                    }
                }
            }
            catch(Exception x){
                MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        #endregion

        #region method m_pToolbar_NAT_DropDown_ItemClicked

        /// <summary>
        /// Is called when new NAT handling method is selected.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pToolbar_NAT_DropDown_ItemClicked(object sender,ToolStripItemClickedEventArgs e)
        {
            if(!e.ClickedItem.Enabled){
                return;
            }

            foreach(ToolStripMenuItem item in ((ToolStripDropDownMenu)sender).Items){
                if(item.Equals(e.ClickedItem)){
                    item.Checked = true;
                    m_NatHandlingType = item.Name;
                }
                else{
                    item.Checked = false;
                }
            }
        }

        #endregion

        #region method m_pToggleOnHold_Click

        /// <summary>
        /// Is called when toggle call on hold has clicked.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pToggleOnHold_Click(object sender,EventArgs e)
        {
            try{
                if(m_pToggleOnHold.Text == "Hold"){
                    PutCallOnHold();
                    m_pToggleOnHold.Enabled = false;
                }
                else{
                    PutCallUnHold();
                    m_pToggleOnHold.Enabled = false;
                }
            }
            catch(Exception x){
                MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        #endregion

        #region method m_pCall_HangUp_Click

        /// <summary>
        /// Is called when Call/HangUp button has clicked.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pCall_HangUp_Click(object sender,EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            try{
                if(m_pCall != null){
                    m_pCall.Terminate("Hang up.");
                }
                else{
                    #region Validate From:/To:

                    SIP_t_NameAddress to = null;
                    try{
                        to = new SIP_t_NameAddress(m_pTo.Text);
                        
                        if(!to.IsSipOrSipsUri){
                            throw new ArgumentException("To: is not SIP URI.");
                        }
                    }
                    catch{
                        MessageBox.Show("To: is not SIP URI.","Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);

                        return;
                    }
                    SIP_t_NameAddress from = null;
                    try{
                        from = new SIP_t_NameAddress(m_pFrom.Text);
                        
                        if(!to.IsSipOrSipsUri){
                            throw new ArgumentException("From: is not SIP URI.");
                        }
                    }
                    catch{
                        MessageBox.Show("From: is not SIP URI.","Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);

                        return;
                    }

                    #endregion

                    Call(from,to);

                    m_pCall_HangUp.Image = ResManager.GetIcon("call_hangup.ico",new Size(24,24)).ToBitmap();
                }
            }
            catch(Exception x){
                MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
            this.Cursor = Cursors.Default;
        }

        #endregion

        #region method m_pTimerDuration_Tick

        /// <summary>
        /// Is called when call duration refresh timer triggers.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pTimerDuration_Tick(object sender, EventArgs e)
        {
            try{
                if(m_pCall != null && m_pCall.State == SIP_CallState.Active){
                    TimeSpan duration = (DateTime.Now - m_pCall.StartTime);
                    m_pStatusBar.Items["duration"].Text = duration.Hours.ToString("00") + ":" + duration.Minutes.ToString("00") + ":" + duration.Seconds.ToString("00");
                }
            }
            catch{
                // We don't care about errors here.
            }
        }

        #endregion


        #region method m_pStack_RequestReceived

        /// <summary>
        /// Is called when SIP stack has received request.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pStack_RequestReceived(object sender,SIP_RequestReceivedEventArgs e)
        {
            try{
                #region CANCEL

                if(e.Request.RequestLine.Method == SIP_Methods.CANCEL){
                    /* RFC 3261 9.2.
                        If the UAS did not find a matching transaction for the CANCEL
                        according to the procedure above, it SHOULD respond to the CANCEL
                        with a 481 (Call Leg/Transaction Does Not Exist).
                  
                        Regardless of the method of the original request, as long as the
                        CANCEL matched an existing transaction, the UAS answers the CANCEL
                        request itself with a 200 (OK) response.
                    */

                    SIP_ServerTransaction trToCancel = m_pStack.TransactionLayer.MatchCancelToTransaction(e.Request);
                    if(trToCancel != null){
                        trToCancel.Cancel();
                        e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x200_Ok,e.Request));
                    }
                    else{
                        e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x481_Call_Transaction_Does_Not_Exist,e.Request));
                    }
                }

                #endregion

                #region BYE

                else if(e.Request.RequestLine.Method == SIP_Methods.BYE){
                    /* RFC 3261 15.1.2.
                        If the BYE does not match an existing dialog, the UAS core SHOULD generate a 481
                        (Call/Transaction Does Not Exist) response and pass that to the server transaction.
                    */

                    // Currently we match BYE to dialog and it processes it,
                    // so BYE what reaches here doesnt match to any dialog.

                    e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x481_Call_Transaction_Does_Not_Exist,e.Request));
                }

                #endregion

                #region INVITE

                else if(e.Request.RequestLine.Method == SIP_Methods.INVITE){

                    #region Incoming call

                    if(e.Dialog == null){
                        #region Validate incoming call

                        // We don't accept more than 1 call at time.
                        if(m_pIncomingCallUI != null || m_pCall != null){
                            e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x600_Busy_Everywhere,e.Request));

                            return;
                        }

                        // We don't accept SDP offerless calls.
                        if(e.Request.ContentType == null || e.Request.ContentType.ToLower().IndexOf("application/sdp") == -1){
                            e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x606_Not_Acceptable + ": We don't accpet SDP offerless calls.",e.Request));

                            return;
                        }

                        SDP_Message sdpOffer = SDP_Message.Parse(Encoding.UTF8.GetString(e.Request.Data));

                        // Check if we can accept any media stream.
                        bool canAccept = false;
                        foreach(SDP_MediaDescription media in sdpOffer.MediaDescriptions){
                            if(CanSupportMedia(media)){
                                canAccept = true;

                                break;
                            }
                        }
                        if(!canAccept){
                            e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x606_Not_Acceptable,e.Request));

                            return;
                        }
                        
                        #endregion
                                                                        
                        // Send ringing to remote-party.
                        SIP_Response responseRinging = m_pStack.CreateResponse(SIP_ResponseCodes.x180_Ringing,e.Request,e.Flow);
                        responseRinging.To.Tag = SIP_Utils.CreateTag();
                        e.ServerTransaction.SendResponse(responseRinging);

                        SIP_Dialog_Invite dialog = (SIP_Dialog_Invite)m_pStack.TransactionLayer.GetOrCreateDialog(e.ServerTransaction,responseRinging);
                                                
                        // We need invoke here, otherwise we block SIP stack RequestReceived event while incoming call UI showed.
                        this.BeginInvoke(new MethodInvoker(delegate(){
                            try{
                                m_pPlayer.Play(ResManager.GetStream("ringing.wav"),20);

                                // Show incoming call UI.
                                m_pIncomingCallUI = new wfrm_IncomingCall(e.ServerTransaction);
                                // Call accepted.
                                if(m_pIncomingCallUI.ShowDialog(this) == DialogResult.Yes){  
                                    RTP_MultimediaSession rtpMultimediaSession = new RTP_MultimediaSession(RTP_Utils.GenerateCNAME());

                                    // Build local SDP template
                                    SDP_Message sdpLocal = new SDP_Message();
                                    sdpLocal.Version = "0";
                                    sdpLocal.Origin = new SDP_Origin("-",sdpLocal.GetHashCode(),1,"IN","IP4",System.Net.Dns.GetHostAddresses("")[0].ToString());
                                    sdpLocal.SessionName = "SIP Call";            
                                    sdpLocal.Times.Add(new SDP_Time(0,0));
                           
                                    ProcessMediaOffer(dialog,e.ServerTransaction,rtpMultimediaSession,sdpOffer,sdpLocal);

                                    // Create call.
                                    m_pCall = new SIP_Call(m_pStack,dialog,rtpMultimediaSession,sdpLocal);
                                    m_pCall.StateChanged += new EventHandler(m_pCall_StateChanged);
                                    m_pCall_StateChanged(m_pCall,new EventArgs());

                                    if(m_IsDebug){
                                        wfrm_RTP_Debug rtpDebug = new wfrm_RTP_Debug(m_pCall.RtpMultimediaSession);
                                        rtpDebug.Show();
                                    }
                                }
                                // Call rejected.
                                else{
                                    // Transaction response is sent in call UI.

                                    dialog.Terminate(null,false);
                                }
                                m_pIncomingCallUI = null;
                                m_pPlayer.Stop();
                            }
                            catch(Exception x1){
                                MessageBox.Show("Error: " + x1.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                            }
                        }));
                    }

                    #endregion

                    #region Re-INVITE

                    else{
                        try{                                                        
                            // Remote-party provided SDP offer.
                            if(e.Request.ContentType != null && e.Request.ContentType.ToLower().IndexOf("application/sdp") > -1){
                                ProcessMediaOffer(m_pCall.Dialog,e.ServerTransaction,m_pCall.RtpMultimediaSession,SDP_Message.Parse(Encoding.UTF8.GetString(e.Request.Data)),m_pCall.LocalSDP);

                                // We are holding a call.
                                if(m_pToggleOnHold.Text == "Unhold"){
                                    // We don't need to do anything here.
                                }
                                // Remote-party is holding a call.
                                else if(IsRemotePartyHolding(SDP_Message.Parse(Encoding.UTF8.GetString(e.Request.Data)))){
                                    // We need invoke here, we are running on thread pool thread.
                                    this.BeginInvoke(new MethodInvoker(delegate(){
                                        m_pStatusBar.Items["text"].Text = "Remote party holding a call";                                    
                                    }));

                                    m_pPlayer.Play(ResManager.GetStream("onhold.wav"),20);
                                }
                                // Call is active.
                                else{
                                    // We need invoke here, we are running on thread pool thread.
                                    this.BeginInvoke(new MethodInvoker(delegate(){
                                        m_pStatusBar.Items["text"].Text = "Call established";                                    
                                    }));

                                    m_pPlayer.Stop();
                                }
                            }
                            // Error: Re-INVITE can't be SDP offerless.
                            else{
                                e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": Re-INVITE must contain SDP offer.",e.Request));
                            }
                        }
                        catch(Exception x1){
                            e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": " + x1.Message,e.Request));
                        }
                    }

                    #endregion
                }

                #endregion

                #region ACK

                else if(e.Request.RequestLine.Method == SIP_Methods.ACK){
                    // Abandoned ACK, just skip it.
                }

                #endregion

                #region Other

                else{
                    // ACK is response less method.
                    if(e.Request.RequestLine.Method != SIP_Methods.ACK){
                        e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x501_Not_Implemented,e.Request));
                    }
                }

                #endregion
            }
            catch{
                e.ServerTransaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error,e.Request));
            }
        }

        #endregion

        #region method m_pStack_Error

        /// <summary>
        /// Is called when SIp stack has unhandled error.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pStack_Error(object sender,ExceptionEventArgs e)
        {
            if(!m_IsClosing){
                MessageBox.Show("Error: " + e.Exception.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        #endregion


        #region method m_pCall_StateChanged

        /// <summary>
        /// Is called when call state has changed.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pCall_StateChanged(object sender,EventArgs e)
        {
            #region Active

            if(m_pCall.State == SIP_CallState.Active){
                // We need invoke here, we are running on thread pool thread.
                this.BeginInvoke(new MethodInvoker(delegate(){
                    m_pCall_HangUp.Image = ResManager.GetIcon("call_hangup.ico",new Size(24,24)).ToBitmap();
                    m_pToggleOnHold.Enabled = true;
                    m_pToggleOnHold.Text = "Hold";

                    m_pStatusBar.Items["text"].Text = "Call established";
                }));
            }

            #endregion

            #region Terminated

            else if(m_pCall.State == SIP_CallState.Terminated){
                SDP_Message localSDP = m_pCall.LocalSDP;
     
                foreach(SDP_MediaDescription media in localSDP.MediaDescriptions){
                    if(media.Tags.ContainsKey("rtp_audio_in")){
                        ((AudioIn_RTP)media.Tags["rtp_audio_in"]).Dispose();
                    }
                    if(media.Tags.ContainsKey("rtp_audio_out")){
                        ((AudioOut_RTP)media.Tags["rtp_audio_out"]).Dispose();
                    }

                    if(media.Tags.ContainsKey("upnp_rtp_map")){
                        try{
                            m_pUPnP.DeletePortMapping((UPnP_NAT_Map)media.Tags["upnp_rtp_map"]);
                        }
                        catch{
                        }
                    }
                    if(media.Tags.ContainsKey("upnp_rtcp_map")){
                        try{
                            m_pUPnP.DeletePortMapping((UPnP_NAT_Map)media.Tags["upnp_rtcp_map"]);
                        }
                        catch{
                        }
                    }
                }

                if(m_pCall.RtpMultimediaSession != null){
                    m_pCall.RtpMultimediaSession.Dispose();
                }

                if(m_pCall.Dialog != null && m_pCall.Dialog.IsTerminatedByRemoteParty){
                    m_pPlayer.Play(ResManager.GetStream("hangup.wav"),1);
                }
            }

            #endregion

            #region Disposed

            else if(m_pCall.State == SIP_CallState.Disposed){
                if(!m_IsClosing){
                    // We need invoke here, we are running on thread pool thread.
                    this.BeginInvoke(new MethodInvoker(delegate(){
                        m_pCall_HangUp.Image = ResManager.GetIcon("call.ico",new Size(24,24)).ToBitmap();

                        m_pToggleOnHold.Enabled = false;
                        m_pToggleOnHold.Text = "Hold";

                        m_pStatusBar.Items["text"].Text = "";
                    }));
                }
                
                m_pCall = null;
            }

            #endregion
        }

        #endregion

        #endregion


        #region method InitStack

        /// <summary>
        /// Initializes SIP stack.
        /// </summary>
        private void InitStack()
        {
            #region Init audio devices

            if(AudioOut.Devices.Length == 0){
                foreach(Control control in this.Controls){
                    control.Enabled = false;
                }

                MessageBox.Show("Calling not possible, there are no speakers in computer.","Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);

                return;
            }

            if(AudioIn.Devices.Length == 0){
                foreach(Control control in this.Controls){
                    control.Enabled = false;
                }

                MessageBox.Show("Calling not possible, there is no microphone in computer.","Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);

                return;
            }

            m_pAudioOutDevice = AudioOut.Devices[0];
            m_pAudioInDevice  = AudioIn.Devices[0];

            m_pAudioCodecs = new Dictionary<int,AudioCodec>();
            m_pAudioCodecs.Add(0,new PCMU());
            m_pAudioCodecs.Add(8,new PCMA());

            m_pPlayer = new WavePlayer(AudioOut.Devices[0]);

            #endregion
                        
            #region Get NAT handling methods

            m_pUPnP = new UPnP_NAT_Client();

            STUN_Result stunResult = new STUN_Result(STUN_NetType.UdpBlocked,null);
            try{
                stunResult = STUN_Client.Query(m_StunServer,3478,new IPEndPoint(IPAddress.Any,0));                
            }
            catch{                
            }

            if(stunResult.NetType == STUN_NetType.Symmetric || stunResult.NetType == STUN_NetType.UdpBlocked){
                ToolStripMenuItem item_stun = new ToolStripMenuItem("STUN (" + stunResult.NetType + ")");
                item_stun.Name = "stun";
                item_stun.Enabled = false;
                ((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems.Add(item_stun);
            }
            else{
                ToolStripMenuItem item_stun = new ToolStripMenuItem("STUN (" + stunResult.NetType + ")");
                item_stun.Name = "stun";
                ((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems.Add(item_stun);
            }

            if(m_pUPnP.IsSupported){
                ToolStripMenuItem item_upnp = new ToolStripMenuItem("UPnP");
                item_upnp.Name = "upnp";
                ((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems.Add(item_upnp);
            }
            else{
                ToolStripMenuItem item_upnp = new ToolStripMenuItem("UPnP Not Supported");
                item_upnp.Name = "upnp";
                item_upnp.Enabled = false;
                ((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems.Add(item_upnp);
            }

            //if(!((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems["stun"].Enabled && !((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems["upnp"].Enabled){
            //    MessageBox.Show("Calling may not possible, your firewall or router blocks STUN and doesn't support UPnP.\r\n\r\nSTUN Net Type: " + stunResult.NetType + "\r\n\r\nUPnP Supported: " + m_pUPnP.IsSupported,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            //}

            ToolStripMenuItem item_no_nat = new ToolStripMenuItem("No NAT handling");
            item_no_nat.Name = "no_nat";
            ((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems.Add(item_no_nat);

            // Select first enabled item.
            foreach(ToolStripItem it in ((ToolStripDropDownButton)m_pToolbar.Items["nat"]).DropDownItems){
                if(it.Enabled){
                    ((ToolStripMenuItem)it).Checked = true;
                    m_NatHandlingType = it.Name;

                    break;
                }
            }

            #endregion
                                                                        
            m_pStack = new SIP_Stack();
            m_pStack.UserAgent = "LumiSoft SIP UA 1.0";
            m_pStack.BindInfo = new IPBindInfo[]{new IPBindInfo("",BindInfoProtocol.UDP,IPAddress.Any,m_SipPort)};
            //m_pStack.Allow
            m_pStack.Error += new EventHandler<ExceptionEventArgs>(m_pStack_Error);
            m_pStack.RequestReceived += new EventHandler<SIP_RequestReceivedEventArgs>(m_pStack_RequestReceived);
            m_pStack.Start();

            if(m_IsDebug){
                wfrm_SIP_Debug debug = new wfrm_SIP_Debug(m_pStack);
                debug.Show();
            }
        }
                                
        #endregion


        #region method Call

        /// <summary>
        /// Starts calling.
        /// </summary>
        /// <param name="from">From address.</param>
        /// <param name="to">To address.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>from</b> or <b>to</b> is null reference.</exception>
        private void Call(SIP_t_NameAddress from,SIP_t_NameAddress to)
        {            
            if(from == null){
                throw new ArgumentNullException("from");
            }
            if(to == null){
                throw new ArgumentNullException("to");
            }

            #region Setup RTP session
            
            RTP_MultimediaSession rtpMultimediaSession = new RTP_MultimediaSession(RTP_Utils.GenerateCNAME());
            RTP_Session rtpSession = CreateRtpSession(rtpMultimediaSession);
            // Port search failed.
            if(rtpSession == null){
                throw new Exception("Calling not possible, RTP session failed to allocate IP end points.");
            }
            
            if(m_IsDebug){
                wfrm_RTP_Debug rtpDebug = new wfrm_RTP_Debug(rtpMultimediaSession);
                rtpDebug.Show();
            }

            #endregion

            #region Create SDP offer

            SDP_Message sdpOffer = new SDP_Message();
            sdpOffer.Version = "0";
            sdpOffer.Origin = new SDP_Origin("-",sdpOffer.GetHashCode(),1,"IN","IP4",System.Net.Dns.GetHostAddresses("")[0].ToString());
            sdpOffer.SessionName = "SIP Call";            
            sdpOffer.Times.Add(new SDP_Time(0,0));

            #region Add 1 audio stream

            SDP_MediaDescription mediaStream = new SDP_MediaDescription(SDP_MediaTypes.audio,0,1,"RTP/AVP",null);

            rtpSession.NewReceiveStream += delegate(object s,RTP_ReceiveStreamEventArgs e){
                AudioOut_RTP audioOut = new AudioOut_RTP(m_pAudioOutDevice,e.Stream,m_pAudioCodecs);
                audioOut.Start();
                mediaStream.Tags["rtp_audio_out"] = audioOut;
            };

            if(!HandleNAT(mediaStream,rtpSession)){
                throw new Exception("Calling not possible, because of NAT or firewall restrictions.");
            }
                        
            foreach(KeyValuePair<int,AudioCodec> entry in m_pAudioCodecs){
                mediaStream.Attributes.Add(new SDP_Attribute("rtpmap",entry.Key + " " + entry.Value.Name + "/" + entry.Value.CompressedAudioFormat.SamplesPerSecond));
                mediaStream.MediaFormats.Add(entry.Key.ToString());
            }
            mediaStream.Attributes.Add(new SDP_Attribute("ptime","20"));
            mediaStream.Attributes.Add(new SDP_Attribute("sendrecv",""));
            mediaStream.Tags["rtp_session"] = rtpSession;
            mediaStream.Tags["audio_codecs"] = m_pAudioCodecs;
            sdpOffer.MediaDescriptions.Add(mediaStream);

            #endregion

            #endregion

            // Create INVITE request.
            SIP_Request invite = m_pStack.CreateRequest(SIP_Methods.INVITE,to,from);
            invite.ContentType = "application/sdp";
            invite.Data        = sdpOffer.ToByte();

            SIP_RequestSender sender = m_pStack.CreateRequestSender(invite);

            // Create call.
            m_pCall = new SIP_Call(m_pStack,sender,rtpMultimediaSession);
            m_pCall.LocalSDP = sdpOffer;
            m_pCall.StateChanged += new EventHandler(m_pCall_StateChanged);

            bool finalResponseSeen = false;
            List<SIP_Dialog_Invite> earlyDialogs = new List<SIP_Dialog_Invite>();            
            sender.ResponseReceived += delegate(object s,SIP_ResponseReceivedEventArgs e){
                // Skip 2xx retransmited response.
                if(finalResponseSeen){
                    return;
                }
                if(e.Response.StatusCode >= 200){
                    finalResponseSeen = true;
                }

                try{
                    #region Provisional

                    if(e.Response.StatusCodeType == SIP_StatusCodeType.Provisional){
                        /* RFC 3261 13.2.2.1.
                            Zero, one or multiple provisional responses may arrive before one or
                            more final responses are received.  Provisional responses for an
                            INVITE request can create "early dialogs".  If a provisional response
                            has a tag in the To field, and if the dialog ID of the response does
                            not match an existing dialog, one is constructed using the procedures
                            defined in Section 12.1.2.
                        */
                        if(e.Response.StatusCode > 100 && e.Response.To.Tag != null){
                            earlyDialogs.Add((SIP_Dialog_Invite)e.GetOrCreateDialog);
                        }

                        // 180_Ringing.
                        if(e.Response.StatusCode == 180){
                            m_pPlayer.Play(ResManager.GetStream("ringing.wav"),10);

                            // We need BeginInvoke here, otherwise we block client transaction.
                            m_pStatusBar.BeginInvoke(new MethodInvoker(delegate(){
                                m_pStatusBar.Items["text"].Text = "Ringing";
                            }));
                        }
                    }

                    #endregion

                    #region Success

                    else if(e.Response.StatusCodeType == SIP_StatusCodeType.Success){
                        SIP_Dialog dialog = e.GetOrCreateDialog;

                        /* Exit all all other dialogs created by this call (due to forking).
                           That is not defined in RFC but, since UAC can send BYE to early and confirmed dialogs, 
                           all this is 100% valid.
                        */                        
                        foreach(SIP_Dialog_Invite d in earlyDialogs.ToArray()){
                            if(!d.Equals(dialog)){
                                d.Terminate("Another forking leg accepted.",true);
                            }
                        }

                        m_pCall.InitCalling(dialog,sdpOffer);

                        // Remote-party provided SDP.
                        if(e.Response.ContentType != null && e.Response.ContentType.ToLower().IndexOf("application/sdp") > -1){
                            try{
                                // SDP offer. We sent offerless INVITE, we need to send SDP answer in ACK request.'
                                if(e.ClientTransaction.Request.ContentType == null || e.ClientTransaction.Request.ContentType.ToLower().IndexOf("application/sdp") == -1){
                                    // Currently we never do it, so it never happens. This is place holder, if we ever support it.
                                }
                                // SDP answer to our offer.
                                else{
                                    // This method takes care of ACK sending and 2xx response retransmission ACK sending.
                                    HandleAck(m_pCall.Dialog,e.ClientTransaction);

                                    ProcessMediaAnswer(m_pCall,m_pCall.LocalSDP,SDP_Message.Parse(Encoding.UTF8.GetString(e.Response.Data)));                                    
                                }
                            }
                            catch{
                                m_pCall.Terminate("SDP answer parsing/processing failed.");
                            }
                        }
                        else{
                            // If we provided SDP offer, there must be SDP answer.
                            if(e.ClientTransaction.Request.ContentType != null && e.ClientTransaction.Request.ContentType.ToLower().IndexOf("application/sdp") > -1){
                                m_pCall.Terminate("Invalid 2xx response, required SDP answer is missing.");
                            }
                        }
                                                
                        // Stop ringing.
                        m_pPlayer.Stop();
                    }

                    #endregion

                    #region Failure

                    else{
                        /* RFC 3261 13.2.2.3.
                            All early dialogs are considered terminated upon reception of the non-2xx final response.
                        */
                        foreach(SIP_Dialog_Invite dialog in earlyDialogs.ToArray()){
                            dialog.Terminate("All early dialogs are considered terminated upon reception of the non-2xx final response. (RFC 3261 13.2.2.3)",false);
                        }

                        // We need BeginInvoke here, otherwise we block client transaction while message box open.
                        if(m_pCall.State != SIP_CallState.Terminating){
                            this.BeginInvoke(new MethodInvoker(delegate(){
                                m_pCall_HangUp.Image = ResManager.GetIcon("call.ico",new Size(24,24)).ToBitmap();
                                MessageBox.Show("Calling failed: " + e.Response.StatusCode_ReasonPhrase,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                            }));
                        }

                        // We need BeginInvoke here, otherwise we block client transaction.
                        m_pStatusBar.BeginInvoke(new MethodInvoker(delegate(){
                            m_pStatusBar.Items["text"].Text = "";
                        }));
                        // Stop calling or ringing.
                        m_pPlayer.Stop();

                        // Terminate call.
                        m_pCall.Terminate("Remote party rejected a call.",false);
                    }

                    #endregion
                }
                catch(Exception x){
                    // We need BeginInvoke here, otherwise we block client transaction while message box open.
                    this.BeginInvoke(new MethodInvoker(delegate(){
                        MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    }));
                }
            };

            m_pStatusBar.Items["text"].Text = "Calling";
            m_pStatusBar.Items["duration"].Text = "00:00:00";
            m_pPlayer.Play(ResManager.GetStream("calling.wav"),10);

            // Start calling.
            sender.Start();
        }              
                
        #endregion
                
        #region method PutCallOnHold

        /// <summary>
        /// Puts call on hold.
        /// </summary>
        private void PutCallOnHold()
        {
            // Get copy of SDP.            
            SDP_Message onHoldOffer = m_pCall.LocalSDP.Clone();
            // Each time we modify SDP message we need to increase session version.
            onHoldOffer.Origin.SessionVersion++;

            // Mark all active enabled media streams inactive.
            foreach(SDP_MediaDescription m in onHoldOffer.MediaDescriptions){
                if(m.Port != 0){
                    m.SetStreamMode("inactive");
                }
            }

            // Create INVITE request.
            SIP_Request invite = m_pCall.Dialog.CreateRequest(SIP_Methods.INVITE);
            invite.ContentType = "application/sdp";
            invite.Data        = onHoldOffer.ToByte();

            bool finalResponseSeen = false;
            SIP_RequestSender sender = m_pCall.Dialog.CreateRequestSender(invite);
            sender.ResponseReceived += delegate(object s,SIP_ResponseReceivedEventArgs e){
                // Skip 2xx retransmited response.
                if(finalResponseSeen){
                    return;
                }
                if(e.Response.StatusCode >= 200){
                    finalResponseSeen = true;
                }

                try{
                    #region Provisional

                    if(e.Response.StatusCodeType == SIP_StatusCodeType.Provisional){
                        // We don't care provisional responses here.
                    }

                    #endregion
                 
                    #region Success

                    else if(e.Response.StatusCodeType == SIP_StatusCodeType.Success){
                        // Remote-party provided SDP answer.
                        if(e.Response.ContentType != null && e.Response.ContentType.ToLower().IndexOf("application/sdp") > -1){
                            try{
                                // This method takes care of ACK sending and 2xx response retransmission ACK sending.
                                HandleAck(m_pCall.Dialog,e.ClientTransaction);

                                ProcessMediaAnswer(m_pCall,onHoldOffer,SDP_Message.Parse(Encoding.UTF8.GetString(e.Response.Data))); 
                            }
                            catch{
                                m_pCall.Terminate("SDP answer parsing failed.");
                            }

                            // We need invoke here, we are running on thread pool thread.
                            this.BeginInvoke(new MethodInvoker(delegate(){
                                m_pToggleOnHold.Enabled = true;
                                m_pToggleOnHold.Text = "Unhold";

                                m_pStatusBar.Items["text"].Text = "We are holding a call";
                            }));
                        }
                    }

                    #endregion

                    #region Failure

                    else{
                        // We need BeginInvoke here, otherwise we block client transaction while message box open.
                        this.BeginInvoke(new MethodInvoker(delegate(){
                            MessageBox.Show("Re-INVITE Error: " + e.Response.StatusCode_ReasonPhrase,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                            m_pToggleOnHold.Enabled = true;
                        }));

                        // 481 Call Transaction Does Not Exist.
                        if(e.Response.StatusCode == 481){
                            m_pCall.Terminate("Remote-party call does not exist any more.",false);
                        }
                    }

                    #endregion
                }
                catch(Exception x){
                    // We need BeginInvoke here, otherwise we block client transaction while message box open.
                    this.BeginInvoke(new MethodInvoker(delegate(){
                        MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    }));
                }
            };
            sender.Start();
        }

        #endregion

        #region method PutCallUnHold

        /// <summary>
        /// Takes call off on hold.
        /// </summary>
        private void PutCallUnHold()
        {
            // Get copy of SDP.            
            SDP_Message onHoldOffer = m_pCall.LocalSDP.Clone();
            // Each time we modify SDP message we need to increase session version.
            onHoldOffer.Origin.SessionVersion++;

            // Mark all active enabled media streams sendrecv.
            foreach(SDP_MediaDescription m in onHoldOffer.MediaDescriptions){
                if(m.Port != 0){
                    m.SetStreamMode("sendrecv");
                }
            }

            // Create INVITE request.
            SIP_Request invite = m_pCall.Dialog.CreateRequest(SIP_Methods.INVITE);
            invite.ContentType = "application/sdp";
            invite.Data        = onHoldOffer.ToByte();

            bool finalResponseSeen = false;
            SIP_RequestSender sender = m_pCall.Dialog.CreateRequestSender(invite);
            sender.ResponseReceived += delegate(object s,SIP_ResponseReceivedEventArgs e){
                // Skip 2xx retransmited response.
                if(finalResponseSeen){
                    return;
                }
                if(e.Response.StatusCode >= 200){
                    finalResponseSeen = true;
                }

                try{
                    #region Provisional

                    if(e.Response.StatusCodeType == SIP_StatusCodeType.Provisional){
                        // We don't care provisional responses here.
                    }

                    #endregion
                 
                    #region Success

                    else if(e.Response.StatusCodeType == SIP_StatusCodeType.Success){
                        // Remote-party provided SDP answer.
                        if(e.Response.ContentType != null && e.Response.ContentType.ToLower().IndexOf("application/sdp") > -1){
                            try{
                                SDP_Message sdpAnswer = SDP_Message.Parse(Encoding.UTF8.GetString(e.Response.Data));

                                // This method takes care of ACK sending and 2xx response retransmission ACK sending.
                                HandleAck(m_pCall.Dialog,e.ClientTransaction);

                                ProcessMediaAnswer(m_pCall,onHoldOffer,sdpAnswer);
                                                                
                                // We need invoke here, we are running on thread pool thread.
                                this.BeginInvoke(new MethodInvoker(delegate(){
                                    m_pToggleOnHold.Enabled = true;
                                    m_pToggleOnHold.Text = "Hold";

                                    if(IsRemotePartyHolding(sdpAnswer)){
                                        m_pStatusBar.Items["text"].Text = "Remote party holding a call";

                                        m_pPlayer.Play(ResManager.GetStream("onhold.wav"),20);
                                    }
                                    else{
                                        m_pStatusBar.Items["text"].Text = "Call established";
                                    }
                                }));
                            }
                            catch{
                                m_pCall.Terminate("SDP answer parsing failed.");
                            }                            
                        }
                    }

                    #endregion

                    #region Failure

                    else{
                        // We need BeginInvoke here, otherwise we block client transaction while message box open.
                        this.BeginInvoke(new MethodInvoker(delegate(){
                            MessageBox.Show("Re-INVITE Error: " + e.Response.StatusCode_ReasonPhrase,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                            m_pToggleOnHold.Enabled = true;
                        }));

                        // 481 Call Transaction Does Not Exist.
                        if(e.Response.StatusCode == 481){
                            m_pCall.Terminate("Remote-party call does not exist any more.",false);
                        }
                    }

                    #endregion
                }
                catch(Exception x){
                    // We need BeginInvoke here, otherwise we block client transaction while message box open.
                    this.BeginInvoke(new MethodInvoker(delegate(){
                        MessageBox.Show("Error: " + x.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    }));
                }
            };
            sender.Start();
        }

        #endregion


        #region method ProcessMediaOffer

        /// <summary>
        /// Processes media offer.
        /// </summary>
        /// <param name="dialog">SIP dialog.</param>
        /// <param name="transaction">Server transaction</param>
        /// <param name="rtpMultimediaSession">RTP multimedia session.</param>
        /// <param name="offer">Remote-party SDP offer.</param>
        /// <param name="localSDP">Current local SDP.</param>
        /// <exception cref="ArgumentNullException">Is raised <b>dialog</b>,<b>transaction</b>,<b>rtpMultimediaSession</b>,<b>offer</b> or <b>localSDP</b> is null reference.</exception>
        private void ProcessMediaOffer(SIP_Dialog dialog,SIP_ServerTransaction transaction,RTP_MultimediaSession rtpMultimediaSession,SDP_Message offer,SDP_Message localSDP)
        {    
            if(dialog == null){
                throw new ArgumentNullException("dialog");
            }
            if(transaction == null){
                throw new ArgumentNullException("transaction");
            }
            if(rtpMultimediaSession == null){
                throw new ArgumentNullException("rtpMultimediaSession");
            }
            if(offer == null){
                throw new ArgumentNullException("offer");
            }
            if(localSDP == null){
                throw new ArgumentNullException("localSDP");
            }
                        
            try{                         
                bool onHold = m_pToggleOnHold.Text == "Unhold";

                #region SDP basic validation

                // Version field must exist.
                if(offer.Version == null){
                    transaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": Invalid SDP answer: Required 'v'(Protocol Version) field is missing.",transaction.Request));

                    return;
                }

                // Origin field must exist.
                if(offer.Origin == null){
                    transaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": Invalid SDP answer: Required 'o'(Origin) field is missing.",transaction.Request));

                    return;
                }

                // Session Name field.

                // Check That global 'c' connection attribute exists or otherwise each enabled media stream must contain one.
                if(offer.Connection == null){
                    for(int i=0;i<offer.MediaDescriptions.Count;i++){
                        if(offer.MediaDescriptions[i].Connection == null){
                            transaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": Invalid SDP answer: Global or per media stream no: " + i + " 'c'(Connection) attribute is missing.",transaction.Request));

                            return;
                        }
                    }
                }

                #endregion
                                                            
                // Re-INVITE media streams count must be >= current SDP streams.
                if(localSDP.MediaDescriptions.Count > offer.MediaDescriptions.Count){
                    transaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": re-INVITE SDP offer media stream count must be >= current session stream count.",transaction.Request));

                    return;
                }

                bool audioAccepted = false;
                // Process media streams info.
                for(int i=0;i<offer.MediaDescriptions.Count;i++){
                    SDP_MediaDescription offerMedia  = offer.MediaDescriptions[i];
                    SDP_MediaDescription answerMedia = (localSDP.MediaDescriptions.Count > i ? localSDP.MediaDescriptions[i] : null);

                    // Disabled stream.
                    if(offerMedia.Port == 0){
                        // Remote-party offered new disabled stream.
                        if(answerMedia == null){
                            // Just copy offer media stream data to answer and set port to zero.
                            localSDP.MediaDescriptions.Add(offerMedia);
                            localSDP.MediaDescriptions[i].Port = 0;
                        }
                        // Existing disabled stream or remote party disabled it.
                        else{
                            answerMedia.Port = 0;

                            #region Cleanup active RTP stream and it's resources, if it exists

                            // Dispose existing RTP session.
                            if(answerMedia.Tags.ContainsKey("rtp_session")){                            
                                ((RTP_Session)offerMedia.Tags["rtp_session"]).Dispose();
                                answerMedia.Tags.Remove("rtp_session");
                            }

                            // Release UPnPports if any.
                            if(answerMedia.Tags.ContainsKey("upnp_rtp_map")){
                                try{
                                    m_pUPnP.DeletePortMapping((UPnP_NAT_Map)answerMedia.Tags["upnp_rtp_map"]);
                                }
                                catch{
                                }
                                answerMedia.Tags.Remove("upnp_rtp_map");
                            }
                            if(answerMedia.Tags.ContainsKey("upnp_rtcp_map")){
                                try{
                                    m_pUPnP.DeletePortMapping((UPnP_NAT_Map)answerMedia.Tags["upnp_rtcp_map"]);
                                }
                                catch{
                                }
                                answerMedia.Tags.Remove("upnp_rtcp_map");
                            }

                            #endregion
                        }
                    }
                    // Remote-party wants to communicate with this stream.
                    else{
                        // See if we can support this stream.
                        if(!audioAccepted && CanSupportMedia(offerMedia)){
                            // New stream.
                            if(answerMedia == null){
                                answerMedia = new SDP_MediaDescription(SDP_MediaTypes.audio,0,2,"RTP/AVP",null);
                                localSDP.MediaDescriptions.Add(answerMedia);
                            }
                            
                            #region Build audio codec map with codecs which we support
   
                            Dictionary<int,AudioCodec> audioCodecs = GetOurSupportedAudioCodecs(offerMedia);
                            answerMedia.MediaFormats.Clear();
                            answerMedia.Attributes.Clear();
                            foreach(KeyValuePair<int,AudioCodec> entry in audioCodecs){
                                answerMedia.Attributes.Add(new SDP_Attribute("rtpmap",entry.Key + " " + entry.Value.Name + "/" + entry.Value.CompressedAudioFormat.SamplesPerSecond));
                                answerMedia.MediaFormats.Add(entry.Key.ToString());
                            }
                            answerMedia.Attributes.Add(new SDP_Attribute("ptime","20"));
                            answerMedia.Tags["audio_codecs"] = audioCodecs;

                            #endregion

                            #region Create/modify RTP session

                            // RTP session doesn't exist, create it.
                            if(!answerMedia.Tags.ContainsKey("rtp_session")){
                                RTP_Session rtpSess = CreateRtpSession(rtpMultimediaSession);
                                // RTP session creation failed,disable this stream.
                                if(rtpSess == null){                                    
                                    answerMedia.Port = 0;

                                    break;
                                }
                                answerMedia.Tags.Add("rtp_session",rtpSess);

                                rtpSess.NewReceiveStream += delegate(object s,RTP_ReceiveStreamEventArgs e){
                                    if(answerMedia.Tags.ContainsKey("rtp_audio_out")){
                                        ((AudioOut_RTP)answerMedia.Tags["rtp_audio_out"]).Dispose();
                                    }
            
                                    AudioOut_RTP audioOut = new AudioOut_RTP(m_pAudioOutDevice,e.Stream,audioCodecs);
                                    audioOut.Start();
                                    answerMedia.Tags["rtp_audio_out"] = audioOut;
                                };

                                // NAT
                                if(!HandleNAT(answerMedia,rtpSess)){
                                    // NAT handling failed,disable this stream.
                                    answerMedia.Port = 0;

                                    break;
                                }                                
                            }
                                                        
                            RTP_StreamMode offerStreamMode = GetRtpStreamMode(offer,offerMedia);
                            if(offerStreamMode == RTP_StreamMode.Inactive){
                                answerMedia.SetStreamMode("inactive");
                            }
                            else if(offerStreamMode == RTP_StreamMode.Receive){
                                answerMedia.SetStreamMode("sendonly");
                            }
                            else if(offerStreamMode == RTP_StreamMode.Send){
                                if(onHold){
                                    answerMedia.SetStreamMode("inactive");
                                }
                                else{
                                    answerMedia.SetStreamMode("recvonly");
                                }
                            }
                            else if(offerStreamMode == RTP_StreamMode.SendReceive){
                                if(onHold){
                                    answerMedia.SetStreamMode("inactive");
                                }
                                else{
                                    answerMedia.SetStreamMode("sendrecv");
                                }
                            }
                            
                            RTP_Session rtpSession = (RTP_Session)answerMedia.Tags["rtp_session"];                                              
                            rtpSession.Payload = Convert.ToInt32(answerMedia.MediaFormats[0]);
                            rtpSession.StreamMode = GetRtpStreamMode(localSDP,answerMedia);
                            rtpSession.RemoveTargets();
                            if(GetSdpHost(offer,offerMedia) != "0.0.0.0"){
                                rtpSession.AddTarget(GetRtpTarget(offer,offerMedia));
                            }
                            rtpSession.Start();

                            #endregion

                            #region Create/modify audio-in source
                                                    
                            if(!answerMedia.Tags.ContainsKey("rtp_audio_in")){
                                AudioIn_RTP rtpAudioIn = new AudioIn_RTP(m_pAudioInDevice,20,audioCodecs,rtpSession.CreateSendStream());                        
                                rtpAudioIn.Start();
                                answerMedia.Tags.Add("rtp_audio_in",rtpAudioIn);
                            }
                            else{
                                ((AudioIn_RTP)answerMedia.Tags["rtp_audio_in"]).AudioCodecs = audioCodecs;
                            }
                            
                            #endregion

                            audioAccepted = true;
                        }
                        // We don't accept this stream, so disable it.
                        else{                            
                            // Just copy offer media stream data to answer and set port to zero.

                            // Delete exisiting media stream.
                            if(answerMedia != null){
                                localSDP.MediaDescriptions.RemoveAt(i);
                            }
                            localSDP.MediaDescriptions.Add(offerMedia);
                            localSDP.MediaDescriptions[i].Port = 0;
                        }
                    }
                }

                #region Create and send 2xx response
            
                SIP_Response response = m_pStack.CreateResponse(SIP_ResponseCodes.x200_Ok,transaction.Request,transaction.Flow);
                //response.Contact = SIP stack will allocate it as needed;
                response.ContentType = "application/sdp";
                response.Data = localSDP.ToByte();

                transaction.SendResponse(response);
                
                // Start retransmitting 2xx response, while ACK receives.
                Handle2xx(dialog,transaction);

                // REMOVE ME: 27.11.2010
                // Start retransmitting 2xx response, while ACK receives.
                //m_pInvite2xxMgr.Add(dialog,transaction);
                                
                #endregion
            }
            catch(Exception x){
                transaction.SendResponse(m_pStack.CreateResponse(SIP_ResponseCodes.x500_Server_Internal_Error + ": " + x.Message,transaction.Request));
            }
        }

        #endregion

        #region method ProcessMediaAnswer

        /// <summary>
        /// Processes media answer.
        /// </summary>
        /// <param name="call">SIP call.</param>
        /// <param name="offer">SDP media offer.</param>
        /// <param name="answer">SDP remote-party meida answer.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>call</b>,<b>offer</b> or <b>answer</b> is null reference.</exception>
        private void ProcessMediaAnswer(SIP_Call call,SDP_Message offer,SDP_Message answer)
        {
            if(call == null){
                throw new ArgumentNullException("call");
            }
            if(offer == null){
                throw new ArgumentNullException("offer");
            }
            if(answer == null){
                throw new ArgumentNullException("answer");
            }

            try{
                #region SDP basic validation

                // Version field must exist.
                if(offer.Version == null){
                    call.Terminate("Invalid SDP answer: Required 'v'(Protocol Version) field is missing.");

                    return;
                }

                // Origin field must exist.
                if(offer.Origin == null){
                    call.Terminate("Invalid SDP answer: Required 'o'(Origin) field is missing.");

                    return;
                }

                // Session Name field.

                // Check That global 'c' connection attribute exists or otherwise each enabled media stream must contain one.
                if(offer.Connection == null){
                    for(int i=0;i<offer.MediaDescriptions.Count;i++){
                        if(offer.MediaDescriptions[i].Connection == null){
                            call.Terminate("Invalid SDP answer: Global or per media stream no: " + i + " 'c'(Connection) attribute is missing.");

                            return;
                        }
                    }
                }


                // Check media streams count.
                if(offer.MediaDescriptions.Count != answer.MediaDescriptions.Count){
                    call.Terminate("Invalid SDP answer, media descriptions count in answer must be equal to count in media offer (RFC 3264 6.).");

                    return;
                }

                #endregion
                                                                                
                // Process media streams info.
                for(int i=0;i<offer.MediaDescriptions.Count;i++){
                    SDP_MediaDescription offerMedia  = offer.MediaDescriptions[i];
                    SDP_MediaDescription answerMedia = answer.MediaDescriptions[i];
                    
                    // Remote-party disabled this stream.
                    if(answerMedia.Port == 0){

                        #region Cleanup active RTP stream and it's resources, if it exists

                        // Dispose existing RTP session.
                        if(offerMedia.Tags.ContainsKey("rtp_session")){                            
                            ((RTP_Session)offerMedia.Tags["rtp_session"]).Dispose();
                            offerMedia.Tags.Remove("rtp_session");
                        }

                        // Release UPnPports if any.
                        if(offerMedia.Tags.ContainsKey("upnp_rtp_map")){
                            try{
                                m_pUPnP.DeletePortMapping((UPnP_NAT_Map)offerMedia.Tags["upnp_rtp_map"]);
                            }
                            catch{
                            }
                            offerMedia.Tags.Remove("upnp_rtp_map");
                        }
                        if(offerMedia.Tags.ContainsKey("upnp_rtcp_map")){
                            try{
                                m_pUPnP.DeletePortMapping((UPnP_NAT_Map)offerMedia.Tags["upnp_rtcp_map"]);
                            }
                            catch{
                            }
                            offerMedia.Tags.Remove("upnp_rtcp_map");
                        }

                        #endregion
                    }
                    // Remote-party accepted stream.
                    else{
                        Dictionary<int,AudioCodec> audioCodecs = (Dictionary<int,AudioCodec>)offerMedia.Tags["audio_codecs"];

                        #region Validate stream-mode disabled,inactive,sendonly,recvonly

                        /* RFC 3264 6.1.
                            If a stream is offered as sendonly, the corresponding stream MUST be
                            marked as recvonly or inactive in the answer.  If a media stream is
                            listed as recvonly in the offer, the answer MUST be marked as
                            sendonly or inactive in the answer.  If an offered media stream is
                            listed as sendrecv (or if there is no direction attribute at the
                            media or session level, in which case the stream is sendrecv by
                            default), the corresponding stream in the answer MAY be marked as
                            sendonly, recvonly, sendrecv, or inactive.  If an offered media
                            stream is listed as inactive, it MUST be marked as inactive in the
                            answer.
                        */

                        // If we disabled this stream in offer and answer enables it (no allowed), terminate call.
                        if(offerMedia.Port == 0){
                            call.Terminate("Invalid SDP answer, you may not enable sdp-offer disabled stream no: " + i + " (RFC 3264 6.).");

                            return;
                        }

                        RTP_StreamMode offerStreamMode  = GetRtpStreamMode(offer,offerMedia);
                        RTP_StreamMode answerStreamMode = GetRtpStreamMode(answer,answerMedia);                                                
                        if(offerStreamMode == RTP_StreamMode.Send && answerStreamMode != RTP_StreamMode.Receive){
                            call.Terminate("Invalid SDP answer, sdp stream no: " + i + " stream-mode must be 'recvonly' (RFC 3264 6.).");

                            return;
                        }
                        if(offerStreamMode == RTP_StreamMode.Receive && answerStreamMode != RTP_StreamMode.Send){
                            call.Terminate("Invalid SDP answer, sdp stream no: " + i + " stream-mode must be 'sendonly' (RFC 3264 6.).");

                            return;
                        }
                        if(offerStreamMode == RTP_StreamMode.Inactive && answerStreamMode != RTP_StreamMode.Inactive){
                            call.Terminate("Invalid SDP answer, sdp stream no: " + i + " stream-mode must be 'inactive' (RFC 3264 6.).");

                            return;
                        }

                        #endregion

                        #region Create/modify RTP session
                                                
                        RTP_Session rtpSession = (RTP_Session)offerMedia.Tags["rtp_session"];
                        rtpSession.Payload = Convert.ToInt32(answerMedia.MediaFormats[0]);
                        rtpSession.StreamMode = (answerStreamMode == RTP_StreamMode.Inactive ? RTP_StreamMode.Inactive : offerStreamMode);                        
                        rtpSession.RemoveTargets();
                        if(GetSdpHost(answer,answerMedia) != "0.0.0.0"){
                            rtpSession.AddTarget(GetRtpTarget(answer,answerMedia));
                        }
                        rtpSession.Start();

                        #endregion

                        #region Create/modify audio-in source

                        if(!offerMedia.Tags.ContainsKey("rtp_audio_in")){
                            AudioIn_RTP rtpAudioIn = new AudioIn_RTP(m_pAudioInDevice,20,audioCodecs,rtpSession.CreateSendStream());                        
                            rtpAudioIn.Start();
                            offerMedia.Tags.Add("rtp_audio_in",rtpAudioIn);
                        }
                        
                        #endregion
                    }
                }

                call.LocalSDP  = offer;
                call.RemoteSDP = answer;
            }
            catch(Exception x){
                call.Terminate("Error processing SDP answer: " + x.Message);
            }
        }

        #endregion

        #region method CanSupportMedia

        /// <summary>
        /// Checks if we can support the specified media.
        /// </summary>
        /// <param name="media">SDP media.</param>
        /// <returns>Returns true if we can support this media, otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>media</b> is null reference.</exception>
        private bool CanSupportMedia(SDP_MediaDescription media)
        {
            if(media == null){
                throw new ArgumentNullException("media");
            }

            if(!string.Equals(media.MediaType,SDP_MediaTypes.audio,StringComparison.InvariantCultureIgnoreCase)){
                return false;
            }
            if(!string.Equals(media.Protocol,"RTP/AVP",StringComparison.InvariantCultureIgnoreCase)){
                return false;
            }

            if(GetOurSupportedAudioCodecs(media).Count > 0){
                return true;
            }

            return false;
        }

        #endregion

        #region method GetOurSupportedAudioCodecs

        /// <summary>
        /// Gets audio codecs which we can support from SDP media stream.
        /// </summary>
        /// <param name="media">SDP media stream.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>media</b> is null reference.</exception>
        /// <returns>Returns audio codecs which support.</returns>
        private Dictionary<int,AudioCodec> GetOurSupportedAudioCodecs(SDP_MediaDescription media)
        {
            if(media == null){
                throw new ArgumentNullException("media");
            }

            Dictionary<int,AudioCodec> codecs = new Dictionary<int,AudioCodec>();

            // Check for IANA registered payload. Custom range is 96-127 and always must have rtpmap attribute.
            foreach(string format in media.MediaFormats){
                int payload = Convert.ToInt32(format);
                if(payload < 96 && m_pAudioCodecs.ContainsKey(payload)){
                    if(!codecs.ContainsKey(payload)){
                        codecs.Add(payload,m_pAudioCodecs[payload]);
                    }
                }
            }

            // Check rtpmap payloads.
            foreach(SDP_Attribute a in media.Attributes){
                if(string.Equals(a.Name,"rtpmap",StringComparison.InvariantCultureIgnoreCase)){
                    // Example: 0 PCMU/8000
                    string[] parts = a.Value.Split(' ');
                    int    payload   = Convert.ToInt32(parts[0]);
                    string codecName = parts[1].Split('/')[0];

                    foreach(AudioCodec codec in m_pAudioCodecs.Values){
                        if(string.Equals(codec.Name,codecName,StringComparison.InvariantCultureIgnoreCase)){
                            if(!codecs.ContainsKey(payload)){
                                codecs.Add(payload,codec);
                            }
                        }
                    }
                }
            }

            return codecs;
        }

        #endregion

        #region method CreateRtpSession

        /// <summary>
        /// Creates new RTP session.
        /// </summary>
        /// <param name="rtpMultimediaSession">RTP multimedia session.</param>
        /// <returns>Returns created RTP session or null if failed to create RTP session.</returns>
        /// <exception cref="ArgumentNullException">Is raised <b>rtpMultimediaSession</b> is null reference.</exception>
        private RTP_Session CreateRtpSession(RTP_MultimediaSession rtpMultimediaSession)
        {
            if(rtpMultimediaSession == null){
                throw new ArgumentNullException("rtpMultimediaSession");
            }

            //--- Search RTP IP -------------------------------------------------------//
            IPAddress rtpIP = null;
            foreach(IPAddress ip in Dns.GetHostAddresses("")){
                if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork){
                    rtpIP = ip;
                    break;
                }
            }
            if(rtpIP == null){
                throw new Exception("None of the network connection is available.");
            }
            //------------------------------------------------------------------------//

            // Search free ports for RTP session.
            for(int i=0;i<100;i+=2){
                try{
                    return rtpMultimediaSession.CreateSession(new RTP_Address(rtpIP,m_RtpBasePort,m_RtpBasePort + 1),new RTP_Clock(1,8000));
                }
                catch{
                    m_RtpBasePort += 2;
                }
            }
            
            return null;
        }

        #endregion

        #region method HandleNAT

        /// <summary>
        /// Handles NAT and stores RTP data to <b>mediaStream</b>.
        /// </summary>
        /// <param name="mediaStream">SDP media stream.</param>
        /// <param name="rtpSession">RTP session.</param>
        /// <returns>Returns true if NAT handled ok, otherwise false.</returns>
        private bool HandleNAT(SDP_MediaDescription mediaStream,RTP_Session rtpSession)
        {
            if(mediaStream == null){
                throw new ArgumentNullException("mediaStream");
            }
            if(rtpSession == null){
                throw new ArgumentNullException("rtpSession");
            }

            IPEndPoint   rtpPublicEP   = null;
            IPEndPoint   rtcpPublicEP  = null;

            // We have public IP.
            if(!Net_Utils.IsPrivateIP(rtpSession.LocalEP.IP)){
                rtpPublicEP  = rtpSession.LocalEP.RtpEP;
                rtcpPublicEP = rtpSession.LocalEP.RtcpEP;
            }
            // No NAT handling.
            else if(m_NatHandlingType == "no_nat"){
                rtpPublicEP  = rtpSession.LocalEP.RtpEP;
                rtcpPublicEP = rtpSession.LocalEP.RtcpEP;
            }
            // Use STUN.
            else if(m_NatHandlingType == "stun"){
                rtpSession.StunPublicEndPoints(m_StunServer,3478,out rtpPublicEP,out rtcpPublicEP);
            }
            // Use UPnP.
            else if(m_NatHandlingType == "upnp"){
                // Try to open UPnP ports.
                if(m_pUPnP.IsSupported){
                    int rtpPublicPort  = rtpSession.LocalEP.RtpEP.Port;
                    int rtcpPublicPort = rtpSession.LocalEP.RtcpEP.Port;

                    try{
                        UPnP_NAT_Map[] maps = m_pUPnP.GetPortMappings();
                        while(true){
                            bool conficts = false;
                            // Check that some other application doesn't use that port.
                            foreach(UPnP_NAT_Map map in maps){
                                // Existing map entry conflicts.
                                if(Convert.ToInt32(map.ExternalPort) == rtpPublicPort || Convert.ToInt32(map.ExternalPort) == rtcpPublicPort){
                                    rtpPublicPort  += 2;
                                    rtcpPublicPort += 2;
                                    conficts = true;

                                    break;
                                }
                            }
                            if(!conficts){
                                break;
                            }
                        }

                        m_pUPnP.AddPortMapping(true,"LS RTP","UDP",null,rtpPublicPort,rtpSession.LocalEP.RtpEP,0);
                        m_pUPnP.AddPortMapping(true,"LS RTCP","UDP",null,rtcpPublicPort,rtpSession.LocalEP.RtcpEP,0);
                                        
                        IPAddress publicIP = m_pUPnP.GetExternalIPAddress();

                        rtpPublicEP  = new IPEndPoint(publicIP,rtpPublicPort);
                        rtcpPublicEP = new IPEndPoint(publicIP,rtcpPublicPort);

                        mediaStream.Tags.Add("upnp_rtp_map",new UPnP_NAT_Map(true,"UDP","",rtpPublicPort.ToString(),rtpSession.LocalEP.IP.ToString(),rtpSession.LocalEP.RtpEP.Port,"LS RTP",0));
                        mediaStream.Tags.Add("upnp_rtcp_map",new UPnP_NAT_Map(true,"UDP","",rtcpPublicPort.ToString(),rtpSession.LocalEP.IP.ToString(),rtpSession.LocalEP.RtcpEP.Port,"LS RTCP",0));                    
                    }
                    catch{                        
                    }
                }
            }
      
            if(rtpPublicEP != null && rtcpPublicEP != null){
                mediaStream.Port = rtpPublicEP.Port;
                if((rtpPublicEP.Port + 1) != rtcpPublicEP.Port){
                    // Remove old rport attribute, if any.
                    for(int i=0;i<mediaStream.Attributes.Count;i++){
                        if(string.Equals(mediaStream.Attributes[i].Name,"rport",StringComparison.InvariantCultureIgnoreCase)){
                            mediaStream.Attributes.RemoveAt(i);
                            i--;
                        }
                    }
                    mediaStream.Attributes.Add(new SDP_Attribute("rport",rtcpPublicEP.Port.ToString()));
                }
                mediaStream.Connection = new SDP_Connection("IN","IP4",rtpPublicEP.Address.ToString());

                return true;
            }
            
            return false;
        }

        #endregion

        #region method GetRtpStreamMode

        /// <summary>
        /// Gets RTP stream mode.
        /// </summary>
        /// <param name="sdp">SDP message.</param>
        /// <param name="media">SDP media.</param>
        /// <returns>Returns RTP stream mode.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>sdp</b> or <b>media</b> is null reference.</exception>
        private RTP_StreamMode GetRtpStreamMode(SDP_Message sdp,SDP_MediaDescription media)
        {
            if(sdp == null){
                throw new ArgumentNullException("sdp");
            }
            if(media == null){
                throw new ArgumentNullException("media");
            }

            // Try to get per media stream mode.
            foreach(SDP_Attribute a in media.Attributes){
                if(string.Equals(a.Name,"sendrecv",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.SendReceive;
                }
                else if(string.Equals(a.Name,"sendonly",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.Send;
                }
                else if(string.Equals(a.Name,"recvonly",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.Receive;
                }
                else if(string.Equals(a.Name,"inactive",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.Inactive;
                }
            }

            // No per media stream mode, try to get per session stream mode.
            foreach(SDP_Attribute a in sdp.Attributes){
                if(string.Equals(a.Name,"sendrecv",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.SendReceive;
                }
                else if(string.Equals(a.Name,"sendonly",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.Send;
                }
                else if(string.Equals(a.Name,"recvonly",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.Receive;
                }
                else if(string.Equals(a.Name,"inactive",StringComparison.InvariantCultureIgnoreCase)){
                    return RTP_StreamMode.Inactive;
                }
            }

            return RTP_StreamMode.SendReceive;
        }

        #endregion

        #region method GetSdpHost

        /// <summary>
        /// Gets SDP per media or global connection host.
        /// </summary>
        /// <param name="sdp">SDP message.</param>
        /// <param name="mediaStream">SDP media stream.</param>
        /// <returns>Returns SDP per media or global connection host.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>sdp</b> or <b>mediaStream</b> is null reference.</exception>
        private string GetSdpHost(SDP_Message sdp,SDP_MediaDescription mediaStream)
        {
            if(sdp == null){
                throw new ArgumentNullException("sdp");
            }
            if(mediaStream == null){
                throw new ArgumentNullException("mediaStream");
            }

            // We must have SDP global or per media connection info.
            string host = mediaStream.Connection != null ? mediaStream.Connection.Address : null;
            if(host == null){
                host = sdp.Connection.Address != null ? sdp.Connection.Address : null;

                if(host == null){
                    throw new ArgumentException("Invalid SDP message, global or per media 'c'(Connection) attribute is missing.");
                }
            }

            return host;
        }

        #endregion

        #region method GetRtpTarget

        /// <summary>
        /// Gets RTP target for SDP media stream.
        /// </summary>
        /// <param name="sdp">SDP message.</param>
        /// <param name="mediaStream">SDP media stream.</param>
        /// <returns>Return RTP target.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>sdp</b> or <b>mediaStream</b> is null reference.</exception>
        private RTP_Address GetRtpTarget(SDP_Message sdp,SDP_MediaDescription mediaStream)
        {
            if(sdp == null){
                throw new ArgumentNullException("sdp");
            }
            if(mediaStream == null){
                throw new ArgumentNullException("mediaStream");
            }

            // We must have SDP global or per media connection info.
            string host = mediaStream.Connection != null ? mediaStream.Connection.Address : null;
            if(host == null){
                host = sdp.Connection.Address != null ? sdp.Connection.Address : null;

                if(host == null){
                    throw new ArgumentException("Invalid SDP message, global or per media 'c'(Connection) attribute is missing.");
                }
            }

            int remoteRtcpPort = mediaStream.Port + 1;
            // Use specified RTCP port, if specified.
            foreach(SDP_Attribute attribute in mediaStream.Attributes){
                if(string.Equals(attribute.Name,"rtcp",StringComparison.InvariantCultureIgnoreCase)){
                    remoteRtcpPort = Convert.ToInt32(attribute.Value);

                    break;
                }
            }

            return new RTP_Address(System.Net.Dns.GetHostAddresses(host)[0],mediaStream.Port,remoteRtcpPort);
        }

        #endregion

        #region method IsRemotePartyHolding

        /// <summary>
        /// Checks if remote-party is holding audio.
        /// </summary>
        /// <param name="sdp">Remote-party SDP offer/answer.</param>
        /// <returns>Returns true is remote-party is holding audio, otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>sdp</b> is null reference.</exception>
        private bool IsRemotePartyHolding(SDP_Message sdp)
        {
            if(sdp == null){
                throw new ArgumentNullException("sdp");
            }

            // Check if first audio stream is SendRecv, otherwise remote-party holding audio.
            foreach(SDP_MediaDescription media in sdp.MediaDescriptions){
                if(media.Port != 0 && media.MediaType == "audio"){
                    if(GetRtpStreamMode(sdp,media) != RTP_StreamMode.SendReceive){
                        return true;
                    }

                    break;
                }
            }

            return false;
        }

        #endregion

        #region method HandleAck

        /// <summary>
        /// This method takes care of ACK sending and 2xx response retransmission ACK sending.
        /// </summary>
        /// <param name="dialog">SIP dialog.</param>
        /// <param name="transaction">SIP client transaction.</param>
        private void HandleAck(SIP_Dialog dialog,SIP_ClientTransaction transaction)
        {
            if(dialog == null){
                throw new ArgumentNullException("dialog");
            }
            if(transaction == null){
                throw new ArgumentNullException("transaction");
            }
            
            /* RFC 3261 6.
                The ACK for a 2xx response to an INVITE request is a separate transaction.
              
               RFC 3261 13.2.2.4.
                The UAC core MUST generate an ACK request for each 2xx received from
                the transaction layer.  The header fields of the ACK are constructed
                in the same way as for any request sent within a dialog (see Section
                12) with the exception of the CSeq and the header fields related to
                authentication.  The sequence number of the CSeq header field MUST be
                the same as the INVITE being acknowledged, but the CSeq method MUST
                be ACK.  The ACK MUST contain the same credentials as the INVITE.  If
                the 2xx contains an offer (based on the rules above), the ACK MUST
                carry an answer in its body.
            */

            SIP_t_ViaParm via = new SIP_t_ViaParm();
            via.ProtocolName = "SIP";
            via.ProtocolVersion = "2.0";
            via.ProtocolTransport = transaction.Flow.Transport;
            via.SentBy = new HostEndPoint(transaction.Flow.LocalEP);
            via.Branch = SIP_t_ViaParm.CreateBranch();
            via.RPort = 0;

            SIP_Request ackRequest = dialog.CreateRequest(SIP_Methods.ACK);
            ackRequest.Via.AddToTop(via.ToStringValue());
            ackRequest.CSeq = new SIP_t_CSeq(transaction.Request.CSeq.SequenceNumber,SIP_Methods.ACK);
            // Authorization
            foreach(SIP_HeaderField h in transaction.Request.Authorization.HeaderFields){
                ackRequest.Authorization.Add(h.Value);
            }
            // Proxy-Authorization 
            foreach(SIP_HeaderField h in transaction.Request.ProxyAuthorization.HeaderFields){
                ackRequest.Authorization.Add(h.Value);
            }

            // Send ACK.
            SendAck(dialog,ackRequest);

            // Start receive 2xx retransmissions.
            transaction.ResponseReceived += delegate(object sender,SIP_ResponseReceivedEventArgs e){
                if(dialog.State == SIP_DialogState.Disposed || dialog.State == SIP_DialogState.Terminated){                    
                    return;
                }

                // Don't send ACK for forked 2xx, our sent BYE(to all early dialogs) or their early timer will kill these dialogs.
                // Send ACK only to our accepted dialog 2xx response retransmission.
                if(e.Response.From.Tag == ackRequest.From.Tag && e.Response.To.Tag == ackRequest.To.Tag){
                    SendAck(dialog,ackRequest);
                }
            };
        }

        #endregion

        #region method SendAck

        /// <summary>
        /// Sends ACK to remote-party.
        /// </summary>
        /// <param name="dialog">SIP dialog.</param>
        /// <param name="ack">SIP ACK request.</param>
        private void SendAck(SIP_Dialog dialog,SIP_Request ack)
        {
            if(dialog == null){
                throw new ArgumentNullException("dialog");
            }
            if(ack == null){
                throw new ArgumentNullException("ack");
            }

            try{
                // Try existing flow.
                dialog.Flow.Send(ack);

                // Log
                if(dialog.Stack.Logger != null){
                    byte[] ackBytes = ack.ToByteData();

                    dialog.Stack.Logger.AddWrite(
                        dialog.ID,
                        null,
                        ackBytes.Length,
                        "Request [DialogID='" +  dialog.ID + "';" + "method='" + ack.RequestLine.Method + "'; cseq='" + ack.CSeq.SequenceNumber + "'; " + 
                        "transport='" + dialog.Flow.Transport + "'; size='" + ackBytes.Length + "'] sent '" + dialog.Flow.LocalEP + "' -> '" + dialog.Flow.RemoteEP + "'.",                                
                        dialog.Flow.LocalEP,
                        dialog.Flow.RemoteEP,
                        ackBytes
                    );
                }
            }
            catch{
                /* RFC 3261 13.2.2.4.
                    Once the ACK has been constructed, the procedures of [4] are used to
                    determine the destination address, port and transport.  However, the
                    request is passed to the transport layer directly for transmission,
                    rather than a client transaction.
                */
                try{
                    dialog.Stack.TransportLayer.SendRequest(ack);
                }
                catch(Exception x){
                    // Log
                    if(dialog.Stack.Logger != null){
                        dialog.Stack.Logger.AddText("Dialog [id='" + dialog.ID + "'] ACK send for 2xx response failed: " + x.Message + ".");
                    }
                }
            }
        }

        #endregion

        #region method Handle2xx

        /// <summary>
        /// This method takes care of INVITE 2xx response retransmissions while ACK received.
        /// </summary>
        /// <param name="dialog">SIP dialog.</param>
        /// <param name="transaction">INVITE server transaction.</param>
        /// <exception cref="ArgumentException">Is raised when <b>dialog</b>,<b>transaction</b> is null reference.</exception>
        private void Handle2xx(SIP_Dialog dialog,SIP_ServerTransaction transaction)
        {
            if(dialog == null){
                throw new ArgumentNullException("dialog");
            }
            if(transaction == null){
                throw new ArgumentException("transaction");
            }

            /* RFC 6026 8.1.
                Once the response has been constructed, it is passed to the INVITE
                server transaction.  In order to ensure reliable end-to-end
                transport of the response, it is necessary to periodically pass
                the response directly to the transport until the ACK arrives.  The
                2xx response is passed to the transport with an interval that
                starts at T1 seconds and doubles for each retransmission until it
                reaches T2 seconds (T1 and T2 are defined in Section 17).
                Response retransmissions cease when an ACK request for the
                response is received.  This is independent of whatever transport
                protocols are used to send the response.
             
                If the server retransmits the 2xx response for 64*T1 seconds without
                receiving an ACK, the dialog is confirmed, but the session SHOULD be
                terminated.  This is accomplished with a BYE, as described in Section
                15.
              
                 T1 - 500
                 T2 - 4000
            */

            TimerEx timer = null;
            
            EventHandler<SIP_RequestReceivedEventArgs> callback = delegate(object s1,SIP_RequestReceivedEventArgs e){
                try{
                    if(e.Request.RequestLine.Method == SIP_Methods.ACK){
                        // ACK for INVITE 2xx response received, stop retransmitting INVITE 2xx response.
                        if(transaction.Request.CSeq.SequenceNumber == e.Request.CSeq.SequenceNumber){
                            if(timer != null){
                                timer.Dispose();
                            }
                        }
                    }
                }
                catch{
                    // We don't care about errors here.
                }
            };
            dialog.RequestReceived += callback;
                
            // Create timer and sart retransmitting INVITE 2xx response.
            timer = new TimerEx(500);
            timer.AutoReset = false;
            timer.Elapsed += delegate(object s,System.Timers.ElapsedEventArgs e){
                try{
                    lock(transaction.SyncRoot){
                        if(transaction.State == SIP_TransactionState.Accpeted){
                            transaction.SendResponse(transaction.FinalResponse);
                        }
                        else{
                            timer.Dispose();

                            return;
                        }
                    }

                    timer.Interval = Math.Min(timer.Interval * 2,4000);
                    timer.Enabled = true;
                }
                catch{
                    // We don't care about errors here.
                }
            };
            timer.Disposed += delegate(object s1,EventArgs e1){
                try{
                    dialog.RequestReceived -= callback;
                }
                catch{
                    // We don't care about errors here.
                }
            };
            timer.Enabled = true;                       
        }

        #endregion


        #region Properties implementation

        #endregion
    }
}
