﻿/*
Autowikibrowser
Copyright (C) 2006 Martin Richards
(C) 2007 Stephen Kennedy (Kingboyk) http://www.sdk-software.com/

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;
using System.Web;
using System.Xml;
using System.Reflection;
using System.Diagnostics;
using WikiFunctions;
using WikiFunctions.Plugin;
using WikiFunctions.Parse;
using WikiFunctions.Lists;
using WikiFunctions.Logging;
using WikiFunctions.Browser;
using WikiFunctions.Controls;
using System.Collections.Specialized;
using WikiFunctions.Background;
using System.Security.Permissions;

[assembly: CLSCompliant(true)]
namespace AutoWikiBrowser
{
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisibleAttribute(true)]
    public partial class MainForm : Form, IAutoWikiBrowser
    {
        #region constructor etc.

        public MainForm()
        {
            InitializeComponent();

            try
            {
                lblUserName.Alignment = ToolStripItemAlignment.Right;
                lblProject.Alignment = ToolStripItemAlignment.Right;
                lblTimer.Alignment = ToolStripItemAlignment.Right;
                lblEditsPerMin.Alignment = ToolStripItemAlignment.Right;
                lblIgnoredArticles.Alignment = ToolStripItemAlignment.Right;
                lblEditCount.Alignment = ToolStripItemAlignment.Right;

                btntsShowHide.Image = Resources.btnshowhide_image;
                btntsShowHideParameters.Image = Resources.btnshowhideparameters_image;
                btntsSave.Image = Resources.btntssave_image;
                btntsIgnore.Image = Resources.GoLtr;
                btntsStop.Image = Resources.Stop;
                btntsPreview.Image = Resources.preview;
                btntsChanges.Image = Resources.changes;
                btntsFalsePositive.Image = Resources.RolledBack;
                btntsStart.Image = Resources.Run;

                //btnSave.Image = Resources.btntssave_image;
                //btnIgnore.Image = Resources.GoLtr;

                //btnDiff.Image = Resources.changes;
                //btnPreview.Image = Resources.preview;

                int stubcount = 500;
                bool catkey = false;
                try
                {
                    stubcount = AutoWikiBrowser.Properties.Settings.Default.StubMaxWordCount;
                    catkey = AutoWikiBrowser.Properties.Settings.Default.AddHummanKeyToCats;
                    parsers = new Parsers(stubcount, catkey);
                }
                catch (Exception ex)
                {
                    parsers = new Parsers();
                    MessageBox.Show(ex.Message);
                }

                toolStripComboOnLoad.SelectedIndex = 0;
                cmboCategorise.SelectedIndex = 0;
                cmboImages.SelectedIndex = 0;
                lblStatusText.AutoSize = true;
                lblBotTimer.AutoSize = true;

                Variables.User.UserNameChanged += UpdateUserName;
                Variables.User.BotStatusChanged += UpdateBotStatus;
                Variables.User.AdminStatusChanged += UpdateAdminStatus;

                Variables.User.webBrowserLogin.DocumentCompleted += web4Completed;
                Variables.User.webBrowserLogin.Navigating += web4Starting;

                webBrowserEdit.Loaded += CaseWasLoad;
                webBrowserEdit.Diffed += CaseWasDiff;
                webBrowserEdit.Saved += CaseWasSaved;
                webBrowserEdit.None += CaseWasNull;
                webBrowserEdit.Fault += StartDelayedRestartTimer;
                webBrowserEdit.StatusChanged += UpdateWebBrowserStatus;

                listMaker1.BusyStateChanged += SetProgressBar;
                listMaker1.NoOfArticlesChanged += UpdateButtons;
                listMaker1.StatusTextChanged += UpdateListStatus;
                Text = "AutoWikiBrowser - Default.xml";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        bool Abort = false;

        string LastArticle = "";
        string SettingsFile = "";
        string LastMove = "";
        string LastDelete = "";

        int oldselection = 0;
        int retries = 0;

        bool PageReload = false;

        int mnudges = 0;
        int sameArticleNudges = 0;

        bool boolSaved = true;
        HideText RemoveText = new HideText(false, true, false);
        List<string> noParse = new List<string>();
        FindandReplace findAndReplace = new FindandReplace();
        SubstTemplates substTemplates = new SubstTemplates();
        RegExTypoFix RegexTypos;
        SkipOptions Skip = new SkipOptions();
        WikiFunctions.MWB.ReplaceSpecial replaceSpecial = new WikiFunctions.MWB.ReplaceSpecial();
        Parsers parsers;
        TimeSpan StartTime = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        StringCollection RecentList = new StringCollection();
        CustomModule cModule = new CustomModule();
        public RegexTester regexTester = new RegexTester();
        bool userTalkWarningsLoaded = false;
        Regex userTalkTemplatesRegex;

        private void MainForm_Load(object sender, EventArgs e)
        {
            lblStatusText.Text = "Initialising...";
            Application.DoEvents();
            updateUpdater();

            try
            {
                //MessageBox.Show(WikiDiff.WikiDiffVersion.FileVersion);
                //check that we are not using an old OS. 98 seems to mangled some unicode
                if (Environment.OSVersion.Version.Major < 5)
                {
                    MessageBox.Show("You appear to be using an older operating system, this software may have trouble with some unicode fonts on operating systems older than Windows 2000, the start button has been disabled.", "Operating system", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    SetStartButton(false);
                }
                else
                    listMaker1.MakeListEnabled = true;

                if (AutoWikiBrowser.Properties.Settings.Default.LogInOnStart)
                    CheckStatus();

                LogControl1.Initialise(listMaker1);

                if (Properties.Settings.Default.WindowLocation != null)
                    this.Location = Properties.Settings.Default.WindowLocation;

                if (Properties.Settings.Default.WindowSize != null)
                    this.Size = Properties.Settings.Default.WindowSize;

                Debug();
                LoadPlugins();
                LoadPrefs();
                UpdateButtons();
                LoadRecentSettingsList();

                if (Variables.User.checkEnabled() == WikiStatusResult.OldVersion)
                    oldVersion();

                webBrowserDiff.Navigate("about:blank");
                webBrowserDiff.ObjectForScripting = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            lblStatusText.Text = "";
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if ((Minimize) && (this.WindowState == FormWindowState.Minimized))
                this.Visible = false;
        }

        #endregion

        #region Properties

        Article stredittingarticle = new Article("");
        public Article TheArticle
        {
            get { return stredittingarticle; }
            private set { stredittingarticle = value; }
        }

        private bool BotMode
        {
            get { return chkAutoMode.Checked; }
            set { chkAutoMode.Checked = value; }
        }

        int intEdits = 0;
        private int NumberOfEdits
        {
            get { return intEdits; }
            set
            {
                intEdits = value;
                lblEditCount.Text = "Edits: " + value.ToString();
            }
        }

        int intIgnoredEdits = 0;
        private int NumberOfIgnoredEdits
        {
            get { return intIgnoredEdits; }
            set
            {
                intIgnoredEdits = value;
                lblIgnoredArticles.Text = "Ignored: " + value.ToString();
            }
        }

        int intEditsPerMin = 0;
        private int NumberOfEditsPerMinute
        {
            get { return intEditsPerMin; }
            set
            {
                intEditsPerMin = value;
                lblEditsPerMin.Text = "Edits/min: " + value.ToString();
            }
        }

        bool bLowThreadPriority = false;
        private bool LowThreadPriority
        {
            get { return bLowThreadPriority; }
            set
            {
                bLowThreadPriority = value;
                if (value)
                    Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                else
                    Thread.CurrentThread.Priority = ThreadPriority.Normal;
            }
        }

        private bool FlashAndBeep
        {
            set { bFlash = value; bBeep = value; }
        }

        bool bFlash = false;
        private bool Flash
        {
            get { return bFlash; }
            set { bFlash = value; }
        }

        bool bBeep = false;
        private bool Beep
        {
            get { return bBeep; }
            set { bBeep = value; }
        }

        bool bMinimize = false;
        private bool Minimize
        {
            get { return bMinimize; }
            set { bMinimize = value; }
        }

        decimal dTimeOut = 30;
        private decimal TimeOut
        {
            get { return dTimeOut; }
            set
            {
                dTimeOut = value;
                webBrowserEdit.TimeoutLimit = int.Parse(value.ToString());
            }
        }

        bool bSaveArticleList = false;
        private bool SaveArticleList
        {
            get { return bSaveArticleList; }
            set { bSaveArticleList = value; }
        }

        bool bOverrideWatchlist = false;
        private bool OverrideWatchlist
        {
            get { return bOverrideWatchlist; }
            set { bOverrideWatchlist = value; }
        }

        bool bAutoSaveEdit = false;
        private bool AutoSaveEditBoxEnabled
        {
            get { return bAutoSaveEdit; }
            set { bAutoSaveEdit = value; }
        }

        string sAutoSaveEditFile = "Edit Box.txt";
        private string AutoSaveEditBoxFile
        {
            get { return sAutoSaveEditFile; }
            set { sAutoSaveEditFile = value; }
        }

        decimal dAutoSaveEditPeriod = 60;
        private decimal AutoSaveEditBoxPeriod
        {
            get { return dAutoSaveEditPeriod; }
            set { dAutoSaveEditPeriod = value; EditBoxSaveTimer.Interval = int.Parse((value * 1000).ToString()); }
        }

        #endregion

        #region MainProcess

        private void Start()
        {
            try
            {
                Tools.WriteDebug(this.Name, "Starting");

                //check edit summary
                webBrowserEdit.BringToFront();
                if (cmboEditSummary.Text == "" && AWBPlugins.Count == 0)
                    MessageBox.Show("Please enter an edit summary.", "Edit summary", MessageBoxButtons.OK, 
                        MessageBoxIcon.Exclamation);

                StopDelayedRestartTimer();
                DisableButtons();
                TheArticle.EditSummary = "";
                skippable = true;
                txtEdit.Clear();

                if (webBrowserEdit.IsBusy)
                    webBrowserEdit.Stop();

                if (webBrowserEdit.Document != null)
                    webBrowserEdit.Document.Write("");

                //check we are logged in
                if (!Variables.User.WikiStatus && !CheckStatus())
                    return;

                ArticleInfo(true);

                if (listMaker1.NumberOfArticles < 1)
                {
                    webBrowserEdit.Busy = false;
                    stopSaveInterval();
                    lblTimer.Text = "";
                    lblStatusText.Text = "No articles in list, you need to use the Make list";
                    this.Text = "AutoWikiBrowser";
                    webBrowserEdit.Document.Write("");
                    listMaker1.MakeListEnabled = true;
                    return;
                }
                else
                    webBrowserEdit.Busy = true;

                TheArticle = listMaker1.SelectedArticle();
                TheArticle.InitialiseLogListener();

                if (!Tools.IsValidTitle(TheArticle.Name))
                {
                    SkipPage("Invalid page title");
                    return;
                }
                if (BotMode)
                    NudgeTimer.StartMe();

                EditBoxSaveTimer.Enabled = AutoSaveEditBoxEnabled;

                //Navigate to edit page
                webBrowserEdit.LoadEditPage(TheArticle.Name);
            }
            catch (Exception ex)
            {
                Tools.WriteDebug(this.Name, "Start() error: " + ex.Message);
                StartDelayedRestartTimer();
            }
        }

        private void CaseWasLoad()
        {
            if (!loadSuccess())
                return;

            string strTemp = webBrowserEdit.GetArticleText();

            this.Text = "AutoWikiBrowser" + SettingsFile + " - " + TheArticle.Name;

            //check for redirect
            if (bypassRedirectsToolStripMenuItem.Checked && Tools.IsRedirect(strTemp) && !PageReload)
            {
                Article Redirect = new Article(Tools.RedirectTarget(strTemp));

                if (Redirect.Name == TheArticle.Name)
                {//ignore recursive redirects
                    SkipPage("Recursive redirect");
                    return;
                }

                listMaker1.ReplaceArticle(TheArticle, Redirect);
                TheArticle = Redirect;
                TheArticle.InitialiseLogListener();

                webBrowserEdit.LoadEditPage(Redirect.Name);
                return;
            }
            TheArticle.OriginalArticleText = strTemp;

            if (PageReload)
            {
                PageReload = false;
                GetDiff();
                return;
            }

            //check not in use
            if (TheArticle.IsInUse() && !BotMode)
                MessageBox.Show("This page has the \"Inuse\" tag, consider skipping it");

            if (chkSkipIfContains.Checked && TheArticle.SkipIfContains(txtSkipIfContains.Text, 
                chkSkipIsRegex.Checked, chkSkipCaseSensitive.Checked, true))
            {
                SkipPage("Article contains: " + txtSkipIfContains.Text);
                return;
            }

            if (chkSkipIfNotContains.Checked && TheArticle.SkipIfContains(txtSkipIfNotContains.Text,
                chkSkipIsRegex.Checked, chkSkipCaseSensitive.Checked, false))
            {
                SkipPage("Article does not contain: " + txtSkipIfNotContains.Text);
                return;
            }

            if (!Skip.skipIf(TheArticle.OriginalArticleText))
            {                
                SkipPage("skipIf custom code"); 
                return;
            }

            if (!doNotAutomaticallyDoAnythingToolStripMenuItem.Checked)
            {
                ProcessPage();

                if (!Abort && skippable && chkSkipNoChanges.Checked && 
                    TheArticle.ArticleText == TheArticle.OriginalArticleText)
                {
                    SkipPage("No changes made");
                    return;
                }
                else if (!Abort && TheArticle.SkipArticle)
                {
                    SkipPageReasonAlreadyProvided(); // Don't send a reason; ProcessPage() should already have logged one
                    return;
                }
            }

            webBrowserEdit.SetArticleText(TheArticle.ArticleText);
            TheArticle.SaveSummary();
            txtEdit.Text = TheArticle.ArticleText;

            //Update statistics and alerts
            ArticleInfo(false);

            if (!Abort)
            {
                if (BotMode && chkQuickSave.Checked)
                    startDelayedAutoSaveTimer();
                else if (toolStripComboOnLoad.SelectedIndex == 0)
                    GetDiff();
                else if (toolStripComboOnLoad.SelectedIndex == 1)
                    GetPreview();
                else if (toolStripComboOnLoad.SelectedIndex == 2)
                {
                    if (BotMode)
                    {
                        startDelayedAutoSaveTimer();
                        return;
                    }

                    bleepflash();

                    this.Focus();
                    txtEdit.Focus();
                    txtEdit.SelectionLength = 0;

                    EnableButtons();
                }
            }
            else
            {
                EnableButtons();
                Abort = false;
            }
        }

        private void bleepflash()
        {
            if (!this.ContainsFocus && (Beep && Flash))
            {
                Tools.FlashWindow(this);
                Tools.Beep1();
            }
            else if (!this.ContainsFocus && Flash)
                Tools.FlashWindow(this);
            else if (!this.ContainsFocus && Beep)
                Tools.Beep1();
        }

        private bool loadSuccess()
        {
            try
            {
                string HTML = webBrowserEdit.Document.Body.InnerHtml;
                if (HTML.Contains("The Wikipedia database is temporarily in read-only mode for the following reason"))
                {//http://en.wikipedia.org/wiki/MediaWiki:Readonlytext

                    if (retries < 10)
                    {
                        StartDelayedRestartTimer();
                        retries++;
                        Start();
                        return false;
                    }
                    else
                    {
                        retries = 0;
                        SkipPage("Database is locked, tried 10 times");
                        return false;
                    }
                }
                if (HTML.Contains("readOnly"))
                {
                    if (Variables.User.IsAdmin)
                        return true;
                    else
                    {
                        NudgeTimer.Stop();
                        SkipPage("Page is protected");
                        return false;
                    }
                }
                //check we are still logged in
                try
                {
                    if (!webBrowserEdit.GetLogInStatus())
                    {
                        Variables.User.LoggedIn = false;
                        NudgeTimer.Stop();
                        Start();
                        return false;
                    }
                }
                catch
                {
                    // No point writing to log listener I think, as it gets destroyed when we Stop?
                    Stop();
                    Start();
                    return false;
                }

                if (webBrowserEdit.NewMessage)
                {//check if we have any messages
                    NudgeTimer.Stop();
                    Variables.User.WikiStatus = false;
                    UpdateButtons();
                    webBrowserEdit.Document.Write("");
                    this.Focus();

                    dlgTalk DlgTalk = new dlgTalk();
                    if (DlgTalk.ShowDialog() == DialogResult.Yes)
                        Tools.OpenUserTalkInBrowser();
                    else
                        System.Diagnostics.Process.Start("IExplore", Variables.GetUserTalkURL());

                    DlgTalk = null;
                    return false;
                }
                if (!webBrowserEdit.HasArticleTextBox)
                {
                    if (!BotMode)
                    {
                        MessageBox.Show("There was a problem loading the page. Re-start the process", "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    lblStatusText.Text = "There was a problem loading the page. Re-starting.";
                    StartDelayedRestartTimer();
                    return false;
                }
                if (webBrowserEdit.Document.GetElementById("wpTextbox1").InnerText == null && chkSkipNonExistent.Checked)
                {//check if it is a non-existent page, if so then skip it automatically.
                    SkipPage("Non-existent page");
                    return false;
                }
                if (webBrowserEdit.Document.GetElementById("wpTextbox1").InnerText != null && chkSkipExistent.Checked)
                {
                    SkipPage("Existing page");
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            NudgeTimer.Reset();
            return true;
        }

        bool skippable = true;
        private void CaseWasDiff()
        {
            //if (diffChecker(webBrowserEdit.Document.Body.InnerHtml))
            //{//check if there are no changes and we want to skip
            //    SkipPage("No changes made");
            //    return;
            //}

            if (BotMode)
            {
                startDelayedAutoSaveTimer();
                return;
            }

            bleepflash();

            this.Focus();
            txtEdit.Focus();
            txtEdit.SelectionLength = 0;

            EnableButtons();
        }

        //private bool diffChecker(string strHTML)
        //{//check diff to see if it should be skipped

        //    if (!skippable || chkSkipNoChanges.Checked || toolStripComboOnLoad.SelectedIndex != 0 || doNotAutomaticallyDoAnythingToolStripMenuItem.Checked)
        //        return false;

        //    //if (!strHTML.Contains("class=diff-context") && !strHTML.Contains("class=diff-deletedline"))
        //    //    return true;

        //    strHTML = strHTML.Replace("<SPAN class=diffchange></SPAN>", "");
        //    strHTML = Regex.Match(strHTML, "<TD align=left colSpan=2.*?</DIV>", RegexOptions.Singleline).Value;

        //    //check for no changes, or no new lines (that have text on the new line)
        //    if (strHTML.Contains("<SPAN class=diffchange>") || Regex.IsMatch(strHTML, "class=diff-deletedline>[^<]") || Regex.IsMatch(strHTML, "<TD colSpan=2>&nbsp;</TD>\r\n<TD>\\+</TD>\r\n<TD class=diff-addedline>[^<]"))
        //        return false;

        //    return true;
        //}

        private void CaseWasSaved()
        {
            if (webBrowserEdit.Document.Body.InnerHtml.Contains("<H1 class=firstHeading>Edit conflict: "))
            {//if session data is lost, if data is lost then save after delay with tmrAutoSaveDelay
                MessageBox.Show("There has been an Edit Conflict. AWB will now re-apply its changes on the updated page. \n\r Please re-review the changes before saving. Any Custom edits will be lost, and have to be re-added manually.", "Edit Conflict");
                NudgeTimer.Stop();
                Start();
                return;
            }
            else if (!BotMode && webBrowserEdit.Document.Body.InnerHtml.Contains("<A class=extiw title=m:spam_blacklist href=\"http://meta.wikimedia.org/wiki/spam_blacklist\">"))
            {//check edit wasn't blocked due to spam filter
                if (MessageBox.Show("Edit has been blocked by spam blacklist. Try and edit again?", "Spam blacklist", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Start();
                    return;
                }
            }
            else if (webBrowserEdit.Document.Body.InnerHtml.Contains("<DIV CLASS=PREVIEWNOTE"))
            {//if session data is lost, if data is lost then save after delay with tmrAutoSaveDelay
                StartDelayedRestartTimer();
                return;
            }

            //lower restart delay
            if (intRestartDelay > 5)
                intRestartDelay -= 1;

            NumberOfEdits++;

            LastArticle = "";
            listMaker1.Remove(TheArticle);
            NudgeTimer.Stop();
            sameArticleNudges = 0;

            LogControl1.AddLog(false, TheArticle.LogListener);

            if (listMaker1.Count == 0)
                if (AutoSaveEditBoxEnabled)
                    EditBoxSaveTimer.Enabled = false;
            retries = 0;
            Start();
        }

        private void CaseWasNull()
        {
            if (webBrowserEdit.Document.Body.InnerHtml.Contains("<B>You have successfully signed in to Wikipedia as"))
            {
                lblStatusText.Text = "Signed in, now re-starting";

                if (!Variables.User.WikiStatus)
                    CheckStatus();
            }
        }

        private void SkipPageReasonAlreadyProvided()
        {
            try
            {
                //reset timer.
                NumberOfIgnoredEdits++;
                stopDelayedAutoSaveTimer();
                NudgeTimer.Stop();
                listMaker1.Remove(TheArticle);
                sameArticleNudges = 0;
                LogControl1.AddLog(true, TheArticle.LogListener);
                retries = 0;
                Start();
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }

        private void SkipPage(string reason)
        {
            switch (reason)
            {
                case "user":
                TheArticle.LogListener.UserSkipped();
                break;

                case "plugin":
                TheArticle.LogListener.PluginSkipped();
                break;

                case "": break;

                default:
                TheArticle.LogListener.AWBSkipped(reason);
                break;
            }

            SkipPageReasonAlreadyProvided();
        }

        private void SendPageToCustomModule()
        {
            ProcessArticleEventArgs ProcessArticleEventArgs = TheArticle;
            string strEditSummary = "", strTemp; bool SkipArticle;

            strTemp = cModule.Module.ProcessArticle(ProcessArticleEventArgs.ArticleText,
                ProcessArticleEventArgs.ArticleTitle, TheArticle.NameSpaceKey, out strEditSummary, out SkipArticle);

            if (!SkipArticle)
            {
                ProcessArticleEventArgs.EditSummary = strEditSummary;
                ProcessArticleEventArgs.Skip = false;
                TheArticle.AWBChangeArticleText("Custom module", strTemp, true);
                TheArticle.AppendPluginEditSummary();
            }
        }

        private void ProcessPage()
        {
            bool process = true;

            try
            {
                if (noParse.Contains(TheArticle.Name))
                    process = false;

                if (!ignoreNoBotsToolStripMenuItem.Checked &&
                    !Parsers.CheckNoBots(TheArticle.ArticleText, Variables.User.Name))
                {
                    TheArticle.AWBSkip("Bot Edits not Allowed");
                    return;
                }

                if (cModule.ModuleEnabled && cModule.Module != null)
                {
                    SendPageToCustomModule();
                    if (TheArticle.SkipArticle) return;
                }

                if (AWBPlugins.Count > 0)
                {
                    foreach (KeyValuePair<string, IAWBPlugin> a in AWBPlugins)
                    {
                        TheArticle.SendPageToPlugin(a.Value, this);
                        if (TheArticle.SkipArticle) return;
                    }
                }

                if (chkUnicodifyWhole.Checked && process)
                {
                    TheArticle.Unicodify(Skip.SkipNoUnicode, parsers);
                    if (TheArticle.SkipArticle) return;
                }

                if (cmboImages.SelectedIndex != 0)
                {
                    TheArticle.UpdateImages((WikiFunctions.Options.ImageReplaceOptions)cmboImages.SelectedIndex,
                        parsers, txtImageReplace.Text, txtImageWith.Text, chkSkipNoImgChange.Checked);
                    if (TheArticle.SkipArticle) return;
                }

                if (cmboCategorise.SelectedIndex != 0)
                {
                    TheArticle.Categorisation((WikiFunctions.Options.CategorisationOptions)
                        cmboCategorise.SelectedIndex, parsers, chkSkipNoCatChange.Checked, txtNewCategory.Text,
                        txtNewCategory2.Text);
                    if (TheArticle.SkipArticle) return;
                }

                if (chkFindandReplace.Checked && !findAndReplace.AfterOtherFixes)
                {
                    TheArticle.PerformFindAndReplace(findAndReplace, substTemplates, replaceSpecial,
                        chkSkipWhenNoFAR.Checked);
                    if (TheArticle.SkipArticle) return;
                }

                if (chkRegExTypo.Checked && RegexTypos != null && !BotMode && !Tools.IsTalkPage(TheArticle.NameSpaceKey))
                {
                    TheArticle.PerformTypoFixes(RegexTypos, chkSkipIfNoRegexTypo.Checked);
                    if (TheArticle.SkipArticle) return;
                }

                if (TheArticle.CanDoGeneralFixes)
                {
                    if (process && chkAutoTagger.Checked)
                    {
                        TheArticle.AutoTag(parsers, Skip.SkipNoTag);
                        if (TheArticle.SkipArticle) return;
                    }

                    if (process && chkGeneralFixes.Checked)
                    {
                        TheArticle.HideText(RemoveText);

                        TheArticle.FixHeaderErrors(parsers, Variables.LangCode, Skip.SkipNoHeaderError);

                        TheArticle.AWBChangeArticleText("Fix categories", parsers.FixCategories(TheArticle.ArticleText), true);
                        TheArticle.AWBChangeArticleText("Fix images", parsers.FixImages(TheArticle.ArticleText), true);
                        TheArticle.AWBChangeArticleText("Fix syntax", parsers.FixSyntax(TheArticle.ArticleText), true);
                        TheArticle.AWBChangeArticleText("Fix temperatures", parsers.FixTemperatures(TheArticle.ArticleText), true);

                        TheArticle.FixLinks(parsers, Skip.SkipNoBadLink);
                        TheArticle.BulletExternalLinks(parsers, Skip.SkipNoBulletedLink);

                        TheArticle.AWBChangeArticleText("Sort meta data",
                            parsers.SortMetaData(TheArticle.ArticleText, TheArticle.Name), true);

                        TheArticle.EmboldenTitles(parsers, Skip.SkipNoBoldTitle);

                        TheArticle.AWBChangeArticleText("Format sticky links",
                            parsers.StickyLinks(parsers.SimplifyLinks(TheArticle.ArticleText)), true);

                        TheArticle.UnHideText(RemoveText);
                    }
                }
                else if (process && chkGeneralFixes.Checked && TheArticle.NameSpaceKey == 3)
                {
                    TheArticle.HideText(RemoveText);

                    if (!userTalkWarningsLoaded)
                        loadUserTalkWarnings();

                    TheArticle.AWBChangeArticleText("Subst user talk warnings",
                        parsers.SubstUserTemplates(TheArticle.ArticleText, TheArticle.Name, userTalkTemplatesRegex), true);

                    TheArticle.UnHideText(RemoveText);
                }

                if (chkAppend.Checked)
                {
                    if (rdoAppend.Checked)
                        TheArticle.AWBChangeArticleText("Appended your message",
                            TheArticle.ArticleText + "\r\n\r\n" + txtAppendMessage.Text, false);
                    else
                        TheArticle.AWBChangeArticleText("Prepended your message",
                            txtAppendMessage.Text + "\r\n\r\n" + TheArticle.ArticleText, false);
                }

                if (chkFindandReplace.Checked && findAndReplace.AfterOtherFixes)
                {
                    TheArticle.PerformFindAndReplace(findAndReplace, substTemplates, replaceSpecial, 
                        chkSkipWhenNoFAR.Checked);
                    if (TheArticle.SkipArticle) return;
                }

                if (chkEnableDab.Checked && txtDabLink.Text.Trim() != "" &&
                    txtDabVariants.Text.Trim() != "")
                {
                    if (TheArticle.Disambiguate(txtDabLink.Text.Trim(), txtDabVariants.Lines, BotMode,
                        (int)udContextChars.Value, chkSkipNoDab.Checked))
                    {
                        if (TheArticle.SkipArticle) return;
                    }
                    else
                    {
                        Stop();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                TheArticle.LogListener.AWBSkipped("Error");
            }
        }

        private void GetDiff()
        {
            try
            {
                webBrowserDiff.BringToFront();
                webBrowserDiff.Document.OpenNew(false);

                if (TheArticle.OriginalArticleText == txtEdit.Text)
                {
                    webBrowserDiff.Document.Write(@"<h2 style='padding-top: .5em;
padding-bottom: .17em;
border-bottom: 1px solid #aaa;
font-size: 150%;'>No changes</h2><p>Press the ""Ignore"" button below to skip to the next page.</p>");
                }
                //else if (TheArticle.OriginalArticleText == "")
                //{

                //}
                else
                {
                    webBrowserDiff.Document.Write("<html><head>" +
                        WikiDiff.DiffHead() + @"</head><body>" + WikiDiff.TableHeader() +
                        WikiDiff.GetDiff(TheArticle.OriginalArticleText, txtEdit.Text, 1) +
                        @"</table></body></html>");
                }
                
                CaseWasDiff();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            //*/
        }

        private void GetPreview()
        {
            webBrowserEdit.BringToFront();
            webBrowserEdit.SetArticleText(txtEdit.Text);

            DisableButtons();
            LastArticle = txtEdit.Text;

            skippable = false;
            webBrowserEdit.ShowPreview();
        }

        private void Save()
        {
            DisableButtons();
            if (txtEdit.Text.Length > 0)
                SaveArticle();
            else if (MessageBox.Show("Do you really want to save a blank page?", "Save?", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                SaveArticle();
            else
                SkipPage("Nothing to save - blank page");
        }

        private void SaveArticle()
        {
            webBrowserEdit.BringToFront();

            //remember article text in case it is lost, this is set to "" again when the article title is removed
            LastArticle = txtEdit.Text;

            if (showTimerToolStripMenuItem.Checked)
            {
                stopSaveInterval();
                ticker += SaveInterval;
            }

            try
            {
                setCheckBoxes();

                webBrowserEdit.SetArticleText(txtEdit.Text);
                webBrowserEdit.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region extra stuff

        readonly Regex DiffIdParser = new Regex(@"[a-z](-?\d*)x(-?\d*)");

        public void DiffDblClicked(string id)
        {
            try
            {
                Match m = DiffIdParser.Match(id);
                int SrcLine = int.Parse(m.Groups[1].Value) - 1;
                int DestLine = int.Parse(m.Groups[2].Value) - 1;

                if (SrcLine < 0 || DestLine < 0) return;

                string[] Src = TheArticle.OriginalArticleText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                string[] Dest = txtEdit.Text.Split(new string[] { "\r\n" }, StringSplitOptions.None);

                List<string> Lst = new List<string>(Dest);
                switch (id[0])
                {
                    case 'a':
                        Lst.RemoveAt(DestLine);
                        Dest = Lst.ToArray();
                        break;
                    case 'd':
                        Lst.Insert(DestLine, Src[Math.Min(SrcLine, Src.Length - 1)]);
                        Dest = Lst.ToArray();
                        break;
                    case 'r':
                        Dest[DestLine] = Src[SrcLine];
                        break;
                    default:
                        return;
                }
                txtEdit.Text = string.Join("\r\n", Dest);
                GetDiff();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        public void DiffClicked(string id)
        {
            try
            {
                tabControl2.SelectedTab = tpEdit;
                txtEdit.Select();
                Match m = DiffIdParser.Match(id);
                int DestLine = int.Parse(m.Groups[2].Value) - 1;
                if (DestLine < 0) return;

                MatchCollection mc = Regex.Matches(txtEdit.Text, "\r\n");
                if (mc.Count < DestLine) return;

                if (DestLine == 0) txtEdit.Select(0, 0);
                else
                    txtEdit.Select(mc[DestLine - 1].Index + 2, 0);
                txtEdit.ScrollToCaret();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void panelShowHide()
        {
            if (splitContainer1.Visible)
            { splitContainer1.Hide(); }
            else
            { splitContainer1.Show(); }
            setBrowserSize();
        }

        private void parametersShowHide()
        {
            enlargeEditAreaToolStripMenuItem.Checked = !enlargeEditAreaToolStripMenuItem.Checked;
            splitContainer1.Panel1Collapsed = !splitContainer1.Panel1Collapsed;
        }

        private void UpdateUserName(object sender, EventArgs e)
        {
            lblUserName.Text = Variables.User.Name;
        }

        private void UpdateWebBrowserStatus()
        {
            lblStatusText.Text = webBrowserEdit.Status;
        }

        private void UpdateListStatus()
        {
            lblStatusText.Text = listMaker1.Status;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            WebControl.Shutdown = true;

            Properties.Settings.Default.WindowLocation = this.Location;

            if (this.WindowState == FormWindowState.Normal)
                Properties.Settings.Default.WindowSize = this.Size;
            else
                Properties.Settings.Default.WindowSize = this.RestoreBounds.Size;

            Properties.Settings.Default.Save();

            if (AutoWikiBrowser.Properties.Settings.Default.DontAskForTerminate)
            {
                // save user persistent settings
                AutoWikiBrowser.Properties.Settings.Default.Save();
                return;
            }
            string msg = "";
            if (boolSaved == false)
                msg = "You have changed the list since last saving it!\r\n";

            TimeSpan Time = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
            Time = Time.Subtract(StartTime);
            ExitQuestion dlg = new ExitQuestion(Time, NumberOfEdits, msg);
            dlg.ShowDialog();
            if (dlg.DialogResult == DialogResult.OK)
            {
                AutoWikiBrowser.Properties.Settings.Default.DontAskForTerminate = dlg.checkBoxDontAskAgain;

                // save user persistent settings
                AutoWikiBrowser.Properties.Settings.Default.Save();
            }
            else
            {
                e.Cancel = true;
                dlg = null;
                return;
            }

            if (webBrowserEdit.IsBusy)
                webBrowserEdit.Stop2();
            if (Variables.User.webBrowserLogin.IsBusy)
                Variables.User.webBrowserLogin.Stop();

            SaveRecentSettingsList();
        }

        private void setCheckBoxes()
        {
            if (webBrowserEdit.Document.Body.InnerHtml.Contains("wpMinoredit"))
            {
                if (markAllAsMinorToolStripMenuItem.Checked)
                    webBrowserEdit.SetMinor(true);
                if (addAllToWatchlistToolStripMenuItem.Checked)
                    webBrowserEdit.SetWatch(true);
                if (!addAllToWatchlistToolStripMenuItem.Checked && bOverrideWatchlist)
                    webBrowserEdit.SetWatch(false);
                webBrowserEdit.SetSummary(MakeSummary());
            }
        }

        private string MakeSummary()
        {
            string tag = cmboEditSummary.Text + TheArticle.SavedSummary;
            if (!BotMode || !chkSuppressTag.Checked) tag += " " + Variables.SummaryTag;

            return tag;
        }

        private void chkFindandReplace_CheckedChanged(object sender, EventArgs e)
        {
            btnMoreFindAndReplce.Enabled = chkFindandReplace.Checked;
            btnFindAndReplaceAdvanced.Enabled = chkFindandReplace.Checked;
            chkSkipWhenNoFAR.Enabled = chkFindandReplace.Checked;
            btnSubst.Enabled = chkFindandReplace.Checked;
        }

        private void cmboCategorise_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmboCategorise.SelectedIndex > 0)
            {
                txtNewCategory.Enabled = true;
                if (cmboCategorise.SelectedIndex == 1)
                {
                    label1.Text = "with Category:";
                    txtNewCategory2.Enabled = true;
                }
                else
                {
                    label1.Text = "";
                    txtNewCategory2.Enabled = false;
                }
                chkSkipNoCatChange.Enabled = true;
            }
            else
            {
                chkSkipNoCatChange.Enabled = false;
                label1.Text = "";
                txtNewCategory2.Enabled = false;
                txtNewCategory.Enabled = false;
            }
        }

        private void web4Completed(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            toolStripProgressBar1.MarqueeAnimationSpeed = 0;
            toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
        }

        private void web4Starting(object sender, WebBrowserNavigatingEventArgs e)
        {
            toolStripProgressBar1.MarqueeAnimationSpeed = 100;
            toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
        }

        private void UpdateBotStatus(object sender, EventArgs e)
        {
            chkAutoMode.Enabled = Variables.User.IsBot;
            if (BotMode)
                BotMode = Variables.User.IsBot;
            if (chkQuickSave.Checked)
                chkQuickSave.Checked = Variables.User.IsBot;
            lblOnlyBots.Visible = !Variables.User.IsBot;
        }

        private void UpdateAdminStatus(object sender, EventArgs e)
        {
        }

        private void chkAutoMode_CheckedChanged(object sender, EventArgs e)
        {
            if (BotMode)
            {
                label2.Enabled = true;
                chkSuppressTag.Enabled = true;
                chkQuickSave.Enabled = true;
                nudBotSpeed.Enabled = true;
                lblAutoDelay.Enabled = true;
                btnResetNudges.Enabled = true;
                lblNudges.Enabled = true;
                chkNudge.Enabled = true;
                chkNudgeSkip.Enabled = true;
                chkNudge.Checked = true;
                chkNudgeSkip.Checked = false; // default to false until such time as the settings file has this! mets! :P

                if (chkRegExTypo.Checked)
                {
                    MessageBox.Show("Auto cannot be used with RegExTypoFix.\r\nRegExTypoFix will now be turned off", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    chkRegExTypo.Checked = false;
                }
            }
            else
            {
                label2.Enabled = false;
                chkSuppressTag.Enabled = false;
                chkQuickSave.Enabled = false;
                nudBotSpeed.Enabled = false;
                lblAutoDelay.Enabled = false;
                btnResetNudges.Enabled = false;
                lblNudges.Enabled = false;
                chkNudge.Enabled = false;
                chkNudgeSkip.Enabled = false;
                stopDelayedAutoSaveTimer();
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TimeSpan Time = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
            Time = Time.Subtract(StartTime);
            AboutBox About = new AboutBox(webBrowserEdit.Version.ToString(), Time, NumberOfEdits);
            About.Show();
        }

        private void loginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckStatus();
        }

        private void logOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Would you really like to logout?", "Logout", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                chkAutoMode.Enabled = false;
                BotMode = false;
                chkQuickSave.Checked = false;
                lblOnlyBots.Visible = true;
                webBrowserEdit.BringToFront();
                webBrowserEdit.LoadLogOut();
                webBrowserEdit.Wait();
                Variables.User.UpdateWikiStatus();
            }
        }

        private bool CheckStatus()
        {
            lblStatusText.Text = "Loading page to check if we are logged in.";
            WikiStatusResult Result = Variables.User.UpdateWikiStatus();

            bool b = false;
            string label = "Software disabled";

            switch (Result)
            {
                case WikiStatusResult.Error:
                    lblUserName.BackColor = Color.Red;
                    MessageBox.Show("Check page failed to load.\r\n\r\nCheck your Internet Explorer is working and that the Wikipedia servers are online, also try clearing Internet Explorer cache.", "User check problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;

                case WikiStatusResult.NotLoggedIn:
                    lblUserName.BackColor = Color.Red;
                    MessageBox.Show("You are not logged in. The log in screen will now load, enter your name and password, click \"Log in\", wait for it to complete, then start the process again.\r\n\r\nIn the future you can make sure this won't happen by logging in to Wikipedia using Microsoft Internet Explorer.", "Not logged in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    webBrowserEdit.LoadLogInPage();
                    webBrowserEdit.BringToFront();
                    break;

                case WikiStatusResult.NotRegistered:
                    lblUserName.BackColor = Color.Red;
                    MessageBox.Show(Variables.User.Name + " is not enabled to use this.", "Not enabled", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    System.Diagnostics.Process.Start(Variables.URL + "/wiki/Project:AutoWikiBrowser/CheckPage");
                    break;

                case WikiStatusResult.OldVersion:
                    oldVersion();
                    break;

                case WikiStatusResult.Registered:
                    b = true;
                    label = string.Format("Logged in, user and software enabled. Bot = {0}, Admin = {1}", Variables.User.IsBot, Variables.User.IsAdmin);
                    lblUserName.BackColor = Color.LightGreen;

                    //Get list of articles not to apply general fixes to.
                    Match n = Regex.Match(Variables.User.CheckPageText, "<!--No general fixes:.*?-->", RegexOptions.Singleline);
                    if (n.Success)
                    {
                        foreach (Match link in WikiRegexes.UnPipedWikiLink.Matches(n.Value))
                            if (!noParse.Contains(link.Groups[1].Value))
                                noParse.Add(link.Groups[1].Value);
                    }
                    break;
            }

            lblStatusText.Text = label;
            UpdateButtons();

            return b;
        }

        private void oldVersion()
        {
            if (!WebControl.Shutdown)
            {
                lblUserName.BackColor = Color.Red;

                DialogResult yesnocancel = MessageBox.Show("This version is not enabled, please download the newest version. If you have the newest version, check that Wikipedia is online.\r\n\r\nPlease press \"Yes\" to run the AutoUpdater, \"No\" to load the download page and update manually, or \"Cancel\" to not update (but you will not be able to edit).", "Problem", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                if (yesnocancel == DialogResult.Yes)
                    runUpdater();

                if (yesnocancel == DialogResult.No)
                    System.Diagnostics.Process.Start("http://sourceforge.net/project/showfiles.php?group_id=158332");
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
            Application.Exit();
        }

        private void chkAppend_CheckedChanged(object sender, EventArgs e)
        {
            txtAppendMessage.Enabled = chkAppend.Checked;
            rdoAppend.Enabled = chkAppend.Checked;
            rdoPrepend.Enabled = chkAppend.Checked;
        }

        private void wordWrapToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            txtEdit.WordWrap = wordWrapToolStripMenuItem1.Checked;
        }

        private void chkIgnoreIfContains_CheckedChanged(object sender, EventArgs e)
        {
            txtSkipIfContains.Enabled = chkSkipIfContains.Checked;
        }

        private void chkOnlyIfContains_CheckedChanged(object sender, EventArgs e)
        {
            txtSkipIfNotContains.Enabled = chkSkipIfNotContains.Checked;
        }

        private void txtNewCategory_Leave(object sender, EventArgs e)
        {
            txtNewCategory.Text = txtNewCategory.Text.Trim('[', ']');
            txtNewCategory.Text = Regex.Replace(txtNewCategory.Text, "^" + Variables.NamespacesCaseInsensitive[14], "");
            txtNewCategory.Text = Tools.TurnFirstToUpper(txtNewCategory.Text);
        }

        private void txtNewCategory2_Leave(object sender, EventArgs e)
        {
            txtNewCategory2.Text = txtNewCategory2.Text.Trim('[', ']');
            txtNewCategory2.Text = Regex.Replace(txtNewCategory2.Text, "^" + Variables.NamespacesCaseInsensitive[14], "");
            txtNewCategory2.Text = Tools.TurnFirstToUpper(txtNewCategory2.Text);
        }

        private void ArticleInfo(bool reset)
        {
            string ArticleText = txtEdit.Text;
            int intWords = 0;
            int intCats = 0;
            int intImages = 0;
            int intLinks = 0;
            int intInterLinks = 0;
            lblWarn.Text = "";

            if (reset)
            {
                //Resets all the alerts.
                lblWords.Text = "Words: ";
                lblCats.Text = "Categories: ";
                lblImages.Text = "Images: ";
                lblLinks.Text = "Links: ";
                lblInterLinks.Text = "Inter links: ";

                lbDuplicateWikilinks.Items.Clear();
                lblDuplicateWikilinks.Visible = false;
                lbDuplicateWikilinks.Visible = false;
                btnRemove.Visible = false;
            }
            else
            {
                intWords = Tools.WordCount(ArticleText);

                foreach (Match m in Regex.Matches(ArticleText, "\\[\\[" + Variables.Namespaces[14], RegexOptions.IgnoreCase))
                    intCats++;

                foreach (Match m in Regex.Matches(ArticleText, "\\[\\[" + Variables.Namespaces[6], RegexOptions.IgnoreCase))
                    intImages++;

                foreach (Match m in WikiRegexes.InterWikiLinks.Matches(ArticleText))
                    intInterLinks++;

                foreach (Match m in WikiRegexes.WikiLinksOnly.Matches(ArticleText))
                    intLinks++;

                intLinks = intLinks - intInterLinks - intImages - intCats;

                if (TheArticle.NameSpaceKey == 0 && (WikiRegexes.Stub.IsMatch(ArticleText)) && (intWords > 500))
                    lblWarn.Text = "Long article with a stub tag.\r\n";

                if (!(Regex.IsMatch(ArticleText, "\\[\\[" + Variables.Namespaces[14], RegexOptions.IgnoreCase)))
                    lblWarn.Text += "No category (although one may be in a template)\r\n";

                if (ArticleText.StartsWith("=="))
                    lblWarn.Text += "Starts with heading.";

                lblWords.Text = "Words: " + intWords.ToString();
                lblCats.Text = "Categories: " + intCats.ToString();
                lblImages.Text = "Images: " + intImages.ToString();
                lblLinks.Text = "Links: " + intLinks.ToString();
                lblInterLinks.Text = "Inter links: " + intInterLinks.ToString();

                //Find multiple links                
                lbDuplicateWikilinks.Items.Clear();
                ArrayList ArrayLinks = new ArrayList();
                string x = "";
                //get all the links
                foreach (Match m in WikiRegexes.WikiLink.Matches(ArticleText))
                {
                    x = m.Groups[1].Value;
                    if (!WikiRegexes.Dates.IsMatch(x) && !WikiRegexes.Dates2.IsMatch(x))
                        ArrayLinks.Add(x);
                }

                lbDuplicateWikilinks.Sorted = true;

                //add the duplicate articles to the listbox
                foreach (string z in ArrayLinks)
                {
                    if ((ArrayLinks.IndexOf(z) < ArrayLinks.LastIndexOf(z)) && (!lbDuplicateWikilinks.Items.Contains(z)))
                        lbDuplicateWikilinks.Items.Add(z);
                }
                ArrayLinks = null;

                if (lbDuplicateWikilinks.Items.Count > 0)
                {
                    lblDuplicateWikilinks.Visible = true;
                    lbDuplicateWikilinks.Visible = true;
                    btnRemove.Visible = true;
                }
            }
        }

        private void lbDuplicateWikilinks_Click(object sender, EventArgs e)
        {
            int selection = lbDuplicateWikilinks.SelectedIndex;
            if (selection != oldselection)
                resetFind();
            if (lbDuplicateWikilinks.SelectedIndex != -1)
            {
                string strLink = Regex.Escape(lbDuplicateWikilinks.SelectedItem.ToString());
                find("\\[\\[" + strLink + "(\\|.*?)?\\]\\]", true, true);
                btnRemove.Enabled = true;
            }
            else
            {
                resetFind();
                btnRemove.Enabled = false;
            }

            ArticleInfo(false);
            try
            {
                if (lbDuplicateWikilinks.Items.Count != selection + 2)
                    lbDuplicateWikilinks.SelectedIndex = selection + 2;
                else
                    lbDuplicateWikilinks.SelectedIndex = selection + 1;
                lbDuplicateWikilinks.SelectedIndex = selection;
            }
            catch
            {
                lbDuplicateWikilinks.SelectedIndex = lbDuplicateWikilinks.Items.Count - 1;
            }
            oldselection = selection;
        }

        private void txtFind_TextChanged(object sender, EventArgs e)
        {
            resetFind();
        }

        private void chkFindRegex_CheckedChanged(object sender, EventArgs e)
        {
            resetFind();
        }
        private void txtEdit_TextChanged(object sender, EventArgs e)
        {
            resetFind();
            TheArticle.EditSummary = "";
        }
        private void chkFindCaseSensitive_CheckedChanged(object sender, EventArgs e)
        {
            resetFind();
        }
        private void resetFind()
        {
            regexObj = null;
            matchObj = null;
        }
        private void btnFind_Click(object sender, EventArgs e)
        {
            lblDone.Text = "";
            find(txtFind.Text, chkFindRegex.Checked, chkFindCaseSensitive.Checked);
        }

        private Regex regexObj;
        private Match matchObj;

        private void find(string strRegex, bool isRegex, bool caseSensive)
        {
            string ArticleText = txtEdit.Text;

            RegexOptions regOptions;

            if (caseSensive)
                regOptions = RegexOptions.None;
            else
                regOptions = RegexOptions.IgnoreCase;

            strRegex = Tools.ApplyKeyWords(TheArticle.Name, strRegex);

            if (!isRegex)
                strRegex = Regex.Escape(strRegex);

            if (matchObj == null || regexObj == null)
            {
                int findStart = txtEdit.SelectionStart;

                regexObj = new Regex(strRegex, regOptions);
                matchObj = regexObj.Match(ArticleText, findStart);
                txtEdit.SelectionStart = matchObj.Index;
                txtEdit.SelectionLength = matchObj.Length;
                txtEdit.Focus();
                txtEdit.ScrollToCaret();
                return;
            }
            else
            {
                if (matchObj.NextMatch().Success)
                {
                    matchObj = matchObj.NextMatch();
                    txtEdit.SelectionStart = matchObj.Index;
                    txtEdit.SelectionLength = matchObj.Length;
                    txtEdit.Focus();
                    txtEdit.ScrollToCaret();
                }
                else
                {
                    lblDone.Text = "Done";
                    txtEdit.SelectionStart = 0;
                    txtEdit.SelectionLength = 0;
                    txtEdit.Focus();
                    txtEdit.ScrollToCaret();
                    resetFind();
                }
            }
        }

        private void toolStripTextBox2_Click(object sender, EventArgs e)
        {
            toolStripTextBox2.Text = "";
        }

        private void toolStripTextBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsNumber(e.KeyChar) && e.KeyChar != 8)
                e.Handled = true;

            if (e.KeyChar == '\r' && toolStripTextBox2.Text.Length > 0)
            {
                e.Handled = true;
                GoToLine();
                mnuTextBox.Hide();
            }
        }

        private void GoToLine()
        {
            int i = 1;
            int intLine = int.Parse(toolStripTextBox2.Text);
            int intStart = 0;
            int intEnd = 0;

            foreach (Match m in Regex.Matches(txtEdit.Text, "^.*?$", RegexOptions.Multiline))
            {
                if (i == intLine)
                {
                    intStart = m.Index;
                    intEnd = intStart + m.Length;
                    break;
                }
                i++;
            }

            txtEdit.Select(intStart, intEnd - intStart);
            txtEdit.ScrollToCaret();
            txtEdit.Focus();
        }

        [Conditional("DEBUG")]
        public void Debug()
        {//stop logging in when de-bugging
            Tools.WriteDebugEnabled = true;
            listMaker1.Add("Wikipedia:AutoWikiBrowser/Sandbox");
            //Variables.User.WikiStatus = true; // Stop logging in and the username code doesn't work!
            Variables.User.IsBot = true;
            Variables.User.IsAdmin = true;
            chkQuickSave.Enabled = true;
            lblOnlyBots.Visible = false;
            dumpHTMLToolStripMenuItem.Visible = true;
            logOutDebugToolStripMenuItem.Visible = true;
            bypassAllRedirectsToolStripMenuItem.Enabled = true;
            webBrowserEdit.IsWebBrowserContextMenuEnabled = true;
            recycleWebControlToolStripMenuItem.Visible = true;
        }

        #endregion

        #region set variables

        private void PreferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MyPreferences MyPrefs = new MyPreferences(Variables.LangCode, Variables.Project, Variables.CustomProject, webBrowserEdit.EnhanceDiffEnabled, webBrowserEdit.ScrollDown, webBrowserEdit.DiffFontSize, txtEdit.Font, LowThreadPriority, Flash, Beep, Minimize, SaveArticleList, OverrideWatchlist, TimeOut, AutoSaveEditBoxEnabled, AutoSaveEditBoxFile, AutoSaveEditBoxPeriod);

            if (MyPrefs.ShowDialog(this) == DialogResult.OK)
            {
                webBrowserEdit.EnhanceDiffEnabled = MyPrefs.EnhanceDiff;
                webBrowserEdit.ScrollDown = MyPrefs.ScrollDown;
                webBrowserEdit.DiffFontSize = MyPrefs.DiffFontSize;
                txtEdit.Font = MyPrefs.TextBoxFont;
                LowThreadPriority = MyPrefs.LowThreadPriority;
                Flash = MyPrefs.perfFlash;
                Beep = MyPrefs.perfBeep;
                Minimize = MyPrefs.perfMinimize;
                SaveArticleList = MyPrefs.perfSaveArticleList;
                OverrideWatchlist = MyPrefs.perfOverrideWatchlist;
                TimeOut = MyPrefs.perfTimeOutLimit;
                AutoSaveEditBoxEnabled = MyPrefs.perfAutoSaveEditBoxEnabled;
                AutoSaveEditBoxPeriod = MyPrefs.perfAutoSaveEditBoxPeriod;
                AutoSaveEditBoxFile = MyPrefs.perfAutoSaveEditBoxFile;

                if (MyPrefs.Language != Variables.LangCode || MyPrefs.Project != Variables.Project || MyPrefs.CustomProject != Variables.CustomProject)
                {
                    SetProject(MyPrefs.Language, MyPrefs.Project, MyPrefs.CustomProject);

                    Variables.User.WikiStatus = false;
                    chkQuickSave.Checked = false;
                    BotMode = false;
                    lblOnlyBots.Visible = true;
                    Variables.User.IsBot = false;
                    Variables.User.IsAdmin = false;
                }
            }
            MyPrefs = null;

            listMaker1.AddRemoveRedirects();
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //refresh typo list
            loadTypos(true);

            //refresh talk warnings list
            loadUserTalkWarnings();

            //refresh login status, and reload check list
            if (!Variables.User.WikiStatus)
            {
                if (!CheckStatus())
                    return;
            }
        }

        private void SetProject(LangCodeEnum Code, ProjectEnum Project, string CustomProject)
        {
            //set namespaces
            Variables.SetProject(Code, Project, CustomProject);

            //set interwikiorder
            if (Code == LangCodeEnum.en || Code == LangCodeEnum.pl || Code == LangCodeEnum.simple)
                parsers.InterWikiOrder = InterWikiOrderEnum.LocalLanguageAlpha;
            //else if (Code == "fi")
            //    parsers.InterWikiOrder = InterWikiOrderEnum.LocalLanguageFirstWord;
            else
                parsers.InterWikiOrder = InterWikiOrderEnum.Alphabetical;

            if (Code != LangCodeEnum.en || Project != ProjectEnum.wikipedia)
            {
                chkAutoTagger.Checked = false;
                chkGeneralFixes.Checked = false;
            }
            if (Project != ProjectEnum.custom) lblProject.Text = Variables.LangCode.ToString().ToLower() + "." + Variables.Project;
            else lblProject.Text = Variables.URL;
        }

        #endregion

        #region Enabling/Disabling of buttons

        private void UpdateButtons()
        {
            bool enabled = listMaker1.NumberOfArticles > 0;
            SetStartButton(enabled);

            //    btnMove.Visible = Variables.User.IsAdmin;
            //    btnDelete.Visible = Variables.User.IsAdmin;

            listMaker1.ButtonsEnabled = enabled;
            lbltsNumberofItems.Text = "Articles: " + listMaker1.NumberOfArticles.ToString();
            bypassAllRedirectsToolStripMenuItem.Enabled = Variables.User.IsAdmin;
        }

        private void SetStartButton(bool enabled)
        {
            /* Please don't remove the If statements; otherwise the EnabledChange event fires even if the button
             * button was already named. The Kingbotk plugin attaches to that event. */
            if (!btnStart.Enabled) btnStart.Enabled = enabled;
            if (!btntsStart.Enabled) btntsStart.Enabled = enabled;
        }

        private void DisableButtons()
        {
            SetStartButton(false);

            if (listMaker1.NumberOfArticles == 0)
                btnIgnore.Enabled = false;

            btnPreview.Enabled = false;
            btnDiff.Enabled = false;
            btntsPreview.Enabled = false;
            btntsChanges.Enabled = false;

            listMaker1.MakeListEnabled = false;

            btnSave.Enabled = false;
            btntsSave.Enabled = false;

            btnMove.Enabled = false;
            btnDelete.Enabled = false;

            if (cmboEditSummary.Focused) txtEdit.Focus();
        }

        private void EnableButtons()
        {
            UpdateButtons();
            btnSave.Enabled = true;
            btnIgnore.Enabled = true;
            btnPreview.Enabled = true;
            btnDiff.Enabled = true;
            btntsPreview.Enabled = true;
            btntsChanges.Enabled = true;

            listMaker1.MakeListEnabled = true;

            btntsSave.Enabled = true;
            btntsIgnore.Enabled = true;

            btnMove.Enabled = true;
            btnDelete.Enabled = true;
        }

        #endregion

        #region timers

        int intRestartDelay = 5;
        int intStartInSeconds = 5;
        private void DelayedRestart()
        {
            stopDelayedAutoSaveTimer();
            lblStatusText.Text = "Restarting in " + intStartInSeconds.ToString();

            if (intStartInSeconds == 0)
            {
                StopDelayedRestartTimer();
                Start();
            }
            else
                intStartInSeconds--;
        }
        private void StartDelayedRestartTimer()
        {
            intStartInSeconds = intRestartDelay;
            ticker += DelayedRestart;
            //increase the restart delay each time, this is decreased by 1 on each successfull save
            intRestartDelay += 5;
        }
        private void StartDelayedRestartTimer(int Delay)
        {
            intStartInSeconds = Delay;
            ticker += DelayedRestart;
        }
        private void StopDelayedRestartTimer()
        {
            ticker -= DelayedRestart;
            intStartInSeconds = intRestartDelay;
        }

        private void stopDelayedAutoSaveTimer()
        {
            ticker -= DelayedAutoSave;
            intTimer = 0;
            lblBotTimer.Text = "Bot timer: " + intTimer.ToString();
        }

        private void startDelayedAutoSaveTimer()
        {
            ticker += DelayedAutoSave;
        }

        int intTimer = 0;
        private void DelayedAutoSave()
        {
            if (intTimer < nudBotSpeed.Value)
            {
                intTimer++;
                if (intTimer == 1)
                    lblBotTimer.BackColor = Color.Red;
                else
                    lblBotTimer.BackColor = DefaultBackColor;
            }
            else
            {
                stopDelayedAutoSaveTimer();
                SaveArticle();
            }

            lblBotTimer.Text = "Bot timer: " + intTimer.ToString();
        }

        private void showTimerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showTimer();
        }

        private void showTimer()
        {
            lblTimer.Visible = showTimerToolStripMenuItem.Checked;
            stopSaveInterval();
        }

        int intStartTimer = 0;
        private void SaveInterval()
        {
            intStartTimer++;
            lblTimer.Text = "Timer: " + intStartTimer.ToString();
        }
        private void stopSaveInterval()
        {
            intStartTimer = 0;
            lblTimer.Text = "Timer: 0";
            ticker -= SaveInterval;
        }

        public delegate void Tick();
        public event Tick ticker;
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (ticker != null)
                ticker();

            seconds++;
            if (seconds == 60)
            {
                seconds = 0;
                EditsPerMin();
            }
        }

        int seconds = 0;
        int lastTotal = 0;
        private void EditsPerMin()
        {
            int editsInLastMin = NumberOfEdits - lastTotal;
            NumberOfEditsPerMinute = editsInLastMin;
            lastTotal = NumberOfEdits;
        }

        #endregion

        #region menus and buttons

        private void makeModuleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cModule.Show();
        }

        private void btnMoreSkip_Click(object sender, EventArgs e)
        {
            Skip.ShowDialog();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            GetPreview();
        }

        private void btnDiff_Click(object sender, EventArgs e)
        {
            GetDiff();
        }

        private void btnFalsePositive_Click(object sender, EventArgs e)
        {
            FalsePositive();
        }

        private void tsbuttonFalsePositive_Click(object sender, EventArgs e)
        {
            FalsePositive();
        }

        private void FalsePositive()
        {
            if (TheArticle.Name.Length > 0)
                Tools.WriteLog("#[[" + TheArticle.Name + "]]\r\n", @"False positives.txt");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            Start();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            Stop();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void btnIgnore_Click(object sender, EventArgs e)
        {
            SkipPage("user");
        }

        private void btnMove_Click(object sender, EventArgs e)
        {
            MoveArticle();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            DeleteArticle();
        }

        private void filterOutNonMainSpaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (filterOutNonMainSpaceToolStripMenuItem.Checked)
            {
                listMaker1.FilterNonMainAuto = true;
                listMaker1.FilterNonMainArticles();
            }
            else
                listMaker1.FilterNonMainAuto = false;
        }

        private void specialFilterToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            listMaker1.Filter();
        }

        private void convertToTalkPagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listMaker1.ConvertToTalkPages();
        }

        private void convertFromTalkPagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listMaker1.ConvertFromTalkPages();
        }

        private void sortAlphabeticallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sortAlphabeticallyToolStripMenuItem.Checked)
            {
                listMaker1.AutoAlpha = true;
                listMaker1.AlphaSortList();
            }
            else
                listMaker1.AutoAlpha = false;
        }

        private void saveListToTextFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listMaker1.SaveList();
        }

        private void launchListComparerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListComparer lc;

            if (listMaker1.Count > 0 && MessageBox.Show("Would you like to copy your current Article List to the ListComparer?", "Copy Article List?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                lc = new ListComparer(listMaker1.GetArticleList());
            else
                lc = new ListComparer();

            lc.ShowDialog();
            lc.Dispose();
        }

        private void launchListSplitterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListSplitter splitter;
            WikiFunctions.AWBSettings.UserPrefs P = MakePrefs();

            if (listMaker1.Count > 0 && MessageBox.Show("Would you like to copy your current Article List to the ListSplitter?", "Copy Article List?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                splitter = new ListSplitter(P, savePluginSettings(P), listMaker1.GetArticleList());
            else
                splitter = new ListSplitter(P, savePluginSettings(P));

            splitter.ShowDialog();
            splitter.Dispose();
        }

        private void launchDumpSearcherToolStripMenuItem_Click(object sender, EventArgs e)
        {
            launchDumpSearcher();
        }

        private void launchDumpSearcher()
        {
            WikiFunctions.DatabaseScanner.DatabaseScanner ds = new WikiFunctions.DatabaseScanner.DatabaseScanner();
            ds.Show();
            UpdateButtons();
        }

        private void addIgnoredToLogFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnFalsePositive.Visible = addIgnoredToLogFileToolStripMenuItem.Checked;
            btntsFalsePositive.Visible = addIgnoredToLogFileToolStripMenuItem.Checked;
        }

        private void alphaSortInterwikiLinksToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            parsers.sortInterwikiOrder = alphaSortInterwikiLinksToolStripMenuItem.Checked;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && btnStop.Enabled)
            {
                Stop();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Modifiers == Keys.Control)
            {
                if (e.KeyCode == Keys.S && btnSave.Enabled)
                {
                    Save();
                    e.SuppressKeyPress = true;
                    return;
                }
                else if (e.KeyCode == Keys.S && btnStart.Enabled)
                {
                    Start();
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.I && btnIgnore.Enabled)
                {
                    SkipPage("user");
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.D && btnDiff.Enabled)
                {
                    GetDiff();
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.E && btnPreview.Enabled)
                {
                    GetPreview();
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.F)
                {
                    find(txtFind.Text, chkFindRegex.Checked, chkFindCaseSensitive.Checked);
                    e.SuppressKeyPress = true;
                    return;
                }
            }
        }

        private void cmbEditSummary_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !cmboEditSummary.Items.Contains(cmboEditSummary.Text))
            {
                e.SuppressKeyPress = true;
                cmboEditSummary.Items.Add(cmboEditSummary.Text);
            }
        }

        private void copyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if ((webBrowserEdit.Document != null)) webBrowserEdit.Document.ExecCommand("Copy", false, System.DBNull.Value);
        }

        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = Tools.HTMLListToWiki(txtEdit.SelectedText, "*");
        }

        private void listToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = Tools.HTMLListToWiki(txtEdit.SelectedText, "#");
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Cut();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Copy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Paste();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectAll();
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Undo();
        }

        private void humanNameDisambigTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = "{{Hndis|name=" + Tools.MakeHumanCatKey(TheArticle.Name) + "}}";
        }

        private void wikifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Text = "{{Wikify|{{subst:CURRENTMONTHNAME}} {{subst:CURRENTYEAR}}}}\r\n\r\n" + txtEdit.Text;
        }

        private void cleanupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Text = "{{cleanup|{{subst:CURRENTMONTHNAME}} {{subst:CURRENTYEAR}}}}\r\n\r\n" + txtEdit.Text;
        }

        private void expandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Text = "{{Expand}}\r\n\r\n" + txtEdit.Text;
        }

        private void speedyDeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Text = "{{Delete}}\r\n\r\n" + txtEdit.Text;
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = "{{subst:clear}}";
        }

        private void uncategorisedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = "{{Uncategorized|{{subst:CURRENTMONTHNAME}} {{subst:CURRENTYEAR}}}}";
        }

        private void bypassAllRedirectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //txtEdit.Text = parsers.BypassRedirects(txtEdit.Text);
            if (MessageBox.Show("Replacement of links to redirects with direct links is strongly discouraged, " +
                "however it could be useful in some circumstances. Are you sure you want to continue?",
                "Bypass redirects", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)

                return;

            BackgroundRequest r = new BackgroundRequest();

            Enabled = false;
            r.BypassRedirects(txtEdit.Text);
            while (!r.Done) Application.DoEvents();
            Enabled = true;

            txtEdit.Text = (string)r.Result;
        }

        private void unicodifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = txtEdit.SelectedText;
            text = parsers.Unicodify(text);
            txtEdit.SelectedText = text;
        }

        private void metadataTemplateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = "{{Persondata\r\n|NAME=\r\n|ALTERNATIVE NAMES=\r\n|SHORT DESCRIPTION=\r\n|DATE OF BIRTH=\r\n|PLACE OF BIRTH=\r\n|DATE OF DEATH=\r\n|PLACE OF DEATH=\r\n}}";
        }

        private void humanNameCategoryKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = "{{DEFAULTSORT:" + Tools.MakeHumanCatKey(TheArticle.Name) + "}}";
        }

        private void birthdeathCatsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //find first dates
            string strBirth = "";
            string strDeath = "";
            Regex RegexDates = new Regex("[1-2][0-9]{3}");

            try
            {
                MatchCollection m = RegexDates.Matches(txtEdit.Text);

                if (m.Count >= 1)
                    strBirth = m[0].Value;
                if (m.Count >= 2)
                    strDeath = m[1].Value;

                //make name, surname, firstname
                string strName = Tools.MakeHumanCatKey(TheArticle.Name);

                string Categories = "";

                if (strDeath.Length == 0 || int.Parse(strDeath) < int.Parse(strBirth) + 20)
                    Categories = "[[Category:" + strBirth + " births|" + strName + "]]";
                else
                    Categories = "[[Category:" + strBirth + " births|" + strName + "]]\r\n[[Category:" + strDeath + " deaths|" + strName + "]]";

                txtEdit.SelectedText = Categories;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void stubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.SelectedText = toolStripTextBox1.Text;
        }
        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            txtEdit.Focus();

            if (txtEdit.SelectedText.Length > 0)
            {
                cutToolStripMenuItem.Enabled = true;
                copyToolStripMenuItem.Enabled = true;
                openSelectionInBrowserToolStripMenuItem.Enabled = true;
            }
            else
            {
                cutToolStripMenuItem.Enabled = false;
                copyToolStripMenuItem.Enabled = false;
                openSelectionInBrowserToolStripMenuItem.Enabled = false;
            }

            undoToolStripMenuItem.Enabled = txtEdit.CanUndo;

            openPageInBrowserToolStripMenuItem.Enabled = TheArticle.Name.Length > 0;
            openTalkPageInBrowserToolStripMenuItem.Enabled = TheArticle.Name.Length > 0;
            openHistoryMenuItem.Enabled = TheArticle.Name.Length > 0;
            replaceTextWithLastEditToolStripMenuItem.Enabled = LastArticle.Length > 0;
        }

        private void openPageInBrowserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Variables.URLLong + "index.php?title=" + TheArticle.URLEncodedName);
        }

        private void openTalkPageInBrowserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Variables.URLLong + "index.php?title=" + GetLists.ConvertToTalk(TheArticle));
        }

        private void openHistoryMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Variables.URLLong + "index.php?title=" + TheArticle.URLEncodedName + "&action=history");
        }

        private void openSelectionInBrowserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Variables.URLLong + "index.php?title=" + txtEdit.SelectedText);
        }

        private void chkGeneralParse_CheckedChanged(object sender, EventArgs e)
        {
            alphaSortInterwikiLinksToolStripMenuItem.Enabled = chkGeneralFixes.Checked;
        }

        private void btnFindAndReplaceAdvanced_Click(object sender, EventArgs e)
        {
            if (!replaceSpecial.Visible)
                replaceSpecial.Show();
            else
                replaceSpecial.Hide();
        }

        private void btnMoreFindAndReplce_Click(object sender, EventArgs e)
        {
            if (!findAndReplace.Visible)
                findAndReplace.ShowDialog();
            else
                findAndReplace.Hide();
        }

        private void Stop()
        {
            PageReload = false;
            NudgeTimer.Stop();
            UpdateButtons();
            if (intTimer > 0)
            {//stop and reset the bot timer.
                stopDelayedAutoSaveTimer();
                EnableButtons();
                return;
            }

            stopSaveInterval();
            StopDelayedRestartTimer();
            if (webBrowserEdit.IsBusy)
                webBrowserEdit.Stop2();
            if (Variables.User.webBrowserLogin.IsBusy)
                Variables.User.webBrowserLogin.Stop();

            listMaker1.Stop();

            if (AutoSaveEditBoxEnabled)
                EditBoxSaveTimer.Enabled = false;

            lblStatusText.Text = "Stopped";
        }

        private void helpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser/User_manual");
        }

        private void reparseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* bool b = true;
            txtEdit.Text = ProcessPage(txtEdit.Text, out b); */
            //TODO
        }

        private void replaceTextWithLastEditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastArticle.Length > 0)
                txtEdit.Text = LastArticle;
        }

        private void PasteMore1_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore1.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore2_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore2.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore3_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore3.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore4_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore4.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore5_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore5.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore6_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore6.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore7_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore7.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore8_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore8.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore9_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore9.Text;
            mnuTextBox.Hide();
        }

        private void PasteMore10_DoubleClick(object sender, EventArgs e)
        {
            txtEdit.SelectedText = PasteMore10.Text;
            mnuTextBox.Hide();
        }

        private void removeAllExcessWhitespaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = RemoveText.Hide(txtEdit.Text);
            text = parsers.RemoveAllWhiteSpace(text);
            text = RemoveText.AddBack(text);

            txtEdit.Text = text;
        }

        private void txtNewCategory_DoubleClick(object sender, EventArgs e)
        {
            txtNewCategory.SelectAll();
        }

        private void cmboEditSummary_MouseMove(object sender, MouseEventArgs e)
        {
            if (TheArticle.EditSummary == "")
                toolTip1.SetToolTip(cmboEditSummary, "");
            else
                toolTip1.SetToolTip(cmboEditSummary, MakeSummary());
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void chkRegExTypo_CheckedChanged(object sender, EventArgs e)
        {
            if (BotMode && chkRegExTypo.Checked)
            {
                MessageBox.Show("RegexTypoFix cannot be used with auto save on.\r\nAutosave will now be turned off, and Typos loaded.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                BotMode = false;
                //return;
            }
            loadTypos(false);
            chkSkipIfNoRegexTypo.Enabled = chkRegExTypo.Checked;
        }

        private void loadTypos(bool Reload)
        {
            if (chkRegExTypo.Checked)
            {
                lblStatusText.Text = "Loading typos";

                string s = Variables.RETFPath;

                if (!s.StartsWith("http:")) s = Variables.URL + "/wiki/" + Tools.WikiEncode(s);

                string message = @"1. Check each edit before you make it. Although this has been built to be very accurate there is always the possibility of an error which requires your attention.

2. Optional: Select [[WP:AWB/T|Typo fixing]] as the edit summary. This lets everyone know where to bring issues with the typo correction.";

                if (RegexTypos == null)
                    message += "\r\n\r\nThe newest typos will now be downloaded from " + s + " when you press OK.";

                MessageBox.Show(message, "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                if (RegexTypos == null || Reload)
                {
                    RegexTypos = new RegExTypoFix();
                    lblStatusText.Text = RegexTypos.Typos.Count.ToString() + " typos loaded";
                }
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://en.wikipedia.org/wiki/User:Mboverload/RegExTypoFix");
        }

        private void webBrowserEdit_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            toolStripProgressBar1.MarqueeAnimationSpeed = 0;
            toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
        }

        private void webBrowserEdit_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            webBrowserEdit.BringToFront();
            toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            toolStripProgressBar1.MarqueeAnimationSpeed = 100;
        }

        private void dumpHTMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Text = webBrowserEdit.Document.Body.InnerHtml;
        }

        private void logOutDebugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Variables.User.WikiStatus = false;
        }

        private void summariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SummaryEditor se = new SummaryEditor();

            string[] summaries = new string[cmboEditSummary.Items.Count];
            cmboEditSummary.Items.CopyTo(summaries, 0);
            se.Summaries.Lines = summaries;
            se.Summaries.Select(0, 0);

            string PrevSummary = cmboEditSummary.SelectedText;

            if (se.ShowDialog() == DialogResult.OK)
            {
                cmboEditSummary.Items.Clear();

                foreach (string s in se.Summaries.Lines)
                {
                    if (s.Trim() == "") continue;
                    cmboEditSummary.Items.Add(s.Trim());
                }

                if (cmboEditSummary.Items.Contains(PrevSummary))
                    cmboEditSummary.SelectedText = PrevSummary;
                else cmboEditSummary.SelectedItem = 0;
            }
        }

        private void showHidePanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panelShowHide();
        }

        private void enlargeEditAreaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            parametersShowHide();
        }

        #endregion

        #region tool bar stuff

        private void btnShowHide_Click(object sender, EventArgs e)
        {
            panelShowHide();
        }

        private void btntsShowHideParameters_Click(object sender, EventArgs e)
        {
            parametersShowHide();
        }

        private void btntsStart_Click(object sender, EventArgs e)
        {
            Start();
        }

        private void btntsSave_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void btntsIgnore_Click(object sender, EventArgs e)
        {
            SkipPage("user");
        }

        private void btntsStop_Click(object sender, EventArgs e)
        {
            Stop();
        }

        private void btntsPreview_Click(object sender, EventArgs e)
        {
            GetPreview();
        }

        private void btntsChanges_Click(object sender, EventArgs e)
        {
            GetDiff();
        }

        private void setBrowserSize()
        {
            if (toolStrip.Visible)
            {
                webBrowserEdit.Location = new Point(webBrowserEdit.Location.X, 48);
                if (splitContainer1.Visible)
                    webBrowserEdit.Height = splitContainer1.Location.Y - 48;
                else
                    webBrowserEdit.Height = statusStrip1.Location.Y - 48;
            }
            else
            {
                webBrowserEdit.Location = new Point(webBrowserEdit.Location.X, 25);
                if (splitContainer1.Visible)
                    webBrowserEdit.Height = splitContainer1.Location.Y - 25;
                else
                    webBrowserEdit.Height = statusStrip1.Location.Y - 25;
            }

            webBrowserDiff.Location = webBrowserEdit.Location;
            webBrowserDiff.Size = webBrowserEdit.Size;
        }

        private void enableTheToolbarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            enableToolBar = enableTheToolbarToolStripMenuItem.Checked;
        }

        private bool boolEnableToolbar = false;
        private bool enableToolBar
        {
            get { return boolEnableToolbar; }
            set
            {
                if (value == true)
                    toolStrip.Show();
                else
                    toolStrip.Hide();
                setBrowserSize();
                enableTheToolbarToolStripMenuItem.Checked = value;
                boolEnableToolbar = value;
            }
        }

        #endregion

        #region Images

        private void cmboImages_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmboImages.SelectedIndex == 0)
            {
                lblImageWith.Text = "";

                txtImageReplace.Enabled = false;
                txtImageWith.Enabled = false;
                chkSkipNoImgChange.Enabled = false;
            }
            else if (cmboImages.SelectedIndex == 1)
            {
                lblImageWith.Text = "With Image:";

                txtImageWith.Enabled = true;
                txtImageReplace.Enabled = true;
                chkSkipNoImgChange.Enabled = true;
            }
            else if (cmboImages.SelectedIndex == 2)
            {
                lblImageWith.Text = "";

                txtImageWith.Enabled = false;
                txtImageReplace.Enabled = true;
                chkSkipNoImgChange.Enabled = true;
            }
            else if (cmboImages.SelectedIndex == 3)
            {
                lblImageWith.Text = "Comment:";

                txtImageWith.Enabled = true;
                txtImageReplace.Enabled = true;
                chkSkipNoImgChange.Enabled = true;
            }
        }

        private void txtImageReplace_Leave(object sender, EventArgs e)
        {
            txtImageReplace.Text = Regex.Replace(txtImageReplace.Text, "^" + Variables.Namespaces[6], "", RegexOptions.IgnoreCase);
        }

        private void txtImageWith_Leave(object sender, EventArgs e)
        {
            txtImageWith.Text = Regex.Replace(txtImageWith.Text, "^" + Variables.Namespaces[6], "", RegexOptions.IgnoreCase);
        }

        private void SetProgressBar()
        {
            if (listMaker1.BusyStatus)
            {
                toolStripProgressBar1.MarqueeAnimationSpeed = 100;
                toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                toolStripProgressBar1.MarqueeAnimationSpeed = 0;
                toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
            }
        }

        #endregion

        #region Plugin

        Dictionary<string, IAWBPlugin> AWBPlugins = new Dictionary<string, IAWBPlugin>();
        private void LoadPlugins()
        {
            try
            {
                string path = Application.StartupPath;
                string[] pluginFiles = Directory.GetFiles(path, "*.DLL");

                foreach (string s in pluginFiles)
                {
                    if (s.EndsWith("DotNetWikiBot.dll") || s.EndsWith("Wikidiff2.dll"))
                        continue;

                    string imFile = Path.GetFileName(s);

                    Assembly asm = null;
                    try
                    {
                        asm = Assembly.LoadFile(path + "\\" + imFile);
                    }
                    catch { }

                    if (asm != null)
                    {
                        Type[] types = asm.GetTypes();

                        foreach (Type t in types)
                        {
                            Type g = t.GetInterface("IAWBPlugin");

                            if (g != null)
                            {
                                IAWBPlugin awb = (IAWBPlugin)Activator.CreateInstance(t);
                                AWBPlugins.Add(awb.Name, awb);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Problem loading plugin");
            }

            foreach (KeyValuePair<string, IAWBPlugin> a in AWBPlugins)
            {
                a.Value.Initialise(this);
            }

            pluginsToolStripMenuItem.Visible = AWBPlugins.Count > 0;
        }

        private void MoveArticle()
        {
            MoveDeleteDialog dlg = new MoveDeleteDialog(true);

            try
            {
                dlg.NewTitle = TheArticle.Name;
                dlg.Summary = LastMove;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LastMove = dlg.Summary;
                    webBrowserEdit.MovePage(TheArticle.Name, dlg.NewTitle, dlg.Summary);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                dlg.Dispose();
            }
        }

        private void DeleteArticle()
        {
            MoveDeleteDialog dlg = new MoveDeleteDialog(false);

            try
            {
                dlg.Summary = LastDelete;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LastDelete = dlg.Summary;
                    webBrowserEdit.DeletePage(TheArticle.Name, dlg.Summary);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                dlg.Dispose();
            }
        }

        #endregion

        private void btnSubst_Click(object sender, EventArgs e)
        {
            substTemplates.ShowDialog();
        }

        private void testRegexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            regexTester.ShowDialog();
        }

        private void chkLock_CheckedChanged(object sender, EventArgs e)
        {
            cmboEditSummary.Visible = !chkLock.Checked;
            lblSummary.Text = cmboEditSummary.Text;
            lblSummary.Visible = chkLock.Checked;
        }

        private void txtDabLink_TextChanged(object sender, EventArgs e)
        {
            btnLoadLinks.Enabled = txtDabLink.Text.Trim() != "";
        }

        private void txtDabLink_Enter(object sender, EventArgs e)
        {
            if (txtDabLink.Text == "") txtDabLink.Text = listMaker1.SourceText;
        }

        private void chkEnableDab_CheckedChanged(object sender, EventArgs e)
        {
            panelDab.Enabled = chkEnableDab.Checked;
        }

        private void btnLoadLinks_Click(object sender, EventArgs e)
        {
            try
            {
                string name = txtDabLink.Text.Trim();
                if (name.Contains("|")) name = name.Substring(0, name.IndexOf('|') - 1);
                Article link = new Article(name);
                List<Article> l = GetLists.FromLinksOnPage(txtDabLink.Text);
                txtDabVariants.Text = "";
                foreach (Article a in l)
                {
                    uint i;
                    // exclude years
                    if (uint.TryParse(a.Name, out i) && (i < 2100)) continue;

                    // disambigs typically link to pages in the same namespace only
                    if (link.NameSpaceKey != a.NameSpaceKey) continue;

                    txtDabVariants.Text += a.Name + "\r\n";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void txtDabLink_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '\r':
                    e.Handled = true;
                    btnLoadLinks_Click(this, null);
                    break;
            }
        }

        private void txtFind_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '\r':
                    e.Handled = true;
                    btnFind_Click(this, null);
                    break;
            }
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                toolStripHide();
            }
        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.Visible)
                this.Visible = false;
        }

        private void exitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            this.Close();
            Application.Exit();
        }

        private void ntfyTray_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!this.Visible)
                toolStripHide();
            else
                this.Visible = false;
        }

        private void toolStripHide()
        {
            this.Visible = true;
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
        }

        private void updateUpdater()
        {
            Updater Updater = new Updater();
            Updater.Update();
        }

        public void NotifyBalloon(string Message, ToolTipIcon Icon)
        {
            ntfyTray.BalloonTipText = Message;
            ntfyTray.BalloonTipIcon = Icon;
            ntfyTray.ShowBalloonTip(10000);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            string selectedtext = txtEdit.SelectedText;
            if (selectedtext.StartsWith("[[") && selectedtext.EndsWith("]]"))
            {
                selectedtext = selectedtext.Trim('[').Trim(']');
                if (selectedtext.EndsWith("|"))
                {
                    if (selectedtext.Contains("(") && selectedtext.Contains(")"))
                        selectedtext = selectedtext.Substring(0, selectedtext.IndexOf("("));
                    if (selectedtext.Contains(":"))
                        selectedtext = selectedtext.Substring(selectedtext.IndexOf(":")).TrimEnd('|');
                    if ("[[" + selectedtext + "]]" == txtEdit.SelectedText)
                    {
                        MessageBox.Show("The selected link could not be removed.");
                        selectedtext = "[[" + selectedtext + "]]";
                    }
                }
                else if (selectedtext.Contains("|"))
                    selectedtext = selectedtext.Substring(selectedtext.IndexOf("|") + 1);

                txtEdit.SelectedText = selectedtext;
            }
            else
                MessageBox.Show("Please select a link to remove either manually or by clicking a link in the list above.");
        }

        private void runUpdaterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            runUpdater();
        }

        private void runUpdater()
        {
            System.Diagnostics.Process.Start(Path.GetDirectoryName(Application.ExecutablePath) + "\\AWBUpdater.exe");

            DialogResult closeAWB = MessageBox.Show("AWB needs to be closed. To do this now, click 'yes'. If you need to save your settings, do this now, the updater will not complete until AWB is closed.", "Close AWB?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (closeAWB == DialogResult.Yes)
                Application.Exit();
        }

        private void btnResetNudges_Click(object sender, EventArgs e)
        {
            Nudges = 0;
            sameArticleNudges = 0;
            lblNudges.Text = NudgeTimerString + "0";
        }

        #region "Nudge timer"
        private const string NudgeTimerString = "Total nudges: ";

        private void NudgeTimer_Tick(object sender, NudgeTimer.NudgeTimerEventArgs e)
        {
            //make sure there was no error and bot mode is still enabled
            if (BotMode)
            {
                bool Cancel;
                // Tell plugins we're about to nudge, and give them the opportunity to cancel:
                foreach (KeyValuePair<string, IAWBPlugin> a in AWBPlugins)
                {
                    a.Value.Nudge(out Cancel);
                    if (Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // Update stats and nudge:
                Nudges++;
                lblNudges.Text = NudgeTimerString + Nudges;
                NudgeTimer.Stop();
                if (chkNudgeSkip.Checked && sameArticleNudges > 0)
                {
                    sameArticleNudges = 0;
                    SkipPage("There was an error saving the page twice");
                }
                else
                {
                    sameArticleNudges++;
                    Stop();
                    Start();
                }

                // Inform plugins:
                foreach (KeyValuePair<string, IAWBPlugin> a in AWBPlugins)
                { a.Value.Nudged(Nudges); }
            }
        }

        public int Nudges
        {
            get { return mnudges; }
            private set { mnudges = value; }
        }
        #endregion

        #region IAutoWikiBrowser:
        // Objects:
            TabPage IAutoWikiBrowser.MoreOptionsTab { get { return tpMoreOptions; } }
            TabPage IAutoWikiBrowser.OptionsTab { get { return tpSetOptions; } }
            TabPage IAutoWikiBrowser.StartTab { get { return tpStart; } }
            TabPage IAutoWikiBrowser.DabTab { get { return tpDab; } }
            TabPage IAutoWikiBrowser.BotTab { get { return tpBots; } }
            CheckBox IAutoWikiBrowser.BotModeCheckbox { get { return chkAutoMode; } }
            Button IAutoWikiBrowser.PreviewButton { get { return btnPreview; } }
            Button IAutoWikiBrowser.SaveButton { get { return btnSave; } }
            Button IAutoWikiBrowser.SkipButton { get { return btnIgnore; } }
            Button IAutoWikiBrowser.StopButton { get { return btnStop; } }
            Button IAutoWikiBrowser.DiffButton { get { return btnDiff; } }
            Button IAutoWikiBrowser.StartButton { get { return btnStart; } }
            ComboBox IAutoWikiBrowser.EditSummary { get { return cmboEditSummary; } }
            StatusStrip IAutoWikiBrowser.StatusStrip { get { return statusStrip1; } }
            NotifyIcon IAutoWikiBrowser.NotifyIcon { get { return ntfyTray; } }
            CheckBox IAutoWikiBrowser.SkipNonExistentPagesCheckBox { get { return chkSkipNonExistent; } }
            CheckBox IAutoWikiBrowser.ApplyGeneralFixesCheckBox { get { return chkGeneralFixes; } }
            CheckBox IAutoWikiBrowser.AutoTagCheckBox { get { return chkAutoTagger; } }
            ToolStripMenuItem IAutoWikiBrowser.HelpToolStripMenuItem { get { return helpToolStripMenuItem; } }
            TextBox IAutoWikiBrowser.EditBox { get { return txtEdit; } }
            Form IAutoWikiBrowser.Form { get { return this; } }
            ToolStripMenuItem IAutoWikiBrowser.PluginsToolStripMenuItem { get { return pluginsToolStripMenuItem; } }
            WikiFunctions.Lists.ListMaker IAutoWikiBrowser.ListMaker { get { return listMaker1; } }
            WikiFunctions.Browser.WebControl IAutoWikiBrowser.WebControl { get { return webBrowserEdit;} }
            ContextMenuStrip IAutoWikiBrowser.EditBoxContextMenu { get { return mnuTextBox; } }
            TabControl IAutoWikiBrowser.Tab { get { return tabControl1; } }
            WikiFunctions.Parse.FindandReplace IAutoWikiBrowser.FindandReplace { get { return findAndReplace; } }
            WikiFunctions.SubstTemplates IAutoWikiBrowser.SubstTemplates { get { return substTemplates; } }
            string IAutoWikiBrowser.CustomModule { get { if (cModule.ModuleEnabled && cModule.Module != null) return cModule.Code; else return null; } }
            System.Version IAutoWikiBrowser.AWBVersion { get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; } }
            System.Version IAutoWikiBrowser.WikiFunctionsVersion { get { return WikiFunctions.Tools.Version; } }

        // "Events":
            void IAutoWikiBrowser.SkipPage(IAWBPlugin sender, string reason) { SkipPage(sender.Name, reason); }
            void IAutoWikiBrowser.Start(IAWBPlugin sender) { Start(sender.Name); }
            void IAutoWikiBrowser.Stop(IAWBPlugin sender) { Stop(sender.Name); }
            void IAutoWikiBrowser.GetDiff(IAWBPlugin sender) { GetDiff(sender.Name); }
            void IAutoWikiBrowser.GetPreview(IAWBPlugin sender) { GetPreview(sender.Name); }
            void IAutoWikiBrowser.Save(IAWBPlugin sender) { Save(sender.Name); }

            /* In the (perhaps unlikely) event we need to know the name of the plugin which calls these subroutines,
             * the code is here and ready to go. */
            public void SkipPage(string sender, string reason) { SkipPage(reason); }
            public void Start(string sender) { Start(); }
            public void Stop(string sender) { Stop(); }
            public void GetDiff(string sender) { GetDiff(); }
            public void GetPreview(string sender) { GetPreview(); }
            public void Save(string sender) { Save(); }
        #endregion

        /// <summary>
        /// Save List Box to a text file
        /// </summary>
        /// <param name="listbox"></param>
        public void SaveList(ListBox listbox)
        {
            try
            {
                StringBuilder strList = new StringBuilder("");
                StreamWriter sw;
                string strListFile;
                if (saveListDialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (String a in listbox.Items)
                        strList.AppendLine(a);
                    strListFile = saveListDialog.FileName;
                    sw = new StreamWriter(strListFile, false, Encoding.UTF8);
                    sw.Write(strList);
                    sw.Close();
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditBoxSaveTimer_Tick(object sender, EventArgs e)
        {
            if (!System.IO.Directory.Exists(Application.StartupPath + "\\EditBoxSaves"))
                System.IO.Directory.CreateDirectory(Application.StartupPath + "\\EditBoxSaves");

            saveEditBoxText(Application.StartupPath + "\\EditBoxSaves\\" + AutoSaveEditBoxFile);
        }

        private void saveEditBoxText(string path)
        {
            try
            {
                StreamWriter sw = new StreamWriter(path.ToString(), false, Encoding.UTF8);
                sw.Write(txtEdit.Text);
                sw.Close();
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void saveTextToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveListDialog.ShowDialog() == DialogResult.OK)
                saveEditBoxText(saveListDialog.FileName);
        }

        private void loadUserTalkWarnings()
        {
            string finalRegex = "\\{\\{ ?(template:)? ?((";
            Regex userTalkTemplate = new Regex(@"# \[\[Template:(.*?)\]\]");
            try
            {
                string text = "";
                try
                {
                    text = Tools.GetHTML(Variables.URLLong + "index.php?title=Wikipedia:AutoWikiBrowser/User talk templates&action=raw&ctype=text/plain&dontcountme=s", Encoding.UTF8);
                }
                catch
                {
                }
                foreach (Match m in userTalkTemplate.Matches(text))
                {
                    try
                    {
                        finalRegex = finalRegex + m.Groups[1].Value + "|";
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finalRegex = finalRegex.Trim('|') + ") ?(\\|.*?)?) ?\\}\\}";
            userTalkWarningsLoaded = true;
            userTalkTemplatesRegex = new Regex(finalRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private void undoAllChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtEdit.Text = TheArticle.OriginalArticleText;
        }

        private void reloadEditPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PageReload = true;
            webBrowserEdit.LoadEditPage(TheArticle.Name);
            TheArticle.OriginalArticleText = webBrowserEdit.GetArticleText();
        }

        private void recycleWebControl()
        {
            webBrowserEdit.Dispose();
            webBrowserEdit = new WebControl();
            
            webBrowserEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            webBrowserEdit.ArticleText = "";
            webBrowserEdit.Busy = false;
            webBrowserEdit.ContextMenuStrip = this.mnuWebBrowser;
            webBrowserEdit.DiffFontSize = 120;
            webBrowserEdit.EnhanceDiffEnabled = true;
            webBrowserEdit.IsWebBrowserContextMenuEnabled = false;
            webBrowserEdit.Location = new System.Drawing.Point(0, 25);
            webBrowserEdit.MinimumSize = new System.Drawing.Size(20, 20);
            webBrowserEdit.Name = "webBrowserEdit";
            webBrowserEdit.ProcessStage = WikiFunctions.Browser.enumProcessStage.none;
            webBrowserEdit.ScriptErrorsSuppressed = true;
            webBrowserEdit.ScrollDown = true;
            webBrowserEdit.Size = new System.Drawing.Size(788, 195);
            webBrowserEdit.TabIndex = 670;
            webBrowserEdit.TabStop = false;
            webBrowserEdit.TimeoutLimit = 30;
            webBrowserEdit.WebBrowserShortcutsEnabled = false;
            webBrowserEdit.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(this.webBrowserEdit_Navigating);
            webBrowserEdit.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(this.webBrowserEdit_DocumentCompleted);

            webBrowserEdit.Loaded += CaseWasLoad;
            webBrowserEdit.Diffed += CaseWasDiff;
            webBrowserEdit.Saved += CaseWasSaved;
            webBrowserEdit.None += CaseWasNull;
            webBrowserEdit.Fault += StartDelayedRestartTimer;
            webBrowserEdit.StatusChanged += UpdateWebBrowserStatus;

            webBrowserEdit.Visible = true;
            webBrowserEdit.Show();
            webBrowserEdit.BringToFront();
        }

        private void recycleWebControlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            recycleWebControl();
            Application.DoEvents();
        }

        private void chkSkipNonExistent_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSkipNonExistent.Checked)
                chkSkipExistent.Checked = false;
        }

        private void chkSkipExistent_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSkipExistent.Checked)
                chkSkipNonExistent.Checked = false;
        }
    }
}
