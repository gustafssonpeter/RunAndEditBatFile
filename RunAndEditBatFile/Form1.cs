﻿using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace DB_Updater
{
    public partial class Form1 : Form
    {
        string replaceFileLatestVersion, strFileProd, strFileHist, strSearch, strSearchResult, restoreToBaseBat,
            setupLocalTrunkBat, restoreAndToQF1bat;
        string myHostName = System.Net.Dns.GetHostName();
        int count, from, to, outputValue, rbState;
        bool isFirstRun, isNumberFrom, isNumberTo, isRestoreFromBase, isNotLocalServer, isRestored;

        string help =
@"To use this program you need to create the directory C:\Databaser on your computer. This directory need to be shared to the network.

You also need to put the unziped Database QF files in the direcory ""Path to folder of QF upgrade files""";

        string copyFiles =
@"
copy /Y \\profdoc.lab\dfs01\Gemensam\Test\Verktyg\unzip.exe c:\Databaser\unzip.exe
copy \\profdoc.lab\dfs01\Databaser\Test\Orginal\%VERSION%\P%FILE_VERSION%TCO_LATEST.bak c:\Databaser\P%FILE_VERSION%TCO_LATEST.bak
copy \\profdoc.lab\dfs01\Databaser\Test\Orginal\%VERSION%\H%FILE_VERSION%TCO_LATEST.bak c:\Databaser\H%FILE_VERSION%TCO_LATEST.bak";

        string upgradeToLatestTrunk =
@"
for /f ""delims="" %%i in (\\profdoc.lab\dfs01\System\Autobuild\dblatest.txt) do copy /Y ""\\profdoc.lab\dfs01\System\Autobuild\%%i.exe"" ""c:\databaser""
for /f ""delims="" %%i in (\\profdoc.lab\dfs01\System\Autobuild\dblatest.txt) do unzip ""c:\databaser\%%i.exe""
for /f ""delims="" %%i in (\\profdoc.lab\dfs01\System\Autobuild\dblatest.txt) do cd ""c:\databaser\%%i""
START /B ""Upgrade historic"" ""Upgrade Historic.bat"" SYSADM SYSADM %DATABASE_H% %CLIENT_UPGRADE%
START ""Upgrade production"" ""Upgrade Production.bat"" SYSADM SYSADM %DATABASE_P% %CLIENT_UPGRADE%";

        string runRestoreScript =
@"
cd ""c:\databaser""
sqlcmd -S %CLIENT% -d %DATABASE_P% -U SYSADM -P SYSADM -i DBupdate_restoreSQL.sql -o ""c:\databaser\DBupdate_restoreSQL.txt""";

        string upgradeFromQfToQf =
@"
cd ""%QF_PATH%""
START /B ""Upgrade historic"" ""Upgrade Historic From %VERSION% QF%FROM% To %VERSION% QF%TO%.bat"" SYSADM SYSADM %DATABASE_H% %CLIENT_UPGRADE%
START /B ""Upgrade production"" ""Upgrade Production From %VERSION% QF%FROM% To %VERSION% QF%TO%.bat"" SYSADM SYSADM %DATABASE_P% %CLIENT_UPGRADE% 
pause";

        string upgradeToQF1 =
@"
cd ""%QF_PATH%""
START /B ""Upgrade production"" ""Upgrade Production From %VERSION% To %VERSION% QF1.bat"" SYSADM SYSADM %DATABASE_P% %CLIENT_UPGRADE% 
START /B ""Upgrade historic"" ""Upgrade Historic From %VERSION% To %VERSION% QF1.bat"" SYSADM SYSADM %DATABASE_H% %CLIENT_UPGRADE% 
pause";

        string upgradeFromPath =
@"
cd ""%PATH%""
START /B ""Upgrade historic"" ""Upgrade Historic.bat"" SYSADM SYSADM %DATABASE_H% %CLIENT_UPGRADE% 
START ""Upgrade production"" ""Upgrade Production.bat"" SYSADM SYSADM %DATABASE_P% %CLIENT_UPGRADE% ";

        string sqlRestoreScript =
@"
use master
RESTORE DATABASE %DATABASE_P% FROM  DISK = N'%RESTORE_FILE_PROD%' WITH  FILE = 1,  NOUNLOAD,  REPLACE,  STATS = 10
GO
RESTORE DATABASE %DATABASE_H%  FROM  DISK = N'%RESTORE_FILE_HIST%' WITH  FILE = 1,  NOUNLOAD,  REPLACE,  STATS = 10
GO
use %DATABASE_P%
update databaser set dat_databas = '%DATABASE_P%', dat_orgdatabas='%DATABASE_P%', dat_servernamn='%CLIENT%' where dat_typ ='P'
update databaser set dat_databas = '%DATABASE_H%', dat_orgdatabas='%DATABASE_H%', dat_servernamn='%CLIENT%' where dat_typ ='H'
Exec GrantAnalytixPermissions
use %DATABASE_H%
Exec a_ResetHistProdUser";

        string sqlRestoreScriptProd =
@"
use master
RESTORE DATABASE %DATABASE_P% FROM  DISK = N'%RESTORE_FILE_PROD%' WITH  FILE = 1,  NOUNLOAD,  REPLACE,  STATS = 10
GO
use %DATABASE_P%
update databaser set dat_databas = '%DATABASE_P%', dat_orgdatabas='%DATABASE_P%', dat_servernamn='%CLIENT%' where dat_typ ='P'
Exec GrantAnalytixPermissions";

        public Form1()
        {
            InitializeComponent();
            loadSettings();
        }

        //Start button
        private void button1_Click(object sender, EventArgs e)
        {
            if (textBoxClient.Text.IndexOf(@"\") != -1)
                isNotLocalServer = true;
            else
                isNotLocalServer = false;

            if (radioButtonUpgradeQFdb.Checked)
            {
                rbState = 1;
                startQfUpgrade();
                saveSettings();
            }

            if (radioButtonRestoreToBase.Checked)
            {
                rbState = 2;
                restoreToBaseVersion();
                saveSettings();
            }

            if (radioButtonRestoreToTRUNK.Checked)
            {
                rbState = 3;
                restoreToTrunk();
                saveSettings();
            }

            if (radioButtonRestoreFromOtherFiles.Checked)
            {
                rbState = 4;
                restoreToOther();
                saveSettings();
            }

            if (rbUpgradeFromPath.Checked)
            {
                rbState = 5;
                upgradeFromPathFiles();
                saveSettings();
            }

            if (isRestored)
            {
                MessageBox.Show(File.ReadAllText("C:\\databaser\\DBupdate_restoreSQL.txt"), "Restore result!");
                isRestored = false;
            }
        }

        //Clear button
        private void button2_Click(object sender, EventArgs e)
        {
            textBoxFrom.Clear();
            textBoxTo.Clear();
            textBoxClient.Clear();
            textBoxDatabaseP.Clear();
            textBoxDatabaseH.Clear();
            textBoxVersion.Clear();
            textBoxPath.Clear();
            textBoxFileProd.Clear();
            textBoxFileHist.Clear();
            checkBoxRestoreDB.Checked = false;
            //radioButtonUpgradeQFdb.Checked = true;
            radioButtonRestoreToTRUNK.Checked = true;
            checkBoxRestoreDB.Focus();
            textBoxQfPath.Clear();
            checkBoxNotCopyFiles.Checked = false;
        }

        private void startQfUpgrade()
        {
            if (!String.IsNullOrEmpty(textBoxFrom.Text) && !String.IsNullOrEmpty(textBoxTo.Text) && !String.IsNullOrEmpty(textBoxVersion.Text)
                && !String.IsNullOrEmpty(textBoxClient.Text) && !String.IsNullOrEmpty(textBoxDatabaseP.Text) && !String.IsNullOrEmpty(textBoxDatabaseH.Text) && !String.IsNullOrEmpty(textBoxQfPath.Text))
            {
                isNumberFrom = int.TryParse(textBoxFrom.Text, out outputValue);
                isNumberTo = int.TryParse(textBoxTo.Text, out outputValue);

                if (!isNumberFrom || !isNumberTo)
                {
                    MessageBox.Show("Only numbers in QF From and QF To!");
                }
                else
                {
                    if (Directory.Exists(textBoxQfPath.Text))
                    {
                        count = int.Parse(textBoxTo.Text) - int.Parse(textBoxFrom.Text);
                        from = int.Parse(textBoxFrom.Text);
                        isFirstRun = true;

                        while (count > 0)
                        {
                            //if-sats för första körningen om restore av Db
                            if (textBoxFrom.Text == "0" && isFirstRun)
                            {
                                restoreAndToQF1bat = "";
                                isRestoreFromBase = true;
                                if (checkBoxRestoreDB.Checked == true)
                                {
                                    createFile("C:\\Databaser\\DBupdate_restoreSQL.sql", sqlRestoreScript);
                                    if (checkBoxNotCopyFiles.Checked == true)
                                        restoreAndToQF1bat = runRestoreScript + upgradeToQF1;
                                    else
                                        restoreAndToQF1bat = copyFiles + runRestoreScript + upgradeToQF1;
                                }
                                else
                                    restoreAndToQF1bat = runRestoreScript + upgradeToQF1;
                                to = 1;
                                createFile("C:\\Databaser\\DBupdate_RestoreQF.bat", restoreAndToQF1bat);
                                isRestored = true;
                                startFile("C:\\Databaser\\DBupdate_RestoreQF.bat");
                            }

                            //if-sats för resterande, eller första med bara upgrade av Db
                            if (!isRestoreFromBase)
                            {
                                if (textBoxFrom.Text == "0")
                                    from++;
                                to = from + 1;

                                createFile("C:\\Databaser\\DBupdate_QfToQf.bat", upgradeFromQfToQf);
                                startFile("C:\\Databaser\\DBupdate_QfToQf.bat");

                                if (textBoxFrom.Text != "0")
                                    from++;
                            }
                            count--;
                            isFirstRun = false;
                            isRestoreFromBase = false;
                        }
                    }
                    else
                        MessageBox.Show("The path " + textBoxQfPath.Text + " don't exists");
                }
            }
            else
                MessageBox.Show("Please enter:\rQF From, QF To, QF Path, Version, Client, Database Prod. and Database Hist.");
        }

        private void restoreToBaseVersion()
        {
            if (!String.IsNullOrEmpty(textBoxVersion.Text) && !String.IsNullOrEmpty(textBoxClient.Text) && !String.IsNullOrEmpty(textBoxDatabaseP.Text) && !String.IsNullOrEmpty(textBoxDatabaseH.Text))
            {
                restoreToBaseBat = "";
                if (checkBoxNotCopyFiles.Checked == true)
                    restoreToBaseBat = runRestoreScript;
                else
                    restoreToBaseBat = copyFiles + runRestoreScript;
                createFile("C:\\Databaser\\DBupdate_restoreSQL.sql", sqlRestoreScript);
                createFile("C:\\Databaser\\DBupdate_RestoreToBase.bat", restoreToBaseBat);
                isRestored = true;
                startFile("C:\\Databaser\\DBupdate_RestoreToBase.bat");
            }
            else
                MessageBox.Show("Please enter:\rVersion, Client, Database Prod. and Database Hist.");
        }

        private void restoreToTrunk()
        {
            if (!String.IsNullOrEmpty(textBoxVersion.Text) && !String.IsNullOrEmpty(textBoxClient.Text) && !String.IsNullOrEmpty(textBoxDatabaseP.Text) && !String.IsNullOrEmpty(textBoxDatabaseH.Text))
            {
                setupLocalTrunkBat = "";
                if (checkBoxNotCopyFiles.Checked == true)
                    setupLocalTrunkBat = runRestoreScript + upgradeToLatestTrunk;
                else
                    setupLocalTrunkBat = copyFiles + runRestoreScript + upgradeToLatestTrunk;
                createFile("C:\\Databaser\\DBupdate_restoreSQL.sql", sqlRestoreScript);
                createFile("C:\\Databaser\\DBupdate_SetupLocalTrunk.bat", setupLocalTrunkBat);
                isRestored = true;
                startFile("C:\\Databaser\\DBupdate_SetupLocalTrunk.bat");
            }
            else
                MessageBox.Show("Please enter:\rVersion, Client, Database Prod. and Database Hist.");
        }

        private void restoreToOther()
        {
            if (!String.IsNullOrEmpty(textBoxClient.Text) && (!String.IsNullOrEmpty(textBoxDatabaseP.Text) || !String.IsNullOrEmpty(textBoxDatabaseH.Text)) && (!String.IsNullOrEmpty(textBoxFileProd.Text) || !String.IsNullOrEmpty(textBoxFileHist.Text)))
            {
                if (File.Exists(textBoxFileHist.Text) || File.Exists(textBoxFileProd.Text))
                {
                    if (String.IsNullOrEmpty(textBoxDatabaseH.Text))
                        createFile("C:\\Databaser\\DBupdate_restoreSQL.sql", sqlRestoreScriptProd);
                    else
                        createFile("C:\\Databaser\\DBupdate_restoreSQL.sql", sqlRestoreScript);
                    createFile("C:\\Databaser\\DBupdate_RestoreToOtherFiles.bat", runRestoreScript);
                    isRestored = true;
                    startFile("C:\\Databaser\\DBupdate_RestoreToOtherFiles.bat");

                }
                else
                    MessageBox.Show("One (or both) of the backup files to restore from doesn't exists");
            }
            else
                MessageBox.Show("Please enter:\rClient, Database Prod, Database Hist\nFile to restore Prod and File to restore Hist");
        }

        private void upgradeFromPathFiles()
        {
            if (!String.IsNullOrEmpty(textBoxVersion.Text) && !String.IsNullOrEmpty(textBoxClient.Text) && !String.IsNullOrEmpty(textBoxDatabaseP.Text) && !String.IsNullOrEmpty(textBoxDatabaseH.Text) && !String.IsNullOrEmpty(textBoxPath.Text))
            {
                if (Directory.Exists(textBoxPath.Text))
                {
                    createFile("C:\\Databaser\\DBupdate_upgradeFromPath.bat", upgradeFromPath);
                    startFile("C:\\Databaser\\DBupdate_upgradeFromPath.bat");
                }
                else
                    MessageBox.Show("The directory " + textBoxPath.Text + " don't exists");
            }
            else
                MessageBox.Show("Please enter:\rClient, Database Prod, Database Hist and Path");
        }

        private void createFile(string filePath, string content)
        {
            if (textBoxVersion.Text.Contains("."))
                replaceFileLatestVersion = textBoxVersion.Text.Replace(".", "") + "0";
            if (textBoxVersion.Text.Contains(","))
                replaceFileLatestVersion = textBoxVersion.Text.Replace(",", "") + "0";

            if (radioButtonUpgradeQFdb.Checked == true)
            {
                strSearch = @"*database*" + textBoxVersion.Text.Replace(",", ".") + @"*" + "qf" + @"*" + to.ToString() + @"*";
                strSearchResult = getSubFolderPath(textBoxQfPath.Text, strSearch);
                content = content.Replace("%QF_PATH%", strSearchResult);
                content = content.Replace("%TO%", to.ToString());
                content = content.Replace("%FROM%", from.ToString());
            }

            content = content.Replace("%DATABASE_P%", textBoxDatabaseP.Text);
            content = content.Replace("%DATABASE_H%", textBoxDatabaseH.Text);
            content = content.Replace("%VERSION%", textBoxVersion.Text.Replace(",", "."));
            if (checkBoxNotCopyFiles.Checked == false)
                content = content.Replace("%FILE_VERSION%", replaceFileLatestVersion);
            if (rbUpgradeFromPath.Checked == true)
                content = content.Replace("%PATH%", textBoxPath.Text);

            if (radioButtonRestoreFromOtherFiles.Checked)
            {
                if (isNotLocalServer)
                {
                    content = content.Replace("%RESTORE_FILE_PROD%", @"\\" + myHostName + textBoxFileProd.Text.Remove(0, 2));
                    content = content.Replace("%RESTORE_FILE_HIST%", @"\\" + myHostName + textBoxFileHist.Text.Remove(0, 2));
                }
                else
                {
                    content = content.Replace("%RESTORE_FILE_PROD%", textBoxFileProd.Text);
                    content = content.Replace("%RESTORE_FILE_HIST%", textBoxFileHist.Text);
                }

            }
            else
            {
                if (isNotLocalServer)
                {
                    content = content.Replace("%RESTORE_FILE_PROD%", @"\\" + myHostName + @"\Databaser\P" + replaceFileLatestVersion + "TCO_LATEST.bak");
                    content = content.Replace("%RESTORE_FILE_HIST%", @"\\" + myHostName + @"\Databaser\H" + replaceFileLatestVersion + "TCO_LATEST.bak");
                }

                else
                {
                    content = content.Replace("%RESTORE_FILE_PROD%", @"C:\Databaser\P" + replaceFileLatestVersion + "TCO_LATEST.bak");
                    content = content.Replace("%RESTORE_FILE_HIST%", @"C:\Databaser\H" + replaceFileLatestVersion + "TCO_LATEST.bak");
                }
            }

            if (isNotLocalServer)
            {
                string str = textBoxClient.Text;
                str = str.Insert(str.IndexOf(@"\"), " ");

                content = content.Replace("%CLIENT_UPGRADE%", str);
                content = content.Replace("%CLIENT%", textBoxClient.Text);
            }

            else
            {
                content = content.Replace("%CLIENT_UPGRADE%", textBoxClient.Text);
                content = content.Replace("%CLIENT%", textBoxClient.Text);
            }

            StreamWriter writer = new StreamWriter(filePath);
            writer.Write(content);
            writer.Close();
        }

        private void loadSettings()
        {
            textBoxVersion.Text = MySettings.Default.version;
            textBoxClient.Text = MySettings.Default.client;
            textBoxDatabaseP.Text = MySettings.Default.dbp;
            textBoxDatabaseH.Text = MySettings.Default.dbh;
            textBoxPath.Text = MySettings.Default.path;
            textBoxFileProd.Text = MySettings.Default.restoreFileProd;
            textBoxFileHist.Text = MySettings.Default.restoreFileHist;
            textBoxQfPath.Text = MySettings.Default.qfPath;
            switch (MySettings.Default.rb)
            {
                case 1:
                    radioButtonUpgradeQFdb.Checked = true;
                    break;
                case 2:
                    radioButtonRestoreToBase.Checked = true;
                    break;
                case 3:
                    radioButtonRestoreToTRUNK.Checked = true;
                    break;
                case 4:
                    radioButtonRestoreFromOtherFiles.Checked = true;
                    break;
                case 5:
                    rbUpgradeFromPath.Checked = true;
                    break;
            }
        }

        private void saveSettings()
        {
            MySettings.Default.version = textBoxVersion.Text;
            MySettings.Default.client = textBoxClient.Text;
            MySettings.Default.dbp = textBoxDatabaseP.Text;
            MySettings.Default.dbh = textBoxDatabaseH.Text;
            MySettings.Default.path = textBoxPath.Text;
            MySettings.Default.restoreFileProd = textBoxFileProd.Text;
            MySettings.Default.restoreFileHist = textBoxFileHist.Text;
            MySettings.Default.rb = rbState;
            MySettings.Default.qfPath = textBoxQfPath.Text;
            MySettings.Default.Save();
        }

        private void buttonFolderPath_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.textBoxPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                strFileProd = openFileDialog1.FileName;
                this.textBoxFileProd.Text = strFileProd;
            }
        }

        private void buttonFileHist_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                strFileHist = openFileDialog1.FileName;
                this.textBoxFileHist.Text = strFileHist;
            }
        }

        private void buttonQfPath_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.textBoxQfPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(help);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Made by:\nPeter Gustafsson");
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            textBoxFrom.Enabled = true;
            textBoxTo.Enabled = true;
            textBoxVersion.Enabled = true;
            textBoxClient.Enabled = true;
            textBoxDatabaseP.Enabled = true;
            textBoxDatabaseH.Enabled = true;
            textBoxPath.Enabled = false;
            checkBoxRestoreDB.Enabled = true;
            label1.Enabled = true;
            label2.Enabled = true;
            label6.Enabled = true;
            label7.Enabled = false;
            buttonFolderPath.Enabled = false;
            labelFileProd.Enabled = false;
            labelFileHist.Enabled = false;
            textBoxFileProd.Enabled = false;
            textBoxFileHist.Enabled = false;
            buttonFileProd.Enabled = false;
            buttonFileHist.Enabled = false;
            checkBoxRestoreDB.Focus();
            labelQfPath.Enabled = true;
            textBoxQfPath.Enabled = true;
            buttonQfPath.Enabled = true;
            checkBoxNotCopyFiles.Enabled = true;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            textBoxFrom.Enabled = false;
            textBoxTo.Enabled = false;
            textBoxVersion.Enabled = true;
            textBoxClient.Enabled = true;
            textBoxDatabaseP.Enabled = true;
            textBoxDatabaseH.Enabled = true;
            textBoxPath.Enabled = false;
            checkBoxRestoreDB.Enabled = false;
            label1.Enabled = false;
            label2.Enabled = false;
            label6.Enabled = true;
            label7.Enabled = false;
            buttonFolderPath.Enabled = false;
            labelFileProd.Enabled = false;
            labelFileHist.Enabled = false;
            textBoxFileProd.Enabled = false;
            textBoxFileHist.Enabled = false;
            buttonFileProd.Enabled = false;
            buttonFileHist.Enabled = false;
            textBoxVersion.Focus();
            labelQfPath.Enabled = false;
            textBoxQfPath.Enabled = false;
            buttonQfPath.Enabled = false;
            checkBoxNotCopyFiles.Enabled = true;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            textBoxFrom.Enabled = false;
            textBoxTo.Enabled = false;
            textBoxVersion.Enabled = true;
            textBoxClient.Enabled = true;
            textBoxDatabaseP.Enabled = true;
            textBoxDatabaseH.Enabled = true;
            textBoxPath.Enabled = false;
            checkBoxRestoreDB.Enabled = false;
            label1.Enabled = false;
            label2.Enabled = false;
            label6.Enabled = true;
            label7.Enabled = false;
            buttonFolderPath.Enabled = false;
            labelFileProd.Enabled = false;
            labelFileHist.Enabled = false;
            textBoxFileProd.Enabled = false;
            textBoxFileHist.Enabled = false;
            buttonFileProd.Enabled = false;
            buttonFileHist.Enabled = false;
            textBoxVersion.Focus();
            labelQfPath.Enabled = false;
            textBoxQfPath.Enabled = false;
            buttonQfPath.Enabled = false;
            checkBoxNotCopyFiles.Enabled = true;
        }

        private void radioButtonRestoreOtherFiles_CheckedChanged(object sender, EventArgs e)
        {
            textBoxFrom.Enabled = false;
            textBoxTo.Enabled = false;
            textBoxVersion.Enabled = false;
            textBoxClient.Enabled = true;
            textBoxDatabaseP.Enabled = true;
            textBoxDatabaseH.Enabled = true;
            textBoxPath.Enabled = false;
            checkBoxRestoreDB.Enabled = false;
            label1.Enabled = false;
            label2.Enabled = false;
            label6.Enabled = false;
            label7.Enabled = false;
            buttonFolderPath.Enabled = false;
            labelFileProd.Enabled = true;
            labelFileHist.Enabled = true;
            textBoxFileProd.Enabled = true;
            textBoxFileHist.Enabled = true;
            buttonFileProd.Enabled = true;
            buttonFileHist.Enabled = true;
            textBoxVersion.Focus();
            labelQfPath.Enabled = false;
            textBoxQfPath.Enabled = false;
            buttonQfPath.Enabled = false;
            checkBoxNotCopyFiles.Enabled = false;
        }

        private void rbUpgradeFromPath_CheckedChanged(object sender, EventArgs e)
        {
            textBoxFrom.Enabled = false;
            textBoxTo.Enabled = false;
            textBoxVersion.Enabled = false;
            textBoxClient.Enabled = true;
            textBoxDatabaseP.Enabled = true;
            textBoxDatabaseH.Enabled = true;
            textBoxPath.Enabled = true;
            checkBoxRestoreDB.Enabled = false;
            label1.Enabled = false;
            label2.Enabled = false;
            label6.Enabled = false;
            label7.Enabled = true;
            buttonFolderPath.Enabled = true;
            labelFileProd.Enabled = false;
            labelFileHist.Enabled = false;
            textBoxFileProd.Enabled = false;
            textBoxFileHist.Enabled = false;
            buttonFileProd.Enabled = false;
            buttonFileHist.Enabled = false;
            textBoxVersion.Focus();
            labelQfPath.Enabled = false;
            textBoxQfPath.Enabled = false;
            buttonQfPath.Enabled = false;
            checkBoxNotCopyFiles.Enabled = false;
        }

        static public void startFile(string file)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = @"/C " + file;
            proc.Start();
            proc.WaitForExit();
            proc.Close();
        }

        private string getSubFolderPath(string strPath, string SearchString)
        {
            try
            {
                string path = "";
                string[] dirs = Directory.GetDirectories(strPath, SearchString);
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (string dir in dirs)
                {
                    string normalized1 = SearchString.Replace("*", "");
                    string normalized2 = dir.Replace(" ", "");
                    if (normalized2.ToLower().Contains(normalized1.ToLower()))
                        path = dir;
                }
                return path;
            }
            catch (Exception ee)
            {
                MessageBox.Show(ee.ToString());
                return null;
            }
        }
    }
}