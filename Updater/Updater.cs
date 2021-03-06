﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Runtime.InteropServices;

namespace Updater
{
    public partial class Updater : Form
    {
        private const string baseRemotePath = "http://dl.dropbox.com/u/84527004/SAI-Editor/";
        private const string baseRemoteDownloadPath = "http://dl.dropbox.com/u/84527004/SAI-Editor/SAI-Editor/";
        private readonly bool startSaiEditorOnClose = false;
        readonly List<string> _files = new List<string>();

        public Updater()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();
            startSaiEditorOnClose = args.Length > 1 && args[1] == "RanFromSaiEditor";
        }

        //! Start the initial search after a second (button is disabled in the meantime) so the form
        //! is able to finish loading visually for the user.
        private void timerStartSearchingOnLaunch_Tick(object sender, EventArgs e)
        {
            timerStartSearchingOnLaunch.Enabled = false;
            CheckForUpdates();
        }

        private void buttonCheckForUpdates_Click(object sender, EventArgs e)
        {
            buttonCheckForUpdates.Enabled = false;
            buttonCheckForUpdates.Update();
            buttonUpdateToLatest.Enabled = false;
            buttonUpdateToLatest.Update();
            listBoxFilesToUpdate.DataSource = null;
            listBoxFilesToUpdate.Update();
            _files.Clear();
            textBoxChangelog.Text = String.Empty;

            CheckForUpdates();
            buttonCheckForUpdates.Enabled = true;
            buttonUpdateToLatest.Enabled = _files.Count > 0;
        }

