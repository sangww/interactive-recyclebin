using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.IO;
using System.Timers;



namespace RecycleDesign
{
    class Program : System.Windows.Forms.Form
    {
        private const int SPI_SETICONS = 0x0058; 
        private const int SPIF_UPDATEINIFILE = 0x1; 
        private const int SPIF_SENDWININICHANGE = 0x2; 
        private const int HWND_BROADCAST = 0xffff; 
        private const int WM_SETTINGCHANGE = 0x001A; 
        private const int SMTO_ABORTIFHUNG = 0x0002;
        private const int SPI_SETNONCLIENTMETRICS = 0x0002;

        private const int FO_DELETE = 3;
        private const int FOF_ALLOWUNDO = 0x40;
        private const int FOF_NOCONFIRMATION = 0x10; // Don't prompt the user
        private const int FOF_SILENT = 0x04;

        private enum SHCNE : uint
        {
            SHCNE_RENAMEITEM = 0x00000001,
            SHCNE_CREATE = 0x00000002,
            SHCNE_DELETE = 0x00000004,
            SHCNE_MKDIR = 0x00000008,
            SHCNE_RMDIR = 0x00000010,
            SHCNE_MEDIAINSERTED = 0x00000020,
            SHCNE_MEDIAREMOVED = 0x00000040,
            SHCNE_DRIVEREMOVED = 0x00000080,
            SHCNE_DRIVEADD = 0x00000100,
            SHCNE_NETSHARE = 0x00000200,
            SHCNE_NETUNSHARE = 0x00000400,
            SHCNE_ATTRIBUTES = 0x00000800,
            SHCNE_UPDATEDIR = 0x00001000,
            SHCNE_UPDATEITEM = 0x00002000,
            SHCNE_SERVERDISCONNECT = 0x00004000,
            SHCNE_UPDATEIMAGE = 0x00008000,
            SHCNE_DRIVEADDGUI = 0x00010000,
            SHCNE_RENAMEFOLDER = 0x00020000,
            SHCNE_FREESPACE = 0x00040000,
            SHCNE_EXTENDED_EVENT = 0x04000000,
            SHCNE_ASSOCCHANGED = 0x08000000,
            SHCNE_DISKEVENTS = 0x0002381F,
            SHCNE_GLOBALEVENTS = 0x0C0581E0,
            SHCNE_ALLEVENTS = 0x7FFFFFFF,
            SHCNE_INTERRUPT = 0x80000000
        }
        private enum SHCNF : uint
        {
            SHCNF_IDLIST = 0x0000,
            SHCNF_PATHA = 0x0001,
            SHCNF_PRINTERA = 0x0002,
            SHCNF_DWORD = 0x0003,
            SHCNF_PATHW = 0x0005,
            SHCNF_PRINTERW = 0x0006,
            SHCNF_TYPE = 0x00FF,
            SHCNF_FLUSH = 0x1000,
            SHCNF_FLUSHNOWAIT = 0x2000
        }
        [DllImport("shell32.dll")]
        protected static extern long SendMessageTimeout(
            int hWnd, 
            int Msg, 
            int wParam, 
            int lParam, 
            int fuFlags, 
            int uTimeout, 
            out int lpdwResult
        );

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        protected static extern void SHChangeNotify(
            UInt32 wEventId, 
            UInt32 uFlags, 
            IntPtr dwItem1, 
            IntPtr dwItem2
        );
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        protected static extern long SendMessageTimeout(
            int hWnd, 
            int Msg, 
            int wParam, 
            string lParam, 
            int fuFlags, 
            int uTimeout, 
            out int lpdwResult
            );
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto, Pack=1)]
        protected struct SHFILEOPSTRUCT
        {         
            public IntPtr hwnd;         
            [MarshalAs(UnmanagedType.U4)] 
            public int wFunc;      
            public string pFrom;       
            public string pTo;       
            public short fFlags;  
            [MarshalAs(UnmanagedType.Bool)] 
            public bool fAnyOperationsAborted;      
            public IntPtr hNameMappings;      
            public string lpszProgressTitle; 
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        protected static extern int GetDriveType(string lpRootPathName);
        [DllImport("shell32.dll", CharSet=CharSet.Auto)]
        protected static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        //console handling
        [DllImport("kernel32.dll")] 
        static extern IntPtr GetConsoleWindow();  
        [DllImport("user32.dll")] 
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);  
        const int SW_HIDE = 0; 
        const int SW_SHOW = 5; 











        //main methods
        private static string path, os, defaultpath, defaultpathempty;
        private static RecycleBin bin;
        private static int nTrash;
        private static UInt64 szTrash;
        private static int state;
        private static bool isEnabled;
        private static bool isChanged;
        private static Timer timer;

        //system tray app
        private System.Windows.Forms.NotifyIcon  trayIcon;
        private System.Windows.Forms.ContextMenu trayMenu;
        private bool isConsoleShown;

        protected static void invokeRefresh()
        {
            int res = 0;
            
            //Notify - used a null delete trick
            var shf = new SHFILEOPSTRUCT();
            shf.wFunc = FO_DELETE;
            shf.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT;
            shf.pFrom = @path + @"fake.trash.should.not.exist";
            shf.pTo = @"fake.trash.should.not.exist";
            SHFileOperation(ref shf);
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, SPI_SETNONCLIENTMETRICS, "", SMTO_ABORTIFHUNG, 100000, out res);
            SHChangeNotify((uint)SHCNE.SHCNE_ASSOCCHANGED, (uint)SHCNF.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        private static void scanRecycleBin()
        {
            Console.WriteLine("");
            Console.WriteLine("Items In Recycle Bin...");

            UInt64 sz = 0;
            int nb = 0;
            foreach (RecycleBinItem item in bin.GetRecycleBinItems())
            {
                Console.WriteLine(string.Format("File: {0} => Size: {1} => Type: {2}", item.FileName, item.FileSize, item.FileType));
                sz += Convert.ToUInt64(item.FileSize);
                nb++;
            }

            szTrash = sz;
            nTrash = nb;
            Console.WriteLine("Total # of Items In Recycle Bin: {0}", nTrash);
            Console.WriteLine("Total trashes In Recycle Bin: {0}", szTrash);

            int st;
            if (sz > 500000000)
                st = 6;
            else if (sz > 400000000)
                st = 5;
            else if (sz > 300000000)
                st = 4;
            else if (sz > 200000000)
                st = 3;
            else if (sz > 100000000)
                st = 2;
            else if (sz > 0)
                st = 1;
            else st = -1;
            if (st != state) isChanged = true;

            state = st;
            
        }
        private static void changeRecycleBin()
        {
            //update registry
            RegistryKey k = Registry.CurrentUser.OpenSubKey("Software").
                                                    OpenSubKey("Microsoft").
                                                    OpenSubKey("Windows").
                                                    OpenSubKey("CurrentVersion").
                                                    OpenSubKey("Explorer").
                                                    OpenSubKey("CLSID").
                                                    OpenSubKey("{645FF040-5081-101B-9F08-00AA002F954E}").
                                                    OpenSubKey("DefaultIcon", true);

            //switch - win version
            Console.WriteLine("");
            System.OperatingSystem osInfo = System.Environment.OSVersion;
            if (osInfo.Platform == PlatformID.Win32NT)
            {
                switch (os)
                {
                    case "XP":
                    case "7":
                        if (isEnabled)
                        {
                            if (defaultpath == null || defaultpath.Length < 5)
                                defaultpath = (string)k.GetValue("Full");
                            if (defaultpathempty == null || defaultpathempty.Length < 5)
                                defaultpathempty = (string)k.GetValue("Empty");

                            k.SetValue("Full", path + getResourceName());  //this value in my reg is 32
                            k.SetValue("Empty", path + getEmptyResourceName());  //this value in my reg is 32
                            writeSettingsFile(defaultpath, defaultpathempty);
                        }
                        else
                        {
                            k.SetValue("Full", defaultpath);
                            k.SetValue("Empty", defaultpathempty);
                            writeSettingsFile();
                        }
                        k.Flush(); k.Close();
                        break;
                    default:
                        break;
                }
            }
            else
            {
                Console.WriteLine("Not Supported Version of Window");
            }
        }
        protected static void toggleRecycleBin(object sender, EventArgs e)
        {
            isEnabled = !isEnabled;
            if(isEnabled) scanRecycleBin();
            changeRecycleBin();
            invokeRefresh();
        }
        protected static void updateRecycleBin()
        {
            if (isEnabled) scanRecycleBin();
            changeRecycleBin();
            invokeRefresh();
        }
        private static void handleEvent(object sender, EventArgs e)
        {
            Console.WriteLine("handleEvent");
            if (isEnabled)
            {
                scanRecycleBin();
                if (isChanged)
                {
                    changeRecycleBin();
                    invokeRefresh();
                }
            }
        }

        private static void onDelete(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("onDelete");
            timer.Stop();
            timer.Start();            
        }
        private static void onCreate(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("onCreate");
            timer.Stop();
            timer.Start();
        }
        private static void onChange(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("onChange");
        }
        private static void onRename(object sender, RenamedEventArgs e)
        {
            Console.WriteLine("onRename");
        }

        protected static string getOS()
        {
            System.OperatingSystem osInfo = System.Environment.OSVersion;
            if (osInfo.Platform == PlatformID.Win32NT)
            {
                switch (osInfo.Version.Major)
                {
                    case 5:
                        if (osInfo.Version.Minor == 0)
                        {
                            Console.WriteLine("2000");
                            return "2000";
                        }
                        else
                        {
                            Console.WriteLine("XP");
                            return "XP";
                        }
                    case 6:
                        if (osInfo.Version.Minor == 0)
                        {
                            Console.WriteLine("Vista");
                            return "Vista";
                        }
                        else
                        {
                            Console.WriteLine("7");
                            return "7";
                        }
                    default:
                        return "N/A";
                }
            }
            else
            {
                Console.WriteLine("Not Supported Version of Window");
                return "N/A";
            }
        }
        protected static string getRecycleBinSubPath()
        {
            switch (os)
            {
                case "XP":                    
                    return @"RECYCLER\" + WindowsIdentity.GetCurrent().User.Value + @"\";
                case "7":
                    return @"$Recycle.Bin\" + WindowsIdentity.GetCurrent().User.Value + @"\";
                default:
                    return null;
            }
        }
        protected static string getResourceName()
        {
            if(state == 6)
                return "nospace.ico";
            else if(state ==5)
                return "10percent.ico";
            else if(state ==4)
                return "20percent.ico";
            else if(state ==3)
                return "40percent.ico";
            else if(state ==2)
                return "60percent.ico";
            else if(state ==1)
                return "80percent.ico";
            else 
                return "clean.ico";
        }
        protected static string getEmptyResourceName()
        {
            return "clean.ico";
        }
        protected static string getTaskbarIconName()
        {
            return "recycle_bin.ico";
        }

        private static void readSettingsFile()
        {
            TextReader tx = new StreamReader("settings.cfg");
            string ln;
            while ((ln = tx.ReadLine()).Length>0)
            {
                string index = ln.Substring(ln.IndexOf('[')+1, ln.IndexOf(']') - ln.IndexOf('[')-1);
                if ( index== "enabled")
                {
                    if(Convert.ToInt32((ln.Substring(ln.IndexOf(']')+1)).Trim())==0)
                    {
                        isEnabled = false;
                        Console.WriteLine("Settings - Enabled: false");
                    }
                    else
                    {
                        isEnabled = true;
                        Console.WriteLine("Settings - Enabled: true");
                    } 
                }
                else if (index == "defaultpath")
                {
                    defaultpath = (ln.Substring(ln.IndexOf(']') + 1)).Trim();
                    Console.WriteLine("Settings - Default Path:{0}", defaultpath);
                }
                else if (index == "defaultpathempty")
                {
                    defaultpathempty = (ln.Substring(ln.IndexOf(']') + 1)).Trim();
                    Console.WriteLine("Settings - Default Empty Path:{0}", defaultpathempty);
                }

                if (((StreamReader)tx).EndOfStream) break;
            }
            tx.Close();
        }
        private static void writeSettingsFile(string pth = null, string pth_empty = null)
        {
            TextWriter tx = new StreamWriter("settings.cfg",false);
            if (isEnabled)
                tx.WriteLine("[enabled] " + "1");
            else
                tx.WriteLine("[enabled] " + "0");

            if(pth!=null)
                tx.WriteLine("[defaultpath] " + pth);
            else
                tx.WriteLine("[defaultpath] " + defaultpath);

            if (pth_empty != null)
                tx.WriteLine("[defaultpathempty] " + pth_empty);
            else
                tx.WriteLine("[defaultpathempty] " + defaultpathempty);
            tx.Close();
        }



        [STAThread]
        static void Main(string[] args)
        {
            System.Windows.Forms.Application.Run(new Program());
        }
        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;

            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            isConsoleShown = false;

            base.OnLoad(e);
        }
        private void OnExit(object sender, EventArgs e)
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);

            System.Windows.Forms.Application.Exit();
        }
        protected void toggleConsoleVisibility(object sender, EventArgs e)
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, isConsoleShown?SW_HIDE:SW_SHOW);
            isConsoleShown = !isConsoleShown;
        }
        
        //constructor
        public Program()
        {
            isEnabled = false;
            isChanged = false;
            os = getOS();
            bin = new RecycleBin();

            //timer
            timer = new Timer(300);
            timer.AutoReset = false;
            timer.Elapsed += new ElapsedEventHandler(handleEvent);

            //set Icon Path
            path = AppDomain.CurrentDomain.BaseDirectory + "..\\..\\..\\";
            Console.WriteLine("Icon Directory...");
            Console.WriteLine("-> {0}", path);
            Console.WriteLine("");

            //list hard drives
            string[] DriveList = Environment.GetLogicalDrives();
            List<string> HardDriveList = new List<string>();
            for (int i = 0; i < DriveList.Length; i++)
            {
                if (GetDriveType(DriveList[i]) == 3)
                    HardDriveList.Add(DriveList[i]);
            }

            //set FSW
            List<FileSystemWatcher> FSWList = new List<FileSystemWatcher>();
            for (int i = 0; i < HardDriveList.Count; i++)
            {
                System.IO.FileSystemWatcher fsw = new System.IO.FileSystemWatcher();
                fsw.Path = HardDriveList[i] + getRecycleBinSubPath();
                fsw.Filter = "";
                fsw.Deleted += new FileSystemEventHandler(onDelete);
                fsw.Created += new FileSystemEventHandler(onCreate);
                fsw.EnableRaisingEvents = true;
                fsw.IncludeSubdirectories = true;

                Console.WriteLine(fsw.Path);
                FSWList.Add(fsw);
            }
            Console.WriteLine("FileSystemWatcher Registered...\n\n"); 

            //additional initialization
            readSettingsFile();

            //tray setting            
            trayMenu = new System.Windows.Forms.ContextMenu();
            trayMenu.MenuItems.Add("On/Off", toggleRecycleBin);
            trayMenu.MenuItems.Add("Toggle Console", toggleConsoleVisibility);
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Create a tray icon.
            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Text = "Better Recycle Bins";
            trayIcon.Icon = new Icon(path+getTaskbarIconName());

            // Add menu to tray icon and show it.
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible     = true;
            isConsoleShown = true;
        }
    }
}
