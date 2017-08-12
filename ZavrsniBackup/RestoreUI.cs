using System;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.IO;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.IO.Compression;

namespace ZavrsniBackup
{
    public partial class RestoreUI : Form
    {
        SQLiteConnection connection = new SQLiteConnection(ConfigurationManager.ConnectionStrings["db"].ConnectionString);
        BckSettForm form;

        public RestoreUI()
        {
            InitializeComponent();
        }

        // Menu stuff
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("A file backup and restore application made by\nBorna Jelić as a part of his bachelor thesis. 2017", "About");
        }

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (connection.State != ConnectionState.Open)
            {
                try
                {
                    connection.Open();
                    MessageBox.Show("Connection successful!");
                }
                catch (Exception)
                {
                    MessageBox.Show("Cannot connect to database. Shutting down...", "Error");
                    Application.Exit();
                }
            }

            const string SqlCommand = "SELECT * FROM Files;";
            var dataA = new SQLiteDataAdapter(SqlCommand, connection);
            DataSet dataS = new DataSet();
            dataA.Fill(dataS);
            dataGridView1.DataSource = dataS.Tables[0].DefaultView;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }
            Application.Exit();
        }

        // Files tablica
        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            int selectedrowindex = dataGridView1.SelectedCells[0].RowIndex;
            DataGridViewRow selectedRow = dataGridView1.Rows[selectedrowindex];
            string tempid = Convert.ToString(selectedRow.Cells[0].Value);

            string SqlCommand = "SELECT * FROM Files_versions WHERE ID_file='" + tempid + "';";
            var dataA = new SQLiteDataAdapter(SqlCommand, connection);
            DataSet dataS = new DataSet();
            dataA.Fill(dataS);
            dataGridView2.DataSource = dataS.Tables[0].DefaultView;
            dataGridView1.AutoResizeColumns();
            dataGridView2.AutoResizeColumns();

            btnRestore.Enabled = true;
        }

        // Backup settings
        private void backupSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (form == null || form.Enabled == false)
            {
                form = new BckSettForm();
                form.Show();
            }
            else
            {
                form.Activate();
            }
        }
        
        // Restore
        private void btnRestore_Click(object sender, EventArgs e)
        {
            try
            {
                // Get Path & Name from dataGridView1 (Files)
                int selectedrowindexDGF = dataGridView1.SelectedCells[0].RowIndex;
                DataGridViewRow selectedRowDGF = dataGridView1.Rows[selectedrowindexDGF];
                string fileDestPath = Convert.ToString(selectedRowDGF.Cells[1].Value);
                string fileName = Convert.ToString(selectedRowDGF.Cells[2].Value);

                // Get Hash from dataGridView2 (Versions)
                int selectedrowindexDGFV = dataGridView2.SelectedCells[0].RowIndex;
                DataGridViewRow selectedRowDGFV = dataGridView2.Rows[selectedrowindexDGFV];
                string fileSourceHash = Convert.ToString(selectedRowDGFV.Cells[2].Value);
                string fileDate = Convert.ToString(selectedRowDGFV.Cells[3].Value);

                // Get source of the backup -- previous destination
                string fileSourcePath = ConfigurationManager.AppSettings["destinationDirectory"];

                // Unzip and extract
                ZipFile.ExtractToDirectory(fileSourcePath + "__" + fileDate + ".zip", fileSourcePath);

                // Get Hash from the selected version
                string fileDestHash;
                if (File.Exists(Path.Combine(fileDestPath, fileName)))
                {
                    fileDestHash = GetMD5HashFromFile(Path.Combine(fileDestPath, fileName));
                }
                else
                {
                    fileDestHash = "";
                }

                // tempFileName jer je hash spojen sa imenom radi razlikovanja
                string tempFileName = fileName + "_" + fileSourceHash;

                // Dest. file doesn't exist
                if (!File.Exists(Path.Combine(fileDestPath, fileName)))
                {
                    FileInfo file = new FileInfo(Path.Combine(fileSourcePath, tempFileName));
                    file.CopyTo(Path.Combine(fileDestPath, fileName), true);
                    MessageBox.Show("Restoration successful!", "Result");
                }
                // File exists - same hash
                else if (fileSourceHash == fileDestHash)
                {
                    DialogResult dialogResult = MessageBox.Show("Do you want to copy over the same version?", "Warning", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        FileInfo newFile = new FileInfo(Path.Combine(fileSourcePath, tempFileName));
                        FileInfo olfFile = new FileInfo(Path.Combine(fileDestPath, fileName));
                        if (newFile.LastWriteTime > olfFile.LastWriteTime)
                        {
                            File.Copy(Path.Combine(fileSourcePath, tempFileName), Path.Combine(fileDestPath, fileName), true);
                            MessageBox.Show("Restoration successful!", "Result");
                        }
                    }
                    else if (dialogResult == DialogResult.No)
                    {
                        MessageBox.Show("Please choose another version to restore!", "Instructions");
                    }
                }
                // File exists - different hash
                else
                {
                    File.Copy(Path.Combine(fileSourcePath, tempFileName), Path.Combine(fileDestPath, fileName), true);
                    MessageBox.Show("Restoration successful!", "Result");
                }

                

                // Delete the extracted zip
                Directory.Delete(fileSourcePath, true);
            }
            catch (Exception)
            {
                MessageBox.Show("Restoration failed! Closing application...", "Result");
                if (connection.State != ConnectionState.Closed)
                {
                    connection.Close();
                }
                Application.Exit();
            }
        }
        
        // MD5 hash
        private static string GetMD5HashFromFile(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Open);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] returnValue = md5.ComputeHash(file);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < returnValue.Length; i++)
            {
                sb.Append(returnValue[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
