using System;
using System.Collections.Generic;
using System.Text;
using Shell32;
using System.Runtime.InteropServices;

namespace RecycleDesign
{
    public class RecycleBinItem
    {
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FileSize { get; set; }
    }

    public class RecycleBin
    {
        public List<RecycleBinItem> GetRecycleBinItems()
        {
            try
            {
                //create a new isntance of the Shell32 interface
                Shell shell = new Shell();
                List<RecycleBinItem> list = new List<RecycleBinItem>();

                //create a folder item to our Recycle Bin
                Folder recycleBin = shell.NameSpace(10);

                //now let's loop through all the Items in our Folder object
                //and add them to a generic list
                foreach (FolderItem2 f in recycleBin.Items())
                    list.Add(
                            new RecycleBinItem
                            {
                                FileName = f.Name,
                                FileType = f.Type,
                                FileSize = GetSize(f).ToString()
                            });
                //return the list
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error accessing the Recycle Bin: {0}", ex.Message));
                return null;
            }
        }
        public double GetSize(FolderItem folderItem)
        {
            //check if it's a folder, if it's not then return it's size
            if (!folderItem.IsFolder)
                return (double)folderItem.Size;

            //create a new Shell32.Folder item
            Folder folder = (Folder)folderItem.GetFolder;

            double size = 0;

            //since we're here we're dealing with a folder, so now we will loop
            //through everything in it and get it's size, thus calculating the
            //overall size of the folder
            foreach (FolderItem2 f in folder.Items())
                size += GetSize(f);

            //return the size
            return size;
        }
    }
}
