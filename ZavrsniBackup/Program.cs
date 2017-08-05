using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Configuration;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.IO.Compression;
using System.Threading;
using System.Data;


namespace ZavrsniBackup
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            DBCreation();
            Thread.Sleep(1500);

            const int SW_HIDE = 0; // to hide console
            //const int SW_SHOW = 5; // to show console
            var handle = Helper.GetConsoleWindow();
            Helper.ShowWindow(handle, SW_HIDE);

            // If any arguments, execute GUI -- else execute process
            if (args.Length > 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new RestoreUI());
            }
            else
            {
                try
                {
                    //Start
                    string sourceDirectory = ConfigurationManager.AppSettings["sourceDirectory"];
                    string destinationDirectory = ConfigurationManager.AppSettings["destinationDirectory"];

                    CopyAllFiles(sourceDirectory, destinationDirectory);

                    // Zip backup dir and add dateStamp
                    ZipFile.CreateFromDirectory(destinationDirectory, destinationDirectory + "__" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip", CompressionLevel.Optimal, false);

                    // Delete the destination -- its zipped already :)
                    Directory.Delete(destinationDirectory, true);
                }
                catch (Exception)
                {
                    throw;
                }

                CreateTextFileLog("Task completed successfully.\t" + DateTime.Now.ToString("HH-mm-ss") + "\t-------------------------\r\n");
            }
        }

        // Real Deal
        private static void CopyAllFiles(string sourceDirectory, string destinationDirectory)
        {
            try
            {
                // Get the subdirectories for the specified directory
                DirectoryInfo dir = new DirectoryInfo(sourceDirectory);
                DirectoryInfo[] dirs = dir.GetDirectories();

                // Cannot find source EXIT
                if (!dir.Exists)
                {
                    CreateTextFileLog("Source directory "+ sourceDirectory +" does not exist or could not be found!");
                }

                // If the destination directory doesn't exist, create it
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                
                using(SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["db"].ConnectionString))
                {
                    // Get the files in the directory and copy them to the new location
                    FileInfo[] files = dir.GetFiles();
                    foreach (FileInfo file in files)
                    {
                        string hash = GetMD5HashFromFile(Path.Combine(sourceDirectory, file.Name));

                        bool filesFlag = GetFilesFromDB(sourceDirectory, file.Name);

                        if (!filesFlag)
                        {
                            conn.Open();
                            using (SQLiteCommand cmd = new SQLiteCommand(conn))
                            {
                                // SQL Files Entry
                                string insertFiles = "INSERT INTO Files (Path, Name) VALUES ('" + sourceDirectory + "','" + file.Name + "')";
                                cmd.CommandText = insertFiles;
                                cmd.ExecuteNonQuery();
                            }
                            conn.Close();
                        }
                        else
                        {
                            CreateTextFileLog("The original " + file.Name + " is already backed up!");
                        }

                        bool hashFlag = GetVersionsFromDB(hash);

                        if (!hashFlag)
                        {
                            conn.Open();
                            using (SQLiteCommand cmd = new SQLiteCommand(conn))
                            {
                                // SQL Files_versions entry
                                string getFileId = "SELECT ID FROM Files WHERE Name='" + file.Name + "'";
                                cmd.CommandText = getFileId;
                                cmd.ExecuteNonQuery();
                                Object returnValueId = cmd.ExecuteScalar();

                                string insertVersions = "INSERT INTO Files_versions (ID_file, Hash, TimeS) VALUES ('" + returnValueId + "','" + hash + "','" + DateTime.Now.ToString("yyyy-MM-dd") + "')";
                                cmd.CommandText = insertVersions;
                                cmd.ExecuteNonQuery();
                            }
                            conn.Close();

                            string tempFileName = file.Name + "_" + hash;
                            string tempDestinationPath = Path.Combine(destinationDirectory, tempFileName);
                            file.CopyTo(tempDestinationPath, true);
                        }
                        else
                        {
                            CreateTextFileLog("File " + file.Name + ", version: " + hash + " is already backed up!\r\n");
                        }
                    }
                    
                    // Subdirectories
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        bool subdirFlag = GetFilesFromDB(sourceDirectory, subdir.Name);

                        if (!subdirFlag)
                        {
                            conn.Open();
                            using (SQLiteCommand cmd = new SQLiteCommand(conn))
                            {
                                // SQL Files Entry
                                string insertFiles = "INSERT INTO Files (Path, Name) VALUES ('" + sourceDirectory + "','" + subdir.Name + "')";
                                cmd.CommandText = insertFiles;
                                cmd.ExecuteNonQuery();
                            }
                            conn.Close();

                            string tempDestinationPath = Path.Combine(destinationDirectory, subdir.Name);
                            CopyAllFiles(subdir.FullName, tempDestinationPath);
                        }
                        else
                        {
                            CreateTextFileLog("Subdirectory " + subdir.Name + " is already backed up!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CreateTextFileLog(ex.ToString());
            }
        }

        // TxtLog
        private static void CreateTextFileLog(string Message)
        {
            StreamWriter SW;

            if (!File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "txt_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt")))
            {
                SW = File.CreateText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "txt_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt"));
                SW.Close();
            }

            using (SW = File.AppendText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "txt_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt")))
            {
                SW.WriteLine(Message);
                SW.Close();
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
        // File in DB check
        private static bool GetFilesFromDB(string source, string fileName)
        {
            string searchFiles = "SELECT ID FROM Files WHERE Path='" + source + "' AND Name='" + fileName + "'";
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["db"].ConnectionString))
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }
                using (SQLiteCommand cmd = new SQLiteCommand(searchFiles, conn))
                {
                    using (SQLiteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            conn.Close();
                            return true;
                        }
                    }
                }
                conn.Close();
            }
            return false;
        }
        // File version in DB check
        private static bool GetVersionsFromDB(string hash)
        {
            string searchFiles = "SELECT ID FROM Files_versions WHERE Hash='" + hash + "'";
            using (SQLiteConnection conn = new SQLiteConnection (ConfigurationManager.ConnectionStrings["db"].ConnectionString) )
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }
                using (SQLiteCommand cmd = new SQLiteCommand(searchFiles, conn))
                {
                    using (SQLiteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            conn.Close();
                            return true;
                        }
                    }
                }
                conn.Close();
            }
            return false;
        }

        // Helper class - hiding console
        public static class Helper
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            public static extern IntPtr GetConsoleWindow();
        }

        // DB creation
        private static void DBCreation()
        {
            string createFilesQuery = @"CREATE TABLE IF NOT EXISTS Files (
                          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
                          Path TEXT NOT NULL,
                          Name TEXT NOT NULL
                          )";

            string createFilesVersionsQuery = @"CREATE TABLE IF NOT EXISTS Files_versions (
                          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
                          ID_file INTEGER NOT NULL,
                          Hash TEXT NOT NULL,
                          TimeS TEXT NOT NULL,
                          FOREIGN KEY(ID_file) REFERENCES Files(ID) ON DELETE CASCADE
                          )";

            string path = Path.Combine(Application.StartupPath, "zavrsniDB.db");

            if (!File.Exists(path))
            {
                SQLiteConnection conn;
                SQLiteConnection.CreateFile(path);
                conn = new SQLiteConnection("Data Source=zavrsniDB.db;Version=3;Foreign Keys=true;");

                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var connectionStringsSection = (ConnectionStringsSection)config.GetSection("connectionStrings");
                connectionStringsSection.ConnectionStrings["db"].ConnectionString = "Data Source=zavrsniDB.db;Version=3;Foreign Keys=true;";
                config.Save();
                ConfigurationManager.RefreshSection("connectionStrings");

                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(createFilesQuery, conn))
                {
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = createFilesVersionsQuery;
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
        }
    }
}