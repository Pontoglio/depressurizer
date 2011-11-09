﻿using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Rallion {

    delegate DialogResult DLG_MessageBox( string text );

    public partial class FatalError : Form {
        #region Constants
        /// <summary>
        /// The default height of the Info section
        /// </summary>
        private const int DEFAULT_INFO_HEIGHT = 250;
        private const int MIN_WIDTH = 300;
        private const int MAX_WIDTH = 2000;
        private const int MIN_HEIGHT = 300;
        private const int MAX_HEIGHT = 2000;
        #endregion

        #region Fields
        /// <summary>
        /// The exception being displayed
        /// </summary>
        private Exception ex;
        /// <summary>
        /// Stores whether or not the extra info is being shown
        /// </summary>
        private bool ShowingInfo;
        /// <summary>
        /// The current height of the info section.
        /// </summary>
        private int currentInfoHeight = DEFAULT_INFO_HEIGHT;
        #endregion

        #region Properties
        /// <summary>
        /// The minimum height of the form, without info showing.
        /// </summary>
        private int ShortHeight {
            get {
                return ( this.Height - this.ClientSize.Height ) + cmdClose.Bottom + 10;
            }
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Starts catching all unhandled exceptions for processing.
        /// </summary>
        public static void InitializeHandler() {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler( CurrentDomain_UnhandledException );
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler( Application_ThreadException );
        }

        private static void Application_ThreadException( object sender, System.Threading.ThreadExceptionEventArgs e ) {
            HandleUnhandledException( e.Exception );
        }

        private static void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e ) {
            HandleUnhandledException( (Exception)e.ExceptionObject );
        }

        /// <summary>
        /// Shows the form and ends the program after an exception makes it to the top level.
        /// </summary>
        /// <param name="e">The unhandled exception.</param>
        private static void HandleUnhandledException( Exception e ) {
            FatalError errForm = new FatalError( e );
            errForm.ShowDialog();
            Application.Exit();
        }
        #endregion

        #region Construction
        private FatalError( Exception e ) {
            InitializeComponent();
            ex = e;
            ShowingInfo = true;
            TopMost = true;
            TopLevel = true;
        }

        private void FatalError_Load( object sender, EventArgs e ) {
            HideInfo();

            string appName = Application.ProductName;

            this.Text = string.Format( "{0} - Fatal Error", appName );
            lblMessage.Text = string.Format( "A fatal error has occurred in {0}. The program will now terminate.", appName );

            FillFields();

            this.Activate();
        }

        private void FillFields() {
            txtErrType.Text = ex.GetType().Name;
            txtErrMsg.Text = ex.Message;
            txtTrace.Text = ex.StackTrace;
        }
        #endregion

        #region Info control
        /// <summary>
        /// Displays the extra info
        /// </summary>
        private void ShowInfo() {
            if( ShowingInfo ) return;

            // Increase the form height and allow resizing
            this.MinimumSize = new Size( MIN_WIDTH, MIN_HEIGHT );
            this.MaximumSize = new Size( MAX_WIDTH, MAX_HEIGHT );
            Height = ShortHeight + currentInfoHeight;
            // Show extra components
            grpMoreInfo.Visible = grpMoreInfo.Enabled = ShowingInfo = true;
        }

        /// <summary>
        /// Hides the extra info
        /// </summary>
        private void HideInfo() {
            if( !ShowingInfo ) return;
            // Save the current info height in case we toggle back
            currentInfoHeight = Height - ShortHeight;
            // Resize and disable user resizing
            int newHeight = ShortHeight;
            MinimumSize = new Size( MIN_WIDTH, newHeight );
            MaximumSize = new Size( MAX_WIDTH, newHeight );
            Height = newHeight;
            // Hide extra components
            grpMoreInfo.Visible = grpMoreInfo.Enabled = ShowingInfo = false;
        }

        /// <summary>
        /// Toggles the extra info
        /// </summary>
        private void ToggleInfo() {
            if( ShowingInfo ) HideInfo();
            else ShowInfo();
        }
        #endregion

        #region Event Handlers
        private void cmdShow_Click( object sender, EventArgs e ) {
            ToggleInfo();
        }

        private void cmdSave_Click( object sender, EventArgs e ) {
            SaveToFile();
        }

        private void cmdCopy_Click( object sender, EventArgs e ) {
            SetClipboardText();
        }
        #endregion

        #region Saving methods
        /// <summary>
        /// Saves the exception data to a file in the application directory
        /// </summary>
        private void SaveToFile() {
            try {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.CreatePrompt = false;
                dlg.AddExtension = false;
                dlg.AutoUpgradeEnabled = true;
                dlg.InitialDirectory = Environment.CurrentDirectory;
                dlg.FileName = "dError_" + DateTime.Now.ToString( "yyyyMMddhhmmss" ) + ".log";
                DialogResult res = dlg.ShowDialog();
                if( res == System.Windows.Forms.DialogResult.OK ) {
                    StreamWriter fstr = new StreamWriter( dlg.FileName );
                    string data = string.Format( "{0}: {1}{2}{3}", ex.GetType().Name, ex.Message, Environment.NewLine, ex.StackTrace );
                    fstr.Write( data );
                    fstr.Close();
                    MessageBox.Show( "Error information saved." );
                }
            } catch {
                MessageBox.Show( "Could not save error information." );
            }
        }

        /// <summary>
        /// Copies the exception data to the clipboard
        /// </summary>
        private void SetClipboardText() {
            if( Thread.CurrentThread.GetApartmentState() != ApartmentState.STA ) {
                Thread t = new Thread( SetClipboardText );
                t.SetApartmentState( ApartmentState.STA );
                t.Start();
            } else {
                string dMsg = "Could not copy to clipboard.";
                try {
                    string data = string.Format( "{0}: {1}{2}{3}", ex.GetType().Name, ex.Message, Environment.NewLine, ex.StackTrace );
                    Clipboard.SetText( data );
                    dMsg = "Clipboard updated.";

                } finally {
                    if( this.InvokeRequired ) {
                        this.Invoke( new DLG_MessageBox( MessageBox.Show ), new object[] { dMsg } );
                    } else {
                        MessageBox.Show( dMsg );
                    }
                }
            }
        }
        #endregion
    }
}
