using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace VirtualRescene.net
{
    class VirtualRescene : IDokanOperations
    {
        //private List<FileInformation> files_in_root = new List<FileInformation>();
        private Dictionary<String, FileInformation> files_in_root = new Dictionary<string, FileInformation>();
        //private List<CustomFileInfo> files_info = new List<CustomFileInfo>();
        private Dictionary<String, CustomFileInfo> files_info = new Dictionary<string, CustomFileInfo>();

        private FileStream vid_stream;

        public VirtualRescene(string SRRfile = null, string videofile = null)
        {
            DateTime time = DateTime.Now;
            if (SRRfile != null && videofile != null && File.Exists(SRRfile) && File.Exists(videofile))
            {
                Dictionary<string, RARmetadata> RARheaders = SRR.Dump_headers(SRRfile);
                Dictionary<string, long> RAR_sizes = SRR.Dump_RAR_sizes(SRRfile);
                long offset = 0;
                foreach (string filename in RARheaders.Keys)
                {
                    FileInformation f = new FileInformation
                    {
                        FileName = filename,
                        Attributes = FileAttributes.ReadOnly,
                        Length = RAR_sizes[filename]
                    };
                    f.LastAccessTime = f.LastWriteTime = f.CreationTime = time;
                    files_in_root.Add(@"\" + f.FileName, f);
                    CustomFileInfo info = new CustomFileInfo()
                    {
                        filename = filename,
                        filesize = RAR_sizes[filename],
                        offset = offset,
                        header = RARheaders[filename].header,
                        file_end = RARheaders[filename].file_end
                    };
                    offset += (RAR_sizes[filename] - Convert.ToInt64(info.header.Length + info.file_end.Length));
                    files_info.Add(info.filename, info);
                    Console.WriteLine("Filename: " + filename);
                    Console.WriteLine("metadata length: " + (info.header.Length + info.file_end.Length));
                    Console.WriteLine("Offset after file: " + offset);
                }
                if (offset != (new FileInfo(videofile)).Length)
                {
                    Console.WriteLine("Filesize mismatch.");
                    Console.WriteLine("Original size: " + (new FileInfo(videofile)).Length);
                    Console.WriteLine("Calculated file: " + offset);
                }
                else
                    Console.WriteLine("Filesize matches");
                vid_stream = File.OpenRead(videofile);
            }
            Console.WriteLine("Inited");
        }

        private FileInformation testFile = new FileInformation
        {
            FileName = "Test",
            Attributes = FileAttributes.ReadOnly,
            LastAccessTime = DateTime.Now,
            LastWriteTime = DateTime.Now,
            CreationTime = DateTime.Now,
            Length = 2497363968
        };

        public NtStatus CreateFile(
            string filename,
            FileAccess access,
            FileShare share,
            FileMode mode,
            FileOptions options,
            FileAttributes attributes,
            IDokanFileInfo info)
        {
            if (filename == @"\" || mode == FileMode.Open)
                return DokanResult.Success;
            Console.WriteLine("CreateFile filename: " + filename);
            return DokanResult.DiskFull;
        }

        public NtStatus FindFiles(
            string filename,
            out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            if (filename == @"\")
            {
                foreach (FileInformation f in files_in_root.Values)
                {
                    files.Add(f);

                }
                return DokanResult.Success;
            }
            else
            {
                return DokanResult.Error;
            }
        }

        public NtStatus GetFileInformation(
            string filename,
            out FileInformation fileinfo,
            IDokanFileInfo info)
        {
            fileinfo = new FileInformation { FileName = filename };

            if (filename == @"\")
            {
                fileinfo.Attributes = FileAttributes.Directory;
                fileinfo.LastAccessTime = DateTime.Now;
                fileinfo.LastWriteTime = null;
                fileinfo.CreationTime = null;

                return DokanResult.Success;
            }

            if (files_in_root.ContainsKey(filename))
            {
                fileinfo = files_in_root[filename];
                return DokanResult.Success;
            }

            return DokanResult.Error;
        }

        public NtStatus ReadFile(
            string filename,
            byte[] buffer,
            out int bytesRead,
            long offset,
            IDokanFileInfo info)
        {
            bool debug = false;
            if (debug) Console.WriteLine("!!!======");
            bytesRead = 0;

            //Error handling 1, handle the case where file does not exist
            if (!files_in_root.ContainsKey(filename))
            {
                Console.WriteLine(">>File not found<<");
                Console.WriteLine("Tried to read file \"" + filename + "\" but not found");
                return DokanResult.FileNotFound;
            }

            CustomFileInfo mapped_info = files_info[files_in_root[filename].FileName];
            //Error handling 2, check if filesystem requested the range that's out of bounds.
            /*if (offset > mapped_info.filesize || offset + buffer.Length > mapped_info.filesize)
            {
                Console.WriteLine(">>Out of bounds<<");
                Console.WriteLine("Filename: " + filename);
                Console.WriteLine("Requested offset: " + offset);
                Console.WriteLine("Buffer size: " + buffer.Length);
                Console.WriteLine("File size: " + mapped_info.filesize);
                return DokanResult.Error;
            }*/
            //After some testing... this code seems like the source of error, the following happens when I try to copy the file.
            /*
             * >>Out of bounds<<
                Filename: \(redacted).rar
                Requested offset: 99614720
                Buffer size: 1048576
                File size: 100000000
             */
            //Wondering how this is handled in other filesystems.........
            //.....I moved out of bound checks to if conditions
            //I guess the solution is don't return anything if it's out of bounds, instead of throwing an error.



            //handles header aera
            if (offset < mapped_info.header.Length)
            {
                if (debug) Console.WriteLine("Chunk header");
                if (debug) Console.WriteLine("bytes needed = " + (buffer.Length - bytesRead));
                using (MemoryStream stream = new MemoryStream(mapped_info.header))
                {
                    stream.Position = offset;
                    bytesRead += stream.Read(buffer, bytesRead, Math.Min(mapped_info.header.Length, buffer.Length));
                }
                if (debug) Console.WriteLine("bytes needed = " + (buffer.Length - bytesRead));
            }

            //handles body (video file) aera
            if (buffer.Length > bytesRead &&
                offset + buffer.Length > mapped_info.header.Length &&
                offset < mapped_info.filesize - mapped_info.file_end.Length)
            {
                if (debug) Console.WriteLine("Chunk body");
                if (debug) Console.WriteLine("bytes needed = " + (buffer.Length - bytesRead));
                if (debug) Console.WriteLine("offset = " + offset);
                vid_stream.Position = mapped_info.offset + offset;
                if (offset > mapped_info.header.Length)
                    vid_stream.Position -= mapped_info.header.Length;
                else
                    vid_stream.Position -= offset;
                //int bytes_in_file_end = 0;
                //if (offset + buffer.Length > mapped_info.filesize - mapped_info.file_end.Length)
                //    bytes_in_file_end = Convert.ToInt32(mapped_info.filesize - mapped_info.file_end.Length - offset);
                int bytes_in_file_end = 0;
                if (offset + buffer.Length - bytesRead > mapped_info.filesize - mapped_info.file_end.Length)
                {
                    bytes_in_file_end = Convert.ToInt32(offset - mapped_info.filesize) + buffer.Length - bytesRead + mapped_info.file_end.Length;
                }

                if (debug) Console.WriteLine("bytes_in_file_end " + bytes_in_file_end);
                bytesRead += vid_stream.Read(buffer, bytesRead, buffer.Length - bytesRead - bytes_in_file_end);
                if (debug) Console.WriteLine("bytes needed = " + (buffer.Length - bytesRead));
            }

            //handles file_end area
            if (buffer.Length > bytesRead &&
                offset + buffer.Length >= mapped_info.filesize - mapped_info.file_end.Length &&
                offset < mapped_info.filesize)
            {
                if (debug) Console.WriteLine("Chunk file_end");
                if (debug) Console.WriteLine("bytes needed1 = " + (buffer.Length - bytesRead));
                if (bytesRead > 0)
                {
                    //This read operations contains part of video file and file_end.
                    using (MemoryStream stream = new MemoryStream(mapped_info.file_end))
                    {
                        bytesRead += stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
                    }
                }
                else
                {
                    //This read operations contains file_end only, so it might read from middle of file_end.
                    //Or maybe it only wants to read file_end, but we still calculates offset just to be safe.
                    using (MemoryStream stream = new MemoryStream(mapped_info.file_end))
                    {
                        stream.Position = Convert.ToInt32(mapped_info.filesize - offset);
                        bytesRead += stream.Read(buffer, bytesRead, buffer.Length);
                    }
                }
                if (debug) Console.WriteLine("bytes needed2 = " + (buffer.Length - bytesRead));
                if (debug) Console.WriteLine("buffer.Length = " + buffer.Length);
                if (debug) Console.WriteLine("bytesRead = " + bytesRead);
            }

            ////If filesystem requested the area that contains RAR file header, read RAR file header from RAM.
            //if (offset < mapped_info.header.Length) {
            //    Console.WriteLine("Chunk header");
            //    using (MemoryStream stream = new MemoryStream(mapped_info.header))
            //    {
            //        stream.Position = offset;
            //        bytesRead = stream.Read(buffer, bytesRead, Math.Min(mapped_info.header.Length, buffer.Length));
            //    }
            //    Console.WriteLine("bytes needed = " + (buffer.Length - bytesRead));
            //}
            ////If filesystem requested the area that contains main video file, read video file from hard drive
            ////vid_stream is a file stream that's opened when Virtual Rescene is initialized.
            //if (bytesRead < buffer.Length)
            //{
            //    vid_stream.Position = mapped_info.offset + offset;
            //    Console.WriteLine("position = " + vid_stream.Position);
            //    Console.WriteLine("bytes needed = " + (buffer.Length - bytesRead));
            //    if (vid_stream.Position + buffer.Length - bytesRead < mapped_info.filesize - mapped_info.file_end.Length)
            //    {
            //        //The file system requested part of video file.
            //        Console.WriteLine("Chunk A");
            //        bytesRead = vid_stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
            //    }
            //    else
            //    {
            //        if (mapped_info.filesize - mapped_info.file_end.Length - vid_stream.Position > 0)
            //        {
            //            Console.WriteLine("Chunk B");
            //            //The requested area contains file_end, so we read from
            //            //1. vid_stream
            //            //2. mapped_info.file_end
            //            bytesRead = vid_stream.Read(buffer, bytesRead, Convert.ToInt32(mapped_info.filesize - mapped_info.file_end.Length - vid_stream.Position));
            //            using (MemoryStream stream = new MemoryStream(mapped_info.file_end))
            //            {
            //                stream.Position = 0;
            //                bytesRead = stream.Read(buffer, 0, buffer.Length - bytesRead);
            //            }
            //        }
            //        else
            //        {
            //            if (buffer.Length - bytesRead > mapped_info.file_end.Length) {
            //                Console.WriteLine("Warning!!! logical error!");
            //                Console.ReadLine();
            //            }
            //            Console.WriteLine("Chunk C");
            //            //Read from file_end
            //            //The filesystem might want to read from middle of file_end (I think....), so some calculation is needed.
            //            Console.WriteLine("offset_by_function = " + offset);
            //            Console.WriteLine("buffer length = " + buffer.Length);
            //            Console.WriteLine("bytesread = " + bytesRead);
            //            using (MemoryStream stream = new MemoryStream(mapped_info.file_end))
            //            {
            //                Console.WriteLine("Math1 = " + (mapped_info.file_end.Length - buffer.Length + bytesRead));
            //                stream.Position = mapped_info.file_end.Length - buffer.Length + bytesRead;
            //                bytesRead = stream.Read(buffer, 0, buffer.Length - bytesRead);
            //            }
            //        }
            //    }

            //}
            if (debug) Console.WriteLine("======!!!");
            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(
            out long freeBytesAvailable,
            out long totalBytes,
            out long totalFreeBytes,
            IDokanFileInfo info)
        {
            totalBytes = freeBytesAvailable = 1024 * 1024 * 1024;
            totalFreeBytes = 0;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
            out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "Virtual Rescene";
            features = FileSystemFeatures.ReadOnlyVolume;
            fileSystemName = string.Empty;
            maximumComponentLength = 256;
            return DokanResult.Success;
        }



        #region "Look into them later...or never"
        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            security = null;
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus EnumerateNamedStreams(string fileName, IntPtr enumContext, out string streamName,
            out long streamSize, IDokanFileInfo info)
        {
            streamName = string.Empty;
            streamSize = 0;
            return DokanResult.NotImplemented;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus SetEndOfFile(string filename, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus SetAllocationSize(string filename, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileAttributes(
            string filename,
            FileAttributes attr,
            IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileTime(
            string filename,
            DateTime? ctime,
            DateTime? atime,
            DateTime? mtime,
            IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus UnlockFile(string filename, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus LockFile(
            string filename,
            long offset,
            long length,
            IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus MoveFile(
            string filename,
            string newname,
            bool replace,
            IDokanFileInfo info)
        {
            return DokanResult.Error;
        }


        public NtStatus DeleteDirectory(string filename, IDokanFileInfo info)
        {
            return DokanResult.Error;
        }

        public NtStatus DeleteFile(string filename, IDokanFileInfo info)
        {
            return DokanResult.Error;
        }

        public NtStatus FlushFileBuffers(
            string filename,
            IDokanFileInfo info)
        {
            return DokanResult.Error;
        }
        public NtStatus WriteFile(
            string filename,
            byte[] buffer,
            out int writtenBytes,
            long offset,
            IDokanFileInfo info)
        {
            writtenBytes = 0;
            return DokanResult.NotImplemented;
        }
        public void Cleanup(string filename, IDokanFileInfo info)
        {
        }
        public void CloseFile(string filename, IDokanFileInfo info)
        {
        }
        #endregion
    }
    class CustomFileInfo
    {
        public string filename = "";
        public long filesize = 0, offset = 0;
        public byte[] header = null, file_end = null;
    }
}
