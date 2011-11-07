﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;

namespace Depressurizer {
    public partial class AutoLoadDlg : Form {
        const string PATH_HELP = "This is the path to your Steam installation.\nThe Program will try to set this automatically.\nIf it does not, type in the correct path, or click the Browse button.";
        const string ID_HELP = "This is your numeric Steam user ID.\nThe program will try to automatically fill the box with found options.\nIf you change the Steam path, click Refresh to reload the list.";
        const string PROF_HELP = "This is your Steam profile ID.\nIn order for the program to get your information,\nyou must be connected to the internet, and your profile must not be private.";
        public AutoLoadDlg() {
            InitializeComponent();
            toolTip.SetToolTip( lnkHelpPath, PATH_HELP );
            toolTip.SetToolTip( lnkHelpId, ID_HELP );
            toolTip.SetToolTip( lnkHelpProfile, PROF_HELP );
        }

        private void AutoLoadDlg_Load( object sender, EventArgs e ) {
            txtSteamPath.Text = GetSteamPath();

            RefreshIdList();

            txtProfileName.Focus();
        }

        private void RefreshIdList() {
            combUserId.BeginUpdate();
            combUserId.Items.Clear();
            combUserId.ResetText();
            combUserId.Items.AddRange( GetSteamIds() );
            if( combUserId.Items.Count > 0 ) {
                combUserId.SelectedIndex = 0;
            }
            combUserId.EndUpdate();
        }

        private string GetSteamPath() {
            string s = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "steamPath", "" ) as string;
            if( s == null ) s = "";
            s = s.Replace('/','\\');
            return s;
        }

        private void cmdBrowse_Click( object sender, EventArgs e ) {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            DialogResult res = dlg.ShowDialog();
            if( res == System.Windows.Forms.DialogResult.OK ) {
                txtSteamPath.Text = dlg.SelectedPath;
                RefreshIdList();
            }
        }

        private string[] GetSteamIds() {
            DirectoryInfo dir = new DirectoryInfo( txtSteamPath.Text + "\\userdata");
            if( dir.Exists ) {
                DirectoryInfo[] userDirs = dir.GetDirectories();
                string[] result = new string[userDirs.Length];
                for( int i = 0; i < userDirs.Length; i++ ) {
                    result[i] = userDirs[i].Name;
                }
                return result;
            }
            return new string[0];
        }

        private void cmdRefreshIdList_Click( object sender, EventArgs e ) {
            RefreshIdList();
        }
    }
}
