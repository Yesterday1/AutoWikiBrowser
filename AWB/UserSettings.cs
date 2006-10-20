using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using WikiFunctions;
using WikiFunctions.Plugin;
using WikiFunctions.AWBSettings;

namespace AutoWikiBrowser
{
    public partial class MainForm
    {
        private void saveAsDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SavePrefs();
        }

        private void saveSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveXML.ShowDialog() != DialogResult.OK)
                return;

            SavePrefs(saveXML.FileName);
        }

        private void loadSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loadSettingsDialog();
        }

        private void loadDefaultSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetSettings();
        }

        private void ResetSettings()
        {
            findAndReplace.Clear();
            replaceSpecial.Clear();
            listMaker1.SelectedSource = 0;
            listMaker1.SourceText = "";

            chkGeneralFixes.Checked = true;
            chkAutoTagger.Checked = true;
            chkUnicodifyWhole.Checked = true;

            chkFindandReplace.Checked = false;
            chkSkipWhenNoFAR.Checked = true;
            findAndReplace.ignoreLinks = false;
            findAndReplace.AppendToSummary = true;
            findAndReplace.AfterOtherFixes = false;

            cmboCategorise.SelectedIndex = 0;
            txtNewCategory.Text = "";

            chkSkipIfContains.Checked = false;
            chkSkipIfNotContains.Checked = false;
            chkSkipIsRegex.Checked = false;
            chkSkipCaseSensitive.Checked = false;
            txtSkipIfContains.Text = "";
            txtSkipIfNotContains.Text = "";
            Skip.SelectedItem = "0";

            chkAppend.Checked = false;
            rdoAppend.Checked = true;
            txtAppendMessage.Text = "";

            cmboImages.SelectedIndex = 0;
            txtImageReplace.Text = "";
            txtImageWith.Text = "";

            chkRegExTypo.Checked = false;
            chkSkipIfNoRegexTypo.Checked = false;

            txtFind.Text = "";
            chkFindRegex.Checked = false;
            chkFindCaseSensitive.Checked = false;

            cmboEditSummary.SelectedIndex = 0;

            wordWrapToolStripMenuItem1.Checked = true;
            panel2.Show();
            enableToolBar = false;
            bypassRedirectsToolStripMenuItem.Checked = true;
            chkSkipNonExistent.Checked = true;
            doNotAutomaticallyDoAnythingToolStripMenuItem.Checked = false;
            chkSkipNoChanges.Checked = false;
            previewInsteadOfDiffToolStripMenuItem.Checked = false;
            markAllAsMinorToolStripMenuItem.Checked = false;
            addAllToWatchlistToolStripMenuItem.Checked = false;
            showTimerToolStripMenuItem.Checked = false;
            alphaSortInterwikiLinksToolStripMenuItem.Checked = true;
            addIgnoredToLogFileToolStripMenuItem.Checked = false;

            PasteMore1.Text = "";
            PasteMore2.Text = "";
            PasteMore3.Text = "";
            PasteMore4.Text = "";
            PasteMore5.Text = "";
            PasteMore6.Text = "";
            PasteMore7.Text = "";
            PasteMore8.Text = "";
            PasteMore9.Text = "";
            PasteMore10.Text = "";

            chkAutoMode.Checked = false;
            chkQuickSave.Checked = false;
            nudBotSpeed.Value = 15;

            //preferences
            webBrowserEdit.EnhanceDiffEnabled = true;
            webBrowserEdit.ScrollDown = true;
            webBrowserEdit.DiffFontSize = 150;
            System.Drawing.Font f = new System.Drawing.Font("Courier New", 10F, System.Drawing.FontStyle.Regular);
            txtEdit.Font = f;
            LowThreadPriority = false;
            FlashAndBeep = true;

            try
            {
                foreach (KeyValuePair<string, IAWBPlugin> a in AWBPlugins)
                    a.Value.Reset();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Problem reseting plugin\r\n\r\n" + ex.Message);
            }

            cModule.ModuleEnabled = false;

            lblStatusText.Text = "Default settings loaded.";
        }

        private void loadSettingsDialog()
        {
            if (openXML.ShowDialog() != DialogResult.OK)
                return;

            LoadPrefs(openXML.FileName);
        }

        [Obsolete]
        private void loadSettings(Stream stream)
        {
            try
            {
                findAndReplace.Clear();
                cmboEditSummary.Items.Clear();

                using (XmlTextReader reader = new XmlTextReader(stream))
                {
                    reader.WhitespaceHandling = WhitespaceHandling.None;
                    while (reader.Read())
                    {
                        if (reader.Name == "findandreplacesettings" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                chkFindandReplace.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("ignorenofar"))
                                chkSkipWhenNoFAR.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("ignoretext"))
                                findAndReplace.ignoreLinks = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("appendsummary"))
                                findAndReplace.AppendToSummary = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("afterotherfixes"))
                                findAndReplace.AfterOtherFixes = bool.Parse(reader.Value);

                            continue;
                        }

                        if (reader.Name == "FindAndReplace" && reader.HasAttributes)
                        {
                            string find = "";
                            string replace = "";
                            bool regex = true;
                            bool casesens = true;
                            bool multi = false;
                            bool single = false;
                            int times = -1;
                            bool enabled = true;

                            if (reader.MoveToAttribute("find"))
                                find = reader.Value;
                            if (reader.MoveToAttribute("replacewith"))
                                replace = reader.Value;

                            if (reader.MoveToAttribute("casesensitive"))
                                casesens = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("regex"))
                                regex = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("multi"))
                                multi = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("single"))
                                single = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("enabled"))
                                enabled = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("maxnumber"))
                                times = int.Parse(reader.Value);

                            if (find.Length > 0)
                                findAndReplace.AddNew(find, replace, casesens, regex, multi, single, times, enabled);

                            continue;
                        }

                        if (reader.Name == WikiFunctions.MWB.ReplaceSpecial.XmlName)
                        {
                            bool enabled = false;
                            replaceSpecial.ReadFromXml(reader, ref enabled);
                            continue;
                        }

                        if (reader.Name == "projectlang" && reader.HasAttributes)
                        {
                            string project = "";
                            string language = "";
                            string customproject = "";

                            if (reader.MoveToAttribute("proj"))
                                project = reader.Value;
                            if (reader.MoveToAttribute("lang"))
                                language = reader.Value;
                            if (reader.MoveToAttribute("custom"))
                                customproject = reader.Value;

                            LangCodeEnum l = (LangCodeEnum)Enum.Parse(typeof(LangCodeEnum), language);
                            ProjectEnum p = (ProjectEnum)Enum.Parse(typeof(ProjectEnum), project);

                            SetProject(l, p, customproject);

                            continue;
                        }
                        if (reader.Name == "selectsource" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("index"))
                                listMaker1.SelectedSource = (WikiFunctions.Lists.SourceType)int.Parse(reader.Value);
                            if (reader.MoveToAttribute("text"))
                                listMaker1.SourceText = reader.Value;

                            continue;
                        }
                        if (reader.Name == "general" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("general"))
                                chkGeneralFixes.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("tagger"))
                                chkAutoTagger.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("unicodifyer"))
                                chkUnicodifyWhole.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "categorisation" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("index"))
                                cmboCategorise.SelectedIndex = int.Parse(reader.Value);
                            if (reader.MoveToAttribute("text"))
                                txtNewCategory.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "skip" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("does"))
                                chkSkipIfContains.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("doesnot"))
                                chkSkipIfNotContains.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("regex"))
                                chkSkipIsRegex.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("casesensitive"))
                                chkSkipCaseSensitive.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("doestext"))
                                txtSkipIfContains.Text = reader.Value;
                            if (reader.MoveToAttribute("doesnottext"))
                                txtSkipIfNotContains.Text = reader.Value;
                            if (reader.MoveToAttribute("moreindex"))
                                Skip.SelectedItem = reader.Value;

                            continue;
                        }
                        if (reader.Name == "message" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                chkAppend.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("text"))
                                txtAppendMessage.Text = reader.Value;
                            if (reader.MoveToAttribute("append"))
                                rdoAppend.Checked = bool.Parse(reader.Value);
                            rdoPrepend.Checked = !bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "automode" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("delay"))
                                nudBotSpeed.Value = int.Parse(reader.Value);
                            if (reader.MoveToAttribute("quicksave"))
                                chkQuickSave.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("suppresstag"))
                                chkSuppressTag.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "imager" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("index"))
                                cmboImages.SelectedIndex = int.Parse(reader.Value);
                            if (reader.MoveToAttribute("replace"))
                                txtImageReplace.Text = reader.Value;
                            if (reader.MoveToAttribute("with"))
                                txtImageWith.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "regextypofixproperties" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                chkRegExTypo.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("skipnofixed"))
                                chkSkipIfNoRegexTypo.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "find" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                txtFind.Text = reader.Value;
                            if (reader.MoveToAttribute("regex"))
                                chkFindRegex.Checked = bool.Parse(reader.Value);
                            if (reader.MoveToAttribute("casesensitive"))
                                chkFindCaseSensitive.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "summary" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                if (!cmboEditSummary.Items.Contains(reader.Value) && reader.Value.Length > 0)
                                    cmboEditSummary.Items.Add(reader.Value);

                            continue;
                        }
                        if (reader.Name == "summaryindex" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("index"))
                                cmboEditSummary.Text = reader.Value;

                            continue;
                        }

                        //menu
                        if (reader.Name == "wordwrap" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                wordWrapToolStripMenuItem1.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "toolbar" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                enableToolBar = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "bypass" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                bypassRedirectsToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "ingnorenonexistent" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                chkSkipNonExistent.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "noautochanges" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                doNotAutomaticallyDoAnythingToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "skipnochanges" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                chkSkipNoChanges.Checked = bool.Parse(reader.Value);
                        }
                        if (reader.Name == "preview" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                previewInsteadOfDiffToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "minor" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                markAllAsMinorToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "watch" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                addAllToWatchlistToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "timer" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                showTimerToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "sortinterwiki" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                alphaSortInterwikiLinksToolStripMenuItem.Checked = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "addignoredtolog" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enabled"))
                                addIgnoredToLogFileToolStripMenuItem.Checked = bool.Parse(reader.Value);
                            btnFalsePositive.Visible = bool.Parse(reader.Value);

                            continue;
                        }
                        if (reader.Name == "pastemore1" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore1.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore2" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore2.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore3" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore3.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore4" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore4.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore5" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore5.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore6" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore6.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore7" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore7.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore8" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore8.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore9" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore9.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "pastemore10" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("text"))
                                PasteMore10.Text = reader.Value;

                            continue;
                        }
                        if (reader.Name == "preferencevalues" && reader.HasAttributes)
                        {
                            if (reader.MoveToAttribute("enhancediff"))
                                webBrowserEdit.EnhanceDiffEnabled = bool.Parse(reader.Value);

                            if (reader.MoveToAttribute("scrolldown"))
                                webBrowserEdit.ScrollDown = bool.Parse(reader.Value);

                            if (reader.MoveToAttribute("difffontsize"))
                                webBrowserEdit.DiffFontSize = int.Parse(reader.Value);

                            float s = 10F;
                            string d = "Courier New";
                            if (reader.MoveToAttribute("textboxfontsize"))
                                s = float.Parse(reader.Value);
                            if (reader.MoveToAttribute("textboxfont"))
                                d = reader.Value;
                            System.Drawing.Font f = new System.Drawing.Font(d, s);
                            txtEdit.Font = f;

                            if (reader.MoveToAttribute("lowthreadpriority"))
                                LowThreadPriority = bool.Parse(reader.Value);

                            if (reader.MoveToAttribute("flashandbeep"))
                                FlashAndBeep = bool.Parse(reader.Value);

                            continue;
                        }

                        //foreach (IAWBPlugin a in AWBPlugins)
                        //{
                        //    if (reader.Name == a.Name.Replace(' ', '_') && reader.HasAttributes)
                        //    {
                        //        a.LoadSettings(reader);
                        //        break;
                        //    }
                        //}

                    }
                    stream.Close();
                    findAndReplace.MakeList();
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateRecentList(string[] list)
        {
            RecentList.Clear();
            RecentList.AddRange(list);
            UpdateRecentSettingsMenu();
        }

        public void UpdateRecentList(string s)
        {
            int i = RecentList.IndexOf(s);

            if (i >= 0) RecentList.RemoveAt(i);

            RecentList.Insert(0, s);
            UpdateRecentSettingsMenu();
        }

        public void LoadRecentSettingsList()
        {
            string s;

            try
            {
                Microsoft.Win32.RegistryKey reg = Microsoft.Win32.Registry.CurrentUser.
                    OpenSubKey("Software\\Wikipedia\\AutoWikiBrowser");

                s = reg.GetValue("RecentList", "").ToString();
            }
            catch
            {
                return;
            }
            UpdateRecentList(s.Split('|'));
        }

        private void UpdateRecentSettingsMenu()
        {
            while (RecentList.Count > 5)
                RecentList.RemoveAt(5);

            recentToolStripMenuItem.DropDown.Items.Clear();
            foreach (string filename in RecentList)
            {
                ToolStripItem item = recentToolStripMenuItem.DropDownItems.Add(filename);
                item.Click += RecentSettingsClick;
            }
        }

        public void SaveRecentSettingsList()
        {
            Microsoft.Win32.RegistryKey reg = Microsoft.Win32.Registry.CurrentUser.
                    CreateSubKey("Software\\Wikipedia\\AutoWikiBrowser");

            string list = "";
            foreach (string s in RecentList)
            {
                if (list != "") list += "|";
                list += s;
            }

            reg.SetValue("RecentList", list);
        }

        private void RecentSettingsClick(object sender, EventArgs e)
        {
            LoadPrefs((sender as ToolStripItem).Text);
        }

        //new methods, using serialization

        /// <summary>
        /// Make preferences object from current settings
        /// </summary>
        private UserPrefs MakePrefs()
        {
            UserPrefs p = new UserPrefs();

            p.LanguageCode = Variables.LangCode;
            p.Project = Variables.Project;
            p.CustomProject = Variables.CustomProject;

            p.FindAndReplace.Enabled = chkFindandReplace.Checked;
            p.FindAndReplace.IgnoreSomeText = findAndReplace.ignoreLinks;
            p.FindAndReplace.AppendSummary = findAndReplace.AppendToSummary;
            p.FindAndReplace.Replacements = findAndReplace.GetList();
            p.FindAndReplace.AdvancedReps = replaceSpecial.GetRules();
            
            p.List.ListSource = listMaker1.SourceText;
            p.List.Source = listMaker1.SelectedSource;
            p.List.ArticleList = listMaker1.GetArticleList();


            p.Editprefs.GeneralFixes = chkGeneralFixes.Checked;
            p.Editprefs.Tagger = chkAutoTagger.Checked;
            p.Editprefs.Unicodify = chkUnicodifyWhole.Checked;

            p.Editprefs.Recategorisation = cmboCategorise.SelectedIndex;
            p.Editprefs.NewCategory = txtNewCategory.Text;

            p.Editprefs.ReImage = cmboImages.SelectedIndex;
            p.Editprefs.ImageFind = txtImageReplace.Text;
            p.Editprefs.Replace = txtImageWith.Text;


            p.Editprefs.AppendText = chkAppend.Checked;
            p.Editprefs.Append = rdoAppend.Checked;
            p.Editprefs.Append = !rdoPrepend.Checked;
            p.Editprefs.Text = txtAppendMessage.Text;

            p.Editprefs.AutoDelay = (int)nudBotSpeed.Value;
            p.Editprefs.QuickSave = chkQuickSave.Checked;
            p.Editprefs.SuppressTag = chkSuppressTag.Checked;

            p.Editprefs.RegexTypoFix = chkRegExTypo.Checked;


            p.Skipoptions.SkipNonexistent = chkSkipNonExistent.Checked;
            p.Skipoptions.SkipWhenNoChanges = chkSkipNoChanges.Checked;

            p.Skipoptions.SkipDoes = chkSkipIfContains.Checked;
            p.Skipoptions.SkipDoesNot = chkSkipIfNotContains.Checked;

            p.Skipoptions.SkipDoesText = txtSkipIfContains.Text;
            p.Skipoptions.SkipDoesNotText = txtSkipIfNotContains.Text;

            p.Skipoptions.Regex = chkSkipIsRegex.Checked;
            p.Skipoptions.CaseSensitive = chkSkipCaseSensitive.Checked;

            p.Skipoptions.SkipNoFindAndReplace = chkSkipWhenNoFAR.Checked;
            p.Skipoptions.SkipNoRegexTypoFix = chkSkipIfNoRegexTypo.Checked;
            p.Skipoptions.GeneralSkip = Skip.SelectedItem;


            foreach (object s in cmboEditSummary.Items)
                p.General.Summaries.Add(s.ToString());

            p.General.PasteMore[0] = PasteMore1.Text;
            p.General.PasteMore[1] = PasteMore2.Text;
            p.General.PasteMore[2] = PasteMore3.Text;
            p.General.PasteMore[3] = PasteMore4.Text;
            p.General.PasteMore[4] = PasteMore5.Text;
            p.General.PasteMore[5] = PasteMore6.Text;
            p.General.PasteMore[6] = PasteMore7.Text;
            p.General.PasteMore[7] = PasteMore8.Text;
            p.General.PasteMore[8] = PasteMore9.Text;
            p.General.PasteMore[9] = PasteMore10.Text;


            p.General.FindText = txtFind.Text;
            p.General.FindRegex = chkFindRegex.Checked;
            p.General.FindCaseSensitive = chkFindCaseSensitive.Checked;


            p.General.WordWrap = wordWrapToolStripMenuItem1.Checked;
            p.General.ToolBarEnabled = enableTheToolbarToolStripMenuItem.Checked;
            p.General.BypassRedirect = bypassRedirectsToolStripMenuItem.Checked;
            p.General.NoAutoChanges = doNotAutomaticallyDoAnythingToolStripMenuItem.Checked;
            p.General.Preview = previewInsteadOfDiffToolStripMenuItem.Checked;
            p.General.Minor = markAllAsMinorToolStripMenuItem.Checked;
            p.General.Watch = addAllToWatchlistToolStripMenuItem.Checked;
            p.General.TimerEnabled = showTimerToolStripMenuItem.Checked;
            p.General.SortInterwikiOrder = sortAlphabeticallyToolStripMenuItem.Checked;
            p.General.AddIgnoredToLog = addIgnoredToLogFileToolStripMenuItem.Checked;

            p.General.EnhancedDiff = webBrowserEdit.EnhanceDiffEnabled;
            p.General.ScrollDown = webBrowserEdit.ScrollDown;
            p.General.DiffFontSize = webBrowserEdit.DiffFontSize;

            p.General.TextBoxFont = txtEdit.Font.Name;
            p.General.TextBoxSize = (int)txtEdit.Font.Size;

            p.General.LowThreadPriority = LowThreadPriority;
            p.General.FlashAndBeep = FlashAndBeep;


            p.Module.Enabled = cModule.ModuleEnabled;
            p.Module.Language = cModule.Language;
            p.Module.Code = cModule.Code;

            foreach (KeyValuePair<string, IAWBPlugin> a in AWBPlugins)
            {
                PluginPrefs pp = new PluginPrefs();
                pp.Name = a.Key;
                pp.PluginSettings = a.Value.SaveSettings();

                p.Plugin.Add(pp);
            }

            return p;
        }

        /// <summary>
        /// Load preferences object
        /// </summary>
        private void LoadPrefs(UserPrefs p)
        {
            SetProject(p.LanguageCode, p.Project, p.CustomProject);

            chkFindandReplace.Checked = p.FindAndReplace.Enabled;
            findAndReplace.ignoreLinks = p.FindAndReplace.IgnoreSomeText;
            findAndReplace.AppendToSummary = p.FindAndReplace.AppendSummary;
            findAndReplace.AddNew(p.FindAndReplace.Replacements);
            replaceSpecial.AddNewRule(p.FindAndReplace.AdvancedReps);

            listMaker1.SourceText = p.List.ListSource;
            listMaker1.SelectedSource = p.List.Source;
            listMaker1.Add(p.List.ArticleList);


            chkGeneralFixes.Checked = p.Editprefs.GeneralFixes;
            chkAutoTagger.Checked = p.Editprefs.Tagger;
            chkUnicodifyWhole.Checked = p.Editprefs.Unicodify;

            cmboCategorise.SelectedIndex = p.Editprefs.Recategorisation;
            txtNewCategory.Text = p.Editprefs.NewCategory;

            cmboImages.SelectedIndex = p.Editprefs.ReImage;
            txtImageReplace.Text = p.Editprefs.ImageFind;
            txtImageWith.Text = p.Editprefs.Replace;

            chkAppend.Checked = p.Editprefs.AppendText;
            rdoAppend.Checked = p.Editprefs.Append;
            rdoPrepend.Checked = !p.Editprefs.Append;
            txtAppendMessage.Text = p.Editprefs.Text;

            nudBotSpeed.Value = p.Editprefs.AutoDelay;
            chkQuickSave.Checked = p.Editprefs.QuickSave;
            chkSuppressTag.Checked = p.Editprefs.SuppressTag;

            chkRegExTypo.Checked = p.Editprefs.RegexTypoFix;


            chkSkipNonExistent.Checked = p.Skipoptions.SkipNonexistent;
            chkSkipNoChanges.Checked = p.Skipoptions.SkipWhenNoChanges;

            chkSkipIfContains.Checked = p.Skipoptions.SkipDoes;
            chkSkipIfNotContains.Checked = p.Skipoptions.SkipDoesNot;

            txtSkipIfContains.Text = p.Skipoptions.SkipDoesText;
            txtSkipIfNotContains.Text = p.Skipoptions.SkipDoesNotText;

            chkSkipIsRegex.Checked = p.Skipoptions.Regex;
            chkSkipCaseSensitive.Checked = p.Skipoptions.CaseSensitive;

            chkSkipWhenNoFAR.Checked = p.Skipoptions.SkipNoFindAndReplace;
            chkSkipIfNoRegexTypo.Checked = p.Skipoptions.SkipNoRegexTypoFix;
            Skip.SelectedItem = p.Skipoptions.GeneralSkip;

            foreach (string s in p.General.Summaries)
            {
                if (!cmboEditSummary.Items.Contains(s))
                    cmboEditSummary.Items.Add(s);
            }

            PasteMore1.Text = p.General.PasteMore[0];
            PasteMore2.Text = p.General.PasteMore[1];
            PasteMore3.Text = p.General.PasteMore[2];
            PasteMore4.Text = p.General.PasteMore[3];
            PasteMore5.Text = p.General.PasteMore[4];
            PasteMore6.Text = p.General.PasteMore[5];
            PasteMore7.Text = p.General.PasteMore[6];
            PasteMore8.Text = p.General.PasteMore[7];
            PasteMore9.Text = p.General.PasteMore[8];
            PasteMore10.Text = p.General.PasteMore[9];


            txtFind.Text = p.General.FindText;
            chkFindRegex.Checked = p.General.FindRegex;
            chkFindCaseSensitive.Checked = p.General.FindCaseSensitive;

            wordWrapToolStripMenuItem1.Checked = p.General.WordWrap;
            enableTheToolbarToolStripMenuItem.Checked = p.General.ToolBarEnabled;
            bypassRedirectsToolStripMenuItem.Checked = p.General.BypassRedirect;
            doNotAutomaticallyDoAnythingToolStripMenuItem.Checked = p.General.NoAutoChanges;
            previewInsteadOfDiffToolStripMenuItem.Checked = p.General.Preview;
            markAllAsMinorToolStripMenuItem.Checked = p.General.Minor;
            addAllToWatchlistToolStripMenuItem.Checked = p.General.Watch;
            showTimerToolStripMenuItem.Checked = p.General.TimerEnabled;
            sortAlphabeticallyToolStripMenuItem.Checked = p.General.SortInterwikiOrder;
            addIgnoredToLogFileToolStripMenuItem.Checked = p.General.AddIgnoredToLog;

            webBrowserEdit.EnhanceDiffEnabled = p.General.EnhancedDiff;
            webBrowserEdit.ScrollDown = p.General.ScrollDown;
            webBrowserEdit.DiffFontSize = p.General.DiffFontSize;

            System.Drawing.Font f = new System.Drawing.Font(p.General.TextBoxFont, p.General.TextBoxSize);
            txtEdit.Font = f;

            LowThreadPriority = p.General.LowThreadPriority;
            FlashAndBeep = p.General.FlashAndBeep;


            cModule.ModuleEnabled = p.Module.Enabled;
            cModule.Language = p.Module.Language;
            cModule.Code = p.Module.Code;

            foreach (PluginPrefs pp in p.Plugin)
            {
                if (AWBPlugins.ContainsKey(pp.Name))
                    AWBPlugins[pp.Name].LoadSettings(pp.PluginSettings);
            }
        }

        /// <summary>
        /// Save preferences as default
        /// </summary>
        private void SavePrefs()
        {
            SavePrefs("Default.xml");
        }

        /// <summary>
        /// Save preferences to file
        /// </summary>
        private void SavePrefs(string Path)
        {
            try
            {                
                using (FileStream fStream = new FileStream(Path, FileMode.Open, FileAccess.Read))
                {
                    UserPrefs P = MakePrefs();
                    XmlSerializer xs = new XmlSerializer(typeof(UserPrefs));
                    xs.Serialize(fStream, P);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error saving settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Load default preferences
        /// </summary>
        private void LoadPrefs()
        {
            if (!File.Exists("Default.xml"))
                return;

            LoadPrefs("Default.xml");
        }

        /// <summary>
        /// Load preferences from file
        /// </summary>
        private void LoadPrefs(string Path)
        {
            try
            {
                using (FileStream fStream = new FileStream(Path, FileMode.Open, FileAccess.Read))
                {
                    //todo
                    //test file to see if it is an old AWB file
                    //clear old settings.

                    UserPrefs p;
                    XmlSerializer xs = new XmlSerializer(typeof(UserPrefs));
                    p = (UserPrefs)xs.Deserialize(fStream);
                    LoadPrefs(p);                    
                }

                SettingsFile = " - " + Path.Remove(0, Path.LastIndexOf("\\") + 1);
                this.Text = "AutoWikiBrowser" + SettingsFile;
                lblStatusText.Text = "Settings successfully loaded";
                UpdateRecentList(Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
