using DokanNet;
using DokanNet.Logging;
using System;

namespace VirtualRescene.net
{
    class Program
    {
        public static string SRR_exe, SRRfile, videoFile, driveLetter;
        static void Main(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                //Console.WriteLine("i=" + i + "  arg=" + args[i]);
                if (args[i - 1].ToLower() == "--srrexe")
                    SRR_exe = args[i];
                else if (args[i - 1].ToLower() == "--srrfile")
                    SRRfile = args[i];
                else if (args[i - 1].ToLower() == "--video")
                    videoFile = args[i];
                else if (args[i - 1].ToLower() == "--drive")
                    driveLetter = args[i];
            }
            bool ready = false;
            if (SRR_exe != null && SRRfile != null && videoFile != null && driveLetter != null)
            {
                ready = true;
                if (!System.IO.File.Exists(SRR_exe))
                {
                    Console.WriteLine("<Error> srr.exe not found");
                    ready = false;
                }
                if (!System.IO.File.Exists(SRRfile))
                {
                    Console.WriteLine("<Error> srrfile not found");
                    ready = false;
                }
                if (!System.IO.File.Exists(videoFile))
                {
                    Console.WriteLine("<Error> video file not found");
                    ready = false;
                }
                if (driveLetter.Length > 1 || !Char.IsLetter(driveLetter.ToCharArray()[0]))
                {
                    Console.WriteLine("<Error> drive letter needs to be a single letter.");
                    ready = false;
                }
                else if (System.IO.Directory.Exists(driveLetter + ":/"))
                {
                    Console.WriteLine("<Error> drive letter is occupied.");
                    ready = false;
                }
            }
            Console.WriteLine("");
            if (!ready)
            {
                Console.WriteLine("Usage: (all options are mendatory)");
                Console.WriteLine("--srrexe (path to srr.exe)");
                Console.WriteLine("--srrfile (path to a .srr file)");
                Console.WriteLine("--video (path to a video file)");
                Console.WriteLine("--drive (a drive letter to mount Virtual Rescene, like  V  , you need to pass a single letter)");
            }
            else
            {
                Console.WriteLine("Ready to go! (press enter to continue)");
                Console.ReadLine();
                try
                {
                    VirtualRescene test = new VirtualRescene(SRRfile, videoFile);
                    
                    test.Mount(driveLetter + ":\\", new NullLogger());
                    Console.WriteLine(@"Success");
                }
                catch (DokanException ex)
                {
                    Console.WriteLine(@"Error: " + ex.Message);
                }
            }
            Console.WriteLine("press enter to continue");
            Console.ReadLine();
        }
    }
}
