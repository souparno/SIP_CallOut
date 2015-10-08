using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

using LumiSoft.SIP.UA.Resources;

using LumiSoft.Net.SIP;
using LumiSoft.Net.SIP.Stack;

namespace LumiSoft.SIP.UA
{
    /// <summary>
    /// Incoming call window.
    /// </summary>
    public class wfrm_IncomingCall : Form
    {
        private Label  m_pFrom   = null;
        private Button m_pAccpet = null;
        private Button m_pReject = null;

        private SIP_ServerTransaction m_pTransaction = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="invite">SIP INVITE server transaction.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>invite</b> is null reference.</exception>
        public wfrm_IncomingCall(SIP_ServerTransaction invite)
        {
            if(invite == null){
                throw new ArgumentNullException("invite");
            }
                        
            InitUI();

            m_pTransaction = invite;
            m_pTransaction.Canceled += new EventHandler(m_pTransaction_Canceled);

            m_pFrom.Text = invite.Request.To.Address.ToStringValue();
        }
                
        #region method InitUI

        /// <summary>
        /// Creates and initializes UI.
        /// </summary>
        private void InitUI()
        {
            this.ClientSize = new Size(250,100);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.Text = "Incoming Call:";

            m_pFrom = new Label();
            m_pFrom.Size = new Size(250,20);
            m_pFrom.Location = new Point(0,15);
            m_pFrom.TextAlign = ContentAlignment.MiddleCenter;
            m_pFrom.ForeColor = Color.Gray;
            m_pFrom.Font = new Font(m_pFrom.Font.FontFamily,8,FontStyle.Bold);

            m_pAccpet = new Button();
            m_pAccpet.Size = new Size(45,45);
            m_pAccpet.Location = new Point(10,40);
            m_pAccpet.Image = ResManager.GetIcon("call.ico",new Size(24,24)).ToBitmap();
            m_pAccpet.Click += new EventHandler(m_pAccpet_Click);

            m_pReject = new Button();            
            m_pReject.Size = new Size(45,45);
            m_pReject.Location = new Point(195,40);
            m_pReject.Image = ResManager.GetIcon("call_hangup.ico",new Size(24,24)).ToBitmap();
            m_pReject.Click += new EventHandler(m_pReject_Click);

            this.Controls.Add(m_pFrom);
            this.Controls.Add(m_pAccpet);
            this.Controls.Add(m_pReject);
        }
                                
        #endregion


        #region Event handlign

        #region method m_pAccpet_Click

        /// <summary>
        /// Is called when accpet button has clicked.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pAccpet_Click(object sender,EventArgs e)
        {
            this.DialogResult = DialogResult.Yes;
        }

        #endregion

        #region method m_pReject_Click

        /// <summary>
        /// Is called when reject button has clicked.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pReject_Click(object sender,EventArgs e)
        {
            m_pTransaction.SendResponse(m_pTransaction.Stack.CreateResponse(SIP_ResponseCodes.x600_Busy_Everywhere,m_pTransaction.Request));

            this.DialogResult = DialogResult.No;
        }

        #endregion


        #region method m_pTransaction_Canceled

        /// <summary>
        /// Is called when transcation has canceled.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pTransaction_Canceled(object sender,EventArgs e)
        {
            // We need invoke here, we are running on thread pool thread.
            this.BeginInvoke(new MethodInvoker(delegate(){
                this.DialogResult = DialogResult.No;
            }));
        }

        #endregion

        #endregion

    }
}
