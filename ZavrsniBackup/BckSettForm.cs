using System;
using System.Configuration;
using System.Windows.Forms;

namespace ZavrsniBackup
{
    public partial class BckSettForm : Form
    {
        public BckSettForm()
        {
            InitializeComponent();
            txtSP.Text = ConfigurationManager.AppSettings["sourceDirectory"];
            txtDP.Text = ConfigurationManager.AppSettings["destinationDirectory"];
        }

        // Source browse
        private void btnSP_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            dlg.Description = "Choose the source directory for your backup!";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtSP.Text = dlg.SelectedPath;
            }
        }

        // Destination browse
        private void btnDP_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            dlg.Description = "Choose the destination directory for your backup!";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtDP.Text = dlg.SelectedPath;
            }
        }

        // OK button
        private void btnOK_Click(object sender, EventArgs e)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings["sourceDirectory"].Value = txtSP.Text;
            configuration.AppSettings.Settings["destinationDirectory"].Value = txtDP.Text;
            configuration.Save();

            ConfigurationManager.RefreshSection("appSettings");
            Close();
            Enabled = false;
        }

        // X button
        private void BckSettForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Enabled = false;
        }
    }
}