        private void CheckForUpdates()
        {
            timerCheckForSaiEditorRunning.Enabled = false;
            while (CheckIfSaiEditorRunning()) ;
            timerCheckForSaiEditorRunning.Enabled = true;

            try
            {
                statusLabel.Text = "CHECKING FOR UPDATES...";
                statusLabel.Update();

                using (WebClient client = new WebClient())
                {
                    try
                    {
                        using (Stream streamFileList = client.OpenRead(baseRemotePath + "filelist.txt"))
                        {
                            using (StreamReader streamReaderFileList = new StreamReader(streamFileList))
                            {
                                string currentLine = String.Empty;

                                while ((currentLine = streamReaderFileList.ReadLine()) != null)
                                {
                                    string[] splitLine = currentLine.Split(',');
                                    string filename = splitLine[0];
                                    string md5 = splitLine[1];

                                    if (File.Exists(Directory.GetCurrentDirectory().ToString(CultureInfo.InvariantCulture) + @"\" + filename))
                                    {
                                        string currentMd5 = GetMD5HashFromFile(Directory.GetCurrentDirectory() + @"\" + filename);

                                        if (!String.IsNullOrWhiteSpace(currentMd5) && md5 != currentMd5)
                                            _files.Add(filename);
                                    }
                                    else
                                        _files.Add(filename);
                                }

                                listBoxFilesToUpdate.DataSource = _files;
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        textBoxChangelog.Text = String.Empty;
                        statusLabel.Text = "COULD NOT CONNECT TO WEBSERVER";
                        statusLabel.ForeColor = Color.Red;
                        statusLabel.Update();
                        buttonUpdateToLatest.Enabled = false;
                        buttonCheckForUpdates.Enabled = true;
                        MessageBox.Show("It seems like the application was unable to connect to the internet and therefore there are no updates found. The following error was thrown:\n\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    catch (Exception exe)
                    {
                        MessageBox.Show("Unable to load filelist. Error: \r\n\r\n" + exe.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        textBoxChangelog.Text = "Unable to load filelist";
                        statusLabel.Text = "UNABLE TO LOAD";
                        statusLabel.ForeColor = Color.Red;
                        return;
                    }

                    if (_files.Count > 0)
                    {
                        try
                        {
                            using (Stream streamChangelog = client.OpenRead(baseRemotePath + "changelog.txt"))
                                if (streamChangelog != null)
                                    using (StreamReader streamReaderChangelog = new StreamReader(streamChangelog))
                                        textBoxChangelog.Text = streamReaderChangelog.ReadToEnd();
                        }
                        catch (WebException ex)
                        {
                            textBoxChangelog.Text = String.Empty;
                            statusLabel.Text = "COULD NOT CONNECT TO WEBSERVER";
                            statusLabel.ForeColor = Color.Red;
                            statusLabel.Update();
                            buttonUpdateToLatest.Enabled = false;
                            buttonCheckForUpdates.Enabled = true;
                            MessageBox.Show("It seems like the application was unable to connect to the internet and therefore there are no updates found. The following error was thrown:\n\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Unable to load changelog. Error: \r\n\r\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            textBoxChangelog.Text = String.Empty;
                            statusLabel.Text = "UNABLE TO LOAD";
                            statusLabel.ForeColor = Color.Red;
                        }
                    }
                }

                statusLabel.Text = _files.Count > 0 ? "UPDATES AVAILABLE" : "ALREADY UP TO DATE";
                progressBar.Value = 0;
                buttonUpdateToLatest.Enabled = _files.Count > 0;
                buttonCheckForUpdates.Enabled = true;
            }
            catch (WebException ex)
            {
                textBoxChangelog.Text = String.Empty;
                statusLabel.Text = "COULD NOT CONNECT TO WEBSERVER";
                statusLabel.ForeColor = Color.Red;
                statusLabel.Update();
                buttonUpdateToLatest.Enabled = false;
                buttonCheckForUpdates.Enabled = true;
                MessageBox.Show("It seems like the application was unable to connect to the internet and therefore there are no updates found. The following error was thrown:\n\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong while checking for updates. Please report the following message to developers:\n\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool HasInternetConnectionWithCurrentNetwork()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(x => x.OperationalStatus == OperationalStatus.Up);
        }

        private void buttonUpdateToLatest_Click(object sender, EventArgs e)
        {
            progressBar.Value = 0;
            progressBar.Maximum = _files.Count;
            buttonUpdateToLatest.Enabled = false;
            buttonUpdateToLatest.Update();

            buttonCheckForUpdates.Enabled = false;
            buttonCheckForUpdates.Update();

            bool webExceptionOccurred = false;

            for (int i = 0; i < _files.Count; ++i)
            {
                string file = _files[i];
                progressBar.Value++;

                if (file.Contains("filelist.txt") || file.Contains("changelog.txt"))
                    continue;

                string[] subfolder = file.Split(Char.Parse(@"\"));
                string currentFolder = Directory.GetCurrentDirectory();

                foreach (string folder in subfolder)
                {
                    if (Path.HasExtension(folder))
                        continue;

                    if (!Directory.Exists(Directory.GetCurrentDirectory().ToString(CultureInfo.InvariantCulture) + @"\" + folder))
                        Directory.CreateDirectory(currentFolder + @"\" + folder);

                    currentFolder = currentFolder + @"\" + folder;
                }

                string remotefile = baseRemoteDownloadPath + file;
                string destfile = Directory.GetCurrentDirectory() + @"\" + file;

                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(remotefile, destfile);
                    }
                }
                catch (WebException ex)
                {
                    textBoxChangelog.Text = String.Empty;
                    statusLabel.Text = "COULD NOT CONNECT TO WEBSERVER";
                    statusLabel.ForeColor = Color.Red;
                    statusLabel.Update();
                    buttonUpdateToLatest.Enabled = false;
                    buttonCheckForUpdates.Enabled = true;
                    webExceptionOccurred = true;
                    MessageBox.Show("It seems like the application was unable to connect to the internet and therefore there are no updates found. The following error was thrown:\n\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Something went wrong while attempting to keep track of the use count. Please report the following message to developers:\n\n" + ex.Message, "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            progressBar.Value = 0;
            listBoxFilesToUpdate.DataSource = null;
            listBoxFilesToUpdate.Items.Clear();
            _files.Clear();
            buttonCheckForUpdates.Enabled = true;

            if (!webExceptionOccurred)
                MessageBox.Show("Updated completed succesfully.\r\nYou can now run the latest version of SAI-Editor. Closing the updater will automatically open the SAI-Editor.", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        protected string GetMD5HashFromFile(string fileName)
        {
            byte[] retVal = null;

            try
            {
                using (FileStream file = new FileStream(fileName, FileMode.Open))
                {
                    MD5 md5 = new MD5CryptoServiceProvider();
                    retVal = md5.ComputeHash(file);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = "The MD5 hash of the following file could not be read as reading the file threw an exception:\n\n" + fileName + "\n\nError message:\n" + ex.Message;
                MessageBox.Show(errorMessage, "Someting went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (retVal == null)
                return String.Empty;

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < retVal.Length; i++)
                sb.Append(retVal[i].ToString("x2"));

            return sb.ToString();
        }

        private void changelog_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true; //! Don't allow writing to the changelog rich textbox
        }

        [DllImportAttribute("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImportAttribute("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImportAttribute("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void ShowToFront(string windowName)
        {
            IntPtr firstInstance = FindWindow(null, windowName);
            ShowWindow(firstInstance, 1);
            SetForegroundWindow(firstInstance);
        }

        private void Updater_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!startSaiEditorOnClose)
                return;

            try
            {
                Process.Start(Directory.GetCurrentDirectory() + "\\SAI-Editor.exe");
                ShowToFront(Directory.GetCurrentDirectory() + "\\SAI-Editor.exe");
            }
            catch (Exception)
            {
                MessageBox.Show("The SAI-Editor could not be re-opened. Please do so manually.", "Something went wrong!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void timerCheckForSaiEditorRunning_Tick(object sender, EventArgs e)
        {
            //! Check if there's a SAI-Editor running at the moment
            CheckIfSaiEditorRunning();
        }

        private bool CheckIfSaiEditorRunning()
        {
            if (Process.GetProcessesByName("SAI-Editor").Length > 0)
            {
                timerCheckForSaiEditorRunning.Enabled = false;
                MessageBox.Show("There is an instance of SAI-Editor running at the moment. In order for the updater to work, this may not be the case. Please close it before continuing.", "SAI-Editor is running!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                timerCheckForSaiEditorRunning.Enabled = true;
                return true;
            }

            return false;
        }
    }
}
